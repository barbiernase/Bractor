using Abstractions;
using Pruefdomaene.Messung;
using Rechenwerk.Katalog.Skalierung;

namespace Pruefdomaene.Pipeline.Skalierung;

/// <summary>
/// Client → Übersetzung → Katalogfunktion → Übersetzung → Aggregat (docs/04 §4.4): die Anforderung wird in den Eingang des Katalogs übersetzt (die Id der
/// Messstelle als Bezug, der Faktor bleibt beim Standard), der Skalierer rechnet, das Ergebnis wird ein Command an die Messstelle (der Bezug wird wieder
/// ihre Id).
/// </summary>
public sealed record Hochrechnung(
    [Kopf, Feld(nameof(Skalieren.Bezug), nameof(HochrechnungAngefordert.Stelle))] Uebersetze<HochrechnungAngefordert, Skalieren> Hinein,
    [Von(nameof(Hinein))] Skalierer Rechnen,
    [Von(nameof(Rechnen)), Feld(nameof(MeldeAusreisser.AggregateId), nameof(Skaliert.Bezug))] Uebersetze<Skaliert, MeldeAusreisser> Melden) : IGraph;
