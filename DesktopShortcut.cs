using System.Runtime.InteropServices;
namespace SimpleAimOverlay;

internal static class DesktopShortcut
{
    internal static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Simple Aim Overlay.lnk");
    internal static void Create(string target, string? destination = null)
    {
        if (!File.Exists(target)) throw new FileNotFoundException("Application executable was not found.");
        string path = destination ?? ShortcutPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            try
            {
                link.TargetPath = target; link.Arguments = "";
                link.WorkingDirectory = Path.GetDirectoryName(target)!;
                link.IconLocation = target + ",0"; link.Description = "Simple Aim Overlay";
                link.Save();
            }
            finally { Marshal.FinalReleaseComObject(link); }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}
