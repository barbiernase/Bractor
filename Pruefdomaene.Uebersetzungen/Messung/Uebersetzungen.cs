using Abstractions;
using Pruefwerk.Katalog.Messung;
using Rechenwerk.Katalog.Skalierung;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Uebersetzungen.Messung;

// ÜBERSETZUNGEN in beide Richtungen (docs/04 §4.4): die Hochrechnung aus dem Katalog Rechenwerk wird domänenspezifisch — die Anforderung
// eines Clients am Kopf hinein, das Ergebnis heraus an die Messstelle.

/// <summary>Die Anforderung wird der Eingang des Skalierers: die Id der Messstelle als Bezug, der Faktor bleibt beim Standard.</summary>
[Feld(nameof(Skalieren.Bezug), nameof(HochrechnungAngefordert.Stelle))]
public sealed record Hinein : IUebersetzung<HochrechnungAngefordert, Skalieren>;

/// <summary>Das Ergebnis wird ein Ausreißer an die Messstelle: der Bezug wird wieder ihre Id.</summary>
[Feld(nameof(MeldeAusreisser.AggregateId), nameof(Skaliert.Bezug))]
public sealed record Melden : IUebersetzung<Skaliert, MeldeAusreisser>;

/// <summary>Ein neuer Wert wird der Eingang der Merkmalbildung: die Id der Messstelle als Stelle.</summary>
[Feld(nameof(Messwert.Stelle), nameof(NeuerWert.Stelle))]
public sealed record Messen : IUebersetzung<NeuerWert, Messwert>;

/// <summary>Ein Ausreißer der Merkmalbildung wird ein Ausreißer an die Messstelle.</summary>
[Feld(nameof(MeldeAusreisser.AggregateId), nameof(Ausreisser.Stelle))]
public sealed record AusreisserMelden : IUebersetzung<Ausreisser, MeldeAusreisser>;

/// <summary>Die Schätzung des Schätzers wird an der Messstelle vermerkt.</summary>
[Feld(nameof(VermerkeSchaetzung.AggregateId), nameof(Geschaetzt.Stelle))]
public sealed record SchaetzungVermerken : IUebersetzung<Geschaetzt, VermerkeSchaetzung>;
