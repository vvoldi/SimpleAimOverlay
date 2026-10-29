using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace SimpleAimOverlay;

internal static class StartupRegistration
{
    internal const string LinkName = "Simple Aim Overlay.lnk";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static string LinkPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), LinkName);
    internal static string? StartupMode(bool startWithWindows, bool watchGame)
        => startWithWindows ? "app" : watchGame ? "watcher" : null;

    internal static void Configure(bool startWithWindows, bool watchGame)
    {
        if (StartupMode(startWithWindows, watchGame) != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LinkPath)!);
            var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
            dynamic shell = Activator.CreateInstance(type)!;
            try
            {
                dynamic link = shell.CreateShortcut(LinkPath);
                try
                {
                    link.TargetPath = Application.ExecutablePath;
                    link.Arguments = "--startup";
                    link.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath)!;
                    link.Description = "Simple Aim Overlay";
                    link.IconLocation = Application.ExecutablePath + ",0";
                    link.Save();
                }
                finally { Marshal.FinalReleaseComObject(link); }
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
        else File.Delete(LinkPath);
        // Remove legacy entries only after the replacement was successfully saved.
        using var run = Registry.CurrentUser.OpenSubKey(RunKey, true);
        run?.DeleteValue("SimpleAimOverlay", false);
        run?.DeleteValue("SimpleAimOverlayGameWatcher", false);
    }

    internal static void Migrate(StoreData data)
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        bool legacy = run?.GetValue("SimpleAimOverlay") != null || run?.GetValue("SimpleAimOverlayGameWatcher") != null;
        if (!legacy && (File.Exists(LinkPath) || StartupMode(data.Current.StartWithWindows, data.LaunchWithGame) == null)) return;
        // Respect a startup entry explicitly disabled in Task Manager.
        using var approved = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run");
        foreach (string name in new[] { "SimpleAimOverlay", "SimpleAimOverlayGameWatcher" })
            if (approved?.GetValue(name) is byte[] state && state.Length > 0 && state[0] == 3) return;
        Configure(data.Current.StartWithWindows, data.LaunchWithGame);
    }
}
