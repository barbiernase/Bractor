namespace Abstractions;

/// <summary>
/// Eine ÜBERSETZUNG (docs/04 §4.4, docs/plan-ohne-pipeline.md): sie macht einen domänenneutralen Graphen aus einem Katalog domänenspezifisch und lebt
/// darum in der Domäne, als eigener Record: <c>public sealed record Aufnehmen : IUebersetzung&lt;BildGedeutet, NimmBildAuf&gt;;</c>. Aus einer Nachricht
/// <typeparamref name="TEin"/> macht sie genau eine Nachricht <typeparamref name="TAus"/> — Feld für Feld, ohne Bedingung, ohne Logik. Die Felder ordnet
/// der Generator zu: je Parameter von <typeparamref name="TAus"/> ein <see cref="FeldAttribute"/> am Record, sonst eine Eigenschaft von
/// <typeparamref name="TEin"/> mit gleichem Namen und implizit zuweisbarem Typ, sonst der Standardwert des Parameters (CQRS119).
/// <para>Wohin, sagen nur die Typen (Invariante 3): was ein laufender Graph ausgibt (Ausgang oder Zwischenstand) und <typeparamref name="TEin"/> ist, wird
/// im Kind des Knotens übersetzt; die Nachricht eines Clients beim Annehmen am Kopf; ein Event, mit dem ein Aggregat den Command einer Übersetzung
/// beantwortet, gleich nach dem Senden (eine Antwort-Übersetzung). <typeparamref name="TAus"/> geht an sein Aggregat bzw. an den Kopf genau eines
/// Graphen. Eine Übersetzung hat keinen Parent, keinen Zustand, keine Konfig und keinen Rechner.</para>
/// </summary>
public interface IUebersetzung<TEin, TAus> { }

/// <summary>
/// Eine Abweichung der Feldzuordnung an einer Übersetzung (<see cref="IUebersetzung{TEin, TAus}"/>, docs/04 §4.4): der Parameter <paramref name="ziel"/> der
/// erzeugten Nachricht bekommt die Eigenschaft <paramref name="quelle"/> der genommenen — beide als <c>nameof</c>. Ausdrücklich darf ein
/// <see cref="System.Guid"/> ein <c>Id&lt;T&gt;</c> füllen und umgekehrt; sonst muss der Typ implizit zuweisbar sein.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FeldAttribute(string ziel, string quelle) : Attribute
{
    /// <summary>Der Parameter der erzeugten Nachricht (<c>TAus</c>).</summary>
    public string Ziel { get; } = ziel;

    /// <summary>Die Eigenschaft der genommenen Nachricht (<c>TEin</c>).</summary>
    public string Quelle { get; } = quelle;
}
