using System.Text.Json;
using System.Threading.Channels;

namespace Werkbank.Server;

/// <summary>
/// Der Fortschritts-Kanal der Werkbank (P3.7): jeder Schritt von ⟳, jede Sim-DB-Aktion und jeder Host-Wechsel geht als Ereignis an alle
/// offenen SSE-Verbindungen (<c>GET /api/werkbank/ereignisse</c>). Verlierbar (Inv. 6) — die Wahrheit holt sich die Kopfzeile über
/// <c>/api/werkbank/zustand</c>; das Ereignis sagt nur, dass sie nachsehen soll und was gerade läuft.
/// </summary>
public sealed class Fortschritt
{
    private readonly Lock _sperre = new();
    private readonly List<Channel<string>> _hoerer = [];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Melde(string art, object daten)
    {
        var zeile = JsonSerializer.Serialize(new { art, daten, zeit = DateTimeOffset.UtcNow }, Json);
        lock (_sperre)
            foreach (var h in _hoerer) h.Writer.TryWrite(zeile);
    }

    /// <summary>Schreibt die Ereignisse als <c>text/event-stream</c>, bis der Browser die Verbindung schließt.</summary>
    public async Task SendeAsync(HttpResponse antwort, CancellationToken ct)
    {
        antwort.Headers.ContentType = "text/event-stream";
        antwort.Headers.CacheControl = "no-store";
        var kanal = Channel.CreateBounded<string>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_sperre) _hoerer.Add(kanal);
        try
        {
            await antwort.WriteAsync(": verbunden\n\n", ct);
            await antwort.Body.FlushAsync(ct);
            while (!ct.IsCancellationRequested)
            {
                using var takt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                takt.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    var zeile = await kanal.Reader.ReadAsync(takt.Token);
                    await antwort.WriteAsync($"data: {zeile}\n\n", ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    await antwort.WriteAsync(": takt\n\n", ct);   // hält Proxys und den Browser wach
                }
                await antwort.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { /* Browser weg */ }
        finally { lock (_sperre) _hoerer.Remove(kanal); }
    }
}
