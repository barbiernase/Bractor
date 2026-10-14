using Abstractions;
using Domain.Datensatz;

namespace Domain.Pipeline.Datensatz;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DATENSATZ-RESOLVER als Pipeline-Flüsse (docs/konzept-editor-pipelines.md §14, Variante A).
// Der reine Decider löst die zwei I/O-behafteten Halbschritte nicht selbst — sie sind Katalog-Funktionen mit LESE-Fähigkeit:
//
//   RangeAngefordert ──ƒ Range suchen (ISearchImagePairs) ──▶ NimmRangeAuf
//   EinfrierenAngefordert ──ƒ Mitglieder einfrieren (IFindImagePair) ──▶ SchliesseEinfrierenAb
//
// Die Id des Datensatzes kommt aus dem Strom der Quelle (angefordert.Strom()) — das Event muss sie nicht tragen. Mitgliedschaft +
// Split-Konfig kommen autoritativ aus dem Event (Aggregat-State, Invariante 1); nur der Label-Stand ist ein Snapshot des
// Read-Models — genau das meint „Label-Stand zum Einfrier-Zeitpunkt".
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Range anfordern → suchen → aufnehmen.</summary>
public sealed class DatensatzRangeAufloesung : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var angefordert = p.Auf<RangeAngefordert>();
        var suche = angefordert.Rufe<IRangeSuche>(angefordert => new SucheRange(angefordert.Kriterien));
        var aufnehmen = p.Alle(suche.Bei<RangeGefunden>(), angefordert.Strom()).Sende<NimmRangeAuf>((suche, angefordertStrom) => new NimmRangeAuf(angefordertStrom.Id, suche.ImagePairIds, suche.Herkunft));
    });
}
