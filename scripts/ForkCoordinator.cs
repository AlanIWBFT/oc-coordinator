#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Coordinator.Common.cs

using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LocalFork;

if (!OperatingSystem.IsWindows())
    throw new InvalidOperationException("The local fork coordinator currently supports Windows only.");

var scriptPath = GetSourcePath();
var coordinatorRoot = Directory.GetParent(Path.GetDirectoryName(scriptPath)!)!.FullName;
var command = args.FirstOrDefault() ?? "--help";
var commandArgs = args.Skip(1).ToArray();

switch (command)
{
    case "build-candidate":
        RequireNoArguments(command, commandArgs);
        var candidateBuildTimer = Stopwatch.StartNew();
        BuildCandidate(Coordinator.LoadConfig(coordinatorRoot), coordinatorRoot);
        Console.WriteLine($"Build completed in {candidateBuildTimer.Elapsed:hh\\:mm\\:ss}.");
        break;
    case "build-release":
        var releaseOptions = ReleaseOptions.Parse(commandArgs);
        if (releaseOptions.ShowHelp)
        {
            PrintReleaseHelp();
            break;
        }
        var releaseBuildTimer = Stopwatch.StartNew();
        BuildReleasePackage(Coordinator.LoadConfig(coordinatorRoot), coordinatorRoot, releaseOptions);
        Console.WriteLine($"Build completed in {releaseBuildTimer.Elapsed:hh\\:mm\\:ss}.");
        break;
    case "--help":
    case "-h":
        PrintHelp();
        break;
    default:
        throw new ArgumentException($"Unknown command: {command}");
}

static void BuildCandidate(CoordinatorConfig config, string coordinatorRoot)
{
    var environment = BuildEnvironment(config);
    AddVisualStudioBuildEnvironment(environment, coordinatorRoot);
    Coordinator.Run(
        "pwsh",
        ["-NoProfile", "-File", Path.Combine(coordinatorRoot, "scripts", "Sync-OpenCodeSdk.ps1")],
        coordinatorRoot,
        environment
    );
    Coordinator.Run("bun", ["run", "--cwd", config.OpenChamberRoot, "electron:build"], coordinatorRoot, environment);

    var unpackedDirectory = GetUnpackedDirectory(config);
    var openChamberBinary = Path.Combine(unpackedDirectory, "OpenChamber.exe");
    var bundledOpenCode = Path.Combine(unpackedDirectory, "resources", "opencode-cli", "opencode.exe");
    var shutdownProtocolMarker = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "openchamber-shutdown-protocol.capability");
    var recycleHelper = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "OpenCode.Windows.RecycleBin.dll");
    var openChamberVersion = Coordinator.GetPinnedOpenChamberVersion(config);
    var openCodeVersion = Coordinator.GetPinnedOpenCodeVersion(config);
    if (Coordinator.GetOpenChamberBinaryVersion(openChamberBinary) != openChamberVersion)
        throw new InvalidOperationException("Packaged OpenChamber version does not match the current checkout.");
    Coordinator.AssertOpenCodeBinaryVersion(bundledOpenCode, openCodeVersion, coordinatorRoot);
    AssertWindowsGuiSubsystem(bundledOpenCode);
    RequireFile(shutdownProtocolMarker);
    RequireFile(recycleHelper);
    Console.WriteLine($"Candidate app ready: {openChamberBinary} (OpenChamber {openChamberVersion}, OpenCode {openCodeVersion})");
}

static void BuildReleasePackage(CoordinatorConfig config, string coordinatorRoot, ReleaseOptions options)
{
    var openChamberRoot = Path.GetFullPath(config.OpenChamberRoot);
    var openCodeRoot = Path.GetFullPath(config.OpenCodeRoot);
    var electronRoot = Path.Combine(openChamberRoot, "packages", "electron");
    var distRoot = Path.Combine(electronRoot, "dist");
    var releaseRoot = Path.GetFullPath(options.OutputRoot ?? config.ReleaseRoot);
    var architecture = Coordinator.GetArchitecture();
    EnsureRepositoryReady(openChamberRoot, config.OpenChamberBranch, coordinatorRoot);
    EnsureRepositoryReady(openCodeRoot, config.OpenCodeBranch, coordinatorRoot);

    var openChamberVersion = Coordinator.GetPinnedOpenChamberVersion(config);
    var openCodeVersion = Coordinator.GetPinnedOpenCodeVersion(config);
    var sdkVersion = Coordinator.ReadJsonString(Path.Combine(openCodeRoot, "packages", "sdk", "js", "package.json"), "version");
    if (openCodeVersion != sdkVersion)
        throw new InvalidOperationException($"OpenCode SDK version {sdkVersion} does not match OpenChamber pin {openCodeVersion}.");
    var openChamberCommit = CurrentCommit(openChamberRoot, coordinatorRoot);
    var openCodeCommit = CurrentCommit(openCodeRoot, coordinatorRoot);

    var releaseName = $"OpenChamber-{openChamberVersion}-win-{architecture}";
    var finalDirectory = Path.Combine(releaseRoot, releaseName);
    var stagingDirectory = Path.Combine(releaseRoot, $".{releaseName}.next-{Environment.ProcessId}");
    var backupDirectory = Path.Combine(releaseRoot, $".{releaseName}.previous-{Environment.ProcessId}");
    Directory.CreateDirectory(releaseRoot);
    Directory.CreateDirectory(stagingDirectory);
    var movedExistingRelease = false;

    try
    {
        var environment = BuildEnvironment(config);
        AddVisualStudioBuildEnvironment(environment, coordinatorRoot);
        Coordinator.Run(
            "pwsh",
            ["-NoProfile", "-File", Path.Combine(coordinatorRoot, "scripts", "Sync-OpenCodeSdk.ps1"), "-SkipTypeCheck"],
            coordinatorRoot,
            environment
        );
        EnsureWorktreeClean(openChamberRoot, coordinatorRoot);
        EnsureWorktreeClean(openCodeRoot, coordinatorRoot);
        Coordinator.Run("bun", ["run", "--cwd", openChamberRoot, "type-check"], coordinatorRoot, environment);
        Coordinator.Run("bun", ["run", "--cwd", openChamberRoot, "lint"], coordinatorRoot, environment);
        if (Directory.Exists(distRoot)) Directory.Delete(distRoot, recursive: true);

        Coordinator.Run("bun", ["run", "--cwd", electronRoot, "build:web-assets"], coordinatorRoot, environment);
        Coordinator.Run("bun", ["run", "--cwd", electronRoot, "prepare:opencode-cli"], coordinatorRoot, environment);
        Coordinator.Run("bun", ["run", "--cwd", electronRoot, "verify:opencode-cli"], coordinatorRoot, environment);
        Coordinator.Run("bun", ["run", "--cwd", electronRoot, "bundle:main"], coordinatorRoot, environment);
        Coordinator.Run("bun", ["run", "--cwd", electronRoot, "rebuild:native"], coordinatorRoot, environment);
        Coordinator.Run(
            "node",
            [Path.Combine(electronRoot, "scripts", "package.mjs"), "--win", $"--{architecture}", "--publish=never"],
            electronRoot,
            environment
        );
        Coordinator.Run("bun", ["run", "--cwd", electronRoot, "verify:opencode-cli:packaged"], coordinatorRoot, environment);

        var installerPath = Path.Combine(distRoot, $"OpenChamber-{openChamberVersion}-win-{architecture}.exe");
        var blockmapPath = $"{installerPath}.blockmap";
        var updateManifestPath = Path.Combine(distRoot, "latest.yml");
        RequireFile(installerPath);
        RequireFile(blockmapPath);
        RequireFile(updateManifestPath);
        var unpackedName = architecture == "x64" ? "win-unpacked" : "win-arm64-unpacked";
        var bundledOpenCode = Path.Combine(distRoot, unpackedName, "resources", "opencode-cli", "opencode.exe");
        var shutdownProtocolMarker = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "openchamber-shutdown-protocol.capability");
        var recycleHelper = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "OpenCode.Windows.RecycleBin.dll");
        Coordinator.AssertOpenCodeBinaryVersion(bundledOpenCode, openCodeVersion, coordinatorRoot);
        AssertWindowsGuiSubsystem(bundledOpenCode);
        RequireFile(shutdownProtocolMarker);
        RequireFile(recycleHelper);
        AssertReleaseDatabasePath(bundledOpenCode, stagingDirectory, coordinatorRoot, "opencode-dev.db");
        Coordinator.Run(
            "node",
            [Path.Combine(electronRoot, "scripts", "verify-update-manifest.mjs"), updateManifestPath, installerPath, openChamberVersion],
            coordinatorRoot
        );

        EnsureWorktreeClean(openChamberRoot, coordinatorRoot);
        EnsureWorktreeClean(openCodeRoot, coordinatorRoot);
        if (CurrentCommit(openChamberRoot, coordinatorRoot) != openChamberCommit)
            throw new InvalidOperationException("OpenChamber HEAD changed during release packaging.");
        if (CurrentCommit(openCodeRoot, coordinatorRoot) != openCodeCommit)
            throw new InvalidOperationException("OpenCode HEAD changed during release packaging.");

        var sourceArtifacts = new[] { installerPath, blockmapPath, updateManifestPath };
        foreach (var artifact in sourceArtifacts)
            File.Copy(artifact, Path.Combine(stagingDirectory, Path.GetFileName(artifact)), overwrite: false);
        var copiedArtifacts = sourceArtifacts
            .Select(path => Path.Combine(stagingDirectory, Path.GetFileName(path)))
            .Select(path => new
            {
                name = Path.GetFileName(path),
                size = new FileInfo(path).Length,
                sha256 = Coordinator.Sha256(path),
            })
            .ToArray();
        var releaseManifest = new
        {
            schema = 1,
            platform = "win32",
            architecture,
            openChamberVersion,
            openCodeVersion,
            openCodeChannel = "dev",
            defaultOpenCodeDatabase = "opencode-dev.db",
            bundledOpenCodeSha256 = Coordinator.Sha256(bundledOpenCode),
            source = new { openChamberCommit, openCodeCommit },
            preparedAt = DateTimeOffset.UtcNow.ToString("O"),
            artifacts = copiedArtifacts,
        };
        File.WriteAllText(Path.Combine(stagingDirectory, "release.json"), JsonSerializer.Serialize(releaseManifest, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

        if (Directory.Exists(finalDirectory))
        {
            if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, recursive: true);
            Directory.Move(finalDirectory, backupDirectory);
            movedExistingRelease = true;
        }
        Directory.Move(stagingDirectory, finalDirectory);
        if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, recursive: true);
        Console.WriteLine($"Release package ready: {finalDirectory}");
    }
    catch
    {
        if (movedExistingRelease && Directory.Exists(backupDirectory))
        {
            if (Directory.Exists(finalDirectory)) Directory.Delete(finalDirectory, recursive: true);
            Directory.Move(backupDirectory, finalDirectory);
        }
        if (Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, recursive: true);
        throw;
    }
}

static Dictionary<string, string?> BuildEnvironment(CoordinatorConfig config) => new(StringComparer.OrdinalIgnoreCase)
{
    ["OPENCHAMBER_OPENCODE_SOURCE_DIR"] = config.OpenCodeRoot,
    ["OPENCHAMBER_OPENCODE_CLI_VERSION"] = Coordinator.GetPinnedOpenCodeVersion(config),
    ["XDG_CACHE_HOME"] = null,
    ["BUN_RUNTIME_TRANSPILER_CACHE_PATH"] = GetBunRuntimeTranspilerCachePath(),
};

static void AddVisualStudioBuildEnvironment(Dictionary<string, string?> environment, string coordinatorRoot)
{
    foreach (var variable in Coordinator.LoadVisualStudioBuildEnvironment(coordinatorRoot, Coordinator.GetArchitecture()))
        environment[variable.Key] = variable.Value;
}

static string GetUnpackedDirectory(CoordinatorConfig config)
{
    var name = Coordinator.GetArchitecture() == "arm64" ? "win-arm64-unpacked" : "win-unpacked";
    return Path.Combine(config.OpenChamberRoot, "packages", "electron", "dist", name);
}

static string GetBunRuntimeTranspilerCachePath() =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bun", "install", "cache", "@t@");

static void EnsureRepositoryReady(string repository, string expectedBranch, string workingDirectory)
{
    var branch = Coordinator.Capture("git", ["-C", repository, "branch", "--show-current"], workingDirectory).Trim();
    if (branch != expectedBranch)
        throw new InvalidOperationException($"Expected branch {expectedBranch} in {repository}, got {branch}.");
    EnsureWorktreeClean(repository, workingDirectory);
}

static void EnsureWorktreeClean(string repository, string workingDirectory)
{
    var status = Coordinator.Capture("git", ["-C", repository, "status", "--porcelain"], workingDirectory);
    if (!string.IsNullOrWhiteSpace(status))
        throw new InvalidOperationException($"Release builds require a clean worktree: {repository}");
}

static string CurrentCommit(string repository, string workingDirectory) =>
    Coordinator.Capture("git", ["-C", repository, "rev-parse", "HEAD"], workingDirectory).Trim();

static void AssertReleaseDatabasePath(string binary, string stagingDirectory, string workingDirectory, string expectedDatabase)
{
    var sandbox = Path.Combine(stagingDirectory, "database-verification");
    try
    {
        var output = Coordinator.Capture(binary, ["db", "path"], workingDirectory, new Dictionary<string, string?>
        {
            ["XDG_DATA_HOME"] = sandbox,
            ["OPENCODE_DB"] = null,
        });
        var actual = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).LastOrDefault() ?? "";
        var expected = Path.Combine(sandbox, "opencode", expectedDatabase);
        if (!Path.GetFullPath(actual).Equals(Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Release OpenCode database mismatch: expected {expected}, got {actual}.");
    }
    finally
    {
        if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true);
    }
}

static void AssertWindowsGuiSubsystem(string binary)
{
    using var stream = File.OpenRead(binary);
    using var reader = new PEReader(stream);
    var subsystem = reader.PEHeaders.PEHeader?.Subsystem
        ?? throw new InvalidOperationException($"Bundled OpenCode is not a PE executable: {binary}");
    if (subsystem != Subsystem.WindowsGui)
        throw new InvalidOperationException($"Bundled OpenCode must use the Windows GUI subsystem, got {subsystem}: {binary}");
}

static void RequireFile(string path)
{
    if (!File.Exists(path)) throw new FileNotFoundException("Required file not found.", path);
}

static void RequireNoArguments(string command, string[] arguments)
{
    if (arguments.Length > 0)
        throw new ArgumentException($"{command} does not accept arguments: {string.Join(' ', arguments)}");
}

static void PrintHelp()
{
    Console.WriteLine("Usage: dotnet ForkCoordinator.cs -- <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  build-candidate");
    Console.WriteLine("  build-release [--output-root PATH]");
}

static void PrintReleaseHelp()
{
    Console.WriteLine("Usage: dotnet ForkCoordinator.cs -- build-release [--output-root PATH]");
    Console.WriteLine();
    Console.WriteLine("Builds an installable Windows NSIS package without uploading or installing it.");
}

static string GetSourcePath([CallerFilePath] string path = "") => path;

sealed record ReleaseOptions(string? OutputRoot, bool ShowHelp)
{
    public static ReleaseOptions Parse(string[] arguments)
    {
        string? outputRoot = null;
        var showHelp = false;
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--output-root":
                    if (++index >= arguments.Length) throw new ArgumentException("--output-root requires a path.");
                    outputRoot = arguments[index];
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown build-release argument: {arguments[index]}");
            }
        }
        return new ReleaseOptions(outputRoot, showHelp);
    }
}
