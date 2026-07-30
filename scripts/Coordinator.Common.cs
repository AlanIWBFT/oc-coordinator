using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LocalFork;

internal sealed class CoordinatorConfig
{
    public string OpenChamberRoot { get; init; } = "";
    public string OpenCodeRoot { get; init; } = "";
    public string ReleaseRoot { get; init; } = "";
    public string VisualStudioInstallDir { get; init; } = "";
    public string OpenChamberBranch { get; init; } = "";
    public string OpenCodeBranch { get; init; } = "";
}

internal sealed record ProcessResult(string StandardOutput, string StandardError);

internal static class Coordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static CoordinatorConfig LoadConfig(string coordinatorRoot)
    {
        var path = Path.Combine(coordinatorRoot, "local-fork.config.psd1");
        RequireFile(path);
        var escaped = path.Replace("'", "''");
        var json = Capture(
            "pwsh",
            ["-NoProfile", "-Command", $"$value = Import-PowerShellDataFile -LiteralPath '{escaped}'; $value | ConvertTo-Json -Compress"],
            coordinatorRoot
        );
        var config = JsonSerializer.Deserialize<CoordinatorConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to read coordinator config: {path}");
        RequireDirectory(config.OpenChamberRoot);
        RequireDirectory(config.OpenCodeRoot);
        return config;
    }

    public static string GetPinnedOpenChamberVersion(CoordinatorConfig config) =>
        ReadJsonString(Path.Combine(config.OpenChamberRoot, "package.json"), "version");

    public static string GetPinnedOpenCodeVersion(CoordinatorConfig config) =>
        ReadJsonString(Path.Combine(config.OpenChamberRoot, "package.json"), "dependencies", "@opencode-ai/sdk");

    public static string GetOpenChamberBinaryVersion(string binary)
    {
        RequireFile(binary);
        var raw = FileVersionInfo.GetVersionInfo(binary).ProductVersion ?? "";
        var match = Regex.Match(raw, @"^(\d+)\.(\d+)\.(\d+)");
        if (!match.Success)
            throw new InvalidOperationException($"OpenChamber binary has an invalid product version at {binary}: {raw}");
        return $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
    }

    public static string GetOpenCodeBinaryVersion(string binary, string workingDirectory)
    {
        RequireFile(binary);
        var output = Capture(binary, ["--version"], workingDirectory).Trim();
        return output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? throw new InvalidOperationException($"OpenCode binary returned an empty version: {binary}");
    }

    public static void AssertOpenCodeBinaryVersion(string binary, string expected, string workingDirectory)
    {
        var actual = GetOpenCodeBinaryVersion(binary, workingDirectory);
        if (actual != expected)
            throw new InvalidOperationException($"OpenCode version mismatch at {binary}: expected {expected}, got {actual}");
    }

    public static string Sha256(string path)
    {
        RequireFile(path);
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static string GetArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        _ => throw new InvalidOperationException($"Unsupported Windows architecture: {RuntimeInformation.ProcessArchitecture}"),
    };

    public static string Capture(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null
    ) => Run(fileName, arguments, workingDirectory, environment, capture: true).StandardOutput;

    public static ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?>? environment = null,
        bool capture = false
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
            CreateNoWindow = capture,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (var entry in environment)
            {
                if (entry.Value is null) startInfo.Environment.Remove(entry.Key);
                else startInfo.Environment[entry.Key] = entry.Value;
            }
        }
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start command: {fileName}");
        var standardOutput = capture ? process.StandardOutput.ReadToEndAsync() : null;
        var standardError = capture ? process.StandardError.ReadToEndAsync() : null;
        process.WaitForExit();
        var output = standardOutput?.GetAwaiter().GetResult() ?? "";
        var error = standardError?.GetAwaiter().GetResult() ?? "";
        if (process.ExitCode != 0)
        {
            var detail = string.Join(Environment.NewLine, new[] { output.Trim(), error.Trim() }.Where(value => value.Length > 0));
            throw new InvalidOperationException(
                $"Command failed with exit code {process.ExitCode}: {fileName} {string.Join(' ', arguments)}"
                + (detail.Length > 0 ? Environment.NewLine + detail : "")
            );
        }
        return new ProcessResult(output, error);
    }

    public static string ReadJsonString(string path, params string[] properties)
    {
        RequireFile(path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var value = document.RootElement;
        foreach (var property in properties)
        {
            if (!value.TryGetProperty(property, out value))
                throw new InvalidOperationException($"Missing {string.Join('.', properties)} in {path}");
        }
        return value.GetString() is { Length: > 0 } result
            ? result
            : throw new InvalidOperationException($"Invalid {string.Join('.', properties)} in {path}");
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required file not found.", path);
    }

    private static void RequireDirectory(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
    }
}
