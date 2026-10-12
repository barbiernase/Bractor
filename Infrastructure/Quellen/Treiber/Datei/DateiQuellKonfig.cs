namespace Infrastructure.Quellen.Treiber.Datei;

/// <summary>Wie ein Scan geweckt wird: nur per Intervall oder zusätzlich durch ein Dateisystem-Ereignis (das nur weckt, nie entscheidet).</summary>
public enum DateiErkennung { Abfrage, EreignisUndAbfrage }

/// <summary>Wann eine Datei als fertig gilt.</summary>
public enum DateiFertigArt { Stabil, Marker }

/// <summary>Wie die Kennung (und damit der Vorgang) einer Datei gebildet wird.</summary>
public enum DateiKennungArt { NameGroesse, PfadGroesseZeit }

/// <summary>Wo die Schleife läuft: genau einmal im Cluster (Lease, wandert bei Ausfall) oder auf jedem Knoten (lokale Verzeichnisse).</summary>
public enum QuellLaufmodus { EinmalImCluster, JederKnoten }

/// <summary>Fertig-Erkennung: <c>Stabil</c> = Größe und Änderungszeit über das Fenster unverändert; <c>Marker</c> = <c>&lt;datei&gt;&lt;Endung&gt;</c> existiert.</summary>
public sealed class DateiFertig
{
    public DateiFertigArt Art { get; set; } = DateiFertigArt.Stabil;
    public TimeSpan Fenster { get; set; } = TimeSpan.FromSeconds(1);
    public string Endung { get; set; } = ".done";
}

/// <summary>
/// Die Konfig einer Datei-Quelle (Abschnitt <c>Quellen:&lt;Typ&gt;</c> der Umgebung, docs/konzept-werkbank.md §3.4). Gebunden über den
/// Configuration-Binding-Source-Generator; unbekannte Felder sind ein Fehler. Die Treiber-Wahl steht NICHT hier — sie ist der Basistyp.
/// </summary>
public sealed class DateiQuellKonfig
{
    public string Pfad { get; set; } = "";
    /// <summary>Dateimuster (z. B. <c>*.tiff</c>); leer = alle Dateien.</summary>
    public string[] Muster { get; set; } = [];
    public bool Unterordner { get; set; }
    public DateiErkennung Erkennung { get; set; } = DateiErkennung.Abfrage;
    public TimeSpan Intervall { get; set; } = TimeSpan.FromSeconds(1);
    public DateiFertig Fertig { get; set; } = new();
    public DateiKennungArt Kennung { get; set; } = DateiKennungArt.PfadGroesseZeit;
    public QuellLaufmodus Laufmodus { get; set; } = QuellLaufmodus.EinmalImCluster;

    /// <summary>Die wirksamen Muster (leer → alle).</summary>
    public IReadOnlyList<string> WirksameMuster => Muster is { Length: > 0 } ? Muster : ["*"];
}
