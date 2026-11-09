using Abstractions;
using Pruefwerk.Katalog.Import;

namespace Pruefdomaene.Import;

// ÜBERSETZUNGEN (docs/plan-ohne-pipeline.md): Annahme und Zeilenlauf des Katalogs Pruefwerk werden Importaufträge — der Vorgang ist der Auftrag.

[Feld(nameof(Anfordern.Menge), nameof(ImportAngefordert.Zeilen))]
public sealed record ImportAnnehmen : IUebersetzung<ImportAngefordert, Anfordern>;

[Feld(nameof(StarteImport.AggregateId), nameof(Angenommen.Vorgang)), Feld(nameof(StarteImport.Zeilen), nameof(Angenommen.Menge))]
public sealed record ImportStarten : IUebersetzung<Angenommen, StarteImport>;

[Feld(nameof(Laufen.Vorgang), nameof(ImportZuLaufen.Import)), Feld(nameof(Laufen.Menge), nameof(ImportZuLaufen.Zeilen))]
public sealed record ImportLaufen : IUebersetzung<ImportZuLaufen, Laufen>;

[Feld(nameof(MeldeImportStand.AggregateId), nameof(Stand.Vorgang)), Feld(nameof(MeldeImportStand.Zeilen), nameof(Stand.Gelesen))]
public sealed record ImportstandMelden : IUebersetzung<Stand, MeldeImportStand>;
