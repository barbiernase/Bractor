using Abstractions;
using Client.Infrastructure.Reaktionen;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Akteure;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Import;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Messung;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;

// Dieser Client IST der ReaktionsWorker der Prüfdomäne (docs/konzept-akteure.md §4): er verkörpert die Sachbearbeitung — was sie hört und darf,
//   steht dort.
[assembly: IstClient<ReaktionsWorker>]

namespace Pruefdomaene.Client.Worker;

/// <summary>
/// Die Reaktionen der Prüfdomäne (docs/04 §4.4): ein Decide gibt nur Fakten aus, was danach geschieht, stößt dieser Client an. Wo ein Graph
/// aus dem Katalog Pruefwerk rechnet (Freigabe, Mahnung, Merkmalbildung, Import), gibt er die Nachricht der Domäne, die eine Übersetzung in den Kopf des
/// Graphen macht; wo nur Aggregate einander antworten (Auftrag, Verteilung, Nachschub, Gutschrift), gibt er die Commands selbst — der Beleg im Fakt sagt,
/// zu welchem Vorgang es gehört. Genau einmal trägt der Läufer: dieselbe Position ergibt dieselben Command-Ids.
/// </summary>
public static class Reaktionslauf
{
    public static Reaktionen Registriere(Reaktionen r) => r
        // Abrechnung: Freigabe und Mahnung rechnet der Katalog, die Gutschrift ist ein Command an das Konto
        .Auf<RechnungGestellt>(Gib<RechnungGestellt>((e, _) => [new Freizugeben(e.Rechnung, e.Betrag)]))
        .Auf<RechnungBezahlt>(Gib<RechnungBezahlt>((e, _) => [new BucheGutschrift(e.Konto, e.Rechnung, e.Betrag)]))
        .Auf<ZahlungUeberfaellig>(Gib<ZahlungUeberfaellig>((e, _) => [new Mahnfall(e.Rechnung, e.Konto, e.Betrag, e.Mahnstufe)]))
        // Auftrag: Posten reservieren und Kontingent belegen (zwei Zweige, der Beleg nennt den Auftrag); Gegenzüge bei einem Scheitern
        .Auf<AuftragGestartet>(Gib<AuftragGestartet>((e, _) =>
            [new ReservierePosten(e.PostenId, e.Menge, Auftrag(e.AuftragId.Wert)), new BelegeKontingent(e.KontingentId, e.Menge, Auftrag(e.AuftragId.Wert))]))
        .Auf<PostenfreigabeAngefordert>(Gib<PostenfreigabeAngefordert>((e, strom) => [new GibPostenFrei(e.PostenId, e.Menge, Auftrag(strom))]))
        .Auf<KontingententlastungAngefordert>(Gib<KontingententlastungAngefordert>((e, strom) => [new EntlasteKontingent(e.KontingentId, e.Menge, Auftrag(strom))]))
        .Auf<KontingentBelegt>(Gib<KontingentBelegt>((e, _) => [new VermerkeBelegung(new(e.Beleg.Nummer))]))
        .Auf<BelegungAbgelehnt>(Gib<BelegungAbgelehnt>((e, _) => [new VermerkeAbsage(new(e.Beleg.Nummer), $"Kontingent: {e.Frei} frei, {e.Angefordert} angefordert")]))
        // Verteilung: die Quelle reservieren, dann je Ziel zubuchen; jede Zubuchung meldet sich bei der Verteilung
        .Auf<VerteilungGestartet>(Gib<VerteilungGestartet>((e, _) =>
            [new ReservierePosten(e.QuelleId, e.MengeJeZiel * e.Ziele.Count, Verteilung(e.VerteilungId.Wert))]))
        .Auf<QuelleReserviert>(Gib<QuelleReserviert>((e, strom) => e.Ziele.Select(z => (ICommand)new BuchePostenZu(z, e.MengeJeZiel, Verteilung(strom)))))
        .Auf<PostenZugebucht>(Gib<PostenZugebucht>((e, _) =>
            e.Beleg.Art == Belegart.Verteilung ? [new VermerkeZubuchung(new(e.Beleg.Nummer), e.Menge)] : []))
        // Posten: die Antwort auf eine Reservierung gehört dem Vorgang im Beleg; nur eine Reservierung unter den Mindestbestand ruft den Nachschub
        .Auf<PostenReserviert>(Gib<PostenReserviert>((e, strom) =>
        {
            var aus = new List<ICommand>();
            if (e.Fehlmenge > 0) aus.Add(new BuchePostenZu(new(strom), e.Fehlmenge, Beleg.Ohne));
            if (e.Beleg.Art == Belegart.Auftrag) aus.Add(new VermerkeReservierung(new(e.Beleg.Nummer)));
            if (e.Beleg.Art == Belegart.Verteilung) aus.Add(new VermerkeQuellreservierung(new(e.Beleg.Nummer)));
            return aus;
        }))
        .Auf<ReservierungAbgelehnt>(Gib<ReservierungAbgelehnt>((e, _) => e.Beleg.Art switch
        {
            Belegart.Auftrag => [new VermerkeAbsage(new(e.Beleg.Nummer), $"Posten: {e.Verfuegbar} verfügbar, {e.Angefordert} angefordert")],
            Belegart.Verteilung => [new VermerkeQuellabsage(new(e.Beleg.Nummer), e.Verfuegbar)],
            _ => [],
        }))
        // Messung, Import: die Nachricht der Domäne, die eine Übersetzung in den Kopf des Graphen im Katalog macht
        .Auf<MesswertErfasst>(Gib<MesswertErfasst>((e, strom) => [new NeuerWert(new(strom), e.Wert)]))
        .Auf<ImportGestartet>(Gib<ImportGestartet>((e, strom) => [new ImportZuLaufen(new(strom), e.Zeilen)]));

    private static Beleg Auftrag(Guid nummer) => new(Belegart.Auftrag, nummer);
    private static Beleg Verteilung(Guid nummer) => new(Belegart.Verteilung, nummer);

    /// <summary>Je Fakt die Commands (in Reihenfolge — die Stelle geht in ihre Id ein); der Stream des Fakts ist die Id seines Aggregats.</summary>
    private static Func<TEvent, ReaktionsKontext, Task> Gib<TEvent>(Func<TEvent, Guid, IEnumerable<ICommand>> aus) => (e, k) =>
    {
        foreach (var c in aus(e, k.Ereignis.AggregateId)) k.Sende(c);
        return Task.CompletedTask;
    };

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
