using Abstractions;
using Client.Infrastructure.Reaktionen;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Akteure;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Import;
using Pruefdomaene.Messung;
using Pruefdomaene.Pipeline.Abrechnung;
using Pruefdomaene.Pipeline.Auftrag;
using Pruefdomaene.Pipeline.Import;
using Pruefdomaene.Pipeline.Messung;
using Pruefdomaene.Pipeline.Posten;
using Pruefdomaene.Pipeline.Verteilung;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

// Dieser Client IST der ReaktionsWorker der Prüfdomäne (docs/konzept-akteure.md §4): er verkörpert die Sachbearbeitung — was sie hört und darf,
//   steht dort.
[assembly: IstClient<ReaktionsWorker>]

namespace Pruefdomaene.Client.Worker;

/// <summary>
/// Die Reaktionen der Prüfdomäne (Schritt 6): ein Decide gibt nur Fakten aus; auf jedes Fakt, das einen Ablauf beginnt oder fortsetzt, gibt dieser
/// Client den Command an den Kopf seines Graphen. Genau einmal trägt der Läufer: dieselbe Position ergibt dieselbe Command-Id, der offene Eingang am
/// Kopf verwirft die Wiederholung.
/// </summary>
public static class Reaktionslauf
{
    public static Reaktionen Registriere(Reaktionen r) => r
        // Abrechnung
        .Auf<RechnungGestellt>(Gib<RechnungGestellt>((e, _) => new Freizugeben(e.Rechnung, e.Betrag)))
        .Auf<RechnungBezahlt>(Gib<RechnungBezahlt>((e, _) => new Gutschreibung(e.Konto, e.Rechnung, e.Betrag)))
        .Auf<ZahlungUeberfaellig>(Gib<ZahlungUeberfaellig>((e, _) => new Mahnfall(e.Rechnung, e.Konto, e.Betrag, e.Mahnstufe)))
        // Auftrag: Abwicklung und Gegenzüge
        .Auf<AuftragGestartet>(Gib<AuftragGestartet>((e, _) => new AuftragAbzuwickeln(e.AuftragId, e.PostenId, e.KontingentId, e.Menge)))
        .Auf<PostenfreigabeAngefordert>(Gib<PostenfreigabeAngefordert>((e, strom) => new PostenFreizugeben(new(strom), e.PostenId, e.Menge)))
        .Auf<KontingententlastungAngefordert>(Gib<KontingententlastungAngefordert>((e, strom) => new KontingentZuEntlasten(new(strom), e.KontingentId, e.Menge)))
        // Verteilung
        .Auf<VerteilungGestartet>(Gib<VerteilungGestartet>((e, _) => new VerteilungAbzuwickeln(e.VerteilungId, e.QuelleId, e.Ziele, e.MengeJeZiel)))
        .Auf<QuelleReserviert>(Gib<QuelleReserviert>((e, strom) => new Zubuchen(new(strom), e.Ziele, e.MengeJeZiel)))
        // Posten: nur eine Reservierung unter den Mindestbestand ruft den Nachschub
        .Auf<PostenReserviert>(Gib<PostenReserviert>((e, strom) => e.Fehlmenge > 0 ? new Unterschritten(new(strom), e.Fehlmenge) : null))
        // Messung, Import
        .Auf<MesswertErfasst>(Gib<MesswertErfasst>((e, strom) => new NeuerWert(new(strom), e.Wert)))
        .Auf<ImportGestartet>(Gib<ImportGestartet>((e, strom) => new ImportZuLaufen(new(strom), e.Zeilen)));

    /// <summary>Eine Reaktion, die aus dem Fakt (und dem Stream, in dem es steht) höchstens einen Command macht.</summary>
    private static Func<TEvent, ReaktionsKontext, Task> Gib<TEvent>(Func<TEvent, Guid, ICommand?> aus) => (e, k) =>
    {
        if (aus(e, k.Ereignis.AggregateId) is { } c) k.Sende(c);
        return Task.CompletedTask;
    };

    /// <summary>
    /// Der Worker, bis <paramref name="ct"/> endet: verbindet mit dem Host (<paramref name="adresse"/>, Token der Sachbearbeitung), lässt die Reaktionen
    /// laufen und verbindet nach einer Unterbrechung neu — ab dem gesicherten Stand. Derselbe Lauf als Prozess (Program) und in Prüfständen.
    /// </summary>
    public static async Task LaufeAsync(string adresse, string? token, IReaktionsStand stand, string name, CancellationToken ct, TimeSpan? takt = null,
        Action<string>? melde = null)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Je Verbindung ein Proxy, geschlossen beim Ende — ein offener Stream hielte den Host beim Stoppen auf.
                await using var proxy = new global::Client.Infrastructure.Connection.GrpcProxy(Pruefdomaene.Leitung.GeneratedNutzlast.Instanz) { AkteurToken = token };
                await proxy.ConnectAsync(adresse, Microsoft.Extensions.DependencyInjection.GeneratedWiring_Pruefdomaene_Client_Worker.Vertrag,
                    Microsoft.Extensions.DependencyInjection.GeneratedWiring_Pruefdomaene_Client_Worker.VertragHash);
                melde?.Invoke($"verbunden mit {adresse}");
                await Registriere(new Reaktionen(proxy, stand, name, takt: takt)).LaufeAsync(ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                melde?.Invoke($"unterbrochen ({e.Message}) — neu verbinden");
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct); } catch (OperationCanceledException) { }
            }
        }
    }
}
