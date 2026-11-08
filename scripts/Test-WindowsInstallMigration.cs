#:property PublishAot=false

using Microsoft.Win32;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

[assembly: SupportedOSPlatform("windows")]

const string appId = "dev.openchamber.desktop";
const string guid = "103a64e2-d142-5f36-803b-284d778c8afd";
const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
const string migrationKey = @"Software\dev.openchamber.desktop\PerUserMigration";
var mode = args.FirstOrDefault() ?? "inspect";
if (mode == "tokens")
{
    foreach (var value in args.Skip(1))
    {
        using var process = Process.GetProcessById(int.Parse(value));
        if (!Native.OpenProcessToken(process.Handle, 8, out var token)) throw new System.ComponentModel.Win32Exception();
        try
        {
            if (!Native.GetTokenInformation(token, 20, out var elevated, sizeof(int), out _)) throw new System.ComponentModel.Win32Exception();
            Console.WriteLine($"{process.Id} {process.ProcessName}: elevated={elevated}");
            if (elevated != 0) throw new InvalidOperationException("The installed application and its OpenCode child must run unelevated.");
        }
        finally { Native.CloseHandle(token); }
    }
    return;
}
if (mode == "cdp")
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var http = new HttpClient();
    using var pages = JsonDocument.Parse(http.GetStringAsync($"http://localhost:{args[1]}/json/list", timeout.Token).GetAwaiter().GetResult());
    var page = pages.RootElement.EnumerateArray().First(page => page.GetProperty("type").GetString() == "page");
    using var socket = new ClientWebSocket();
    socket.ConnectAsync(new Uri(page.GetProperty("webSocketDebuggerUrl").GetString()!), timeout.Token).GetAwaiter().GetResult();
    var request = JsonSerializer.SerializeToUtf8Bytes(new { id = 1, method = "Runtime.evaluate", @params = new { expression = args[2], awaitPromise = true, returnByValue = true } });
    socket.SendAsync(new ArraySegment<byte>(request), WebSocketMessageType.Text, true, timeout.Token).GetAwaiter().GetResult();
    while (true)
    {
        using var message = new MemoryStream();
        var buffer = new byte[16384];
        WebSocketReceiveResult received;
        do
        {
            received = socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token).GetAwaiter().GetResult();
            if (received.MessageType == WebSocketMessageType.Close) return;
            message.Write(buffer, 0, received.Count);
        } while (!received.EndOfMessage);
        using var response = JsonDocument.Parse(message.ToArray());
        if (!response.RootElement.TryGetProperty("id", out var id) || id.GetInt32() != 1) continue;
        Console.WriteLine(response.RootElement);
        return;
    }
}
if (mode == "windows")
{
    Native.EnumWindows((window, _) =>
    {
        Native.GetWindowThreadProcessId(window, out var pid);
        var text = new StringBuilder(1024);
        Native.GetWindowText(window, text, text.Capacity);
        if (text.ToString().Contains("OpenChamber", StringComparison.OrdinalIgnoreCase) || text.ToString().Contains("Sandboxie", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{pid}: {text}");
            Native.EnumChildWindows(window, (child, _) =>
            {
                var label = new StringBuilder(2048);
                Native.GetWindowText(child, label, label.Capacity);
                if (label.Length > 0) Console.WriteLine($"  {label}");
                return true;
            }, 0);
        }
        return true;
    }, 0);
    return;
}
if (Native.GetModuleHandle("SbieDll.dll") == 0) throw new InvalidOperationException("Run fixture operations inside the dedicated Sandboxie box.");
var oldRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OpenChamberMigrationBaseline");
var newRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenChamber");
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenChamber");
var data = Path.Combine(home, ".config", "openchamber");
var roots = new[] { profile, data, Path.Combine(home, ".local", "share", "opencode") };
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Directory.CreateDirectory(data);
    File.WriteAllText(Path.Combine(data, "migration-probe.error.txt"), e.ExceptionObject.ToString());
};
if (mode == "interrupt")
{
    using var installer = Process.GetProcessById(int.Parse(args[1]));
    var executable = installer.MainModule!.FileName;
    if (!executable.StartsWith(@"D:\OpenChamber\coordinator\generated\windows-migration-installer\", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("The interruption target must be the generated test installer.");
    var deadline = Stopwatch.StartNew();
    while (Read(Registry.CurrentUser, $@"Software\{guid}", "InstallLocation") is not null || Read(Registry.CurrentUser, migrationKey, "Source") is null)
    {
        if (installer.HasExited || deadline.Elapsed > TimeSpan.FromSeconds(90)) throw new InvalidOperationException("Installer did not reach the post-uninstall migration boundary.");
        Thread.Sleep(25);
    }
    installer.Kill(entireProcessTree: true);
    installer.WaitForExit();
}
if (mode == "seed")
{
    if (!File.Exists(Path.Combine(oldRoot, "OpenChamber.exe"))) throw new InvalidOperationException("Install the per-user baseline first.");
    foreach (var root in roots)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "migration-fixture.txt"), "OpenChamber installation migration fixture");
    }
    File.WriteAllText(Path.Combine(data, "settings.json"), "{\"desktopLocalPort\":57270}");
    using var run = Registry.CurrentUser.CreateSubKey(runKey);
    if (args.ElementAtOrDefault(1) == "disabled") run.DeleteValue(appId, throwOnMissingValue: false);
    else run.SetValue(appId, $"\"{oldRoot}\\OpenChamber.exe\" --background", RegistryValueKind.String);
}
if (mode == "disable-startup")
{
    using var run = Registry.CurrentUser.CreateSubKey(runKey);
    run.DeleteValue(appId, throwOnMissingValue: false);
}
if (mode == "assert")
{
    var expected = args.ElementAtOrDefault(1) ?? "enabled";
    var oldInstall = Read(Registry.CurrentUser, $@"Software\{guid}", "InstallLocation");
    var machineInstall = Read(Registry.LocalMachine, $@"Software\{guid}", "InstallLocation");
    if (oldInstall is not null) throw new InvalidOperationException($"Old installation registration remains: {oldInstall}");
    if (!string.Equals(machineInstall as string, newRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Unexpected machine installation: {machineInstall}");
    if (File.Exists(Path.Combine(oldRoot, "OpenChamber.exe"))) throw new InvalidOperationException("Old executable remains.");
    if (!File.Exists(Path.Combine(newRoot, "OpenChamber.exe"))) throw new InvalidOperationException("Machine executable missing.");
    var expectedCommand = expected == "disabled" ? null : $"\"{newRoot}\\OpenChamber.exe\" --background";
    if (!Equals(Read(Registry.CurrentUser, runKey, appId), expectedCommand)) throw new InvalidOperationException("Startup path/arguments or disabled setting were not preserved.");
    foreach (var root in roots)
        if (File.ReadAllText(Path.Combine(root, "migration-fixture.txt")) != "OpenChamber installation migration fixture") throw new InvalidOperationException($"User data changed: {root}");
    if (Read(Registry.CurrentUser, migrationKey, "Source") is not null) throw new InvalidOperationException("Migration journal was not cleared.");
    Console.WriteLine("PASS: per-user removal, machine install, startup command/off setting, unchanged user data, journal cleanup");
}
var result = JsonSerializer.Serialize(new
{
    sandboxed = true,
    oldRoot,
    newRoot,
    perUserInstall = Read(Registry.CurrentUser, $@"Software\{guid}", "InstallLocation"),
    machineInstall = Read(Registry.LocalMachine, $@"Software\{guid}", "InstallLocation"),
    startup = Read(Registry.CurrentUser, runKey, appId),
    pendingSource = Read(Registry.CurrentUser, migrationKey, "Source"),
    files = roots.Select(root => new { root, fixture = File.Exists(Path.Combine(root, "migration-fixture.txt")) }),
}, new JsonSerializerOptions { WriteIndented = true });
Directory.CreateDirectory(data);
File.WriteAllText(Path.Combine(data, "migration-probe.json"), result);
Console.WriteLine(result);

static object? Read(RegistryKey hive, string path, string name)
{
    using var key = hive.OpenSubKey(path);
    return key?.GetValue(name);
}

static class Native
{
    public delegate bool EnumCallback(nint window, nint parameter);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string name);
    [DllImport("user32")] public static extern bool EnumWindows(EnumCallback callback, nint parameter);
    [DllImport("user32")] public static extern bool EnumChildWindows(nint window, EnumCallback callback, nint parameter);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32")] public static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("advapi32", SetLastError = true)] public static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32", SetLastError = true)] public static extern bool GetTokenInformation(nint token, int informationClass, out int value, int size, out int returned);
    [DllImport("kernel32")] public static extern bool CloseHandle(nint handle);
}
