namespace DomainEditor;

/// <summary>
/// Die Store-Klassen des Frameworks als Daten für den Scaffolder (docs/04 §4.3, docs/10 R14): je Klasse die Basis aus <c>Core</c>, mit der eine
/// NEUE Store-Umsetzung entsteht, und ihr Konstruktor. Der Editor kennt Core nicht als Referenz; dass die Namen stimmen, prüft
/// <c>StoreklassenTests</c> gegen die echten Typen.
/// </summary>
public static class Storeklassen
{
    /// <summary>Eine Basis: Name und Namespace, die Parameter des Konstruktors der Umsetzung, die Argumente an <c>base(…)</c> und die usings.</summary>
    /// <param name="Dokumente">Klasse 1: an <c>base</c> hängen die Dokument-Typen (<c>typeof(…)</c>) — eine neue Generation leert sie.</param>
    public sealed record Basis(int Klasse, string Name, string Namespace, IReadOnlyList<Parameter> Konstruktor, string BasisArgumente,
        IReadOnlyList<string> Usings, bool Dokumente);

    public const string Kern = "Core";

    /// <summary>Klasse 1 atomar (Marten), 2 wiederholungsfest (Datei-Ziel), 3 mindestens einmal (Datei-Senke).</summary>
    public static readonly IReadOnlyList<Basis> Alle =
    [
        new(1, "MartenStore", Kern, [new Parameter { Typ = "IDocumentStore", Name = "store" }], "store", [Kern, "Marten"], Dokumente: true),
        new(2, "DateiZiel", Kern, [new Parameter { Typ = "StoreOrte", Name = "orte" }], "orte.Verzeichnis(nameof({0}))", [Kern], Dokumente: false),
        new(3, "DateiSink", Kern, [new Parameter { Typ = "StoreOrte", Name = "orte" }], "orte.Verzeichnis(nameof({0}))", [Kern], Dokumente: false),
    ];

    public static Basis? Von(int klasse) => Alle.FirstOrDefault(b => b.Klasse == klasse);

    /// <summary>
    /// Die Read-Models, die ein Store schreibt, soweit der Code es sagt: im Store angelegt (<see cref="Record.AmStore"/>) oder Parameter einer
    /// seiner Schreib-Fähigkeiten. Was eine Umsetzung nur im Rumpf schreibt, steht nicht in der Signatur — das meldet die Laufzeit (MartenStore).
    /// </summary>
    public static IEnumerable<string> Dokumente(EditorModell m, Store st)
    {
        var parameter = st.Fns.Where(f => !f.Lesen).SelectMany(f => f.Parameter).Select(p => BoardLeseseite.Basisname(p.Typ)).ToHashSet(StringComparer.Ordinal);
        return m.Records.Where(r => r.Kind == RecordArt.ReadModel && (r.AmStore == st.Name || parameter.Contains(r.Name))).Select(r => r.Name);
    }

    /// <summary>Die Argumente an <c>base(…)</c> für die Umsetzung <paramref name="klasse"/> mit den Dokument-Typen <paramref name="dokumente"/>.</summary>
    public static string Argumente(Basis b, string klasse, IEnumerable<string> dokumente) =>
        string.Join(", ", new[] { string.Format(b.BasisArgumente, klasse) }.Concat(b.Dokumente ? dokumente.Select(d => $"typeof({d})") : []));
}
