using SimpleAimOverlay;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using System.Windows.Forms;

internal static class Program
{
    const string Product = "Simple Aim Overlay";
    static readonly string InstallDir = Directory.Exists(@"C:\Program Files\SimpleAimOverlay Test") ? @"C:\Program Files\SimpleAimOverlay Test" : @"C:\Program Files\SimpleAimOverlay";
    const string AppExe = "SimpleAimOverlay.exe";
    const string CertThumb = "EA96448AEE620904B58AFD355ACC6657BF16828E";
    const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool MoveFileEx(string existing, string? replacement, int flags);

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try {
            string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SimpleAimOverlay","settings.json");
            using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(settings));
            if(json.RootElement.TryGetProperty("Language",out var language)&&language.GetString()=="uk")UiText.Language="uk";
            if(json.RootElement.TryGetProperty("Theme",out var theme)&&theme.GetString()=="light"){using var root=new Control();DarkTheme.Select(true,root);}
        }catch { }
        try
        {
            if (args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))) { Uninstall(); return; }
            int? parent = args.Length == 2 && args[0] == "/update" && int.TryParse(args[1], out int pid) && pid > 0 ? pid : null;
            Install(parent);
        }
        catch (Exception ex) { DarkMessage.Show("The operation could not be completed.\n\n" + ex.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    static void Install(int? parent)
    {
        var result = parent.HasValue ? DialogResult.Yes : DarkMessage.Show(
            "This is a self-signed Simple Aim Overlay 0.2.4 build.\n\n" +
            "Setup will add the SimpleAimOverlay test certificate to this PC's trusted certificate stores " +
            "and install the app in Program Files (required for UIAccess).\n\n" +
            "Only continue if you trust the package source. This build does not include a DXGI hook.\n\n" +
            "Continue? Windows will ask for administrator approval.",
            Product, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;

        using var progress=new SetupProgress();
        progress.Show();progress.Report("Closing the app…",5);

        if (parent.HasValue) {
            try {
                using var app = Process.GetProcessById(parent.Value);
                if (app.ProcessName == "SimpleAimOverlay" && !app.WaitForExit(3000)) {
                    string expected=Path.GetFullPath(Path.Combine(InstallDir,AppExe));
                    string? actual=app.MainModule?.FileName;
                    if(actual==null||!string.Equals(Path.GetFullPath(actual),expected,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Close Simple Aim Overlay and run setup again.");
                    // Older updaters may cancel their first exit while closing
                    // the progress window. Retry a normal close, never force-kill.
                    for(int attempt=0;attempt<5&&!app.HasExited;attempt++) {
                        app.Refresh();app.CloseMainWindow();if(app.WaitForExit(2000))break;
                    }
                    if(!app.HasExited)throw new InvalidOperationException("Exit Simple Aim Overlay using its tray menu, then run setup again. Your settings have not been changed.");
                }
            }
            catch (ArgumentException) { /* The app already exited. */ }
        }
        StopGameWatcher();
        progress.Report("Preparing installation…",20);
        Directory.CreateDirectory(InstallDir);
        var certBytes = ReadResource("test.cer");
        var cert = X509CertificateLoader.LoadCertificate(certBytes);
        if (!cert.Thumbprint!.Equals(CertThumb, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The bundled certificate failed its thumbprint check.");
        AddToStore(StoreName.Root, cert);
        AddToStore(StoreName.TrustedPublisher, cert);
        progress.Report("Installing application files…",40);
        string target = Path.Combine(InstallDir, AppExe), staging = target + ".new";
        File.WriteAllBytes(staging, ReadResource("payload.exe"));
        if (File.Exists(target)) File.Replace(staging, target, null); else File.Move(staging, target);
        string uninstallerPath = Path.Combine(InstallDir, "Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        string runningSetup = Environment.ProcessPath ?? throw new InvalidOperationException("Could not locate the setup executable.");
        File.WriteAllBytes(uninstallerPath, File.ReadAllBytes(runningSetup));
        progress.Report("Updating shortcuts…",75);
        string oldShortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),"SimpleAimOverlay Test.lnk"); if(File.Exists(oldShortcut))File.Delete(oldShortcut);
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Product + ".lnk"), Path.Combine(InstallDir, AppExe));
        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SimpleAimOverlayTest"))
        {
            key.SetValue("DisplayName", Product);
            key.SetValue("DisplayVersion", "0.2.4");
            key.SetValue("Publisher", "SimpleAimOverlay contributors (local test certificate)");
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("DisplayIcon", Path.Combine(InstallDir, AppExe));
            key.SetValue("UninstallString", "\"" + uninstallerPath + "\" /uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        RestoreGameWatcher();
        progress.Report("Installation complete. Restarting…",100);
        if (parent.HasValue || DarkMessage.Show("Installation is complete. Launch Simple Aim Overlay now?", Product, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, AppExe)) { UseShellExecute = true });
    }

    static void AddToStore(StoreName name, X509Certificate2 cert)
    {
        using var store = new X509Store(name, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        if (!store.Certificates.Cast<X509Certificate2>().Any(c => c.Thumbprint == cert.Thumbprint)) store.Add(cert);
    }

    static byte[] ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new InvalidOperationException("A required package file is missing: " + name);
        using var ms = new MemoryStream(); stream.CopyTo(ms); return ms.ToArray();
    }

    static void CreateShortcut(string linkPath, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is unavailable for shortcut creation.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(linkPath);
        shortcut.TargetPath = target; shortcut.WorkingDirectory = InstallDir; shortcut.Description = Product;
        shortcut.Save(); Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell);
    }

    static void StopGameWatcher() {
        string suffix = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        try { using var signal = EventWaitHandle.OpenExisting(@"Local\SimpleAimOverlay.GameWatcher.Stop." + suffix); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { }
        try { using var mutex = Mutex.OpenExisting(@"Local\SimpleAimOverlay.GameWatcher." + suffix); bool acquired = false; try { acquired = mutex.WaitOne(5000); } catch (AbandonedMutexException) { acquired = true; } finally { if (acquired) mutex.ReleaseMutex(); } if (!acquired) throw new InvalidOperationException("The game watcher is still closing. Try setup again."); } catch (WaitHandleCannotBeOpenedException) { }
    }
    static void RestoreGameWatcher() {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (key?.GetValue("SimpleAimOverlayGameWatcher") != null)
            Process.Start(new ProcessStartInfo(Path.Combine(InstallDir, AppExe)) { UseShellExecute = true, Arguments = "--watch-game", WindowStyle = ProcessWindowStyle.Hidden });
    }
    static void Uninstall()
    {
        if (DarkMessage.Show("Remove Simple Aim Overlay Test and its test certificate from this PC's trusted stores?", Product, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        StopGameWatcher();
        using (var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) { run?.DeleteValue("SimpleAimOverlayGameWatcher", false); run?.DeleteValue("SimpleAimOverlay", false); }
        try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Product + ".lnk")); } catch { }
        foreach (var storeName in new[] { StoreName.Root, StoreName.TrustedPublisher })
        {
            using var store = new X509Store(storeName, StoreLocation.LocalMachine); store.Open(OpenFlags.ReadWrite);
            foreach (var c in store.Certificates.Cast<X509Certificate2>().Where(c => c.Thumbprint == CertThumb).ToArray()) store.Remove(c);
        }
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true)) key?.DeleteSubKeyTree("SimpleAimOverlayTest", false);
        string appPath = Path.Combine(InstallDir, AppExe);
        try { File.Delete(appPath); } catch { _ = MoveFileEx(appPath, null, MOVEFILE_DELAY_UNTIL_REBOOT); }
        string? runningUninstaller = Environment.ProcessPath;
        foreach (string stale in Directory.EnumerateFiles(InstallDir, "Uninstall-*.exe"))
            if (!string.Equals(Path.GetFullPath(stale), runningUninstaller, StringComparison.OrdinalIgnoreCase)) try { File.Delete(stale); } catch { }
        try { File.Delete(Path.Combine(InstallDir, "Uninstall.exe")); } catch { }
        if (!string.IsNullOrWhiteSpace(runningUninstaller)) _ = MoveFileEx(runningUninstaller, null, MOVEFILE_DELAY_UNTIL_REBOOT);
        DarkMessage.Show("The app and its test certificate have been removed. The installation folder will be cleaned after Windows restarts.", Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
