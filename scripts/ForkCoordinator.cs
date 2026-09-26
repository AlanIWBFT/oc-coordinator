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
        var candidateOptions = CandidateOptions.Parse(commandArgs);
        if (candidateOptions.ShowHelp)
        {
            PrintCandidateHelp();
            break;
        }
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
    var architecture = Coordinator.GetArchitecture();
    var bunExecutable = ResolveBuildBunExecutable(coordinatorRoot);
    var environment = BuildEnvironment(config, architecture);
    if (architecture == "arm64") PrepareX64WindowsProcessBroker(environment, config, coordinatorRoot, bunExecutable);
    AddVisualStudioBuildEnvironment(environment, coordinatorRoot, architecture);
    ConfigureBunBuildEnvironment(environment, bunExecutable);
    environment["OPENCODE_WINDOWS_PROCESS_BROKER_PREBUILT"] = architecture == "arm64" ? "1" : null;
    var electronRoot = Path.Combine(config.OpenChamberRoot, "packages", "electron");
    Coordinator.Run(
        "pwsh",
        ["-NoProfile", "-File", Path.Combine(coordinatorRoot, "scripts", "Sync-OpenCodeSdk.ps1")],
        coordinatorRoot,
        environment
    );
    Coordinator.Run(bunExecutable, ["run", "--cwd", Path.Combine(config.OpenChamberRoot, "packages", "sdk"), "build"], coordinatorRoot, environment);
    Coordinator.Run(bunExecutable, ["electron:build"], config.OpenChamberRoot, environment);

    var unpackedDirectory = GetUnpackedDirectory(config);
    var openChamberBinary = Path.Combine(unpackedDirectory, "OpenChamber.exe");
    var bundledOpenCode = Path.Combine(unpackedDirectory, "resources", "opencode-cli", "opencode.exe");
    var recycleHelper = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "OpenCode.Windows.RecycleBin.dll");
    var processBroker = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "OpenCode.ProcessBroker.exe");
    var openChamberVersion = Coordinator.GetPinnedOpenChamberVersion(config);
    var openCodeVersion = Coordinator.GetPinnedOpenCodeVersion(config);
    if (Coordinator.GetOpenChamberBinaryVersion(openChamberBinary) != openChamberVersion)
        throw new InvalidOperationException("Packaged OpenChamber version does not match the current checkout.");
    Coordinator.AssertOpenCodeBinaryVersion(bundledOpenCode, openCodeVersion, coordinatorRoot);
    AssertWindowsGuiSubsystem(bundledOpenCode);
    RequireFile(recycleHelper);
    AssertWindowsProcessBroker(processBroker, coordinatorRoot);
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
    var bunExecutable = ResolveBuildBunExecutable(coordinatorRoot);
    EnsureRepositoryReady(openChamberRoot, config.OpenChamberBranch, coordinatorRoot);
    EnsureRepositoryReady(openCodeRoot, config.OpenCodeBranch, coordinatorRoot);

    var openChamberVersion = Coordinator.GetPinnedOpenChamberVersion(config);
    var openCodeVersion = Coordinator.GetPinnedOpenCodeVersion(config);
    foreach (var package in new[] { "client", "schema", "protocol" })
    {
        var sdkVersion = Coordinator.ReadJsonString(Path.Combine(openCodeRoot, "packages", package, "package.json"), "version");
        if (openCodeVersion != sdkVersion)
            throw new InvalidOperationException($"OpenCode {package} version {sdkVersion} does not match OpenChamber pin {openCodeVersion}.");
    }
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
        var environment = BuildEnvironment(config, architecture);
        if (architecture == "arm64") PrepareX64WindowsProcessBroker(environment, config, coordinatorRoot, bunExecutable);
        AddVisualStudioBuildEnvironment(environment, coordinatorRoot, architecture);
        ConfigureBunBuildEnvironment(environment, bunExecutable);
        environment["OPENCODE_WINDOWS_PROCESS_BROKER_PREBUILT"] = architecture == "arm64" ? "1" : null;
        Coordinator.Run(
            "pwsh",
            ["-NoProfile", "-File", Path.Combine(coordinatorRoot, "scripts", "Sync-OpenCodeSdk.ps1"), "-SkipTypeCheck"],
            coordinatorRoot,
            environment
        );
        EnsureWorktreeClean(openChamberRoot, coordinatorRoot);
        EnsureWorktreeClean(openCodeRoot, coordinatorRoot);
        Coordinator.Run(bunExecutable, ["run", "--cwd", openChamberRoot, "type-check"], coordinatorRoot, environment);
        Coordinator.Run(bunExecutable, ["run", "--cwd", openChamberRoot, "lint"], coordinatorRoot, environment);
        if (Directory.Exists(distRoot)) Directory.Delete(distRoot, recursive: true);

        Coordinator.Run(bunExecutable, ["run", "--cwd", Path.Combine(openChamberRoot, "packages", "sdk"), "build"], coordinatorRoot, environment);
        Coordinator.Run(bunExecutable, ["run", "--cwd", electronRoot, "build:web-assets"], coordinatorRoot, environment);
        Coordinator.Run(bunExecutable, ["run", "--cwd", electronRoot, "prepare:opencode-cli"], coordinatorRoot, environment);
        Coordinator.Run(bunExecutable, ["run", "--cwd", electronRoot, "verify:opencode-cli"], coordinatorRoot, environment);
        Coordinator.Run(bunExecutable, ["run", "--cwd", electronRoot, "bundle:main"], coordinatorRoot, environment);
        Coordinator.Run(bunExecutable, ["run", "--cwd", electronRoot, "rebuild:native"], coordinatorRoot, environment);
        Coordinator.Run(
            "node",
            [Path.Combine(electronRoot, "scripts", "package.mjs"), "--win", $"--{architecture}", "--publish=never"],
            electronRoot,
            environment
        );
        Coordinator.Run(bunExecutable, ["run", "--cwd", electronRoot, "verify:opencode-cli:packaged"], coordinatorRoot, environment);

        var installerPath = Path.Combine(distRoot, $"OpenChamber-{openChamberVersion}-win-{architecture}.exe");
        var blockmapPath = $"{installerPath}.blockmap";
        var updateManifestPath = Path.Combine(distRoot, "latest.yml");
        RequireFile(installerPath);
        RequireFile(blockmapPath);
        RequireFile(updateManifestPath);
        var unpackedName = architecture == "x64" ? "win-unpacked" : "win-arm64-unpacked";
        var bundledOpenCode = Path.Combine(distRoot, unpackedName, "resources", "opencode-cli", "opencode.exe");
        var recycleHelper = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "OpenCode.Windows.RecycleBin.dll");
        var processBroker = Path.Combine(Path.GetDirectoryName(bundledOpenCode)!, "OpenCode.ProcessBroker.exe");
        Coordinator.AssertOpenCodeBinaryVersion(bundledOpenCode, openCodeVersion, coordinatorRoot);
        AssertWindowsGuiSubsystem(bundledOpenCode);
        RequireFile(recycleHelper);
        AssertWindowsProcessBroker(processBroker, coordinatorRoot);
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

static Dictionary<string, string?> BuildEnvironment(CoordinatorConfig config, string architecture) => new(StringComparer.OrdinalIgnoreCase)
{
    ["OPENCHAMBER_OPENCODE_SOURCE_DIR"] = config.OpenCodeRoot,
    ["OPENCHAMBER_OPENCODE_CLI_VERSION"] = Coordinator.GetPinnedOpenCodeVersion(config),
    ["OPENCHAMBER_TARGET_ARCH"] = architecture,
    ["OPENCODE_WINDOWS_PROCESS_BROKER_PREBUILT"] = null,
    ["XDG_CACHE_HOME"] = null,
    ["BUN_RUNTIME_TRANSPILER_CACHE_PATH"] = GetBunRuntimeTranspilerCachePath(),
};

static void AddVisualStudioBuildEnvironment(Dictionary<string, string?> environment, string coordinatorRoot, string architecture)
{
    foreach (var variable in Coordinator.LoadVisualStudioBuildEnvironment(coordinatorRoot, architecture))
        environment[variable.Key] = variable.Value;
}

static void PrepareX64WindowsProcessBroker(
    Dictionary<string, string?> environment,
    CoordinatorConfig config,
    string coordinatorRoot,
    string bunExecutable
)
{
    var brokerEnvironment = new Dictionary<string, string?>(environment, StringComparer.OrdinalIgnoreCase);
    AddVisualStudioBuildEnvironment(brokerEnvironment, coordinatorRoot, "x64");
    ConfigureBunBuildEnvironment(brokerEnvironment, bunExecutable);
    brokerEnvironment["OPENCODE_WINDOWS_PROCESS_BROKER_PREBUILT"] = null;
    Coordinator.Run(
        bunExecutable,
        [
            "run",
            "--cwd",
            Path.Combine(config.OpenCodeRoot, "packages", "util"),
            "build:windows-process-broker"
        ],
        coordinatorRoot,
        brokerEnvironment
    );
}

static string ResolveBuildBunExecutable(string coordinatorRoot)
{
    var bunExecutable = Path.GetFullPath(Path.Combine(coordinatorRoot, "..", "bun-v1.4.2-release", "bun.exe"));
    RequireFile(bunExecutable);
    var version = Coordinator.Capture(bunExecutable, ["--version"], coordinatorRoot).Trim();
    if (version != "1.4.2")
        throw new InvalidOperationException($"Expected Bun 1.4.2 at {bunExecutable}, got {version}.");
    return bunExecutable;
}

static void ConfigureBunBuildEnvironment(Dictionary<string, string?> environment, string bunExecutable)
{
    environment["OPENCHAMBER_OPENCODE_BUN_RUNTIME"] = bunExecutable;
    environment["npm_execpath"] = bunExecutable;
    var path = environment.TryGetValue("PATH", out var configuredPath)
        ? configuredPath
        : Environment.GetEnvironmentVariable("PATH");
    environment["PATH"] = string.Join(Path.PathSeparator, new[] { Path.GetDirectoryName(bunExecutable), path }.Where(value => !string.IsNullOrWhiteSpace(value)));
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

static void AssertWindowsProcessBroker(string binary, string workingDirectory)
{
    RequireFile(binary);
    AssertWindowsGuiSubsystem(binary);
    using var stream = File.OpenRead(binary);
    using var reader = new PEReader(stream);
    if (reader.PEHeaders.CoffHeader.Machine != Machine.Amd64)
        throw new InvalidOperationException($"Bundled process broker must be an x64 executable, got {reader.PEHeaders.CoffHeader.Machine}: {binary}");
    var protocol = Coordinator.Capture(binary, ["--protocol-version"], workingDirectory).Trim();
    if (protocol != "2")
        throw new InvalidOperationException($"Bundled process broker protocol mismatch: expected 2, got {protocol}: {binary}");
    var runtime = Coordinator.Capture(binary, ["--runtime-kind"], workingDirectory).Trim();
    if (runtime != "nativeaot")
        throw new InvalidOperationException($"Bundled process broker must be NativeAOT, got {runtime}: {binary}");
}

static void RequireFile(string path)
{
    if (!File.Exists(path)) throw new FileNotFoundException("Required file not found.", path);
}

static void PrintHelp()
{
    Console.WriteLine("Usage: dotnet ForkCoordinator.cs -- <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  build-candidate");
    Console.WriteLine("  build-release [--output-root PATH]");
}

static void PrintCandidateHelp()
{
    Console.WriteLine("Usage: dotnet ForkCoordinator.cs -- build-candidate");
    Console.WriteLine();
    Console.WriteLine("Builds the unpacked Candidate app from the local OpenChamber and OpenCode sources.");
}

static void PrintReleaseHelp()
{
    Console.WriteLine("Usage: dotnet ForkCoordinator.cs -- build-release [--output-root PATH]");
    Console.WriteLine();
    Console.WriteLine("Builds an installable Windows NSIS package without uploading or installing it.");
}

static string GetSourcePath([CallerFilePath] string path = "") => path;

sealed record CandidateOptions(bool ShowHelp)
{
    public static CandidateOptions Parse(string[] arguments)
    {
        var showHelp = false;
        foreach (var argument in arguments)
        {
            switch (argument)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown build-candidate argument: {argument}");
            }
        }
        return new CandidateOptions(showHelp);
    }
}

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
