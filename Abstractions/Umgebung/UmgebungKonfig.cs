using System;
using System.Collections.Generic;

namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// UMGEBUNGEN (docs/konzept-werkbank.md §4): je Umgebung EINE Datei `umgebungen/umgebung.<name>.json`. Struktur ist Code (was
// existiert), Auswahl und Werte sind Konfig (was aktiv ist). Sim und Prod unterscheiden sich nur hier — nie im Code. Die Typen
// unten sind zugleich das Schema, aus dem die Werkbank ihr Formular zeichnet (generischer Formular-Renderer).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Art einer Umgebung: <c>Sim</c> (eigene, zurücksetzbare DB, Uhr verstellbar) oder <c>Prod</c>.</summary>
public enum UmgebungsArt { Sim, Prod }

/// <summary>Wer eine Ressource betreibt: <c>Aus</c> (nicht genutzt), <c>Verwaltet</c> (die Werkbank startet sie per Docker), <c>Vorhanden</c>.</summary>
public enum Betrieb { Aus, Verwaltet, Vorhanden }

/// <summary>Cluster-Mitgliedschaft: ein einzelner Knoten (in-memory) oder Consul (mehrere Knoten).</summary>
public enum ClusterArt { Einzelknoten, Consul }

/// <summary>Rolle eines Knotens beim Schema (Multi-Node-Kaltstart): allein, der eine Migrator, oder Mitglied.</summary>
public enum KnotenRolle { Allein, Migrator, Mitglied }

/// <summary>Wo eine Katalog-Funktion rechnet: im Host (C#-Implementierung), extern (z. B. Python-Worker) oder per Hand (Werkbank).</summary>
public enum FunktionsLaufort { Host, Extern, Hand }

/// <summary>Kopf der Umgebung.</summary>
public sealed class UmgebungKopf
{
    public string Name { get; set; } = "";
    public UmgebungsArt Art { get; set; } = UmgebungsArt.Sim;
}

/// <summary>Der Event-Store (Postgres/Marten) — die Wahrheit.</summary>
public sealed class EventStoreKonfig
{
    public Betrieb Betrieb { get; set; } = Betrieb.Verwaltet;
    /// <summary>Verbindung OHNE Datenbank (Host, Port, Benutzer, Passwort — Geheimnisse als <c>${env:NAME}</c>).</summary>
    public string Verbindung { get; set; } = "Host=localhost;Port=5432;Username=postgres;Password=postgres";
    /// <summary>Datenbankname; <c>${branch}</c> wird durch den Git-Branch ersetzt (eine Sim-DB je Branch).</summary>
    public string Datenbank { get; set; } = "cqrs_events";
    public string Schema { get; set; } = "es";
    public int BefehlsTimeoutSekunden { get; set; } = 30;
}

/// <summary>Der Version-Index (Redis, nicht autoritativ).</summary>
public sealed class VersionIndexKonfig
{
    public Betrieb Betrieb { get; set; } = Betrieb.Verwaltet;
    public string Endpunkt { get; set; } = "localhost:6379";
    public int Datenbank { get; set; } = 1;
    /// <summary>Schlüssel-Präfix — zwei Umgebungen auf einem Redis dürfen sich nie mischen.</summary>
    public string Praefix { get; set; } = "";
}

/// <summary>Der Cluster (Proto.Actor).</summary>
public sealed class ClusterKonfig
{
    public ClusterArt Art { get; set; } = ClusterArt.Einzelknoten;
    /// <summary>Cluster-Name — ein Sim-Knoten darf nie dem Prod-Cluster beitreten (Siegel prüft ihn mit).</summary>
    public string Name { get; set; } = "cqrs";
    public string Consul { get; set; } = "localhost:8500";
    public string AdvertisedHost { get; set; } = "localhost";
    public KnotenRolle Rolle { get; set; } = KnotenRolle.Allein;
}

/// <summary>Der Schreibpfad (Tuning, nie Semantik).</summary>
public sealed class SchreibpfadKonfig
{
    public bool GeneriertesJson { get; set; }
    public bool GroupCommit { get; set; } = true;
    public int BatchGroesse { get; set; } = 256;
    public int LingerMs { get; set; }
    public int Drain { get; set; } = 4;
    public int SnapshotSchwelle { get; set; } = 200;
    public int InboxGrenze { get; set; } = 10_000;
}

/// <summary>Ports: gRPC (HTTP/2) für Clients/Worker, Einblick (HTTP/1.1 + 2) für Werkbank, Webhook und /health; 0 = aus.</summary>
public sealed class TransportKonfig
{
    public int GrpcPort { get; set; } = 5001;
    public int EinblickPort { get; set; }
}

/// <summary>Laufort einer Katalog-Funktion (Abschnitt <c>Funktionen:&lt;IFunktion&gt;</c>).</summary>
public sealed class FunktionsKonfig
{
    public FunktionsLaufort Laufort { get; set; } = FunktionsLaufort.Host;
    /// <summary>Nur Host: der Klassenname der Implementierung — nötig, wenn es mehrere gibt.</summary>
    public string? Implementierung { get; set; }
    public int Slots { get; set; } = 1;
    public int Wiederholungen { get; set; }
}

/// <summary>Die gebundene Umgebung (ohne Quellen, Identitäten, Konfig-Records — die binden ihre eigenen Abschnitte).</summary>
public sealed class UmgebungKonfig
{
    public UmgebungKopf Umgebung { get; set; } = new();
    public EventStoreKonfig EventStore { get; set; } = new();
    public VersionIndexKonfig VersionIndex { get; set; } = new();
    public ClusterKonfig Cluster { get; set; } = new();
    public SchreibpfadKonfig Schreibpfad { get; set; } = new();
    public TransportKonfig Transport { get; set; } = new();
    public Dictionary<string, FunktionsKonfig> Funktionen { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Die Abschnitte einer Umgebungs-Datei — ein anderer Abschnitt ist ein Start-Fehler.</summary>
public static class UmgebungsAbschnitte
{
    public const string Umgebung = "Umgebung", EventStore = "EventStore", VersionIndex = "VersionIndex", Cluster = "Cluster",
        Schreibpfad = "Schreibpfad", Transport = "Transport", Identitaeten = "Identitaeten", Quellen = "Quellen",
        Funktionen = "Funktionen", Konfig = "Konfig", Logging = "Logging";

    public static readonly IReadOnlyList<string> Alle =
        [Umgebung, EventStore, VersionIndex, Cluster, Schreibpfad, Transport, Identitaeten, Quellen, Funktionen, Konfig, Logging];
}
