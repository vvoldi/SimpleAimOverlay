using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SimpleAimOverlay;

internal static class GameLauncher
{
    private static string UserSuffix => WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
    internal static string StopEventName => @"Local\SimpleAimOverlay.GameWatcher.Stop." + UserSuffix;
    internal static bool IsValidTarget(string target) => !string.IsNullOrWhiteSpace(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && (Path.IsPathFullyQualified(target) ? File.Exists(target) : Path.GetFileName(target) == target && target.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    internal static bool MatchesTarget(string target, string processName, string? executablePath)
    {
        if (!string.Equals(Path.GetFileNameWithoutExtension(target), processName, StringComparison.OrdinalIgnoreCase)) return false;
        return !Path.IsPathFullyQualified(target) || executablePath != null && string.Equals(Path.GetFullPath(target), Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase);
    }
    private static readonly Lazy<string> PubgDefault = new(DetectPubg);
    internal static string FindPubg() => PubgDefault.Value;
    private static string DetectPubg()
    {
        try {
            string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "") as string ?? "";
            var libraries = new List<string> { steam, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam") };
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf)) foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"")) libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            foreach (string library in libraries) {
                string candidate = Path.Combine(library, @"steamapps\common\PUBG\TslGame\Binaries\Win64\TslGame.exe");
                if (File.Exists(candidate)) return candidate;
            }
        } catch { }
        return "TslGame.exe";
    }
    internal static void Configure(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("SimpleAimOverlayGameWatcher", $"\"{Application.ExecutablePath}\" --watch-game");
        else { key.DeleteValue("SimpleAimOverlayGameWatcher", false); StopWatcher(); }
    }
    internal static void StartWatcher() => Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Arguments = "--watch-game", WindowStyle = ProcessWindowStyle.Hidden })?.Dispose();
    internal static void StopWatcher() { try { using var signal = EventWaitHandle.OpenExisting(StopEventName); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { } }
    internal static void RunWatcher()
    {
        using var mutex = new Mutex(true, @"Local\SimpleAimOverlay.GameWatcher." + UserSuffix, out bool first);
        if (!first) return;
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
        stop.Reset();
        bool wasRunning = false; string previousTarget = "";
        while (!stop.WaitOne(2000)) {
            StoreData settings;
            try { settings = System.Text.Json.JsonSerializer.Deserialize<StoreData>(File.ReadAllText(Store.FilePath)) ?? throw new InvalidDataException(); } catch { continue; }
            if (!settings.LaunchWithGame) break;
            string target = settings.GameExecutable;
            if (target != previousTarget) { wasRunning = false; previousTarget = target; }
            bool running = IsRunning(target);
            if (running && !wasRunning) {
                try { Process.Start(new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Arguments = "--game" })?.Dispose(); }
                catch { /* Retry on the next check if startup failed. */ continue; }
            }
            wasRunning = running;
        }
    }
    internal static bool IsRunning(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        Process[] processes;
        try { processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target)); } catch { return false; }
        bool found = false;
        foreach (var process in processes) {
            using (process) {
                try { if (MatchesTarget(target, process.ProcessName, Path.IsPathFullyQualified(target) ? ReadPath(process.Id) : null)) found = true; } catch { }
            }
        }
        return found;
    }
    internal static bool IsForegroundTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out uint pid);
        if (pid == 0) return false;
        try {
            using var process = Process.GetProcessById((int)pid);
            return MatchesTarget(target, process.ProcessName, Path.IsPathFullyQualified(target) ? ReadPath(process.Id) : null);
        } catch { return false; }
    }
    internal static bool ShouldShow(bool visible, bool hiddenByHold, bool gameOnly, bool gameForeground)
        => visible && !hiddenByHold && (!gameOnly || gameForeground);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    private static string? ReadPath(int pid)
    {
        IntPtr handle = OpenProcess(0x1000, false, pid);
        if (handle == IntPtr.Zero) return null;
        try { var path = new StringBuilder(32768); int length = path.Capacity; return QueryFullProcessImageName(handle, 0, path, ref length) ? path.ToString() : null; }
        finally { CloseHandle(handle); }
    }
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder path, ref int size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
