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
/// Graphen, von denen er bekommt — je genannte Quelle die Eingänge, deren Typ sie ausgibt. Jedes Argument ist <c>nameof</c> eines anderen Parameters
/// (nie ein String-Literal). An einem Graphen im Graphen speist es seine Köpfe; <c>Von(nameof(Innerer))</c> meint die Ausgänge des inneren Graphen.
/// Eine Kante ist GESICHERT: der Absender wartet auf das „fertig“ des Empfängers, und erst wenn die ganze Kette fertig ist, ist der Kopf erledigt.
/// <c>[Von(nameof(A), Verlierbar = true)]</c> macht die Kanten dieses Attributs VERLIERBAR: einmal geschickt, ohne Antwort, sie halten den Kopf nicht
/// auf. Verschiedene Zustellungen an einem Knoten stehen in eigenen <c>[Von]</c>.
/// <para>An einer Übersetzung, die einen Graphen im Graphen nennt, wählt <c>Innen</c> genau den Knoten darin, dessen Nachricht sie übersetzt (ein
/// ABGRIFF, auch ein Zwischenstand, der im Graphen weiterläuft): <c>[Von(nameof(Aufbereitung), Innen = nameof(Bildaufbereitung.Verkleinern))]</c>,
/// tiefer verschachtelt <c>nameof(A.B) + "." + nameof(B.C)</c> — jeder Name ein <c>nameof</c>, der Compiler prüft und benennt mit um. Ohne
/// <c>Innen</c> nimmt sie einen Ausgang des Graphen bzw. den einen inneren Knoten, der ihren Typ ausgibt.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class VonAttribute(params string[] knoten) : Attribute
{
    /// <summary>Die Knoten, von denen dieser Knoten bekommt (Parameternamen desselben Graphen).</summary>
    public string[] Knoten { get; } = knoten;

    /// <summary>Darf eine Nachricht auf diesen Kanten verloren gehen (einmal geschickt, ohne „fertig“)? Ohne Angabe: gesichert.</summary>
    public bool Verlierbar { get; set; }

    /// <summary>Der Knoten im genannten Graphen, aus dem eine Übersetzung abgreift (Pfad aus <c>nameof</c>, mit „.“ verbunden); sonst <c>null</c>.</summary>
    public string? Innen { get; set; }
}

/// <summary>
/// Ein KOPF (docs/04 §4.4): <c>[Kopf] F Knoten</c> nimmt von außerhalb des Graphen (aus einem Decide, von einem Akteur, vom Ausgang eines anderen
/// Graphen) — die Ports, die keine genannte Quelle speist. An einem Graphen im Graphen macht es seine Köpfe zu Köpfen des äußeren.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class KopfAttribute : Attribute { }

/// <summary>
/// Eine Kette eines Graphen ist gescheitert (docs/04 §4.4): ein Knoten warf auch nach seinen Wiederholungen, sein „gescheitert“ reichte über die
/// gesicherten Kanten bis zum Kopf. Der Kopf ist geparkt; der Hinweis geht an die Akteure, die ihn hören.
/// </summary>
/// <param name="Instanz">Die Instanz des Graphen.</param>
/// <param name="Lauf">Die Kennung des Kopfs (des Eingangs, der die Kette startete).</param>
/// <param name="Knoten">Der Knoten, der scheiterte (Pfad im Graphen).</param>
/// <param name="Grund">Die Meldung des letzten Fehlers.</param>
public sealed record LaufGescheitert(string Instanz, Guid Lauf, string Knoten, string Grund) : ITransientEvent;
