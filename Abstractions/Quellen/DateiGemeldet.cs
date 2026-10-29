using System;

namespace Abstractions;

/// <summary>
/// Was jeder DATEI-Treiber meldet (docs/04 §4.7): eine fertig geschriebene Datei. Der Treiber befüllt alle Felder —
/// eine Domäne benennt ihre Quelle nur als Typ: <c>public sealed record DateiErkannt : DateiGemeldet;</c> (keine eigenen Member,
/// CQRS070). Die Treiber-Wahl ist damit ein Code-Fakt (der Basistyp), die Werte (Pfad, Muster, Fertig-Erkennung …) sind Konfig der
/// Umgebung (<c>Quellen:&lt;Typ&gt;</c>).
/// </summary>
public abstract record DateiGemeldet : IQuellNachricht
{
    /// <summary>Dateiname ohne Verzeichnis.</summary>
    public required string Name { get; init; }

    /// <summary>Absoluter Pfad, wie der Treiber-Host ihn sieht.</summary>
    public required string Pfad { get; init; }

    /// <summary>Pfad relativ zum konfigurierten Verzeichnis (für Ausführer, die das Verzeichnis anders gemountet sehen).</summary>
    public required string RelativerPfad { get; init; }

    /// <summary>Größe in Bytes, als die Datei fertig war.</summary>
    public required long Groesse { get; init; }

    /// <summary>Letzte Änderung laut Dateisystem.</summary>
    public required DateTimeOffset GeaendertAm { get; init; }

    /// <summary>Wann der Treiber die Datei als fertig erkannt hat.</summary>
    public required DateTimeOffset ErkanntAm { get; init; }

    /// <summary>Vom Treiber nach Konfig gebildet (<c>NameGroesse</c> oder <c>PfadGroesseZeit</c>) — dieselbe Datei startet genau einen Vorgang.</summary>
    public required string Kennung { get; init; }
}
