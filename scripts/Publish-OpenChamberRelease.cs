#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Coordinator.Common.cs

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LocalFork;

const string TargetRepository = "AlanIWBFT/openchamber";
const string OfficialRepository = "openchamber/openchamber";
const string RequiredGitHubLogin = "AlanIWBFT";

if (!OperatingSystem.IsWindows())
    throw new InvalidOperationException("The OpenChamber GitHub release publisher supports Windows only.");

var scriptPath = GetSourcePath();
var coordinatorRoot = Directory.GetParent(Path.GetDirectoryName(scriptPath)!)!.FullName;
var options = PublishOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return;
}

var config = Coordinator.LoadConfig(coordinatorRoot);
var releaseDirectory = Path.GetFullPath(options.ReleaseDirectory!, Environment.CurrentDirectory);
var localRelease = LoadLocalRelease(releaseDirectory, Path.GetFullPath(config.ReleaseRoot));
var remote = ValidateRemote(localRelease, coordinatorRoot);

PrintPlan(localRelease, remote);
CreateDraft(localRelease, remote, coordinatorRoot);

static LocalRelease LoadLocalRelease(string releaseDirectory, string releaseRoot)
{
    if (!Directory.Exists(releaseDirectory)) throw new DirectoryNotFoundException(releaseDirectory);
    var parent = Directory.GetParent(releaseDirectory)?.FullName;
    if (parent is null || !Path.GetFullPath(parent).Equals(releaseRoot, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Release directory must be an immutable direct child of {releaseRoot}: {releaseDirectory}");

    var manifestPath = Path.Combine(releaseDirectory, "release.json");
    RequireFile(manifestPath);
    var manifest = JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(manifestPath), Json.Options)
        ?? throw new InvalidOperationException($"Unable to parse release manifest: {manifestPath}");

    if (manifest.Schema != 1) throw new InvalidOperationException($"Unsupported release.json schema: {manifest.Schema}");
    if (manifest.Platform != "win32") throw new InvalidOperationException($"Only win32 releases can be published, got {manifest.Platform}.");
    if (manifest.Architecture is not ("x64" or "arm64"))
        throw new InvalidOperationException($"Unsupported Windows architecture: {manifest.Architecture}");
    if (!IsStableVersion(manifest.OpenChamberVersion))
        throw new InvalidOperationException($"OpenChamber version must be stable three-part semver: {manifest.OpenChamberVersion}");
    if (!IsCommit(manifest.Source.OpenChamberCommit))
        throw new InvalidOperationException($"Invalid OpenChamber source commit in release.json: {manifest.Source.OpenChamberCommit}");

    var expectedDirectoryName = $"OpenChamber-{manifest.OpenChamberVersion}-win-{manifest.Architecture}";
    if (!Path.GetFileName(releaseDirectory).Equals(expectedDirectoryName, StringComparison.Ordinal))
        throw new InvalidOperationException($"Release directory name mismatch: expected {expectedDirectoryName}, got {Path.GetFileName(releaseDirectory)}.");

    var installerName = $"{expectedDirectoryName}.exe";
    var blockmapName = $"{installerName}.blockmap";
    var expectedArtifactNames = new HashSet<string>([installerName, blockmapName, "latest.yml"], StringComparer.Ordinal);
    var actualFiles = Directory.GetFiles(releaseDirectory).Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.Ordinal);
    var expectedFiles = new HashSet<string>(expectedArtifactNames, StringComparer.Ordinal) { "release.json" };
    if (!actualFiles.SetEquals(expectedFiles))
        throw new InvalidOperationException($"Release directory inventory mismatch. Expected: {string.Join(", ", expectedFiles.Order())}; actual: {string.Join(", ", actualFiles.Order())}.");
    if (Directory.GetDirectories(releaseDirectory).Length != 0)
        throw new InvalidOperationException($"Release directory must not contain subdirectories: {releaseDirectory}");

    var artifactByName = manifest.Artifacts.ToDictionary(artifact => artifact.Name, StringComparer.Ordinal);
    if (!artifactByName.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedArtifactNames))
        throw new InvalidOperationException("release.json artifact inventory does not match the Windows release contract.");

    var uploadAssets = new List<UploadAsset>();
    foreach (var name in expectedArtifactNames.Order())
    {
        var artifact = artifactByName[name];
        if (!IsSha256(artifact.Sha256))
            throw new InvalidOperationException($"Invalid SHA-256 for {name} in release.json.");
        var path = Path.Combine(releaseDirectory, name);
        var size = new FileInfo(path).Length;
        if (size != artifact.Size)
            throw new InvalidOperationException($"Size mismatch for {name}: release.json={artifact.Size}, actual={size}.");
        var sha256 = Coordinator.Sha256(path);
        if (!sha256.Equals(artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SHA-256 mismatch for {name}: release.json={artifact.Sha256}, actual={sha256}.");
        var remoteName = name == "latest.yml" && manifest.Architecture == "arm64" ? "latest-arm64.yml" : name;
        uploadAssets.Add(new UploadAsset(remoteName, path, size, sha256.ToLowerInvariant()));
    }

    ValidateUpdateManifest(
        Path.Combine(releaseDirectory, "latest.yml"),
        Path.Combine(releaseDirectory, installerName),
        installerName,
        manifest.OpenChamberVersion
    );

    return new LocalRelease(
        releaseDirectory,
        manifest.OpenChamberVersion,
        manifest.Architecture,
        manifest.Source.OpenChamberCommit.ToLowerInvariant(),
        $"v{manifest.OpenChamberVersion}",
        uploadAssets
    );
}

static void ValidateUpdateManifest(string manifestPath, string installerPath, string installerName, string version)
{
    var content = File.ReadAllText(manifestPath);
    var manifestVersion = RequiredMatch(content, @"(?m)^version:\s*(\S+)\s*$", "version", manifestPath);
    var manifestUrl = RequiredMatch(content, @"(?m)^\s*-\s+url:\s*(\S+)\s*$", "files[0].url", manifestPath);
    var manifestSha512 = RequiredMatch(content, @"(?m)^\s+sha512:\s*(\S+)\s*$", "files[0].sha512", manifestPath);
    var manifestSizeText = RequiredMatch(content, @"(?m)^\s+size:\s*(\d+)\s*$", "files[0].size", manifestPath);
    if (manifestVersion != version) throw new InvalidOperationException($"latest.yml version mismatch: expected {version}, got {manifestVersion}.");
    if (Uri.UnescapeDataString(manifestUrl) != installerName)
        throw new InvalidOperationException($"latest.yml installer mismatch: expected {installerName}, got {manifestUrl}.");
    if (!long.TryParse(manifestSizeText, out var manifestSize) || manifestSize != new FileInfo(installerPath).Length)
        throw new InvalidOperationException($"latest.yml installer size mismatch for {installerName}.");
    using var stream = File.OpenRead(installerPath);
    var sha512 = Convert.ToBase64String(SHA512.HashData(stream));
    if (manifestSha512 != sha512) throw new InvalidOperationException($"latest.yml SHA-512 mismatch for {installerName}.");
}

static string RequiredMatch(string content, string pattern, string field, string path)
{
    var match = Regex.Match(content, pattern);
    if (!match.Success) throw new InvalidOperationException($"Missing {field} in {path}.");
    return match.Groups[1].Value;
}

static RemoteContext ValidateRemote(LocalRelease localRelease, string workingDirectory)
{
    var login = RequireGh(["api", "user", "--jq", ".login"], workingDirectory).StandardOutput.Trim();
    if (!login.Equals(RequiredGitHubLogin, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"gh must be authenticated as {RequiredGitHubLogin}, got {login}.");

    var repository = RequireGhJson<GitHubRepository>(["api", $"repos/{TargetRepository}"], workingDirectory);
    if (!repository.FullName.Equals(TargetRepository, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Resolved unexpected target repository: {repository.FullName}");
    if (repository.Private)
        throw new InvalidOperationException($"{TargetRepository} must remain public for the unauthenticated desktop GitHub updater.");
    if (repository.Permissions?.Push != true)
        throw new InvalidOperationException($"The active gh account does not have push permission for {TargetRepository}.");

    var official = GetRelease(OfficialRepository, localRelease.Tag, workingDirectory)
        ?? throw new InvalidOperationException($"Official release {OfficialRepository}@{localRelease.Tag} does not exist.");
    if (official.Draft || official.Prerelease)
        throw new InvalidOperationException($"Official release {OfficialRepository}@{localRelease.Tag} must be published and stable.");
    if (official.TagName != localRelease.Tag)
        throw new InvalidOperationException($"Official release tag mismatch: expected {localRelease.Tag}, got {official.TagName}.");

    var targetCommit = TryResolveCommit(TargetRepository, localRelease.OpenChamberCommit, workingDirectory);
    if (targetCommit is null)
        throw new InvalidOperationException(
            $"OpenChamber source commit {localRelease.OpenChamberCommit} is not available in {TargetRepository}. "
            + "Push that exact commit before creating the Draft; this script never pushes source."
        );
    if (!targetCommit.Equals(localRelease.OpenChamberCommit, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Target repository resolved source commit {localRelease.OpenChamberCommit} to {targetCommit}.");

    var tagCommit = TryResolveCommit(TargetRepository, localRelease.Tag, workingDirectory);
    if (tagCommit is not null && !tagCommit.Equals(localRelease.OpenChamberCommit, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Target tag {localRelease.Tag} points to {tagCommit}, expected {localRelease.OpenChamberCommit}.");

    var targetRelease = FindTargetRelease(localRelease.Tag, workingDirectory);
    if (targetRelease is not null)
    {
        if (!targetRelease.Draft)
            throw new InvalidOperationException($"Target release {TargetRepository}@{localRelease.Tag} is already published.");
        ValidateReleaseMetadata(targetRelease, official, requireDraft: true);
        ValidateReleaseTarget(targetRelease, localRelease, tagCommit);
        ValidateAssets(targetRelease.Assets, localRelease.Assets, requireComplete: false);
    }

    return new RemoteContext(official, targetRelease, tagCommit is not null);
}

static void PrintPlan(LocalRelease localRelease, RemoteContext remote)
{
    Console.WriteLine($"Target: {TargetRepository}");
    Console.WriteLine($"Official notes: {OfficialRepository}@{localRelease.Tag}");
    Console.WriteLine($"Local release: {localRelease.Directory}");
    Console.WriteLine($"Source commit: {localRelease.OpenChamberCommit}");
    Console.WriteLine($"Mode: {(remote.TargetRelease is null ? "create draft" : "resume matching draft")}");
    Console.WriteLine("Assets:");
    foreach (var asset in localRelease.Assets.OrderBy(asset => asset.Name))
        Console.WriteLine($"  {asset.Name} ({asset.Size} bytes, sha256:{asset.Sha256})");
}

static void CreateDraft(LocalRelease localRelease, RemoteContext remote, string workingDirectory)
{
    var tempDirectory = Path.Combine(Path.GetTempPath(), $"openchamber-release-{localRelease.Version}-{Environment.ProcessId}");
    Directory.CreateDirectory(tempDirectory);
    try
    {
        var notesPath = Path.Combine(tempDirectory, "release-notes.md");
        File.WriteAllText(notesPath, remote.OfficialRelease.Body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var uploadAssets = PrepareUploadAssets(localRelease, tempDirectory);

        var targetRelease = FindTargetRelease(localRelease.Tag, workingDirectory);
        if (targetRelease is null)
        {
            var arguments = new List<string>
            {
                "release", "create", localRelease.Tag,
            };
            arguments.AddRange(uploadAssets.Select(asset => asset.Path));
            arguments.AddRange([
                "--repo", TargetRepository,
                "--draft",
                "--title", remote.OfficialRelease.Name,
                "--notes-file", notesPath,
            ]);
            if (remote.TagExists)
                arguments.Add("--verify-tag");
            else
            {
                arguments.Add("--target");
                arguments.Add(localRelease.OpenChamberCommit);
            }
            RequireGh(arguments, workingDirectory, echoOutput: true);
            targetRelease = WaitForTargetRelease(
                localRelease.Tag,
                workingDirectory,
                release => release.Assets.Count == localRelease.Assets.Count
            )
                ?? throw new InvalidOperationException($"Draft release {localRelease.Tag} was not visible after creation.");
        }

        ValidateReleaseMetadata(targetRelease, remote.OfficialRelease, requireDraft: true);
        var tagCommit = TryResolveCommit(TargetRepository, localRelease.Tag, workingDirectory);
        ValidateReleaseTarget(targetRelease, localRelease, tagCommit);
        ValidateAssets(targetRelease.Assets, localRelease.Assets, requireComplete: false);

        foreach (var asset in uploadAssets)
        {
            var existing = targetRelease.Assets.SingleOrDefault(candidate => candidate.Name == asset.Name);
            if (existing is not null)
            {
                Console.WriteLine($"Asset already verified, skipping: {asset.Name}");
                continue;
            }
            RequireGh(["release", "upload", localRelease.Tag, asset.Path, "--repo", TargetRepository], workingDirectory, echoOutput: true);
            targetRelease = WaitForTargetRelease(
                localRelease.Tag,
                workingDirectory,
                release => release.Assets.Any(candidate => candidate.Name == asset.Name)
            )
                ?? throw new InvalidOperationException($"Draft release {localRelease.Tag} disappeared during upload.");
            ValidateAssets(targetRelease.Assets, localRelease.Assets, requireComplete: false);
        }

        targetRelease = WaitForTargetRelease(
            localRelease.Tag,
            workingDirectory,
            release => release.Assets.Count == localRelease.Assets.Count
        )
            ?? throw new InvalidOperationException($"Draft release {localRelease.Tag} disappeared before final verification.");
        ValidateReleaseMetadata(targetRelease, remote.OfficialRelease, requireDraft: true);
        tagCommit = TryResolveCommit(TargetRepository, localRelease.Tag, workingDirectory);
        ValidateReleaseTarget(targetRelease, localRelease, tagCommit);
        ValidateAssets(targetRelease.Assets, localRelease.Assets, requireComplete: true);

        Console.WriteLine($"Draft created and verified: {targetRelease.HtmlUrl}");
        Console.WriteLine("Review and publish the release manually on GitHub.");
    }
    finally
    {
        if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true);
    }
}

static IReadOnlyList<UploadFile> PrepareUploadAssets(LocalRelease localRelease, string tempDirectory)
{
    var result = new List<UploadFile>();
    foreach (var asset in localRelease.Assets)
    {
        var path = asset.Path;
        if (!Path.GetFileName(path).Equals(asset.Name, StringComparison.Ordinal))
        {
            path = Path.Combine(tempDirectory, asset.Name);
            File.Copy(asset.Path, path, overwrite: false);
            if (!Coordinator.Sha256(path).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Staged upload asset hash mismatch: {asset.Name}");
        }
        result.Add(new UploadFile(asset.Name, path));
    }
    return result;
}

static void ValidateReleaseMetadata(GitHubRelease target, GitHubRelease official, bool requireDraft)
{
    if (target.Draft != requireDraft)
        throw new InvalidOperationException($"Target release {target.TagName} draft state mismatch: expected {requireDraft}, got {target.Draft}.");
    if (target.Prerelease) throw new InvalidOperationException($"Target release {target.TagName} must not be a prerelease.");
    if (target.TagName != official.TagName) throw new InvalidOperationException($"Target release tag does not mirror {OfficialRepository}.");
    if (target.Name != official.Name) throw new InvalidOperationException($"Target release title does not mirror {OfficialRepository}.");
    if (target.Body != official.Body) throw new InvalidOperationException($"Target release notes do not exactly mirror {OfficialRepository}.");
}

static void ValidateReleaseTarget(GitHubRelease target, LocalRelease localRelease, string? tagCommit)
{
    if (tagCommit is not null)
    {
        if (!tagCommit.Equals(localRelease.OpenChamberCommit, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Target tag {localRelease.Tag} points to {tagCommit}, expected {localRelease.OpenChamberCommit}.");
        return;
    }
    if (!target.TargetCommitish.Equals(localRelease.OpenChamberCommit, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            $"Draft {localRelease.Tag} targets {target.TargetCommitish}, expected exact source commit {localRelease.OpenChamberCommit}."
        );
}

static void ValidateAssets(IReadOnlyList<GitHubAsset> actual, IReadOnlyList<UploadAsset> expected, bool requireComplete)
{
    var expectedByName = expected.ToDictionary(asset => asset.Name, StringComparer.Ordinal);
    var duplicate = actual.GroupBy(asset => asset.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
    if (duplicate is not null) throw new InvalidOperationException($"Duplicate target release asset: {duplicate.Key}");
    foreach (var asset in actual)
    {
        if (!expectedByName.TryGetValue(asset.Name, out var expectedAsset))
            throw new InvalidOperationException($"Target release contains a non-Windows or unexpected asset: {asset.Name}");
        if (asset.Size != expectedAsset.Size)
            throw new InvalidOperationException($"Target asset size mismatch for {asset.Name}: expected {expectedAsset.Size}, got {asset.Size}.");
        var expectedDigest = $"sha256:{expectedAsset.Sha256}";
        if (!expectedDigest.Equals(asset.Digest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Target asset digest mismatch for {asset.Name}: expected {expectedDigest}, got {asset.Digest ?? "(missing)"}.");
    }
    if (requireComplete && actual.Count != expected.Count)
    {
        var missing = expectedByName.Keys.Except(actual.Select(asset => asset.Name), StringComparer.Ordinal);
        throw new InvalidOperationException($"Target release is missing assets: {string.Join(", ", missing)}");
    }
}

static GitHubRelease? GetRelease(string repository, string tag, string workingDirectory) =>
    TryGhJson<GitHubRelease>(["api", $"repos/{repository}/releases/tags/{tag}"], workingDirectory, 404);

static GitHubRelease? FindTargetRelease(string tag, string workingDirectory)
{
    var pages = RequireGhJson<IReadOnlyList<IReadOnlyList<GitHubRelease>>>(
        ["api", "--paginate", "--slurp", $"repos/{TargetRepository}/releases?per_page=100"],
        workingDirectory
    );
    var matches = pages.SelectMany(page => page).Where(release => release.TagName == tag).ToArray();
    return matches.Length switch
    {
        0 => null,
        1 => matches[0],
        _ => throw new InvalidOperationException($"Multiple target releases use tag {tag}."),
    };
}

static GitHubRelease? WaitForTargetRelease(string tag, string workingDirectory, Func<GitHubRelease, bool>? ready = null)
{
    for (var attempt = 0; attempt < 10; attempt++)
    {
        var release = FindTargetRelease(tag, workingDirectory);
        if (release is not null && (ready is null || ready(release))) return release;
        Thread.Sleep(TimeSpan.FromSeconds(1));
    }
    return null;
}

static string? TryResolveCommit(string repository, string reference, string workingDirectory)
{
    var result = RunGh(["api", $"repos/{repository}/commits/{reference}", "--jq", ".sha"], workingDirectory);
    if (result.ExitCode == 0) return result.StandardOutput.Trim();
    if (HasHttpStatus(result, 404) || HasHttpStatus(result, 422)) return null;
    throw GhFailure(result);
}

static T RequireGhJson<T>(IReadOnlyList<string> arguments, string workingDirectory) where T : class
{
    var result = RequireGh(arguments, workingDirectory);
    return JsonSerializer.Deserialize<T>(result.StandardOutput, Json.Options)
        ?? throw new InvalidOperationException($"gh returned invalid JSON for {typeof(T).Name}.");
}

static T? TryGhJson<T>(IReadOnlyList<string> arguments, string workingDirectory, int missingStatus) where T : class
{
    var result = RunGh(arguments, workingDirectory);
    if (result.ExitCode == 0)
        return JsonSerializer.Deserialize<T>(result.StandardOutput, Json.Options)
            ?? throw new InvalidOperationException($"gh returned invalid JSON for {typeof(T).Name}.");
    if (HasHttpStatus(result, missingStatus)) return null;
    throw GhFailure(result);
}

static CommandResult RequireGh(IReadOnlyList<string> arguments, string workingDirectory, bool echoOutput = false)
{
    var result = RunGh(arguments, workingDirectory);
    if (result.ExitCode != 0) throw GhFailure(result);
    if (echoOutput && result.StandardOutput.Trim().Length > 0) Console.WriteLine(result.StandardOutput.Trim());
    return result;
}

static CommandResult RunGh(IReadOnlyList<string> arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "gh",
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    startInfo.Environment["GH_HOST"] = "github.com";
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start gh.");
    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    return new CommandResult(process.ExitCode, standardOutput.GetAwaiter().GetResult(), standardError.GetAwaiter().GetResult(), arguments);
}

static bool HasHttpStatus(CommandResult result, int status)
{
    var marker = $"HTTP {status}";
    return result.StandardOutput.Contains(marker, StringComparison.OrdinalIgnoreCase)
        || result.StandardError.Contains(marker, StringComparison.OrdinalIgnoreCase)
        || result.StandardOutput.Contains($"\"status\":\"{status}\"", StringComparison.Ordinal)
        || result.StandardOutput.Contains($"\"status\":{status}", StringComparison.Ordinal);
}

static InvalidOperationException GhFailure(CommandResult result)
{
    var detail = string.Join(Environment.NewLine, new[] { result.StandardOutput.Trim(), result.StandardError.Trim() }.Where(value => value.Length > 0));
    return new InvalidOperationException(
        $"gh failed with exit code {result.ExitCode}: gh {string.Join(' ', result.Arguments)}"
        + (detail.Length > 0 ? Environment.NewLine + detail : "")
    );
}

static void RequireFile(string path)
{
    if (!File.Exists(path)) throw new FileNotFoundException("Required file not found.", path);
}

static void PrintHelp()
{
    Console.WriteLine("Usage: dotnet .\\scripts\\Publish-OpenChamberRelease.cs -- RELEASE_DIRECTORY");
    Console.WriteLine();
    Console.WriteLine($"Mirrors release title and notes from {OfficialRepository} and uploads only validated Windows assets to a draft in {TargetRepository}.");
    Console.WriteLine("The command never publishes the draft or changes the repository's Latest release.");
}

static string GetSourcePath([CallerFilePath] string path = "") => path;

static bool IsStableVersion(string value) => Regex.IsMatch(value, @"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant);
static bool IsCommit(string value) => Regex.IsMatch(value, @"^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);
static bool IsSha256(string value) => Regex.IsMatch(value, @"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

sealed record PublishOptions(string? ReleaseDirectory, bool ShowHelp)
{
    public static PublishOptions Parse(string[] arguments)
    {
        string? releaseDirectory = null;
        var showHelp = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    if (arguments[index].StartsWith('-')) throw new ArgumentException($"Unknown argument: {arguments[index]}");
                    if (releaseDirectory is not null) throw new ArgumentException("Release directory is the only positional argument.");
                    releaseDirectory = arguments[index];
                    break;
            }
        }
        if (!showHelp && string.IsNullOrWhiteSpace(releaseDirectory))
            throw new ArgumentException("Release directory is required.");
        return new PublishOptions(releaseDirectory, showHelp);
    }
}

sealed class ReleaseManifest
{
    public int Schema { get; init; }
    public string Platform { get; init; } = "";
    public string Architecture { get; init; } = "";
    public string OpenChamberVersion { get; init; } = "";
    public ReleaseSource Source { get; init; } = new();
    public IReadOnlyList<ReleaseArtifact> Artifacts { get; init; } = [];
}

sealed class ReleaseSource
{
    public string OpenChamberCommit { get; init; } = "";
}

sealed class ReleaseArtifact
{
    public string Name { get; init; } = "";
    public long Size { get; init; }
    public string Sha256 { get; init; } = "";
}

sealed class GitHubRepository
{
    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = "";
    public bool Private { get; init; }
    public GitHubPermissions? Permissions { get; init; }
}

sealed class GitHubPermissions
{
    public bool Push { get; init; }
}

sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = "";
    [JsonPropertyName("target_commitish")]
    public string TargetCommitish { get; init; } = "";
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = "";
    public string Name { get; init; } = "";
    public string Body { get; init; } = "";
    public bool Draft { get; init; }
    public bool Prerelease { get; init; }
    public IReadOnlyList<GitHubAsset> Assets { get; init; } = [];
}

sealed class GitHubAsset
{
    public string Name { get; init; } = "";
    public long Size { get; init; }
    public string? Digest { get; init; }
}

sealed record UploadAsset(string Name, string Path, long Size, string Sha256);
sealed record UploadFile(string Name, string Path);
sealed record LocalRelease(string Directory, string Version, string Architecture, string OpenChamberCommit, string Tag, IReadOnlyList<UploadAsset> Assets);
sealed record RemoteContext(GitHubRelease OfficialRelease, GitHubRelease? TargetRelease, bool TagExists);
sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError, IReadOnlyList<string> Arguments);

static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
}
