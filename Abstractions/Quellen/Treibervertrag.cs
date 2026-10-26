#nullable enable
namespace Abstractions;

/// <summary>
/// Die fest codierten Treiber (docs/konzept-werkbank.md §3): welcher Basistyp zu welchem Treiber gehört. Die EINE Quelle dieser
/// Zuordnung — der Quellen-Generator, der Analyzer (CQRS070) und der Extractor lesen sie von hier (per Link, netstandard2.0).
/// Ein Quellen-Typ wählt seinen Treiber nur über den Basistyp, nie über einen Namen aus der Konfig (Inv. 3).
/// </summary>
#if VERTRAG_INTERN
internal static class Treibervertrag   // als Link in einem Generator: intern, sonst kollidiert er mit Abstractions.Treibervertrag
#else
public static class Treibervertrag
#endif
{
    /// <summary>Voller Name der Basis des Datei-Treibers.</summary>
    public const string DateiBasis = "Abstractions.DateiGemeldet";

    /// <summary>Anzeigename des Datei-Treibers (Kopf der Quellkarte).</summary>
    public const string Datei = "Datei";

    /// <summary>Voller Name der Basis des Takt-Treibers (Plan Pub/Sub-Neustrukturierung Phase 3).</summary>
    public const string TaktBasis = "Abstractions.TaktGemeldet";

    /// <summary>Anzeigename des Takt-Treibers.</summary>
    public const string Takt = "Takt";

    /// <summary>Alle Treiber-Basen (voller Name) mit ihrem Anzeigenamen.</summary>
    public static readonly string[][] Basen = { new[] { DateiBasis, Datei }, new[] { TaktBasis, Takt } };

    /// <summary>Der Konfig-Abschnitt aller Quellen; darunter je Quellen-Typ (einfacher Typname) ein Abschnitt.</summary>
    public const string KonfigAbschnitt = "Quellen";

    /// <summary>Treiber-Art zu einem Basistyp (voller Name) oder null.</summary>
    public static string? TreiberVon(string basisVollerName) =>
        basisVollerName == DateiBasis ? Datei : basisVollerName == TaktBasis ? Takt : null;
}
