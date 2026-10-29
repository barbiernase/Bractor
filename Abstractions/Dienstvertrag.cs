#nullable enable
namespace Abstractions;

/// <summary>
/// Die Regeln der Dienst-Bindung (docs/06 §6.5) als EINE Quelle: Marker, Konfig-Abschnitte und was ein Implementierungs-Konstruktor nehmen
/// darf. Der <c>DienstGenerator</c>, der Analyzer (CQRS089–CQRS092) und der Extractor lesen sie von hier (per Link, netstandard2.0).
/// </summary>
#if VERTRAG_INTERN
internal static class Dienstvertrag   // als Link in einem Generator: intern, sonst kollidiert er mit Abstractions.Dienstvertrag
#else
public static class Dienstvertrag
#endif
{
    /// <summary>Voller Name des Markers.</summary>
    public const string Marker = "Abstractions.IDienst";

    /// <summary>Abschnitt der Umgebung: je Dienst (einfacher Interface-Name) die gewählte Implementierung.</summary>
    public const string DiensteAbschnitt = "Dienste";

    /// <summary>Abschnitt der Umgebung: je Konfig-Record (einfacher Typname) seine Werte.</summary>
    public const string KonfigAbschnitt = "Konfig";

    /// <summary>Der eine Rahmen-Typ, den ein Implementierungs-Konstruktor außer Diensten und Konfig-Records nehmen darf.</summary>
    public const string Logger = "Microsoft.Extensions.Logging.ILogger`1";

    /// <summary>
    /// Feld-Typen eines Konfig-Records, die aus der Umgebung gebunden werden (voller Name; dazu jedes Enum und <c>string[]</c>).
    /// </summary>
    public static readonly string[] Feldtypen =
    {
        "System.String", "System.Boolean", "System.Int32", "System.Int64", "System.Double", "System.Decimal", "System.TimeSpan", "System.Guid",
    };

    /// <summary>Ist der volle Typname ein bindbarer Feld-Typ (ohne Enum/Array — die prüft der Aufrufer am Symbol)?</summary>
    public static bool IstFeldtyp(string vollerName) => System.Array.IndexOf(Feldtypen, vollerName) >= 0;
}
