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
        yield return new FuegeRangeHinzu(id, MapKriterien(_suche.Suche));
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
