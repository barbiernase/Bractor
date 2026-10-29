using System.Text.Json;
using Abstractions;
using Infrastructure.Funktionen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Infrastructure.Komposition;

namespace Infrastructure.Einblick;

/// <summary>
/// Die Einblick-API des Hosts (docs/04 §4.9, Werkbank P4.7) — NUR LESEND, nur auf dem Einblick-Port (HTTP/1.1, E6).
/// Die Werkbank holt ab ihrem Cursor nach (<c>/einblick/spur?ab=&lt;seq&gt;</c>); <c>/einblick/neu</c> (SSE) meldet nur „neu bis seq“ — ein
/// verlorenes Signal oder ein Neustart schadet nicht, es wird ab dem Cursor nachgelesen (dasselbe Muster wie Wecker + Lesen ab Position im Kern).
/// </summary>
public static class EinblickEndpunkte
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapEinblick(this WebApplication app, UmgebungKonfig u)
    {
        if (u.Transport.EinblickPort <= 0) return;
        var g = app.MapGroup("/einblick").RequireHost($"*:{u.Transport.EinblickPort}");

        // Stand des Laufs: Board, Build und Lauf tragen denselben (Spuren älterer Stände erscheinen grau).
        g.MapGet("/stand", () => Results.Json(new { stand = Spur.Stand, umgebung = u.Umgebung.Name, art = u.Umgebung.Art.ToString() }, Json));

        // Log-Einträge mit Spur ab einer globalen Sequenz (aufsteigend) — der Cursor der Werkbank.
        g.MapGet("/spur", async (SpurLeser leser, long? ab, int? n, CancellationToken ct) =>
        {
            var eintraege = await leser.LeseAbSeqAsync(ab ?? 0, n ?? 500, ct);
            return Results.Json(new { bis = eintraege.Count > 0 ? eintraege[^1].Seq : ab ?? 0, eintraege }, Json);
        });

        // Die jüngsten Vorgänge mit Zustand (läuft · ruht) und Wurzel (Quelle/Akteur).
        g.MapGet("/vorgaenge", async (SpurLeser leser, int? n, CancellationToken ct)
            => Results.Json(new { vorgaenge = await leser.VorgaengeAsync(n ?? 200, ct) }, Json));

        // Alle Einträge eines Vorgangs samt Auslöser — der Graph für die Spur-Überlagerung (Knoten = Einträge, Kanten = ursache).
        g.MapGet("/vorgang/{korrelation}", async (SpurLeser leser, string korrelation, CancellationToken ct)
            => Results.Json(new { korrelation, eintraege = await leser.VorgangAsync(korrelation, ct) }, Json));

        // Offene Aufträge je Funktion (Vermittler, nicht maßgeblich) — Stau an der Funktions-Karte.
        g.MapGet("/auftraege", async (ActorSystem system, CancellationToken ct)
            => Results.Json(new { auftraege = await FunktionsAnbieter.ListeOffeneAsync(system, Anwendung.Funktionen.Bausteine.Select(b => b.Typ), ct) }, Json));

        // Verlierbares (Ablehnungen, transiente Events) — nur in Sim geschrieben.
        g.MapGet("/hinweise", async (long? ab, int? n, CancellationToken ct)
            => Results.Json(new
            {
                hinweise = (await SpurHinweisSchreiber.LiesAsync(u, ab ?? 0, n ?? 500, ct)).Select(x => new
                {
                    id = x.Id, x.Hinweis.Zeit, x.Hinweis.Art, x.Hinweis.Wirker, x.Hinweis.Ursache, x.Hinweis.Korrelation, x.Hinweis.Text,
                }),
            }, Json));

        // SSE: „neu bis seq“ — geweckt vom Appender (SpurSignal), sonst alle 5 s geprüft; dazwischen ein Kommentar als Lebenszeichen.
        g.MapGet("/neu", async (HttpContext http, SpurLeser leser, CancellationToken ct) =>
        {
            http.Response.Headers.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-store";
            long gemeldet = -1;
            while (!ct.IsCancellationRequested)
            {
                long bis;
                try { bis = await leser.BisAsync(ct); }
                catch (OperationCanceledException) { return; }
                catch (Exception) { bis = gemeldet; }
                if (bis != gemeldet)
                {
                    await http.Response.WriteAsync($"event: neu\ndata: {{\"bis\":{bis}}}\n\n", ct);
                    gemeldet = bis;
                }
                else await http.Response.WriteAsync(": lebt\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
                try
                {
                    await SpurSignal.WarteAsync(TimeSpan.FromSeconds(5), ct);
                    await Task.Delay(100, ct);   // ein Schwall von Commits wird zu einer Meldung
                }
                catch (OperationCanceledException) { return; }
            }
        });
    }

    /// <summary>Registriert Leser und (nur Sim) den Hinweis-Schreiber.</summary>
    public static IServiceCollection AddEinblick(this IServiceCollection s, UmgebungKonfig u)
    {
        s.AddSingleton(new SpurLeser(u));
        if (u.Umgebung.Art == UmgebungsArt.Sim) s.AddHostedService(_ => new SpurHinweisSchreiber(u));
        return s;
    }
}
