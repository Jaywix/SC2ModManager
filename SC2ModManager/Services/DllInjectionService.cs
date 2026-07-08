using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SC2ModManager.Services
{
  public class DllInjectionService
    {
        private const string ProcessName = "SupremeCommander2";
        private const string InjectorHelperExe = "Injector32Helper.exe";

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr addr, UIntPtr size, uint type, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr addr, byte[] buffer, uint size, out UIntPtr written);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr attr, uint stack, IntPtr start, IntPtr param, uint flags, out uint tid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint ms);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr handle, out uint code);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr addr, UIntPtr size, uint type);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 4;

        public static Process? FindGameProcess()
        {
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                try
                {
                    if (!p.HasExited)
                        return p;
                }
                catch { }
            }
            return null;
        }

        public static void TryKillGameProcess()
        {
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                try
                {
                    if (!p.HasExited)
                        p.Kill(entireProcessTree: true);
                }
                catch { }
            }
        }

        /// <summary>
        ///     True if the game is currently running. Shared check so the replay browser and the
        ///     launcher don't each start their own copy at the same time. Disposes the process
        ///     handles it opens so we don't leak them.
        /// </summary>
        public static bool IsGameRunning()
        {
            var procs = Process.GetProcessesByName(ProcessName);
            try
            {
                foreach (var p in procs)
                {
                    try { if (!p.HasExited) return true; }
                    catch { }
                }
                return false;
            }
            finally
            {
                foreach (var p in procs) p.Dispose();
            }
        }

        /// <summary>
        ///     Injects a DLL into the target process via LoadLibraryA.
        /// </summary>
        public static bool Inject(int processId, string dllPath)
        {

            if (!File.Exists(dllPath))
                throw new FileNotFoundException("IPC DLL not found", dllPath);

            if (TryInjectViaHelper(processId, dllPath))
                return true;

            return InjectInternal(processId, dllPath);
        }

        private static bool TryInjectViaHelper(int processId, string dllPath)
        {
            string helperPath = ResolveInjectorHelperPath();
            if (!File.Exists(helperPath))
                return false;

            using var helper = Process.Start(new ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = $"{processId} \"{Path.GetFullPath(dllPath)}\"",
                WorkingDirectory = Path.GetDirectoryName(helperPath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (helper == null)
                return false;

            if (!helper.WaitForExit(20000))
            {
                try { helper.Kill(); } catch { }
                return false;
            }

            return helper.ExitCode == 0;
        }

        private static string ResolveInjectorHelperPath()
        {
            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, InjectorHelperExe),
                Path.Combine(AppContext.BaseDirectory, "Injector32Helper", InjectorHelperExe),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            }

            return Path.Combine(AppContext.BaseDirectory, InjectorHelperExe);
        }

        private static bool InjectInternal(int processId, string dllPath)
        {

            string fullPath = Path.GetFullPath(dllPath);
            byte[] pathBytes = Encoding.ASCII.GetBytes(fullPath + "\0");

            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero)
                throw new InvalidOperationException($"OpenProcess failed: {Marshal.GetLastWin32Error()}");

            try
            {
                IntPtr remote = VirtualAllocEx(hProcess, IntPtr.Zero, (UIntPtr)pathBytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                if (remote == IntPtr.Zero)
                    throw new InvalidOperationException($"VirtualAllocEx failed: {Marshal.GetLastWin32Error()}");

                if (!WriteProcessMemory(hProcess, remote, pathBytes, (uint)pathBytes.Length, out _))
                    throw new InvalidOperationException($"WriteProcessMemory failed: {Marshal.GetLastWin32Error()}");

                IntPtr loadLibrary = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
                if (loadLibrary == IntPtr.Zero)
                    throw new InvalidOperationException("LoadLibraryA not found");

                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibrary, remote, 0, out _);
                if (hThread == IntPtr.Zero)
                    throw new InvalidOperationException($"CreateRemoteThread failed: {Marshal.GetLastWin32Error()}");

                try
                {
                    WaitForSingleObject(hThread, 15000);
                    GetExitCodeThread(hThread, out uint exitCode);
                    return exitCode != 0;
                }
                finally
                {
                    CloseHandle(hThread);
                    VirtualFreeEx(hProcess, remote, UIntPtr.Zero, MEM_RELEASE);
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
        }

        public static string ResolveDllPath(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
                return Path.GetFullPath(configuredPath);

            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, "ipc_dll.dll"),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            }

            return configuredPath ?? candidates[0];
        }
    }
}
