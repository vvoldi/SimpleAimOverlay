using SimpleAimOverlay;
using System.Net;
using System.Text.Json;

internal static class ObsChecks
{
    internal static void Run() => Task.Run(Check).GetAwaiter().GetResult();
    private static void Assert(bool valid, string message) { if (!valid) throw new Exception(message); }
    private static async Task Check()
    {
        using var server = new ObsOverlayServer(0);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{server.Port}"), Timeout = TimeSpan.FromSeconds(5) };
        var settings = new AimSettings { Shape = "Cross", Size = 20, Thickness = 2, Gap = 0, Opacity = 100, Visible = true, OffsetX = 5 };
        server.Publish(settings, Color.Lime, Color.Black, new Size(160, 100));
        var state = JsonDocument.Parse(await http.GetStringAsync("/state"));
        string png = state.RootElement.GetProperty("image").GetString()!;
        using (var bytes = new MemoryStream(Convert.FromBase64String(png.Split(',')[1])))
        using (var bitmap = new Bitmap(bytes))
        {
            Assert(bitmap.Size == new Size(160, 100), "OBS preserves monitor dimensions");
            Assert(bitmap.GetPixel(0, 0).A == 0, "OBS background is transparent");
            Assert(bitmap.GetPixel(85, 50).ToArgb() == Color.Lime.ToArgb(), "OBS renders opaque crosshair with center offset");
        }
        using var response = await http.GetAsync("/events", HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
        string? first = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert(first?.Contains("\"visible\":true") == true, "OBS stream publishes its current frame on connect");
        settings.Visible = false; server.Publish(settings, Color.Lime, Color.Black, new Size(160, 100));
        string? next;
        do { next = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)); } while (next != null && !next.StartsWith("data:"));
        Assert(next?.Contains("\"visible\":false") == true, "Hold/hotkey visibility changes reach OBS");
        var hidden = JsonDocument.Parse(await http.GetStringAsync("/state"));
        Assert(hidden.RootElement.GetProperty("image").GetString() == png, "Visibility changes reuse rendered pixels");
        using var badHost = new HttpRequestMessage(HttpMethod.Get, "/state"); badHost.Headers.Host = "untrusted.example";
        using var blocked = await http.SendAsync(badHost); Assert(blocked.StatusCode == HttpStatusCode.Forbidden, "Reject DNS-rebinding hostnames");
        using var badOrigin = new HttpRequestMessage(HttpMethod.Get, "/state"); badOrigin.Headers.Add("Origin", "https://untrusted.example");
        using var originBlocked = await http.SendAsync(badOrigin); Assert(originBlocked.StatusCode == HttpStatusCode.Forbidden, "Reject external website origins");
        using var missing = await http.GetAsync("/settings.json"); Assert(missing.StatusCode == HttpStatusCode.NotFound, "OBS cannot read files or settings");
        string page = await http.GetStringAsync("/"); Assert(page.Contains("events.onerror") && page.Contains("background:transparent"), "OBS hides on disconnect and has transparent background");
        Console.WriteLine("PASS: OBS transparent pixels, dimensions, offsets, live visibility events, reconnect frame and local request restrictions.");
    }
}
