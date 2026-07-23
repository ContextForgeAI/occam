using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OccamMcp.Core.Workers;

/// <summary>
/// Tracks Node/Chromium child processes and kills the tree on Core shutdown or crash-adjacent exit.
/// Windows: Job Object with KILL_ON_JOB_CLOSE (P9-INF2). POSIX: setpgid + kill(-pgid).
/// All platforms: ProcessExit + tracked PID sweep.
/// </summary>
public static class WorkerProcessGroup
{
    private static readonly object Gate = new();
    private static readonly HashSet<int> ActivePids = [];
    private static readonly HashSet<int> ActivePgids = [];
    private static nint JobHandle;
    private static volatile bool ShutdownHookRegistered;

#if OCCAM_GATE
    internal static bool IsShutdownHookRegisteredForTests => ShutdownHookRegistered;
    internal static int ActivePidCountForTests
    {
        get
        {
            lock (Gate)
            {
                return ActivePids.Count;
            }
        }
    }
#endif

    public static Process? Start(ProcessStartInfo psi)
    {
        EnsureShutdownHook();
        ApplyUtf8StreamEncoding(psi);
        var process = Process.Start(psi);
        if (process is not null)
        {
            Attach(process);
        }

        return process;
    }

    /// <summary>
    /// Fire-and-forget drain of a long-lived process's redirected stdout so it can never block on a full OS
    /// pipe buffer (the classic undrained-pipe deadlock). Daemons are driven over HTTP, not stdout, and their
    /// stderr is inherited (flows to the host's diagnostics stderr), so anything they emit to stdout is just
    /// discarded here. Without this a chatty daemon eventually fills the ~4–64 KB pipe, its write() blocks,
    /// and the daemon hangs — surfacing as health-check timeouts only under sustained load.
    /// </summary>
    public static void DrainStandardOutput(Process? process)
    {
        if (process is null || !process.StartInfo.RedirectStandardOutput)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var reader = process.StandardOutput;
                var buffer = new char[4096];
                while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0)
                {
                    // discard — the point is to keep the pipe empty, not to read the content.
                }
            }
            catch
            {
                // process exited / stream closed — nothing left to drain.
            }
        });
    }

    public static void Release(Process? process)
    {
        if (process is null)
        {
            return;
        }

        lock (Gate)
        {
            ActivePids.Remove(process.Id);
            if (PosixProcessGroup.IsSupported)
            {
                ActivePgids.Remove(process.Id);
            }
        }
    }

    /// <summary>
    /// Node workers emit UTF-8 JSON on stdout. Without this, Windows console code pages corrupt smart quotes (mojibake).
    /// </summary>
    private static void ApplyUtf8StreamEncoding(ProcessStartInfo psi)
    {
        if (psi.RedirectStandardOutput && psi.StandardOutputEncoding is null)
        {
            psi.StandardOutputEncoding = Encoding.UTF8;
        }

        if (psi.RedirectStandardError && psi.StandardErrorEncoding is null)
        {
            psi.StandardErrorEncoding = Encoding.UTF8;
        }
    }

    internal static void Attach(Process process)
    {
        lock (Gate)
        {
            ActivePids.Add(process.Id);
        }

        if (OperatingSystem.IsWindows())
        {
            TryAssignToKillJob(process);
            return;
        }

        if (PosixProcessGroup.TrySetLeader(process.Id))
        {
            lock (Gate)
            {
                ActivePgids.Add(process.Id);
            }
            
            // PR-G / INV-10 honesty: PR_SET_PDEATHSIG configures the *calling* process, not a child
            // PID. Calling it from the Core parent with process.Id does not protect the worker.
            // Child-side death-signal setup (if needed) belongs in the worker entrypoint, not here.
            // See docs/rc2/pr-g/LIFECYCLE_IDENTITY_MODEL.md.
        }
    }

    private static void EnsureShutdownHook()
    {
        if (ShutdownHookRegistered)
        {
            return;
        }

        lock (Gate)
        {
            if (ShutdownHookRegistered)
            {
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                JobHandle = CreateKillOnCloseJob();
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAllTracked();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                KillAllTracked();
            };

            ShutdownHookRegistered = true;
        }
    }

    private static void KillAllTracked()
    {
        int[] pids;
        int[] pgids;
        lock (Gate)
        {
            pids = ActivePids.ToArray();
            pgids = ActivePgids.ToArray();
            ActivePids.Clear();
            ActivePgids.Clear();
        }

        if (PosixProcessGroup.IsSupported && pgids.Length > 0)
        {
            foreach (var pgid in pgids)
            {
                PosixProcessGroup.TryTerminateGroup(pgid);
            }
        }

        foreach (var pid in pids)
        {
            if (PosixProcessGroup.IsSupported && pgids.Contains(pid))
            {
                continue;
            }

            TryKillPidTree(pid);
        }

        if (OperatingSystem.IsWindows() && JobHandle != 0)
        {
            WindowsJobApi.CloseHandle(JobHandle);
            JobHandle = 0;
        }
    }

    private static void TryKillPidTree(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited)
            {
                NodeWorkerLifecycle.KillProcessTree(process);
            }
        }
        catch
        {
            // process already exited
        }
    }

    private static nint CreateKillOnCloseJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        var job = WindowsJobApi.CreateJobObjectW(0, null);
        if (job == 0)
        {
            return 0;
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
            },
        };

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        if (!WindowsJobApi.SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)length))
        {
            WindowsJobApi.CloseHandle(job);
            return 0;
        }

        return job;
    }

    private static void TryAssignToKillJob(Process process)
    {
        if (JobHandle == 0)
        {
            return;
        }

        try
        {
            _ = WindowsJobApi.AssignProcessToJobObject(JobHandle, process.Handle);
        }
        catch
        {
            // best effort — tracked PID sweep still runs on exit
        }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    private static class PosixProcessGroup
    {
        internal static bool IsSupported =>
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD();

        private const int SigTerm = 15;
        private const int SigKill = 9;
        private const int SigHup = 1;
        private const int PrSetPdeathsig = 1;

        internal static bool TrySetLeader(int pid)
        {
            if (!IsSupported)
            {
                return false;
            }

            try
            {
                return LibC.setpgid(pid, pid) == 0;
            }
            catch
            {
                return false;
            }
        }

        internal static void TryTerminateGroup(int pgid)
        {
            if (!IsSupported || pgid <= 0)
            {
                return;
            }

            try
            {
                _ = LibC.kill(-pgid, SigTerm);
            }
            catch
            {
                // best effort
            }

            var deadline = Environment.TickCount64 + 2_000;
            while (Environment.TickCount64 < deadline)
            {
                try
                {
                    if (LibC.kill(-pgid, 0) != 0)
                    {
                        return;
                    }
                }
                catch
                {
                    return;
                }

                Thread.Sleep(50);
            }

            try
            {
                _ = LibC.kill(-pgid, SigKill);
            }
            catch
            {
                // best effort
            }
        }

        /// <summary>
        /// Child-side helper: sets PR_SET_PDEATHSIG on the <em>calling</em> process so it receives
        /// SIGHUP when its parent dies. The <paramref name="ignoredPid"/> argument is retained only
        /// for call-site compatibility and is not used — prctl cannot target another PID.
        /// Do not call this from a parent hoping to configure a child.
        /// </summary>
        internal static void TrySetDeathSignal(int ignoredPid)
        {
            _ = ignoredPid;
            if (!IsSupported)
            {
                return;
            }

            try
            {
                // PR_SET_PDEATHSIG = 1, SIGHUP = 1 — applies to this process only.
                _ = LibC.prctl(PrSetPdeathsig, SigHup, 0, 0, 0);
            }
            catch
            {
                // best effort - prctl may not be available on all systems
            }
        }

        private static class LibC
        {
            [DllImport("libc", SetLastError = true)]
            internal static extern int setpgid(int pid, int pgid);

            [DllImport("libc", SetLastError = true)]
            internal static extern int kill(int pid, int sig);

            [DllImport("libc", SetLastError = true)]
            internal static extern int prctl(int option, int arg2, int arg3, int arg4, int arg5);
        }
    }

    private static class WindowsJobApi
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint CreateJobObjectW(nint lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(
            nint hJob,
            int jobObjectInfoClass,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo,
            uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(nint hObject);
    }
}
