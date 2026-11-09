namespace Abstractions;

/// <summary>
/// Eine ÜBERSETZUNG (docs/04 §4.4): sie macht einen domänenneutralen Graphen aus einem Katalog domänenspezifisch. Eine kleine Klasse der Anwendung
/// (<c>&lt;Wurzel&gt;.Uebersetzungen</c>), die der Editor schreibt: ihr Konstruktor nimmt die Quelle <typeparamref name="TSource"/>,
/// <see cref="Translate"/> gibt das Ziel <typeparamref name="TTarget"/> zurück — Feld für Feld aus der Verdrahtung im Editor, ohne Bedingung, ohne
/// Logik; der Compiler prüft, dass das Ziel vollständig gebaut wird. Name: <c>{TSource}_{TTarget}_{Zähler}</c>.
/// <code>
/// public sealed class BildGedeutet_NimmBildAuf_1(BildGedeutet source) : ITranslate&lt;BildGedeutet, NimmBildAuf&gt;
/// {
///     public NimmBildAuf Translate() => new(AggregateId: new(source.Paar), PairKey: source.PairKey);
/// }
/// </code>
/// <para>Wohin, sagen nur die Typen (Invariante 3): was ein laufender Graph ausgibt (Ausgang oder Zwischenstand) und <typeparamref name="TSource"/> ist,
/// wird im Kind des Knotens übersetzt; die Nachricht eines Clients beim Annehmen am Kopf; ein Event, mit dem ein Aggregat den Command einer Übersetzung
/// beantwortet, gleich nach dem Senden (eine Antwort-Übersetzung). <typeparamref name="TTarget"/> geht an sein Aggregat bzw. an den Kopf genau eines
/// Graphen. Den Aufruf erzeugt der Generator (<c>GeneratedUebersetzungen</c>, reflexionsfrei).</para>
/// </summary>
public interface ITranslate<TSource, TTarget>
{
    /// <summary>Das Ziel aus den Feldern der Quelle, die der Konstruktor genommen hat.</summary>
    TTarget Translate();
}
