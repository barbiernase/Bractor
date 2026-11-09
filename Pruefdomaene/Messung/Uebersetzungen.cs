using Abstractions;
using Rechenwerk.Katalog.Skalierung;

namespace Pruefdomaene.Messung;

// ÜBERSETZUNGEN in beide Richtungen (docs/plan-ohne-pipeline.md): die Hochrechnung aus dem Katalog Rechenwerk wird domänenspezifisch — die Anforderung
// eines Clients am Kopf hinein, das Ergebnis heraus an die Messstelle.

/// <summary>Die Anforderung wird der Eingang des Skalierers: die Id der Messstelle als Bezug, der Faktor bleibt beim Standard.</summary>
[Feld(nameof(Skalieren.Bezug), nameof(HochrechnungAngefordert.Stelle))]
public sealed record Hinein : IUebersetzung<HochrechnungAngefordert, Skalieren>;

/// <summary>Das Ergebnis wird ein Ausreißer an die Messstelle: der Bezug wird wieder ihre Id.</summary>
[Feld(nameof(MeldeAusreisser.AggregateId), nameof(Skaliert.Bezug))]
public sealed record Melden : IUebersetzung<Skaliert, MeldeAusreisser>;
