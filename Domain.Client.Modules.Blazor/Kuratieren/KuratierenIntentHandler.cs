using System.Linq;
using Client.Infrastructure.Abstractions;
using Domain.Datensatz;

namespace Domain.Client.Modules.Kuratieren;

/// <summary>
/// Übersetzt die Tag-Geste (Einbild-Taste A / Galerie-Klick) in das manuelle Delta des
/// aktiven Sammel-Ziels: <see cref="NimmPaarAuf"/> bzw. <see cref="EntfernePaar"/> — je
/// nach aktueller Mitgliedschaft. Wiederverwendung der vorhandenen Commands (Handoff §4.1):
/// keine neuen Commands, keine Handverdrahtung.
///
/// Muster identisch zum <c>LabelingIntentHandler</c>: View dispatcht einen client-lokalen
/// Intent, hier wird er cursor-/zielbewusst in einen Domänen-Command gemappt.
/// </summary>
public partial class KuratierenIntentHandler
{
    private readonly Store _store;
    public KuratierenIntentHandler(Store store) => _store = store;

    IEnumerable<object> Handle(DatensatzTagGetoggelt evt, MessageContext ctx)
    {
        if (_store.SammelZiel is not { } ziel) yield break;
        if (ziel.IstEingefroren) yield break;   // eingefroren = immutabel → kein Tag mehr

        var pairId = evt.PairId ?? _store.Cursor.Id;
        if (pairId is not { } id) yield break;

        if (ziel.MitgliederIds.Contains(id))
            yield return new EntfernePaar(ziel.Id, id);
        else
            yield return new NimmPaarAuf(ziel.Id, id);
    }

    // Dedizierte Mehrfach-Auswahl → EIN NimmRangeAuf (Herkunft „manuelle Auswahl"),
    // danach Markierung leeren. Wiederverwendung des Range-Aufnahme-Wegs (Handoff §4.1 /
    // Konzept §7): keine neuen Commands. Nur Nicht-Mitglieder aufnehmen (idempotent, aber
    // hält die Provenienz-Zahl ehrlich).
    IEnumerable<object> Handle(AuswahlAufgenommenAngefordert evt, MessageContext ctx)
    {
        if (_store.SammelZiel is not { } ziel) yield break;
        if (ziel.IstEingefroren) yield break;

        var neue = _store.Markierung.Where(id => !ziel.MitgliederIds.Contains(id)).ToList();
        if (neue.Count > 0)
            yield return new NimmRangeAuf(ziel.Id, neue, new RangeHerkunft(new RangeKriterien(), neue.Count));

        yield return new MarkierungGeleert();
    }
}
