namespace Abstractions;

/// <summary>
/// Der NAME, über den der Framework-Generator eine Katalog-Funktion (<c>IFunktion</c>) aufruft: ihre eine Methode
/// <see cref="Methode"/>. Die EINE Quelle dieser Regel — der <c>FunktionsGenerator</c> (per Link eingebunden, netstandard2.0)
/// und der Domänen-Extractor lesen sie von hier, statt sie je für sich zu kennen (wie <see cref="Aggregatvertrag"/>).
/// </summary>
#if VERTRAG_INTERN
internal static class Funktionsvertrag   // als Link in einem Generator: intern, sonst kollidiert er mit Abstractions.Funktionsvertrag
#else
public static class Funktionsvertrag
#endif
{
    public const string Methode = "RufeAsync";

    // ── Python (Laufort Extern, konzept-werkbank §12.3 E13): die generierte Basis und wo die Implementierung liegt ──

    /// <summary>Die Methode, die eine Python-Implementierung füllt (<c>async def rufe(self, auftrag, x)</c>).</summary>
    public const string PythonMethode = "rufe";

    /// <summary>Der Ordner der Python-Implementierungen im Worker-Projekt (ein Paket, je Funktion eine Datei).</summary>
    public const string PythonOrdner = "funktionen";

    /// <summary><c>IBildVerkleinerung</c> → <c>BildVerkleinerung</c>: der Name ohne Schnittstellen-I (Basis = Name + „Basis“, Klasse = Name).</summary>
    public static string OhneI(string funktion) =>
        funktion.Length > 1 && funktion[0] == 'I' && char.IsUpper(funktion[1]) ? funktion.Substring(1) : funktion;

    /// <summary>Der Klassenname der generierten Python-Basis.</summary>
    public static string PythonBasis(string funktion) => OhneI(funktion) + "Basis";

    /// <summary>Der Modulname der Implementierung: <c>IBildVerkleinerung</c> → <c>bild_verkleinerung</c> (Datei <c>funktionen/bild_verkleinerung.py</c>).</summary>
    public static string PythonModul(string funktion)
    {
        var n = OhneI(funktion);
        var b = new System.Text.StringBuilder();
        for (var i = 0; i < n.Length; i++)
        {
            if (char.IsUpper(n[i]) && i > 0 && !char.IsUpper(n[i - 1])) b.Append('_');
            b.Append(char.ToLowerInvariant(n[i]));
        }
        return b.ToString();
    }
}
