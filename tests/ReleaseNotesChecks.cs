using SimpleAimOverlay;
using System.Net;
using System.Text.Json;

internal static class ReleaseNotesChecks
{
    internal static void Run() => Task.Run(async () => {
        static object Entry(string tag, string? body, bool draft = false, bool prerelease = false) => new { tag_name = tag, body, draft, prerelease };
        var first = Enumerable.Repeat(Entry("v0.1.0", "old"), 100).ToArray();
        first[0] = Entry("v0.2.12", "latest duplicate");
        first[1] = Entry("v0.2.11", "feature eleven");
        first[2] = Entry("v0.2.10", "draft", draft: true);
        first[3] = Entry("v0.2.13", "future");
        first[4] = Entry("v0.2.10", "preview", prerelease: true);
        var handler = new Pages(JsonSerializer.Serialize(first), JsonSerializer.Serialize(new[] { Entry("v0.2.10", "feature ten"), Entry("v0.2.9", "installed"), Entry("v0.2.11", "duplicate") }));
        var latest = new AppUpdater.Release(new Version(0,2,12,0), "", "", 1, "feature twelve");
        using var updater = new AppUpdater(handler);
        var notes = await updater.CollectNotesAsync(latest, new Version(0,2,9,0), CancellationToken.None);
        if (handler.Count != 2 || !notes.Contains("feature ten") || !notes.Contains("feature eleven") || !notes.Contains("feature twelve") || notes.Contains("duplicate") || notes.Contains("preview") || notes.Contains("draft") || notes.Contains("future") || notes.Contains("installed")) throw new Exception("Release history pagination/filtering failed");
        if (notes.IndexOf("feature twelve") > notes.IndexOf("feature eleven") || notes.IndexOf("feature eleven") > notes.IndexOf("feature ten")) throw new Exception("Release history order failed");
        using var unavailable = new AppUpdater(new Pages());
        notes = await unavailable.CollectNotesAsync(latest, new Version(0,2,9,0), CancellationToken.None);
        if (!notes.Contains("incomplete") || !notes.Contains("feature twelve")) throw new Exception("History failure must retain latest notes and explain missing history");
        Console.WriteLine("PASS: cumulative release notes, pagination, stable-version range, duplicates and history failure fallback.");
    }).GetAwaiter().GetResult();

    private sealed class Pages(params string[] pages) : HttpMessageHandler
    {
        public int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            int index = Count++;
            return Task.FromResult(index < pages.Length
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(pages[index]) }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
