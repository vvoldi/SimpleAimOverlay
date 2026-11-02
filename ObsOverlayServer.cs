using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SimpleAimOverlay;

internal sealed class ObsOverlayServer : IDisposable
{
    internal const int DefaultPort = 47836;
    internal const string BrowserUrl = "http://127.0.0.1:47836/";
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim clients = new(8);
    private string frame = "{\"visible\":false}";
    private string? lastAppearance;
    private string png = "";
    internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    internal ObsOverlayServer(int port = DefaultPort)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        _ = Task.Run(AcceptClients);
    }

    internal void Publish(AimSettings settings, Color color, Color outline, Size size)
    {
        // Visibility changes reuse the encoded image, so holding a key never re-renders a monitor-sized bitmap.
        string appearance = JsonSerializer.Serialize(new { settings.Shape, settings.Size, settings.Thickness, settings.Gap,
            settings.OffsetX, settings.OffsetY, settings.Opacity, settings.Outline, settings.OutlineWidth, settings.OutlineOpacity,
            settings.ImagePath, settings.ImageScale, settings.TintImage, color = color.ToArgb(), outline = outline.ToArgb(), size.Width, size.Height,
            imageStamp = File.Exists(settings.ImagePath) ? File.GetLastWriteTimeUtc(settings.ImagePath).Ticks : 0 });
        if (appearance != lastAppearance)
        {
            using var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                AimRenderer.Draw(g, new Rectangle(Point.Empty, size), settings, color, outline, false);
            }
            using var output = new MemoryStream(); bitmap.Save(output, ImageFormat.Png);
            png = "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
            lastAppearance = appearance;
        }
        Volatile.Write(ref frame, JsonSerializer.Serialize(new { visible = settings.Visible, width = size.Width, height = size.Height, image = png }));
    }

    private async Task AcceptClients()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                if (!clients.Wait(0)) { client.Dispose(); continue; }
                _ = Serve(client);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        try
        {
            client.NoDelay = true;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); timeout.CancelAfter(3000);
            var stream = client.GetStream();
            byte[] buffer = new byte[8192]; int count = 0; string headers = "";
            while (!headers.Contains("\r\n\r\n"))
            {
                int read = await stream.ReadAsync(buffer.AsMemory(count), timeout.Token);
                if (read == 0 || (count += read) == buffer.Length) return;
                headers = Encoding.ASCII.GetString(buffer, 0, count);
            }
            string[] lines = headers.Split("\r\n"), request = lines[0].Split(' ');
            string host = lines.FirstOrDefault(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))?[5..].Trim() ?? "";
            string? origin = lines.FirstOrDefault(l => l.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))?[7..].Trim();
            if (host != $"127.0.0.1:{Port}" || (origin != null && origin != $"http://127.0.0.1:{Port}")) { await Reply(stream, "403 Forbidden", "text/plain", "Forbidden", timeout.Token); return; }
            if (request.Length != 3 || request[0] != "GET") { await Reply(stream, "405 Method Not Allowed", "text/plain", "GET only", timeout.Token); return; }
            if (request[1] == "/") { await Reply(stream, "200 OK", "text/html; charset=utf-8", Page, timeout.Token); return; }
            if (request[1] == "/state") { await Reply(stream, "200 OK", "application/json", Volatile.Read(ref frame), timeout.Token); return; }
            if (request[1] != "/events") { await Reply(stream, "404 Not Found", "text/plain", "Not found", timeout.Token); return; }
            timeout.CancelAfter(Timeout.Infinite);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n"), stop.Token);
            string? previous = null; int idle = 0;
            while (!stop.IsCancellationRequested)
            {
                string current = Volatile.Read(ref frame);
                if (current != previous || ++idle >= 60)
                {
                    using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); writeTimeout.CancelAfter(3000);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(current != previous ? "data: " + current + "\n\n" : ": heartbeat\n\n"), writeTimeout.Token);
                    previous = current; idle = 0;
                }
                await Task.Delay(16, stop.Token);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        finally { clients.Release(); }
    }

    private static async Task Reply(NetworkStream stream, string status, string type, string body, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        string header = $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {bytes.Length}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'none'; img-src data:; connect-src 'self'; script-src 'unsafe-inline'; style-src 'unsafe-inline'\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), token); await stream.WriteAsync(bytes, token);
    }

    internal const string Page = """
        <!doctype html><meta charset="utf-8"><title>Simple Aim Overlay — OBS</title>
        <style>html,body{margin:0;width:100%;height:100%;overflow:hidden;background:transparent}img{position:absolute;width:100%;height:100%;object-fit:contain;visibility:hidden}</style>
        <img id="aim" alt=""><script>
        const aim=document.getElementById('aim');let lastImage='',generation=0;
        const events=new EventSource('/events');
        events.onmessage=e=>{const s=JSON.parse(e.data),g=++generation;
          if(!s.visible){aim.style.visibility='hidden';return;}
          if(s.image===lastImage){aim.style.visibility='visible';return;}
          const next=new Image();next.onload=()=>{if(g!==generation)return;aim.src=s.image;lastImage=s.image;aim.style.visibility='visible';};next.src=s.image;};
        events.onerror=()=>{generation++;aim.style.visibility='hidden';};
        </script>
        """;

    public void Dispose() { stop.Cancel(); listener.Stop(); }
}
