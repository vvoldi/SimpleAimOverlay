using SimpleAimOverlay;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using System.Windows.Forms;

internal static class Program
{
    const string Product = "SimpleAimOverlay Test";
    const string InstallDir = @"C:\Program Files\SimpleAimOverlay Test";
    const string AppExe = "SimpleAimOverlay.exe";
    const string CertThumb = "EA96448AEE620904B58AFD355ACC6657BF16828E";
    const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool MoveFileEx(string existing, string? replacement, int flags);

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (args.Any(a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))) { Uninstall(); return; }
            Install();
        }
        catch (Exception ex) { DarkMessage.Show("The operation could not be completed.\n\n" + ex.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    static void Install()
    {
        var result = DarkMessage.Show(
            "This is a self-signed Simple Aim Overlay 0.1.9 build.\n\n" +
            "Setup will add the SimpleAimOverlay test certificate to this PC's trusted certificate stores " +
            "and install the app in Program Files (required for UIAccess).\n\n" +
            "Only continue if you trust the package source. This build does not include a DXGI hook.\n\n" +
            "Continue? Windows will ask for administrator approval.",
            Product, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;

        Directory.CreateDirectory(InstallDir);
        var certBytes = ReadResource("test.cer");
        var cert = X509CertificateLoader.LoadCertificate(certBytes);
        if (!cert.Thumbprint!.Equals(CertThumb, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The bundled certificate failed its thumbprint check.");
        AddToStore(StoreName.Root, cert);
        AddToStore(StoreName.TrustedPublisher, cert);
        File.WriteAllBytes(Path.Combine(InstallDir, AppExe), ReadResource("payload.exe"));
        string uninstallerPath = Path.Combine(InstallDir, "Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        string runningSetup = Environment.ProcessPath ?? throw new InvalidOperationException("Could not locate the setup executable.");
        File.WriteAllBytes(uninstallerPath, File.ReadAllBytes(runningSetup));
        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Product + ".lnk"), Path.Combine(InstallDir, AppExe));
        using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SimpleAimOverlayTest"))
        {
            key.SetValue("DisplayName", Product + " (Self-signed)");
            key.SetValue("DisplayVersion", "0.1.9");
            key.SetValue("Publisher", "SimpleAimOverlay contributors (local test certificate)");
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("DisplayIcon", Path.Combine(InstallDir, AppExe));
            key.SetValue("UninstallString", "\"" + uninstallerPath + "\" /uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        if (DarkMessage.Show("Installation is complete. Launch Simple Aim Overlay now?", Product, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
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

    static void Uninstall()
    {
        if (DarkMessage.Show("Remove Simple Aim Overlay Test and its test certificate from this PC's trusted stores?", Product, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
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
