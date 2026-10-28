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

/// <summary>Wo eine Funktion rechnet: im Host (C#-Implementierung), extern (z. B. Python-Worker) oder per Hand (Werkbank).</summary>
public enum FunktionsLaufort { Host, Extern, Hand }

/// <summary>Kopf der Umgebung.</summary>
public sealed class UmgebungKopf
{
    public string Name { get; set; } = "";
    public UmgebungsArt Art { get; set; } = UmgebungsArt.Sim;
    /// <summary>
    /// Der Python-Interpreter dieser Umgebung (externe Worker, Python-Build der Werkbank) — einer mit betterproto und den Worker-Paketen.
    /// Leer = <c>CQRS_PYTHON</c> bzw. <c>python3</c> (<see cref="UmgebungKonfig.Python"/>).
    /// </summary>
    public string Python { get; set; } = "";
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
    /// <summary>Höchstens so viele wartende Commands entscheidet ein Aggregat gemeinsam und hängt sie in einem Append an; 1 = jeder einzeln.</summary>
    public int BuendelGroesse { get; set; } = 64;
    /// <summary>Aufbewahrung der Inbox (E2): so lange wirkt eine wiederholte CommandId garantiert nicht erneut (Exactly-once-Horizont).</summary>
    public int InboxHorizontTage { get; set; } = 30;
}

/// <summary>
/// Lebenszyklus der virtuellen Actors (Tuning, nie Semantik): nach so vielen Sekunden ohne Nachricht passiviert ein Aggregat-Actor — er stoppt,
/// der Cluster vergisst ihn, die nächste Nachricht aktiviert ihn frisch aus dem Log (bzw. Snapshot). 0 = nie. Batch-Leser und Funktions-Actors
/// passivieren nicht (es gibt nur Konsumenten × Partitionen davon, der Poll weckt sie ohnehin).
/// </summary>
public sealed class LebenszyklusKonfig
{
    public int AggregatLeerlaufSekunden { get; set; } = 600;
}

/// <summary>
/// Der Batch-Konsum der replaybaren Projektionen (Tuning, nie Semantik; docs/04 §4.1): jede Projektion liest in <see cref="Partitionen"/>
/// Partitionen (E4; ein Stream gehört genau einer, letztes UUID-Byte mod P), je Seite höchstens <see cref="BatchGroesse"/> Events (E11). Ein
/// Wechsel der Partitionen ist ein Neuaufbau der Leseseite.
/// </summary>
public sealed class KonsumKonfig
{
    public int Partitionen { get; set; } = 8;
    public int BatchGroesse { get; set; } = 500;
}

/// <summary>Ports: gRPC (HTTP/2) für Clients/Worker, Einblick (HTTP/1.1 + 2) für Werkbank, Webhook und /health; 0 = aus.</summary>
public sealed class TransportKonfig
{
    public int GrpcPort { get; set; } = 5001;
    public int EinblickPort { get; set; }
    /// <summary>JSON-Eingang auf dem Einblick-Port (<c>/eingang/*</c>): Commands/Queries als Akteur (Token → Akteur-Tor) und Antworten von
    /// Hand-Funktionen — der Weg der Werkbank ins echte System. Sim an, Prod aus.</summary>
    public bool JsonEingang { get; set; }
}

/// <summary>Laufort einer Funktion (Abschnitt <c>Funktionen:&lt;IFunktion&gt;</c>).</summary>
public sealed class FunktionsKonfig
{
    public FunktionsLaufort Laufort { get; set; } = FunktionsLaufort.Host;
    /// <summary>Nur Host: der Klassenname der Implementierung — nötig, wenn es mehrere gibt.</summary>
    public string? Implementierung { get; set; }
    public int Slots { get; set; } = 1;
    public int Wiederholungen { get; set; }
    /// <summary>Nur Extern: wie die Werkbank den Worker startet (▶ Worker starten). Der Host liest es nie.</summary>
    public WorkerKonfig? Worker { get; set; }
    /// <summary>Nur eine Funktion mit Zustand (<c>IZustand&lt;T&gt;</c>, Plan §2.9): wo und wie oft er gesichert wird. Ohne Eintrag: im Dokument,
    /// höchstens einmal je Sekunde.</summary>
    public ZustandKonfig? Zustand { get; set; }
}

/// <summary>Wo der Zustand einer Funktion liegt: im Dokument neben der Position (klein) oder als Datei in einem Verzeichnis (groß, Netzgewichte).</summary>
public enum ZustandsAblage { Dokument, Datei }

/// <summary>
/// Die Sicherung des Zustands einer Funktion (Abschnitt <c>Funktionen:&lt;F&gt;:Zustand</c>, Plan §2.9, A5): Ablage, Verzeichnis der Dateien, und wie oft
/// höchstens gesichert wird (Sekunden; beim Stoppen, Passivieren und Umziehen immer). Großer Zustand bremst bei kurzem Intervall den Durchsatz, ein
/// langes kostet nach einem Absturz Nachrechnen.
/// </summary>
public sealed class ZustandKonfig
{
    public ZustandsAblage Ablage { get; set; } = ZustandsAblage.Dokument;
    public string? Verzeichnis { get; set; }
    public double SichernAlleSekunden { get; set; } = 1;
}

/// <summary>
/// Ein externer Funktions-Worker als Prozess (Werkbank, konzept-werkbank §12.3 E10): <c>Python Skript Argumente…</c>. Die Werkbank setzt <c>CQRS_GRPC_HOST</c>,
/// <c>CQRS_GRPC_PORT</c> und <c>CQRS_AKTEUR_TOKEN</c> (aus <see cref="Token"/>) — der Worker meldet sich damit am gRPC-Port an.
/// </summary>
public sealed class WorkerKonfig
{
    /// <summary>Der Interpreter dieses Workers (leer = der der Umgebung, <see cref="UmgebungKonfig.Python"/>).</summary>
    public string Python { get; set; } = "";
    /// <summary>Das Skript, relativ zur Solution.</summary>
    public string Skript { get; set; } = "";
    public string[] Argumente { get; set; } = [];
    /// <summary>Das Akteur-Token (Abschnitt <c>Identitaeten</c>), mit dem sich der Worker anmeldet — sein Akteur braucht <c>IDarf&lt;F&gt;</c>.</summary>
    public string Token { get; set; } = "";
}

/// <summary>
/// Welche Implementierung ein Domänen-Dienst (<c>IDienst</c>) in dieser Umgebung hat (Abschnitt <c>Dienste:&lt;IDienst&gt;</c>, konzept-werkbank §12.3 E11). Ohne
/// Eintrag gilt die einzige Implementierung; gibt es mehrere, muss sie hier stehen.
/// </summary>
public sealed class DienstKonfig
{
    /// <summary>Der Klassenname der Implementierung.</summary>
    public string? Implementierung { get; set; }
}

/// <summary>
/// Wie sich ein Client (<c>IClientVertrag</c>, Handshake-Name) in dieser Umgebung verbindet (Abschnitt <c>Clients:&lt;Name&gt;</c>): das Akteur-Token
/// (Abschnitt <c>Identitaeten</c>; sein Akteur-Satz muss die Akteure des Vertrags verkörpern), die Adresse des gRPC-Servers (leer =
/// <c>http://localhost:&lt;Transport:GrpcPort&gt;</c>), die eigenen URLs (Blazor) und die Verzeichnisse, aus denen er Dateien zeigen darf.
/// </summary>
public sealed class ClientKonfig
{
    public string Token { get; set; } = "";
    public string Adresse { get; set; } = "";
    public string Urls { get; set; } = "http://localhost:5010";
    public string[] Dateien { get; set; } = [];
}

/// <summary>Die gebundene Umgebung (ohne Quellen, Identitäten, Konfig-Records — die binden ihre eigenen Abschnitte).</summary>
public sealed class UmgebungKonfig
{
    public UmgebungKopf Umgebung { get; set; } = new();
    public EventStoreKonfig EventStore { get; set; } = new();
    public ClusterKonfig Cluster { get; set; } = new();
    public SchreibpfadKonfig Schreibpfad { get; set; } = new();
    public LebenszyklusKonfig Lebenszyklus { get; set; } = new();
    public KonsumKonfig Konsum { get; set; } = new();
    public TransportKonfig Transport { get; set; } = new();
    public Dictionary<string, FunktionsKonfig> Funktionen { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, DienstKonfig> Dienste { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ClientKonfig> Clients { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Der Python-Interpreter — eine Regel für alle Aufrufer: der des Workers, sonst der der Umgebung (<c>Umgebung:Python</c>), sonst
    /// <c>CQRS_PYTHON</c>, sonst <c>python3</c> vom PATH.
    /// </summary>
    public string Python(WorkerKonfig? worker = null) =>
        worker?.Python is { Length: > 0 } w ? w
        : Umgebung.Python is { Length: > 0 } u ? u
        : Environment.GetEnvironmentVariable("CQRS_PYTHON") is { Length: > 0 } e ? e
        : "python3";

    /// <summary>Die gRPC-Adresse, unter der sich ein Client verbindet: seine eigene Angabe, sonst der lokale gRPC-Port der Umgebung.</summary>
    public string GrpcAdresse(ClientKonfig c) => c.Adresse is { Length: > 0 } a ? a : $"http://localhost:{Transport.GrpcPort}";
}

/// <summary>Die Abschnitte einer Umgebungs-Datei — ein anderer Abschnitt ist ein Start-Fehler.</summary>
public static class UmgebungsAbschnitte
{
    public const string Umgebung = "Umgebung", EventStore = "EventStore", Cluster = "Cluster",
        Schreibpfad = "Schreibpfad", Lebenszyklus = "Lebenszyklus", Konsum = "Konsum", Transport = "Transport", Identitaeten = "Identitaeten", Quellen = "Quellen",
        Funktionen = "Funktionen", Dienste = "Dienste", Clients = "Clients", Konfig = "Konfig", Logging = "Logging";

    public static readonly IReadOnlyList<string> Alle =
        [Umgebung, EventStore, Cluster, Schreibpfad, Lebenszyklus, Konsum, Transport, Identitaeten, Quellen, Funktionen, Dienste, Clients, Konfig, Logging];
}

/// <summary>
/// Abschnitt <c>Identitaeten:Tokens</c>: Token → Akteur(e). Ein Wert nennt einen Akteur oder mehrere (<c>"Inspekteur,KIOperator"</c>, auch mit
/// <c>+</c>), alternativ steht eine Liste. EINE Regel für den Host (Akteur-Tor) und die Werkbank (Konsole, Worker-Token).
/// </summary>
public static class AkteurTokens
{
    public static IReadOnlyList<string> Akteure(string wert)
        => wert.Split(',', '+').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
}
