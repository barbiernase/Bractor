namespace Abstractions;

/// <summary>
/// Marker eines GRAPHEN (docs/04 §4.4): <c>record X(Knoten…) : IGraph</c> — jeder Parameter ist ein Knoten (Name = Konfig-Schlüssel, Typ = eine
/// Funktion oder ein anderer Graph), die Reihenfolge bedeutet nichts. Jede innere Kante steht am nehmenden Knoten (<see cref="VonAttribute"/>), jeder
/// Kopf als <see cref="KopfAttribute"/> — je Eingangstyp genau einer; eine Funktion darf mehrfach im Graphen stehen. Was ein Knoten ausgibt und kein
/// Knoten über <c>Von</c> von ihm nimmt, verlässt den Graphen (an ein Aggregat, an den Kopf eines anderen Graphen, ein Hinweis). Ein Graph ist eine
/// Blaupause: er läuft als Instanz mit eigener Konfig (<c>umgebungen/&lt;name&gt;/graphen/&lt;instanz&gt;.json</c>), beliebig oft.
/// </summary>
public interface IGraph { }

/// <summary>
/// Die Kanten in einen Knoten (docs/04 §4.4): <c>[Von(nameof(A), nameof(B))] F Knoten</c> an einem Parameter eines Graphen nennt die Knoten desselben
/// Graphen, von denen er bekommt — je genannte Quelle die Ports, deren Typ sie ausgibt. Jedes Argument ist <c>nameof</c> eines anderen Parameters
/// (nie ein String-Literal). An einem Graphen im Graphen speist es seine Köpfe; <c>Von(nameof(Innerer))</c> meint die Ausgänge des inneren Graphen.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class VonAttribute(params string[] knoten) : Attribute
{
    /// <summary>Die Knoten, von denen dieser Knoten bekommt (Parameternamen desselben Graphen).</summary>
    public string[] Knoten { get; } = knoten;
}

/// <summary>
/// Ein KOPF (docs/04 §4.4): <c>[Kopf] F Knoten</c> nimmt von außerhalb des Graphen (aus einem Decide, von einem Akteur, vom Ausgang eines anderen
/// Graphen) — die Ports, die keine genannte Quelle speist. An einem Graphen im Graphen macht es seine Köpfe zu Köpfen des äußeren.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class KopfAttribute : Attribute { }

/// <summary>
/// Ein Lauf eines Graphen ist gescheitert (docs/04 §4.4, Entscheidung 11): ein Knoten warf auch nach seinen Wiederholungen. Der Kopf ist geparkt, die
/// offenen Zweige sind abgebrochen; der Hinweis geht an die Akteure, die ihn hören.
/// </summary>
/// <param name="Instanz">Die Instanz des Graphen.</param>
/// <param name="Lauf">Die Kennung des Kopfs (des Eingangs, der den Lauf startete).</param>
/// <param name="Knoten">Der Knoten, der scheiterte (Pfad im Graphen).</param>
/// <param name="Grund">Die Meldung des letzten Fehlers.</param>
public sealed record LaufGescheitert(string Instanz, Guid Lauf, string Knoten, string Grund) : ITransientEvent;
