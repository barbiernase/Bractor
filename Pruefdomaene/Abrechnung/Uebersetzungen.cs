using Abstractions;
using Pruefwerk.Katalog.Abrechnung;

namespace Pruefdomaene.Abrechnung;

// ÜBERSETZUNGEN (docs/plan-ohne-pipeline.md): Freigabe und Mahnung des Katalogs Pruefwerk werden Rechnungen und Kundenkonten — der Vorgang ist die
// Rechnung, der Empfänger das Konto.

[Feld(nameof(Pruefe.Vorgang), nameof(Freizugeben.Rechnung))]
public sealed record ZurFreigabe : IUebersetzung<Freizugeben, Pruefe>;

[Feld(nameof(GibRechnungFrei.AggregateId), nameof(Freigegeben.Vorgang))]
public sealed record Freigeben : IUebersetzung<Freigegeben, GibRechnungFrei>;

[Feld(nameof(LegeRechnungZurPruefungVor.AggregateId), nameof(ZurPruefungVorzulegen.Vorgang))]
public sealed record ZurPruefung : IUebersetzung<ZurPruefungVorzulegen, LegeRechnungZurPruefungVor>;

[Feld(nameof(Mahne.Vorgang), nameof(Mahnfall.Rechnung)), Feld(nameof(Mahne.Empfaenger), nameof(Mahnfall.Konto)), Feld(nameof(Mahne.Stufe), nameof(Mahnfall.Mahnstufe))]
public sealed record ZurMahnung : IUebersetzung<Mahnfall, Mahne>;

[Feld(nameof(VermerkeMahnung.AggregateId), nameof(Gemahnt.Empfaenger)), Feld(nameof(VermerkeMahnung.Rechnung), nameof(Gemahnt.Vorgang))]
public sealed record MahnungVermerken : IUebersetzung<Gemahnt, VermerkeMahnung>;
