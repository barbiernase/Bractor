using System;
using System.Collections.Generic;

namespace Abstractions;

/// <summary>Wie ein Scan geweckt wird: nur per Intervall oder zusätzlich durch ein Dateisystem-Ereignis (das nur weckt, nie entscheidet).</summary>
public enum DateiErkennung { Abfrage, EreignisUndAbfrage }

/// <summary>Wann eine Datei als fertig gilt.</summary>
public enum DateiFertigArt { Stabil, Marker }

/// <summary>Wie die Kennung (und damit der Vorgang) einer Datei gebildet wird.</summary>
public enum DateiKennungArt { NameGroesse, PfadGroesseZeit }

/// <summary>Wo die Schleife läuft: genau einmal im Cluster (Lease, wandert bei Ausfall) oder auf jedem Knoten (lokale Verzeichnisse).</summary>
public enum QuellLaufmodus { EinmalImCluster, JederKnoten }

/// <summary>
/// In welchen Stream eine Quell-Nachricht geht (Plan Pub/Sub-Neustrukturierung §4.2): <c>JeNachricht</c> — ein Stream je Kennung, genau einmal über
/// Version 0; <c>JeSchluessel</c> — ein Stream je Schlüssel des Treibers (Datei: ihr Ordner; Takt: die Quelle), genau einmal über das
/// Kennungs-Verzeichnis. Weniger Streams für hochfrequente Quellen; ein Vorgang je Nachricht gibt es dann nicht mehr.
/// </summary>
public enum QuellStromArt { JeNachricht, JeSchluessel }

/// <summary>Fertig-Erkennung: <c>Stabil</c> = Größe und Änderungszeit über das Fenster unverändert; <c>Marker</c> = <c>&lt;datei&gt;&lt;Endung&gt;</c> existiert.</summary>
public sealed class DateiFertig
{
    public DateiFertigArt Art { get; set; } = DateiFertigArt.Stabil;
    public TimeSpan Fenster { get; set; } = TimeSpan.FromSeconds(1);
    public string Endung { get; set; } = ".done";
}

/// <summary>
/// Was jede Quellen-Konfig hat (Abschnitt <c>Quellen:&lt;Typ&gt;</c> der Umgebung), gleich welcher Treiber: wo die Schleife läuft und ob ein Webhook
/// ein zweiter Weg in dieselbe Quelle ist. Die Felder des Treibers stehen in seiner Konfig (<see cref="DateiQuellKonfig"/>, <see cref="TaktQuellKonfig"/>).
/// </summary>
public abstract class QuellKonfig
{
    public QuellLaufmodus Laufmodus { get; set; } = QuellLaufmodus.EinmalImCluster;
    /// <summary>Ein Stream je Nachricht (Standard) oder je Schlüssel des Treibers (<see cref="QuellStromArt"/>).</summary>
    public QuellStromArt Strom { get; set; } = QuellStromArt.JeNachricht;
    /// <summary>
    /// Ein zweiter Weg in dieselbe Quelle (konzept-werkbank §12.3 E12): <c>POST {Webhook}</c> auf dem Einblick-Port mit der Nachricht als JSON — genau einmal ins
    /// Log, Vorgang gestartet (dieselbe Nachricht zweimal → derselbe Vorgang). Leer = kein Webhook. Die Route ist Betrieb, nicht Domäne.
    /// </summary>
    public string? Webhook { get; set; }
}

/// <summary>
/// Die Konfig einer Datei-Quelle (Abschnitt <c>Quellen:&lt;Typ&gt;</c> der Umgebung, docs/konzept-werkbank.md §3.4). Gebunden über den
/// Configuration-Binding-Source-Generator; unbekannte Felder sind ein Fehler. Die Treiber-Wahl steht NICHT hier — sie ist der Basistyp.
/// </summary>
public sealed class DateiQuellKonfig : QuellKonfig
{
    public string Pfad { get; set; } = "";
    /// <summary>Dateimuster (z. B. <c>*.tiff</c>); leer = alle Dateien.</summary>
    public string[] Muster { get; set; } = [];
    public bool Unterordner { get; set; }
    public DateiErkennung Erkennung { get; set; } = DateiErkennung.Abfrage;
    public TimeSpan Intervall { get; set; } = TimeSpan.FromSeconds(1);
    public DateiFertig Fertig { get; set; } = new();
    public DateiKennungArt Kennung { get; set; } = DateiKennungArt.PfadGroesseZeit;

    /// <summary>Die wirksamen Muster (leer → alle).</summary>
    public IReadOnlyList<string> WirksameMuster => Muster is { Length: > 0 } ? Muster : ["*"];
}
