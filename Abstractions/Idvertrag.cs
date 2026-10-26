namespace Abstractions;

/// <summary>
/// Die NAMEN, an denen Generatoren und Werkzeuge eine typisierte Id (<c>Id&lt;TAggregat&gt;</c>) und ein Command-Ziel (<c>ICommand&lt;TAggregat&gt;</c>)
/// erkennen — die EINE Quelle dieser Regel (wie <see cref="Aggregatvertrag"/>): Proto, Nutzlast-Mapper, STJ-Kontexte, Python, Routing,
/// Client-Wiring und Extractor lesen sie von hier. Auf der Leitung ist eine Id ein UUID-String.
/// </summary>
#if VERTRAG_INTERN
internal static class Idvertrag   // als Link in einem Generator: intern, sonst kollidiert er mit Abstractions.Idvertrag
#else
public static class Idvertrag
#endif
{
    /// <summary>Metadaten-Name der typisierten Id (<c>Compilation.GetTypeByMetadataName</c>).</summary>
    public const string IdMetadatenName = "Abstractions.Id`1";

    /// <summary>Metadaten-Name des typisierten Command-Ziels.</summary>
    public const string CommandMetadatenName = "Abstractions.ICommand`1";

    /// <summary>Die Eigenschaft, die den Wert (die UUID) trägt.</summary>
    public const string Wert = "Wert";

    /// <summary>Die Eigenschaft eines Commands, die sein Ziel trägt.</summary>
    public const string CommandZiel = "AggregateId";

    /// <summary>Der geschlossene JSON-Konverter je Ziel-Typ (für die STJ-Kontexte).</summary>
    public static string Konverter(string zielTyp) => "global::Abstractions.IdJsonKonverter<global::" + OhneGlobal(zielTyp) + ">";

    private const string Praefix = "Abstractions.Id<";

    /// <summary>
    /// Ist <paramref name="anzeigeName"/> (Roslyns <c>ToDisplayString()</c> bzw. dieselbe Form aus dem Codegen, optional mit <c>global::</c> und
    /// <c>?</c>) eine typisierte Id? Dann der Ziel-Typ (voll qualifiziert, ohne <c>global::</c>) und ob sie nullable ist.
    /// </summary>
    public static bool IstId(string? anzeigeName, out string zielTyp, out bool nullable)
    {
        zielTyp = "";
        nullable = false;
        if (string.IsNullOrEmpty(anzeigeName)) return false;
        var n = OhneGlobal(anzeigeName!.Trim());
        if (n.EndsWith("?")) { nullable = true; n = n.Substring(0, n.Length - 1); }
        else if (n.StartsWith("System.Nullable<") && n.EndsWith(">")) { nullable = true; n = OhneGlobal(n.Substring(16, n.Length - 17)); }
        if (!n.StartsWith(Praefix) || !n.EndsWith(">")) return false;
        zielTyp = OhneGlobal(n.Substring(Praefix.Length, n.Length - Praefix.Length - 1));
        return zielTyp.Length > 0;
    }

    public static bool IstId(string? anzeigeName) => IstId(anzeigeName, out _, out _);

    /// <summary>Der C#-Typname einer Id auf <paramref name="zielTyp"/> (voll qualifiziert, mit <c>global::</c>).</summary>
    public static string IdTyp(string zielTyp) => "global::Abstractions.Id<global::" + OhneGlobal(zielTyp) + ">";

    private static string OhneGlobal(string n) => n.StartsWith("global::") ? n.Substring(8) : n;
}
