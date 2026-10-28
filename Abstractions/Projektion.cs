namespace Abstractions;

/// <summary>
/// Eine Projektion (Plan Pub/Sub-Neustrukturierung §3.3): hört Events aus dem Log und schreibt ihren Store über Fähigkeiten — sonst nichts.
/// <code>
/// public sealed class Kontoauszug : IProjektion
/// {
///     public Task Auf(Abgebucht e, Herkunft h, ISchreibeBuchungszeile zeile) => zeile.SchreibeAsync(h.Id&lt;Konto&gt;(), e.Betrag, h);
/// }
/// </code>
/// Je gehörtem Event ein <c>Auf(TEvent e[, Herkunft h], Schreib-Fähigkeiten…[, CancellationToken])</c> mit Rückgabe <c>Task</c>. Die Kennung ist
/// der Typname (<see cref="Projektionsvertrag.Kennung"/>); Position, Ordnung, Wiederholung und Store-Transaktion trägt das Framework. Die
/// Schreib-Fähigkeiten gehören zu genau einem Store (R9); er implementiert genau eine Store-Klasse (<see cref="IProjektionsZiel"/>, R14).
/// Eine Projektion rechnet bei gleicher Eingabe gleich (R11): Zeit nur aus der <see cref="Herkunft"/>.
/// </summary>
public interface IProjektion { }

/// <summary>
/// Ein Reader (Plan §3.3): beantwortet Queries über Lese-Fähigkeiten.
/// <code>
/// public sealed class Kontoabfragen : IReader
/// {
///     public async Task&lt;OneOf&lt;KontoAntwort, KontoUnbekannt&gt;&gt; Frage(HoleKonto q, IFindeKonto finde) => …;
/// }
/// </code>
/// Was eine Antwort zeigt, sagen ihre <c>Id&lt;T&gt;</c>-Felder (und die der Query, nach der gefragt wurde); den Stand je gezeigter Id hängt das
/// Framework an (§2.5). Welche Projektionen hinter einer Frage stehen, folgt aus ihren Lese-Fähigkeiten: die Projektionen, die in denselben
/// Store schreiben.
/// </summary>
public interface IReader { }

/// <summary>
/// Die ausdrückliche Erlaubnis an einer Projektion, in einen Store der Klasse 3 (<see cref="INurMindestensEinmal"/>) zu schreiben (R14): ihre
/// Wirkungen gelten dann nur mindestens einmal — nach einem Abbruch können sie doppelt stehen (eine Metrik, ein Zähler zur Beobachtung).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MindestensEinmalAttribute : Attribute { }

/// <summary>
/// Der Code-Stand einer Projektion, geschrieben vom Generator in der Compilation der Projektion (nur dort ist ihr Quelltext sichtbar): ändert er
/// sich, baut eine Sim-Umgebung die Projektion beim Start neu auf (zusammen mit dem Code-Stand ihres Stores).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class ProjektionsstandAttribute : Attribute
{
    public ProjektionsstandAttribute(Type projektion, string hash)
    {
        Projektion = projektion;
        Hash = hash;
    }

    public Type Projektion { get; }
    public string Hash { get; }
}
