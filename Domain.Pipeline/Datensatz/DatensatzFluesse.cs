using Abstractions;
using Domain.Datensatz;
using Domain.Projections;    // ISearchImagePairs/IFindImagePair (Lese-Fähigkeiten der Funktionen)

namespace Domain.Pipeline.Datensatz;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DATENSATZ-RESOLVER als Pipeline-Flüsse (docs/konzept-editor-pipelines.md §14, Konzept Datensatz §3.2/§11, Variante A).
// Der reine Decider löst die zwei I/O-behafteten Halbschritte nicht selbst — sie sind Katalog-Funktionen mit LESE-Fähigkeit:
//
//   RangeAngefordert ──ƒ Range suchen (ISearchImagePairs) ──▶ NimmRangeAuf
//   EinfrierenAngefordert ──ƒ Mitglieder einfrieren (IFindImagePair) ──▶ SchliesseEinfrierenAb
//
// Die Id des Datensatzes kommt aus dem Strom der Quelle (angefordert.Strom()) — das Event muss sie nicht tragen. Mitgliedschaft +
// Split-Konfig kommen autoritativ aus dem Event (Aggregat-State, Invariante 1); nur der Label-Stand ist ein Snapshot des
// Read-Models — genau das meint „Label-Stand zum Einfrier-Zeitpunkt".
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Die Suche, die eine Range beschreibt (1:1 aus den Kriterien), durchpaginiert zu konkreten Paar-Ids auflösen.</summary>
public sealed record SucheRange(RangeKriterien Kriterien) : IAuftrag<IRangeSuche>;

/// <summary>Die Range traf Paare: ihre Ids und die Herkunft (Kriterien + Anzahl) für die Nachvollziehbarkeit.</summary>
public record RangeGefunden(IReadOnlyList<Guid> ImagePairIds, RangeHerkunft Herkunft) : IEvent;

/// <summary>Die Range traf kein Paar — nichts aufzunehmen.</summary>
public record RangeOhneTreffer(RangeKriterien Kriterien) : IEvent;

public interface IRangeSuche : IFunktion
{
    Task<OneOf<RangeGefunden, RangeOhneTreffer>> RufeAsync(SucheRange auftrag, IAusfuehrung x, ISearchImagePairs suche);
}

/// <summary>Je Mitglied den Label-Stand und die Bildpfade lesen und den Split deterministisch zuteilen.</summary>
public sealed record FriereMitgliederEin(IReadOnlyList<Guid> Mitglieder, SplitKonfig Split) : IAuftrag<IMitgliederEinfrieren>;

/// <summary>Der immutable Mitglieder-Snapshot (stabile Reihenfolge).</summary>
public record MitgliederEingefroren(IReadOnlyList<DatensatzMitglied> Mitglieder) : IEvent;

/// <summary>Kein Mitglied war im Read-Model auffindbar — das Einfrieren wird nicht abgeschlossen.</summary>
public record KeinMitgliedAuffindbar(int Angefragt) : IEvent;

public interface IMitgliederEinfrieren : IFunktion
{
    Task<OneOf<MitgliederEingefroren, KeinMitgliedAuffindbar>> RufeAsync(FriereMitgliederEin auftrag, IAusfuehrung x, IFindImagePair finde);
}

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

/// <summary>Einfrieren anfordern → Mitglieder schnappschießen → Einfrieren abschließen.</summary>
public sealed class DatensatzEinfrieren : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var angefordert = p.Auf<EinfrierenAngefordert>();
        var einfrieren = angefordert.Rufe<IMitgliederEinfrieren>(angefordert => new FriereMitgliederEin(angefordert.Mitglieder, angefordert.Split));
        var abschliessen = p.Alle(einfrieren.Bei<MitgliederEingefroren>(), angefordert.Strom()).Sende<SchliesseEinfrierenAb>((einfrieren, angefordertStrom) => new SchliesseEinfrierenAb(angefordertStrom.Id, einfrieren.Mitglieder));
    });
}
