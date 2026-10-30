namespace SimpleAimOverlay;

internal static class StartupDiagnostics
{
    // No command-line arguments, game paths or account names are recorded.
    internal static void Write(string message)
    {
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleAimOverlay", "logs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "startup.log");
            using var gate = new Mutex(false, @"Local\SimpleAimOverlay.StartupLog");
            bool held = false;
            try
            {
                try { held = gate.WaitOne(250); } catch (AbandonedMutexException) { held = true; }
                if (!held) return;
                if (File.Exists(path) && new FileInfo(path).Length > 131072) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} v{typeof(Program).Assembly.GetName().Version} pid={Environment.ProcessId} {message}{Environment.NewLine}");
            }
            finally { if (held) gate.ReleaseMutex(); }
        }
        catch { /* Diagnostics must never prevent startup. */ }
    }
}
