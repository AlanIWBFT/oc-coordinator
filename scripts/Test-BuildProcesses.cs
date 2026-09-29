#!/usr/bin/env dotnet
#:property PublishAot=false
#:include Build.Processes.cs

using System.Diagnostics;
using System.Text.Json;
using LocalFork;

var executable = Environment.ProcessPath!;
if (args.Length > 0)
{
    switch (args[0])
    {
        case "leaf":
            File.WriteAllText(args[1], Environment.ProcessId.ToString());
            Thread.Sleep(60000);
            break;
        case "tree":
        case "orphan":
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (var value in new[] { "leaf", args[1] }) start.ArgumentList.Add(value);
            using (var child = Process.Start(start)!) WaitFile(args[1]);
            File.WriteAllText(args[1] + ".parent", Environment.ProcessId.ToString());
            if (args[0] == "tree") Thread.Sleep(60000);
            break;
        case "fail":
            WaitFile(args[1]);
            Environment.Exit(23);
            break;
        case "barrier":
            File.WriteAllText(args[1], "ready");
            WaitFile(args[2]);
            break;
        case "arguments":
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { Arguments = args.Skip(2), Value = Environment.GetEnvironmentVariable("BUILD_TEST_VALUE"), Removed = Environment.GetEnvironmentVariable("BUILD_TEST_REMOVE") }));
            break;
        case "mark":
            File.WriteAllText(args[1], "unexpected");
            break;
        case "lock":
            try { using var other = new BuildLock(args[1]); Environment.Exit(42); }
            catch (InvalidOperationException) { }
            break;
        case "owner":
            BuildProcesses.Run([new("owned tree", new BuildCommand(executable, ["tree", args[1]], Path.GetDirectoryName(args[1])!))], new Dictionary<string, string?>());
            break;
        default: throw new ArgumentException(args[0]);
    }
    return;
}

var root = Path.Combine(Path.GetTempPath(), "opencode", "build-jobs-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
BuildCommand Command(params string[] values) => new(executable, values, root);
var environment = new Dictionary<string, string?>();
var a = Path.Combine(root, "a");
var b = Path.Combine(root, "b");
BuildProcesses.Run([new("barrier A", Command("barrier", a, b)), new("barrier B", Command("barrier", b, a))], environment);
Console.WriteLine("PASS: independent branches run concurrently");

var argumentsFile = Path.Combine(root, "arguments.json");
string[] values = ["", "two words", "a\"b", "trailing slash \\", "中文", "\\\\server\\path\\", "\\\"quoted"];
Environment.SetEnvironmentVariable("BUILD_TEST_REMOVE", "inherited");
try
{
    BuildProcesses.Run([new("arguments", Command(["arguments", argumentsFile, .. values]))],
        new Dictionary<string, string?> { ["BUILD_TEST_VALUE"] = "value 中文", ["BUILD_TEST_REMOVE"] = null });
}
finally { Environment.SetEnvironmentVariable("BUILD_TEST_REMOVE", null); }
using (var document = JsonDocument.Parse(File.ReadAllText(argumentsFile)))
{
    Assert(document.RootElement.GetProperty("Arguments").EnumerateArray().Select(v => v.GetString()).SequenceEqual(values), "Argument quoting changed values.");
    Assert(document.RootElement.GetProperty("Value").GetString() == "value 中文" && document.RootElement.GetProperty("Removed").ValueKind == JsonValueKind.Null, "Environment overlay failed.");
}
Console.WriteLine("PASS: quoting, Unicode and environment removal");

var marker = Path.Combine(root, "child");
var skipped = Path.Combine(root, "skipped");
var timer = Stopwatch.StartNew();
try
{
    BuildProcesses.Run([new("long tree", Command("tree", marker), Command("mark", skipped)), new("intentional failure", Command("fail", marker + ".parent"))], environment);
    throw new Exception("Expected branch failure.");
}
catch (InvalidOperationException error)
{
    Assert(error.InnerException?.Message.Contains("intentional failure: exit 23") == true, "First failure was not preserved.");
}
Assert(timer.Elapsed.TotalSeconds < 15, "Cancellation did not stop the long-running branch promptly.");
Assert(!File.Exists(skipped), "A successor command ran after cancellation.");
AssertStopped(marker);
AssertStopped(marker + ".parent");
Console.WriteLine("PASS: first error cancels sibling and grandchild before returning; successors do not run");

var orphan = Path.Combine(root, "orphan");
BuildProcesses.Run([new("orphan cleanup", Command("orphan", orphan))], environment);
AssertStopped(orphan);
Console.WriteLine("PASS: descendants of successfully exited wrappers are drained");

var killedOwner = Path.Combine(root, "killed-owner-child");
var ownerStart = new ProcessStartInfo(executable) { UseShellExecute = false };
foreach (var value in new[] { "owner", killedOwner }) ownerStart.ArgumentList.Add(value);
using (var owner = Process.Start(ownerStart)!)
{
    try { WaitFile(killedOwner + ".parent"); }
    finally { if (!owner.HasExited) owner.Kill(); owner.WaitForExit(); }
}
WaitStopped(killedOwner);
WaitStopped(killedOwner + ".parent");
Console.WriteLine("PASS: killing the job owner also terminates its entire process tree");

try
{
    BuildProcesses.Run([new("missing executable", new BuildCommand(Path.Combine(root, "missing.exe"), [], root))], environment);
    throw new Exception("Expected launch failure.");
}
catch (InvalidOperationException error) { Assert(error.InnerException is System.ComponentModel.Win32Exception, "Launch failure was lost."); }
Console.WriteLine("PASS: launch errors propagate");

using (var buildLock = new BuildLock(root)) BuildProcesses.Run([new("competing build", Command("lock", root))], environment);
using (var buildLock = new BuildLock(root)) { }
Console.WriteLine("PASS: competing build rejected and ownership released");
Console.WriteLine($"Build process tests passed. Evidence: {root}");

static void WaitFile(string path)
{
    var timer = Stopwatch.StartNew();
    while (!File.Exists(path))
    {
        if (timer.Elapsed.TotalSeconds > 10) throw new TimeoutException(path);
        Thread.Sleep(10);
    }
}
static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
static void AssertStopped(string marker)
{
    var pid = int.Parse(File.ReadAllText(marker));
    try
    {
        using var process = Process.GetProcessById(pid);
        Assert(process.HasExited, $"Process {pid} survived job drain.");
    }
    catch (ArgumentException) { }
}
static void WaitStopped(string marker)
{
    var pid = int.Parse(File.ReadAllText(marker));
    try
    {
        using var process = Process.GetProcessById(pid);
        Assert(process.WaitForExit(10000), $"Process {pid} survived owner termination.");
    }
    catch (ArgumentException) { }
}
