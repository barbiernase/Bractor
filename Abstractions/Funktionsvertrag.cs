namespace Abstractions;

/// <summary>
/// Die Namen, über die Generatoren und Werkzeuge eine Funktion (<c>IFunktion</c>, docs/10 §10.3) erkennen und binden: ihre Methode <see cref="Auf"/>,
/// das Cluster-Kind ihres Actors, ihr Stand, ihre Python-Implementierung. Die EINE Quelle dieser Regeln — Generatoren (per Link eingebunden,
/// netstandard2.0) und Extractor lesen sie von hier, statt sie je für sich zu kennen (wie <see cref="Aggregatvertrag"/>).
/// </summary>
#if VERTRAG_INTERN
internal static class Funktionsvertrag   // als Link in einem Generator: intern, sonst kollidiert er mit Abstractions.Funktionsvertrag
#else
public static class Funktionsvertrag
#endif
{
    /// <summary>Die Methode einer Funktion in Baustein-Form: <c>Auf(TEingang e[, Herkunft h][, Lese-Fähigkeiten…][, CancellationToken])</c>.</summary>
    public const string Auf = "Auf";

    /// <summary>Der volle Name der <c>Herkunft</c> (optionaler Parameter eines <see cref="Auf"/>).</summary>
    public const string Herkunft = "Abstractions.Herkunft";

    /// <summary>Der volle Metadaten-Name des Zustands einer Funktion (<c>IZustand&lt;T&gt;</c> im Konstruktor, docs/04 §4.4).</summary>
    public const string Zustand = "Abstractions.IZustand`1";

    /// <summary>Der volle Name des optionalen Lebenszyklus einer Funktion im Host (docs/04 §4.4, docs/10 E10).</summary>
    public const string Lebenszyklus = "Abstractions.ILebenszyklus";

    /// <summary>Der volle Name des Attributs „eine Adresse statt einer je Partition“ (docs/04 §4.4, docs/10 A6).</summary>
    public const string EineAdresse = "Abstractions.EineAdresseAttribute";

    /// <summary>Die Identität des einen Funktions-Actors einer Funktion mit <see cref="EineAdresse"/> (er liest alle Partitionen).</summary>
    public const string EinzigeIdentitaet = "0";

    /// <summary>Das Cluster-Kind des Funktions-Actors einer Funktion (Identität = Partition bzw. <see cref="EinzigeIdentitaet"/>).</summary>
    public static string Kind(string funktion) => "funktion-" + funktion;

    /// <summary>Die Id des gesicherten Zustands einer Funktion an einer Adresse (Identität des Funktions-Actors).</summary>
    public static string ZustandId(string funktion, string identitaet) => "zustand:" + funktion + ":" + identitaet;

    /// <summary>Die Id des Stands einer Funktion (Position je Partition, wie bei Projektionen <c>&lt;Id&gt;:&lt;Partition&gt;</c>).</summary>
    public static string StandId(string funktion) => "funktion:" + funktion;

    // ── Python (Laufort Extern, docs/09 §9.5): die generierte Basis und wo die Implementierung liegt ──

    /// <summary>Der Ordner der Python-Implementierungen im Worker-Projekt (ein Paket, je Funktion eine Datei).</summary>
    public const string PythonOrdner = "funktionen";

    /// <summary><c>IBildaufbereitung</c> → <c>Bildaufbereitung</c>: der Name ohne Schnittstellen-I (Basis = Name + „Basis“, Klasse = Name).</summary>
    public static string OhneI(string funktion) =>
        funktion.Length > 1 && funktion[0] == 'I' && char.IsUpper(funktion[1]) ? funktion.Substring(1) : funktion;

    /// <summary>Der Klassenname der generierten Python-Basis.</summary>
    public static string PythonBasis(string funktion) => OhneI(funktion) + "Basis";

    /// <summary>Der Modulname der Implementierung: <c>IBildpaarBeurteilung</c> → <c>bildpaar_beurteilung</c> (Datei <c>funktionen/bildpaar_beurteilung.py</c>).</summary>
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
