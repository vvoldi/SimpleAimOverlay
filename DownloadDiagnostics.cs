using System.Globalization;

namespace SimpleAimOverlay;

internal sealed class RecentDownloadSpeed
{
    private readonly Queue<(double Time,long Bytes)> samples = new();
    public RecentDownloadSpeed() => samples.Enqueue((0,0));
    internal double Sample(double seconds,long bytes) {
        samples.Enqueue((seconds,bytes));
        // Retain one sample at the window boundary, avoiding byte spikes at eviction.
        while(samples.Count>2 && samples.ElementAt(1).Time<=seconds-4)samples.Dequeue();
        var first=samples.Peek();
        return Math.Max(0,bytes-first.Bytes)/Math.Max(.05,seconds-first.Time);
    }
}

internal sealed class DownloadDiagnostics : IDisposable
{
    private StreamWriter? writer;
    public DownloadDiagnostics(long expected) {
        try {
            string folder=Path.Combine(Store.Root,"logs");Directory.CreateDirectory(folder);
            foreach(var old in new DirectoryInfo(folder).GetFiles("download-*.log").OrderByDescending(f=>f.LastWriteTimeUtc).Skip(9))old.Delete();
            writer=new StreamWriter(Path.Combine(folder,$"download-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log")){AutoFlush=true};
            Write($"start version={AppUpdater.CurrentVersion} expected_bytes={expected} host=github.com speed_window_seconds=4");
        }catch{writer?.Dispose();writer=null;}
    }
    internal void Write(string message){try{writer?.WriteLine(DateTime.UtcNow.ToString("O",CultureInfo.InvariantCulture)+" "+message);}catch{}}
    public void Dispose(){try{writer?.Dispose();}catch{}}
}
