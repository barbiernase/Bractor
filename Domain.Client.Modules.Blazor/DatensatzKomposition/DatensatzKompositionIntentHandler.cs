using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Datensaetze;
using Domain.Datensatz;

namespace Domain.Client.Modules.DatensatzKomposition;

/// <summary>
/// Übersetzt die View-Intents in Datensatz-Commands (Konzept §8.1 / Handoff §3.1). Die
/// GUI sendet nur die „Anstoß"-Commands — <c>FuegeRangeHinzu</c>/<c>FriereEin</c> —; das
/// zweiphasige Auflösen (Range → IDs, Freeze → Snapshot) macht der server-seitige Resolver.
///
/// Wiederverwendung des vorhandenen Suchfilters (Weg 1, Handoff §4): der große Paarliste-
/// <see cref="Store"/> hält die <see cref="SuchKriterien"/> als einzige Wahrheit; hier werden
/// sie 1:1 auf <see cref="RangeKriterien"/> gemappt.
/// </summary>
public partial class DatensatzKompositionIntentHandler
{
    private readonly DatensatzKompositionStore _korb;
    private readonly Store _suche;

    public DatensatzKompositionIntentHandler(DatensatzKompositionStore korb, Store suche)
    {
        _korb = korb;
        _suche = suche;
    }

    IEnumerable<object> Handle(ErstelleDatensatzIntent evt, MessageContext ctx)
    {
        var id = Guid.NewGuid();
        var name = string.IsNullOrWhiteSpace(evt.Name) ? "Neuer Datensatz" : evt.Name.Trim();
        yield return new ErstelleDatensatz(id, name);
        // Sofort aktiv wählen — der RefreshHandler lädt dann seinen (leeren) Kopf.
        yield return new DatensatzAusgewaehlt(id);
    }

    IEnumerable<object> Handle(RangeHinzufuegenIntent evt, MessageContext ctx)
    {
        if (_korb.AktuelleDatensatzId is not { } id) yield break;

        var basis = MapKriterien(_suche.Suche);
        var bereiche = _suche.AktiveBereiche;

        // Keine Baum-Auswahl → EINE Range wie bisher (Von/Bis aus Suche: bei Tag-Labeling gesetzt,
        // sonst offen = die unbegrenzte, lazy-paginierte Kandidatenmenge).
        if (bereiche.Count == 0)
        {
            yield return new FuegeRangeHinzu(id, basis);
            yield break;
        }

        // Baum-Auswahl aktiv → EXAKT die gewählten Bereiche aufnehmen: je Bereich eine Range,
        // deckungsgleich mit der Kandidaten-Anzeige (die je Bereich eine Query fährt und merged).
        // Das Aggregat vereinigt + dedupliziert die IDs; die Provenienz hält jeden Zeitschnitt
        // einzeln fest. Die feingranularen Label-/Inspektionsfilter kommen aus `basis`, nur die
        // Datumsgrenzen werden je Bereich überschrieben.
        foreach (var b in bereiche)
            yield return new FuegeRangeHinzu(id, basis with { Von = b.Von, Bis = b.Bis });
    }

    IEnumerable<object> Handle(PaarEntfernenIntent evt, MessageContext ctx)
    {
        if (_korb.AktuelleDatensatzId is not { } id) yield break;
        yield return new EntfernePaar(id, evt.ImagePairId);
    }

    IEnumerable<object> Handle(SplitSetzenIntent evt, MessageContext ctx)
    {
        if (_korb.AktuelleDatensatzId is not { } id) yield break;
        yield return new SetzeSplit(id, evt.Train, evt.Val, evt.Test, evt.Seed);
    }

    IEnumerable<object> Handle(FriereEinIntent evt, MessageContext ctx)
    {
        if (_korb.AktuelleDatensatzId is not { } id) yield break;
        yield return new FriereEin(id);
    }

    // SuchKriterien (Client-Filter) → RangeKriterien (Provenienz-Filter). Feldgleich bis auf
    // die nicht vom Suchpanel gesteuerten Felder (KiKlassifikation/MenschLabel/…): die bleiben
    // offen. NurNichtInspizierte: false == „keine Einschränkung" → null.
    private static RangeKriterien MapKriterien(SuchKriterien s) => new(
        Von: s.Von,
        Bis: s.Bis,
        KiKlassifikation: null,
        MenschLabel: null,
        ProduktLabel: s.ProduktLabel,
        NurKomplette: null,
        HatKiKlassifikation: null,
        HatMenschLabel: s.HatMenschLabel,
        NurNichtInspizierte: s.NurNichtInspizierte ? true : null);
}
