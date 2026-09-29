using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LocalFork;

internal sealed record BuildCommand(string FileName, string[] Arguments, string WorkingDirectory);
internal sealed record BuildBranch(string Name, params BuildCommand[] Commands);

// Each branch owns a job from process creation through descendant exit. Cancelling a
// group closes admission before terminating jobs, including children of exited wrappers.
internal static class BuildProcesses
{
    public static void Run(IReadOnlyList<BuildBranch> branches, IReadOnlyDictionary<string, string?> environment)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Build jobs require Windows.");
        var gate = new object();
        Exception? failure = null;
        var jobs = new List<BuildJob>();
        void Cancel(Exception error)
        {
            lock (gate)
            {
                if (failure is not null) return;
                failure = error;
                foreach (var job in jobs)
                {
                    try { job.Terminate(); }
                    catch (Exception terminationError) { Console.Error.WriteLine($"Build job termination failed: {terminationError.Message}"); }
                }
            }
        }
        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; Cancel(new OperationCanceledException("Build interrupted.")); };
        Console.CancelKeyPress += interrupt;
        try
        {
            var tasks = branches.Select(branch => Task.Factory.StartNew(() =>
            {
                BuildJob? job = null;
                var timer = Stopwatch.StartNew();
                try
                {
                    lock (gate)
                    {
                        if (failure is not null) return;
                        job = new BuildJob();
                        jobs.Add(job);
                    }
                    Console.WriteLine($"[{branch.Name}] started");
                    foreach (var command in branch.Commands)
                    {
                        SafeProcessHandle process;
                        lock (gate)
                        {
                            if (failure is not null) return;
                            process = job.Start(command, environment);
                        }
                        using (process)
                        {
                            var exitCode = BuildNative.Wait(process);
                            if (exitCode != 0) throw new InvalidOperationException($"{branch.Name}: exit {exitCode}: {command.FileName} {string.Join(' ', command.Arguments)}");
                        }
                    }
                    lock (gate)
                    {
                        if (failure is not null) return;
                        Console.WriteLine($"[{branch.Name}] completed in {timer.Elapsed.TotalSeconds:F1}s");
                    }
                }
                catch (Exception error) { Cancel(error); }
                finally
                {
                    // Successful tools can leave compiler servers behind. Drain the job
                    // before any caller restores links or cleans native build resources.
                    if (job is not null)
                    {
                        try { job.Drain(); }
                        catch (Exception error) { Cancel(error); }
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            Task.WaitAll(tasks);
            if (failure is not null) throw new InvalidOperationException("Build stopped; all parallel branches have been terminated.", failure);
        }
        finally
        {
            Console.CancelKeyPress -= interrupt;
            foreach (var job in jobs) job.Dispose();
        }
    }
}

internal sealed class BuildJob : IDisposable
{
    private readonly SafeFileHandle handle = BuildNative.CreateJob();

    public SafeProcessHandle Start(BuildCommand command, IReadOnlyDictionary<string, string?> overrides)
    {
        var environment = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables()) environment[(string)entry.Key] = (string)entry.Value!;
        foreach (var entry in overrides)
        {
            if (entry.Value is null) environment.Remove(entry.Key);
            else environment[entry.Key] = entry.Value;
        }
        environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        environment["MSBUILDDISABLENODEREUSE"] = "1";
        environment["UseSharedCompilation"] = "false";
        var executable = ResolveExecutable(command.FileName, environment.GetValueOrDefault("PATH") ?? "");
        using var input = BuildNative.DuplicateStandardHandle(-10);
        using var output = BuildNative.DuplicateStandardHandle(-11);
        using var error = BuildNative.DuplicateStandardHandle(-12);
        using var attributes = new BuildAttributes([input.DangerousGetHandle(), output.DangerousGetHandle(), error.DangerousGetHandle()], handle.DangerousGetHandle());
        var block = Marshal.StringToHGlobalUni(string.Join('\0', environment.Select(e => $"{e.Key}={e.Value}")) + "\0\0");
        try
        {
            var startup = new BuildNative.StartupInfoEx
            {
                StartupInfo = new BuildNative.StartupInfo
                {
                    Size = (uint)Marshal.SizeOf<BuildNative.StartupInfoEx>(), Flags = 0x100,
                    StdInput = input.DangerousGetHandle(), StdOutput = output.DangerousGetHandle(), StdError = error.DangerousGetHandle(),
                },
                AttributeList = attributes.Pointer,
            };
            var line = new StringBuilder(string.Join(' ', new[] { executable }.Concat(command.Arguments).Select(Quote)));
            if (!BuildNative.CreateProcess(executable, line, 0, 0, true, 0x80400, block, command.WorkingDirectory, ref startup, out var process))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to launch {executable}");
            BuildNative.CloseHandle(process.Thread);
            return new SafeProcessHandle(process.Process, true);
        }
        finally { Marshal.FreeHGlobal(block); }
    }

    public void Terminate()
    {
        if (!BuildNative.TerminateJobObject(handle, 1223)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void Drain()
    {
        Terminate();
        while (true)
        {
            if (!BuildNative.QueryInformationJobObject(handle, 1, out var info, (uint)Marshal.SizeOf<BuildNative.Accounting>(), 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (info.ActiveProcesses == 0) return;
            Thread.Sleep(10);
        }
    }

    public void Dispose() => handle.Dispose();

    private static string ResolveExecutable(string file, string path)
    {
        if (Path.IsPathFullyQualified(file)) return file;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), Path.HasExtension(file) ? file : file + ".exe");
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException($"Build executable not found on PATH: {file}");
    }

    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            text.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character);
            slashes = 0;
        }
        return text.Append('\\', slashes * 2).Append('"').ToString();
    }
}

internal sealed class BuildLock : IDisposable
{
    private readonly Mutex mutex;
    public BuildLock(string root)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\', '/').ToUpperInvariant())));
        mutex = new Mutex(false, $"Local\\OpenChamberBuild-{key}");
        try
        {
            if (mutex.WaitOne(0)) return;
        }
        catch (AbandonedMutexException) { return; }
        mutex.Dispose();
        throw new InvalidOperationException($"Another Candidate or release build owns {root}.");
    }
    public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
}

internal sealed class BuildAttributes : IDisposable
{
    public nint Pointer { get; }
    private readonly nint handles;
    private readonly nint job;
    private bool initialized;

    public BuildAttributes(nint[] inherited, nint jobHandle)
    {
        nuint size = 0;
        BuildNative.InitializeProcThreadAttributeList(0, 2, 0, ref size);
        Pointer = Marshal.AllocHGlobal((nint)size);
        handles = Marshal.AllocHGlobal(inherited.Length * nint.Size);
        job = Marshal.AllocHGlobal(nint.Size);
        try
        {
            if (!BuildNative.InitializeProcThreadAttributeList(Pointer, 2, 0, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            Marshal.Copy(inherited, 0, handles, inherited.Length);
            Marshal.WriteIntPtr(job, jobHandle);
            if (!BuildNative.UpdateProcThreadAttribute(Pointer, 0, 0x20002, handles, (nuint)(inherited.Length * nint.Size), 0, 0) ||
                !BuildNative.UpdateProcThreadAttribute(Pointer, 0, 0x2000D, job, (nuint)nint.Size, 0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (initialized) BuildNative.DeleteProcThreadAttributeList(Pointer);
        Marshal.FreeHGlobal(Pointer);
        Marshal.FreeHGlobal(handles);
        Marshal.FreeHGlobal(job);
    }
}

internal static class BuildNative
{
    public static SafeFileHandle CreateJob()
    {
        var job = CreateJobObject(0, null);
        if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var limits = new Limits { Basic = new BasicLimits { Flags = 0x2000 } };
        if (SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<Limits>())) return job;
        var error = new Win32Exception(Marshal.GetLastWin32Error());
        job.Dispose();
        throw error;
    }

    public static SafeFileHandle DuplicateStandardHandle(int id)
    {
        var process = GetCurrentProcess();
        if (!DuplicateHandle(process, GetStdHandle(id), process, out var handle, 0, true, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return handle;
    }

    public static uint Wait(SafeProcessHandle process)
    {
        if (WaitForSingleObject(process, uint.MaxValue) != 0 || !GetExitCodeProcess(process, out var code)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return code;
    }

    [StructLayout(LayoutKind.Sequential)] internal struct StartupInfo
    {
        public uint Size;
        public nint Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public ushort ShowWindow, ReservedSize;
        public nint Reserved2, StdInput, StdOutput, StdError;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct StartupInfoEx { public StartupInfo StartupInfo; public nint AttributeList; }
    [StructLayout(LayoutKind.Sequential)] internal struct ProcessInfo { public nint Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] internal struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public nuint MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcesses;
        public nuint Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Limits
    {
        public BasicLimits Basic;
        public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
        public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Accounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CreateProcess(string file, StringBuilder command, nint processAttributes, nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, nint environment, string directory, ref StartupInfoEx startup, out ProcessInfo process);
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref Limits limits, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool QueryInformationJobObject(SafeFileHandle job, int kind, out Accounting info, uint size, nint returned);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool InitializeProcThreadAttributeList(nint list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UpdateProcThreadAttribute(nint list, uint flags, nuint attribute, nint value, nuint size, nint previous, nint returned);
    [DllImport("kernel32.dll")] internal static extern void DeleteProcThreadAttributeList(nint list);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint GetStdHandle(int id);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DuplicateHandle(nint source, nint handle, nint target, out SafeFileHandle copy, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeProcessHandle process, uint timeout);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(SafeProcessHandle process, out uint code);
}
