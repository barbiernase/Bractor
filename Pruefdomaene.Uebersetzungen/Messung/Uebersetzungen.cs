using Abstractions;
using Pruefwerk.Katalog;
using Rechenwerk.Katalog;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Uebersetzungen.Messung;

#pragma warning disable CS9113

// ÜBERSETZUNGEN in beide Richtungen (docs/04 §4.4): die Hochrechnung aus dem Katalog Rechenwerk wird domänenspezifisch — die Anforderung
// eines Clients am Kopf hinein, das Ergebnis heraus an die Messstelle.

/// <summary>Die Anforderung wird der Eingang des Skalierers: die Id der Messstelle als Bezug, der Faktor bleibt beim Standard.</summary>
public sealed class HochrechnungAngefordert_Skalieren_1(HochrechnungAngefordert source) : ITranslate<HochrechnungAngefordert, Skalieren>
{
    public Skalieren Translate() => new(Bezug: source.Stelle.Wert, Wert: source.Wert);
}

/// <summary>Das Ergebnis wird ein Ausreißer an die Messstelle: der Bezug wird wieder ihre Id.</summary>
public sealed class Skaliert_MeldeAusreisser_1(Skaliert source) : ITranslate<Skaliert, MeldeAusreisser>
{
    public MeldeAusreisser Translate() => new(AggregateId: new(source.Bezug), Wert: source.Wert);
}

/// <summary>Ein neuer Wert wird der Eingang der Merkmalbildung: die Id der Messstelle als Stelle.</summary>
public sealed class NeuerWert_Messwert_1(NeuerWert source) : ITranslate<NeuerWert, Messwert>
{
    public Messwert Translate() => new(Stelle: source.Stelle.Wert, Wert: source.Wert);
}

/// <summary>Ein Ausreißer der Merkmalbildung wird ein Ausreißer an die Messstelle.</summary>
public sealed class Ausreisser_MeldeAusreisser_1(Ausreisser source) : ITranslate<Ausreisser, MeldeAusreisser>
{
    public MeldeAusreisser Translate() => new(AggregateId: new(source.Stelle), Wert: source.Wert);
}

/// <summary>Die Schätzung des Schätzers wird an der Messstelle vermerkt.</summary>
public sealed class Geschaetzt_VermerkeSchaetzung_1(Geschaetzt source) : ITranslate<Geschaetzt, VermerkeSchaetzung>
{
    public VermerkeSchaetzung Translate() => new(AggregateId: new(source.Stelle), Erwartet: source.Erwartet, Anzahl: source.Anzahl);
}
