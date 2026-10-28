using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SimpleAimOverlay;

internal sealed class AppUpdater : IDisposable
{
    private const string ReleasePrefix = "https://github.com/vvoldi/SimpleAimOverlay/releases/download/";
    private const string Signer = "EA96448AEE620904B58AFD355ACC6657BF16828E";
    private readonly HttpClient client;
    private readonly CancellationTokenSource lifetime = new();
    private bool startupChecked, busy;
    internal static Version CurrentVersion => typeof(AppUpdater).Assembly.GetName().Version ?? new Version(0,0,0);
    internal static Version? ParseVersion(string tag) => Version.TryParse(tag.TrimStart('v').Split('-')[0], out var v) ? new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision)) : null;
    internal sealed record Release(Version Version, string Url, string Digest, long Size);
    internal static Release? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var version = ParseVersion(root.GetProperty("tag_name").GetString() ?? "");
        if (version == null) return null;
        string expected = $"SimpleAimOverlay-{version.Major}.{version.Minor}.{version.Build}-Setup.exe";
        foreach (var asset in root.GetProperty("assets").EnumerateArray()) {
            if (asset.GetProperty("name").GetString() != expected) continue;
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";
            string digest = asset.TryGetProperty("digest",out var d) ? d.GetString() ?? "" : "";
            long size = asset.GetProperty("size").GetInt64();
            if (!url.StartsWith(ReleasePrefix,StringComparison.Ordinal) || !digest.StartsWith("sha256:",StringComparison.Ordinal) || digest.Length != 71 || size <= 0 || size > 600_000_000)
                throw new InvalidDataException("The release package metadata is invalid. Please use the GitHub release page.");
            return new Release(version,url,digest[7..],size);
        }
        return null;
    }
    public AppUpdater() : this(new HttpClientHandler()) { }
    internal AppUpdater(HttpMessageHandler handler) { client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) }; client.DefaultRequestHeaders.UserAgent.ParseAdd("SimpleAimOverlay/" + CurrentVersion.ToString(3)); client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); }
    public async Task CheckAsync(Form owner, bool manual, Action<string> status, Action installed)
    {
        if (busy || owner.IsDisposed || (!manual && startupChecked)) return;
        if (!manual) startupChecked = true;
        busy = true; string? file = null; bool launched = false; UpdateProgressWindow? progressWindow = null;
        try {
            status("Checking for updates…");
            using var checkTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            checkTimeout.CancelAfter(TimeSpan.FromSeconds(20));
            string json = await client.GetStringAsync("https://api.github.com/repos/vvoldi/SimpleAimOverlay/releases/latest", checkTimeout.Token);
            var release = ParseRelease(json);
            if (owner.IsDisposed) return;
            if (release == null) { status("No compatible update package is available."); return; }
            if (release.Version <= CurrentVersion) { status("You're up to date · " + CurrentVersion.ToString(3)); return; }
            status("Update available · " + release.Version.ToString(3));
            if (owner.IsDisposed || DarkMessage.Show(owner,$"Version {release.Version.ToString(3)} is available. Download and install it now?\n\nYour settings will be kept. Windows may ask for administrator approval.","Update available",MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            string directory = Path.Combine(Path.GetTempPath(),"SimpleAimOverlay-update-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            file = Path.Combine(directory,"Setup.exe");
            using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            downloadTimeout.CancelAfter(TimeSpan.FromMinutes(30));
            progressWindow = new UpdateProgressWindow();
            progressWindow.CancelRequested += () => downloadTimeout.Cancel();
            progressWindow.Show(owner);
            var progress = new Progress<DownloadProgress>(p => {
                if (owner.IsDisposed) return;
                status($"Downloading update… {p.Received * 100 / Math.Max(1,p.Total)}%");
                progressWindow.Report(p);
            });
            await DownloadAsync(release, file, progress, downloadTimeout.Token);
            status("Verifying update…"); progressWindow.SetStage("Verifying update…");
            await VerifyAsync(file, release.Digest, downloadTimeout.Token);
            downloadTimeout.Token.ThrowIfCancellationRequested();
            progressWindow.SetStage("Waiting for Windows approval…");
            var start = new ProcessStartInfo(file) { UseShellExecute=true, Verb="runas" };
            start.ArgumentList.Add("/update"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the installer.");
            launched=true; installed();
        }
        catch (OperationCanceledException) { if(!owner.IsDisposed)status("Update check timed out or was cancelled."); }
        catch (System.ComponentModel.Win32Exception ex) when(ex.NativeErrorCode==1223) { if(!owner.IsDisposed)status("Update cancelled. You can try again in Settings."); }
        catch (Exception ex) { if(!owner.IsDisposed){status("Update unavailable · try again in Settings");if(manual || progressWindow != null)DarkMessage.Show(owner,ex.Message,"Update could not be completed");} }
        finally { if(progressWindow != null) { if(!progressWindow.IsDisposed)progressWindow.Finish();progressWindow.Dispose(); } busy=false; if(!launched && file!=null){try{File.Delete(file);Directory.Delete(Path.GetDirectoryName(file)!);}catch{}} }
    }
    internal sealed record DownloadProgress(long Received, long Total, double BytesPerSecond);
    internal async Task DownloadAsync(Release release, string file, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        using var response = await client.GetAsync(release.Url,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var output = new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None,262144,true);
        await CopyDownloadAsync(input,output,release.Size,progress,token).ConfigureAwait(false);
    }
    internal static async Task CopyDownloadAsync(Stream input, Stream output, long expectedSize, IProgress<DownloadProgress> progress, CancellationToken token)
    {
        byte[] buffer = new byte[262144]; long total = 0;
        var elapsed = Stopwatch.StartNew(); long reportedAt = -250;
        while (true) {
            // Abort a stalled connection without imposing a short total download limit.
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            idle.CancelAfter(TimeSpan.FromSeconds(45));
            int read = await input.ReadAsync(buffer,idle.Token).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
            if (total > expectedSize) throw new InvalidDataException("Downloaded package size does not match the release.");
            await output.WriteAsync(buffer.AsMemory(0,read),token).ConfigureAwait(false);
            if (elapsed.ElapsedMilliseconds-reportedAt >= 250 || total==expectedSize) {
                progress.Report(new(total,expectedSize,total/Math.Max(0.1,elapsed.Elapsed.TotalSeconds)));
                reportedAt=elapsed.ElapsedMilliseconds;
            }
        }
        if (total != expectedSize) throw new InvalidDataException("The download is incomplete.");
    }
    internal static async Task VerifyAsync(string file,string digest,CancellationToken token)
    {
        file = Path.GetFullPath(file);
        await using(var stream=File.OpenRead(file)) {
            string actual=Convert.ToHexString(await SHA256.HashDataAsync(stream,token));
            if(!actual.Equals(digest,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Update checksum verification failed.");
        }
        // Validate Authenticode and pin our publisher, not merely any trusted signer.
        string script="$s=Get-AuthenticodeSignature -LiteralPath '"+file.Replace("'","''")+"'; if($s.Status -eq 'Valid' -and $s.SignerCertificate.Thumbprint -eq '"+Signer+"'){exit 0}else{Write-Output $s.Status; Write-Output $s.StatusMessage; exit 1}";
        var psi=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        // Do not inherit PowerShell 7's module path when launched from a developer shell.
        psi.Environment["PSModulePath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\Modules");
        psi.ArgumentList.Add("-NoProfile");psi.ArgumentList.Add("-NonInteractive");psi.ArgumentList.Add("-EncodedCommand");psi.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        using var verifier=Process.Start(psi) ?? throw new InvalidOperationException("Could not verify the update signature.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try{await verifier.WaitForExitAsync(timeout.Token);}catch{try{verifier.Kill();}catch{}throw;}
        if(verifier.ExitCode!=0)throw new InvalidDataException("The update does not have a valid signature from the expected publisher. " + await verifier.StandardOutput.ReadToEndAsync(token) + await verifier.StandardError.ReadToEndAsync(token));
    }
    public void Dispose(){lifetime.Cancel();client.Dispose();lifetime.Dispose();}
}
