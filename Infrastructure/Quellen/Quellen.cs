using System.Security.Cryptography;
using System.Text;
using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Akteure;       // AkteurHerkunft
using Infrastructure.PubSub;       // BrokerPublisher, Commitmeldung
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Quellen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// QUELLEN = fest codierte Treiber + Konfig (docs/konzept-werkbank.md §3). Eine Domäne benennt ihre Quelle nur als Typ
// (`sealed record DateiErkannt : DateiGemeldet;`); der Treiber (hier: Datei) liest, die Konfig der Umgebung (`Quellen:<Typ>`) sagt wie.
// Jede Nachricht wird das erste Event ihres Streams — Id deterministisch aus Typ + Kennung, geschrieben per StartStream (oder, mit „ein Stream je
// Schlüssel“, am Kopf mit Kennungs-Verzeichnis). Dieselbe Nachricht zweimal (Neustart, zweiter Knoten, Re-Scan) steht also genau einmal im Log.
// Danach meldet der Eingang den Commit wie ein Aggregat (Commitmeldung: Push + Signal) — die Funktionen, die den Typ hören, sehen ihn sofort;
// geht die Meldung verloren, holt sie der Takt.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Was ein Datei-Treiber über eine fertige Datei weiß — die Fabrik eines Quellen-Typs baut daraus die Nachricht.</summary>
public sealed record DateiDaten(string Name, string Pfad, string RelativerPfad, long Groesse, DateTimeOffset GeaendertAm,
    DateTimeOffset ErkanntAm, string Kennung);

/// <summary>Was der Takt-Treiber über einen erreichten Zeitpunkt weiß (Beginn des Intervalls und seine Kennung).</summary>
public sealed record TaktDaten(DateTimeOffset Zeitpunkt, string Kennung);

/// <summary>
/// Ein Quellen-Typ aus der generierten Tabelle <c>GeneratedQuellen.Alle</c>: Typ, Treiber, Konfig-Abschnitt (= Typname), Wirker-Id
/// und die reflexionsfreie Fabrik seines Treibers (genau eine ist gesetzt).
/// </summary>
public sealed record QuellTyp(Type Typ, string Treiber, string Abschnitt, string WirkerId, Func<DateiDaten, IQuellNachricht>? DateiFabrik,
    Func<TaktDaten, IQuellNachricht>? TaktFabrik = null);

/// <summary>Deterministische Vorgangs-Ids der Quellen.</summary>
public static class QuellVorgang
{
    /// <summary>Stream-Id des Vorgangs einer Quell-Nachricht: aus Typ und Kennung (gleiche Nachricht → gleicher Vorgang).</summary>
    public static Guid StreamFür(Type nachricht, string kennung)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes($"quelle:{nachricht.FullName}:{kennung}"), hash);
        return new Guid(hash);
    }

    /// <summary>Der Stream eines Schlüssels (Option „ein Stream je Schlüssel“): alle Nachrichten dieses Typs mit diesem Schlüssel.</summary>
    public static Guid StreamFürSchluessel(Type nachricht, string schluessel) => StreamFür(nachricht, "schluessel:" + schluessel);

    /// <summary>
    /// Der Schlüssel einer Nachricht nach dem Treiber, wenn die Quelle einen Stream je Schlüssel hat: eine Datei — ihr Ordner (relativ), ein Takt — die
    /// Quelle selbst; sonst (Client, Stream je Nachricht) keiner.
    /// </summary>
    public static string? Schluessel(IQuellNachricht nachricht, QuellKonfig? konfig) => konfig?.Strom != QuellStromArt.JeSchluessel ? null : nachricht switch
    {
        DateiGemeldet d => System.IO.Path.GetDirectoryName(d.RelativerPfad)?.Replace('\\', '/') ?? "",
        TaktGemeldet => "",
        _ => null,
    };
}

/// <summary>
/// Der transport-freie Kern: Nachricht genau einmal ins Log, dann den Commit melden. Store-Semantik wird gegen echtes Marten geprüft; live über
/// <see cref="QuellenDienst"/> an Marten und den Cluster gebunden.
/// </summary>
public sealed class QuellEingang
{
    /// <summary>Den Commit melden: Events, Stream, Aggregat-Typ, Version davor, Korrelation, Ursache, Akteur (live <see cref="Commitmeldung"/>).</summary>
    public delegate void Gemeldet(IReadOnlyList<IEvent> events, Guid stream, string aggregatTyp, int vorher, string? korrelation, string? ursache, string? akteur);

    private readonly IEventStoreRepository _store;
    private readonly Gemeldet? _melde;
    private readonly Func<Type, string?> _akteur;
    private readonly IStreamBestand? _bestand;

    /// <param name="melde">Meldet den Commit an die Hörer (Push + Signal); null = niemand wird geweckt (der Takt holt es).</param>
    /// <param name="akteur">Der Akteur, der diesen Nachrichtentyp liefert (IDarf), oder null.</param>
    public QuellEingang(IEventStoreRepository store, Gemeldet? melde = null, Func<Type, string?>? akteur = null, IStreamBestand? bestand = null)
    {
        _store = store;
        _melde = melde;
        _akteur = akteur ?? (_ => null);
        _bestand = bestand;
    }

    /// <summary>Der Eingang live: Marten-Log, Commit-Meldung über den Broker, Akteur aus den IDarf-Befugnissen.</summary>
    public static QuellEingang Live(IServiceProvider sp)
    {
        var publisher = sp.GetService<BrokerPublisher>();
        return new QuellEingang(sp.GetRequiredService<IEventStoreRepository>(),
            (events, stream, typ, vorher, korrelation, ursache, akteur) => Commitmeldung.Melde(publisher, events, stream, typ, vorher, korrelation, ursache, akteur),
            AkteurHerkunft.EindeutigerHalter, sp.GetService<IStreamBestand>() ?? new Infrastructure.Persistence.MartenStreamBestand(sp.GetRequiredService<Marten.IDocumentStore>()));
    }

    /// <summary>
    /// Nimmt eine Quell-Nachricht auf: genau einmal ins Log, dann den Commit melden. Liefert den Vorgang (die Id aus Typ und Kennung).
    /// Ohne <paramref name="schluessel"/> wird sie das erste Event ihres eigenen Streams (Version 0 — der Stream ist der Vorgang); mit Schlüssel geht
    /// sie in den Stream des Schlüssels, und die Kennung steht in derselben Transaktion im Kennungs-Verzeichnis (<see cref="QuellKennung"/>).
    /// <paramref name="akteur"/>: wer sie hineingab (das Tor des Eingangs); sonst der eine Akteur mit <c>IDarf</c>.
    /// </summary>
    public async Task<Guid> NimmAufAsync(IQuellNachricht nachricht, CancellationToken ct, string? schluessel = null, string? akteur = null)
    {
        var typ = nachricht.GetType();
        var vorgang = QuellVorgang.StreamFür(typ, nachricht.Kennung);
        // Spur: eine Quell-Nachricht ist eine Wurzel (keine Ursache im Log), geschrieben von der Quelle.
        using var spur = Spur.Von(null, WirkerId.FuerQuelle(typ.Name));
        var wer = akteur ?? _akteur(typ);
        if (schluessel is null)
        {
            try
            {
                await _store.AppendEventsAsync(vorgang, 0, new IEvent[] { nachricht },
                    correlationId: vorgang.ToString(), causationId: vorgang.ToString(), aggregateType: $"Quelle:{typ.Name}", akteur: wer);
            }
            catch (Exception)
            {
                // Schon aufgenommen (Neustart, anderer Knoten)? Dann steht sie im Log und ist gemeldet worden (oder der Takt holt sie).
                if ((await _store.ReadStreamAsync(vorgang, 0, ct)).Count == 0) throw;
                return vorgang;
            }
            _melde?.Invoke(new IEvent[] { nachricht }, vorgang, $"Quelle:{typ.Name}", 0, vorgang.ToString(), vorgang.ToString(), wer);
            return vorgang;
        }

        // Ein Stream je Schlüssel: anhängen mit Versionsprüfung am Kopf; die Kennung macht es genau einmal (Kennungs-Verzeichnis, eine Transaktion).
        var stream = QuellVorgang.StreamFürSchluessel(typ, schluessel);
        for (var versuch = 1; ; versuch++)
        {
            var kopf = await _store.KopfAsync(stream, ct);
            try
            {
                await _store.AppendEventsAsync(stream, kopf, new IEvent[] { nachricht }, correlationId: vorgang.ToString(),
                    causationId: vorgang.ToString(), aggregateType: $"Quelle:{typ.Name}", akteur: wer, beilagen: [new QuellKennung(vorgang)]);
                _melde?.Invoke(new IEvent[] { nachricht }, stream, $"Quelle:{typ.Name}", kopf, vorgang.ToString(), vorgang.ToString(), wer);
                return vorgang;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                if (_bestand is not null && (await _bestand.BekannteAsync(new[] { vorgang }, ct)).Contains(vorgang)) return vorgang;   // schon im Log
                if (versuch >= 5) throw;
                await Task.Delay(Random.Shared.Next(5, 25 * versuch), ct);   // ein anderer Schreiber war am Kopf — neu lesen
            }
        }
    }

}

public static partial class QuellenExtensions
{
    /// <summary>
    /// Die Webhooks der Quellen (konzept-werkbank §12.3 E12) — aus der Konfig, nicht aus dem Programm: je Quelle mit <c>Quellen:&lt;Typ&gt;:Webhook</c> ein
    /// <c>POST {route}</c> auf dem Einblick-Port (HTTP/1.1) mit der Nachricht als JSON (gelesen über den generierten Wire-Kontext, keine
    /// Reflection) → genau einmal ins Log, Vorgang gestartet. Antwortet <c>202 Accepted</c> mit der Vorgangs-Id (dieselbe Nachricht zweimal →
    /// derselbe Vorgang), <c>400</c> bei einem Body, der nicht die Quell-Nachricht ist.
    /// </summary>
    public static void MapQuellWebhooks(this WebApplication app, UmgebungKonfig u)
    {
        var quellen = app.Services.GetServices<KonfigurierteQuelle>().Where(q => !string.IsNullOrWhiteSpace(q.Konfig.Webhook)).ToList();
        if (quellen.Count == 0) return;
        if (u.Transport.EinblickPort <= 0)
            throw new Infrastructure.Umgebung.BootFehler($"{Treibervertrag.KonfigAbschnitt}:{quellen[0].Typ.Abschnitt}:Webhook braucht einen Einblick-Port "
                + "(Transport:EinblickPort, HTTP/1.1) — er ist 0.", "quelle:" + quellen[0].Typ.Abschnitt);
        foreach (var g in quellen.GroupBy(q => q.Konfig.Webhook!.Trim(), StringComparer.Ordinal).Where(g => g.Count() > 1))
            throw new Infrastructure.Umgebung.BootFehler($"Webhook {g.Key} ist mehrfach vergeben ({string.Join(", ", g.Select(q => q.Typ.Abschnitt))}).",
                "quelle:" + g.First().Typ.Abschnitt);
        foreach (var q in quellen)
        {
            var typ = q.Typ.Typ;
            app.MapPost(q.Konfig.Webhook!.Trim(), async (HttpContext http, System.Text.Json.JsonElement body) =>
            {
                IQuellNachricht nachricht;
                try { nachricht = (IQuellNachricht)Infrastructure.Eingang.EingangJson.Lies(body, typ); }
                catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or NotSupportedException or InvalidCastException)
                { return Results.BadRequest(new { fehler = $"{typ.Name} nicht lesbar: {e.Message}" }); }
                var vorgang = await QuellEingang.Live(http.RequestServices).NimmAufAsync(nachricht, http.RequestAborted,
                    QuellVorgang.Schluessel(nachricht, q.Konfig));
                return Results.Accepted(value: new { vorgang });
            }).RequireHost($"*:{u.Transport.EinblickPort}");
            Console.WriteLine($"  ⛲ Quelle {q.Typ.Abschnitt}: Webhook POST :{u.Transport.EinblickPort}{q.Konfig.Webhook!.Trim()}");
        }
    }
}
