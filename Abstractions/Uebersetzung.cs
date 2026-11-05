namespace Abstractions;

/// <summary>
/// Eine ÜBERSETZUNG (docs/04 §4.4): ein Knoten eines Graphen, der aus einer Nachricht <typeparamref name="TEin"/> (ein Command ohne Ziel) genau eine
/// Nachricht <typeparamref name="TAus"/> macht (ein Command ohne Ziel oder ein Command an ein Aggregat) — Feld für Feld, ohne Bedingung, ohne Logik. So
/// bleiben Katalog und Domäne voneinander isoliert; nur der Graph der Anwendung kennt beide. Die Felder ordnet der Generator zu: je Parameter von
/// <typeparamref name="TAus"/> ein <see cref="FeldAttribute"/> am Knoten, sonst eine Eigenschaft von <typeparamref name="TEin"/> mit gleichem Namen
/// und implizit zuweisbarem Typ, sonst der Standardwert des Parameters (CQRS119). Sie rechnet inline auf dem Actor, auf dem die Nachricht gerade ist:
/// hinter einer Funktion in deren Kind, am Kopf beim Annehmen — ohne Parent, Zustand, Konfig oder Rechner.
/// <code>
/// [Von(nameof(Trainieren)), Feld(nameof(VermerkeModell.Pfad), nameof(ModellTrainiert.ModellPfad))]
///     Uebersetze&lt;ModellTrainiert, VermerkeModell&gt; Ergebnis
/// </code>
/// Der Typ steht nur als Knoten in einem Graph-Record; er hat keine Instanzen.
/// </summary>
public sealed class Uebersetze<TEin, TAus>
{
    private Uebersetze() { }
}

/// <summary>
/// Eine Abweichung der Feldzuordnung an einer <see cref="Uebersetze{TEin, TAus}"/> (docs/04 §4.4): der Parameter <paramref name="ziel"/> der
/// erzeugten Nachricht bekommt die Eigenschaft <paramref name="quelle"/> der genommenen — beide als <c>nameof</c>. Ausdrücklich darf ein
/// <see cref="System.Guid"/> ein <c>Id&lt;T&gt;</c> füllen und umgekehrt; sonst muss der Typ implizit zuweisbar sein.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class FeldAttribute(string ziel, string quelle) : Attribute
{
    /// <summary>Der Parameter der erzeugten Nachricht (<c>TAus</c>).</summary>
    public string Ziel { get; } = ziel;

    /// <summary>Die Eigenschaft der genommenen Nachricht (<c>TEin</c>).</summary>
    public string Quelle { get; } = quelle;
}
