using System;
using System.Collections.Generic;

namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// UMGEBUNGEN (docs/06 §6.5): je Umgebung EIN Ordner `umgebungen/<name>/` — `umgebung.json` und je Instanz eines Graphen
// `graphen/<instanz>.json`. Struktur ist Code (was
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
    /// <summary>
    /// Der Name dieser Maschine (docs/04 §4.4, z. B. <c>gpu-1</c>, gern <c>${env:CQRS_RECHNER}</c>): eine Instanz, die ihn als <c>Rechner</c> nennt,
    /// läuft nur hier. Ohne Namen bedient der Host nur Instanzen, die keinen Rechner nennen.
    /// </summary>
    public string? Rechner { get; set; }
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
    /// <summary>Der Takt je Partition in Sekunden — das Netz für verlorene Weckrufe (Standard 30).</summary>
    public int TaktSekunden { get; set; } = 30;
}

/// <summary>Ports: gRPC (HTTP/2) für Clients/Worker, Einblick (HTTP/1.1 + 2) für Werkbank, Webhook und /health; 0 = aus.</summary>
public sealed class TransportKonfig
{
    public int GrpcPort { get; set; } = 5001;
    public int EinblickPort { get; set; }
    /// <summary>JSON-Eingang auf dem Einblick-Port (<c>/eingang/*</c>): Commands/Queries als Akteur (Token → Akteur-Tor) und Antworten auf Arbeit
    /// von Hand — der Weg der Werkbank ins echte System. Sim an, Prod aus.</summary>
    public bool JsonEingang { get; set; }
}

/// <summary>
/// Ein Knoten einer Graph-Instanz (<c>umgebungen/&lt;name&gt;/graphen/&lt;instanz&gt;.json</c>, Schlüssel = Name des Knotens, docs/04 §4.4): wie seine
/// Funktion läuft — Parallel, Warteschlange, Vorwärmen, Wiederholungen, Zeitlimit —, welche Umsetzung jede Technik in ihrem Konstruktor hat (<see cref="Technik"/>, nötig bei mehreren) und die Felder ihrer Konfig-Records (<see cref="Werte"/>, flach, wie in der Datei).
/// </summary>
public sealed class KnotenKonfig
{
    /// <summary>Die Schlüssel eines Knotens, die keine Felder eines Konfig-Records sind.</summary>
    public static readonly IReadOnlyList<string> Rahmenfelder =
        [nameof(Wiederholungen), nameof(Parallel), nameof(Warteschlange), nameof(Vorwaermen), nameof(Zeitlimit), nameof(Technik)];

    /// <summary>So oft rechnet ein Parent einen scheiternden Eingang erneut, bevor er „gescheitert“ meldet; 0 = der Standard.</summary>
    public int Wiederholungen { get; set; }
    /// <summary>So viele Eingänge rechnet ein Knoten zugleich (Kinder seines Parents, docs/04 §4.4); 0 = der Standard. Jedes Kind hat seine eigene
    /// Aktivierung.</summary>
    public int Parallel { get; set; }
    /// <summary>So viele Eingänge wartet ein Parent höchstens (voll → „später“, der Rückstau reicht bis zum durablen Rand); 0 = der Standard.</summary>
    public int Warteschlange { get; set; }
    /// <summary>Alle Kinder schon beim Start anlegen (eine Umsetzung, die lange lädt).</summary>
    public bool Vorwaermen { get; set; }
    /// <summary>
    /// So lange darf eine Rechnung des Knotens dauern (danach gilt sie als gescheitert und wird wiederholt), und so lange wartet ein Auf mit
    /// <c>T?</c>-Eingängen auf sie, sobald alle übrigen da sind (danach feuert es mit null, docs/04 §4.4); <see cref="TimeSpan.Zero"/> = ohne.
    /// </summary>
    public TimeSpan Zeitlimit { get; set; }
    /// <summary>Je Technik im Konstruktor (einfacher Name der Schnittstelle) die gewählte Umsetzung (Klassenname) — nötig, wenn es mehrere gibt.</summary>
    public Dictionary<string, string> Technik { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Die Felder der Konfig-Records des Knotens (Schlüssel relativ zum Knoten, eine Liste als <c>Feld:0</c>, <c>Feld:1</c> …).</summary>
    public Dictionary<string, string?> Werte { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Eine Instanz eines Graphen (docs/04 §4.4): eine Datei <c>umgebungen/&lt;name&gt;/graphen/&lt;instanz&gt;.json</c> — der Graph (Name des Records), wie
/// viele Ketten sie zugleich rechnet (<see cref="Straenge"/>), wahlweise ihr Rechner und je Knoten seine Werte. Je Umgebung läuft ein Graph genau
/// einmal; den Akteur einer Kette bringt ihr Auslöser mit.
/// </summary>
public sealed class GraphInstanzKonfig
{
    /// <summary>Der Standard für <see cref="Straenge"/> — so viele Ketten, wie früher die Warteschlange am Kopf zuließ.</summary>
    public const int StandardStraenge = 10_000;

    public string Graph { get; set; } = "";
    /// <summary>
    /// So viele Ketten rechnet die Instanz zugleich, je Host, der sie hält (docs/04 §4.4); ist die Zahl erreicht, heißt ein neuer Kopf „später“ — der
    /// durable Rand hält ihn, eine Quelle wartet. 1 = eine Kette nach der anderen (je Host der Reihe nach); 0 = der Standard
    /// (<see cref="StandardStraenge"/>). <c>Parallel</c> am Knoten bleibt die Obergrenze seiner Kinder.
    /// </summary>
    public int Straenge { get; set; }
    /// <summary>Der Rechner, auf dem die Instanz läuft (sein <c>Cluster:Rechner</c>); ohne Angabe auf jedem Host. Pflicht mit Zustand im Cluster.</summary>
    public string? Rechner { get; set; }
    public Dictionary<string, KnotenKonfig> Knoten { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Ein Client als Prozess (Werkbank, docs/06 §6.5): <c>Python Skript Argumente…</c> oder ein .NET-Projekt (<see cref="Projekt"/>, <c>dotnet run</c>). Die
/// Werkbank setzt <c>CQRS_GRPC_HOST</c>, <c>CQRS_GRPC_PORT</c>
/// und <c>CQRS_AKTEUR_TOKEN</c> (aus <see cref="Token"/>) — der Client meldet sich damit am gRPC-Port an.
/// </summary>
public sealed class WorkerKonfig
{
    /// <summary>Der Interpreter dieses Workers (leer = der der Umgebung, <see cref="UmgebungKonfig.Python"/>).</summary>
    public string Python { get; set; } = "";
    /// <summary>Das Skript, relativ zur Solution.</summary>
    public string Skript { get; set; } = "";
    /// <summary>Statt eines Skripts: ein .NET-Projekt, relativ zur Solution (<c>Domain.Client.Worker</c>) — die Werkbank startet es mit <c>dotnet run --project</c>.</summary>
    public string Projekt { get; set; } = "";
    public string[] Argumente { get; set; } = [];
    /// <summary>Das Akteur-Token (Abschnitt <c>Identitaeten</c>), mit dem sich der Client anmeldet — es muss die Akteure verkörpern, deren Arbeit er tut.</summary>
    public string Token { get; set; } = "";
}

/// <summary>
/// Ein EINGANG eines Clients aus einer Quelle (docs/04 §4.7, Abschnitt <c>Clients:&lt;Name&gt;:Eingaenge:&lt;eingang&gt;</c>): welche Quellart läuft
/// (<see cref="Art"/>, Klassenname mit <c>IQuellart</c>), für welchen Akteur sie hineingibt (der Client verkörpert ihn; eine Übersetzung der
/// Akteur-Seite macht aus ihrer Meldung eine Nachricht, die er darf), auf welchem Rechner sie lauscht und ihre Werte — die Felder ihrer Konfig-Records,
/// flach wie am Knoten einer Instanz. Drei Verzeichnisse sind drei Eingänge. Struktur ist Code, Auswahl und Werte sind Konfig (W7).
/// </summary>
public sealed class EingangKonfig
{
    /// <summary>Die Schlüssel eines Eingangs, die keine Felder eines Konfig-Records sind.</summary>
    public static readonly IReadOnlyList<string> Rahmenfelder = [nameof(Art), nameof(Akteur), nameof(Rechner), nameof(Offen), nameof(Technik)];

    /// <summary>Der Standard für <see cref="Offen"/>.</summary>
    public const int StandardOffen = 10_000;

    /// <summary>Die Quellart (Klassenname, z. B. <c>Dateiwaechter</c>).</summary>
    public string Art { get; set; } = "";
    /// <summary>Der Akteur, für den der Eingang hineingibt (ein Akteur, den der Client verkörpert).</summary>
    public string Akteur { get; set; } = "";
    /// <summary>Der Rechner, auf dem die Quelle lauscht (sein <c>Cluster:Rechner</c>) — im Cluster Pflicht: eine Quelle lauscht genau einmal.</summary>
    public string? Rechner { get; set; }
    /// <summary>So viele hineingegebene Elemente dürfen offen sein (noch nicht fertig gerechnet), bevor die Quelle wartet; 0 = der Standard.</summary>
    public int Offen { get; set; }
    /// <summary>Je Technik im Konstruktor der Quellart die gewählte Umsetzung (nötig, wenn es mehrere gibt).</summary>
    public Dictionary<string, string> Technik { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Die Felder der Konfig-Records der Quellart (Schlüssel flach, eine Liste als <c>Feld:0</c>, <c>Feld:1</c> …).</summary>
    public Dictionary<string, string?> Werte { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Wie die Aktivierung der Quellart ihre Werte und ihre Technik-Wahl liest — dieselbe Form wie am Knoten einer Instanz.</summary>
    public KnotenKonfig AlsKnoten() => new() { Technik = Technik, Werte = Werte };
}

/// <summary>
/// Wie sich ein Client (<c>IClient</c>, Handshake-Name = Record-Name) in dieser Umgebung verbindet (Abschnitt <c>Clients:&lt;Name&gt;</c>): das
/// Akteur-Token (Abschnitt <c>Identitaeten</c>; sein Akteur-Satz muss Akteure des Clients verkörpern), die Adresse des gRPC-Servers (leer =
/// <c>http://localhost:&lt;Transport:GrpcPort&gt;</c>), die eigenen URLs (Blazor), die Verzeichnisse, aus denen er Dateien zeigen darf, und wie die
/// Werkbank ihn als Prozess startet (<see cref="Worker"/>, nur ein Python-Client).
/// </summary>
public sealed class ClientKonfig
{
    /// <summary>Wie die Werkbank den Client als Prozess startet (▶ Worker starten). Der Host liest es nie.</summary>
    public WorkerKonfig? Worker { get; set; }
    public string Token { get; set; } = "";
    public string Adresse { get; set; } = "";
    public string Urls { get; set; } = "http://localhost:5010";
    public string[] Dateien { get; set; } = [];
    /// <summary>Seine Eingänge aus Quellen (Name → Eingang): sie laufen im Host, je Eingang ein Läufer auf seinem Rechner.</summary>
    public Dictionary<string, EingangKonfig> Eingaenge { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Wo ein Store im Dateisystem liegt (Abschnitt <c>Stores:&lt;Store&gt;</c>, Klassenname des Stores; docs/04 §4.3). Ohne Eintrag
/// unter dem Temp-Verzeichnis je Datenbank und Schema des Logs.
/// </summary>
public sealed class StoreKonfig
{
    public string? Verzeichnis { get; set; }
}

/// <summary>Die gebundene Umgebung (ohne Identitäten — die binden ihren eigenen Abschnitt) mit den Instanzen ihrer Graphen.</summary>
public sealed class UmgebungKonfig
{
    public UmgebungKopf Umgebung { get; set; } = new();
    public EventStoreKonfig EventStore { get; set; } = new();
    public ClusterKonfig Cluster { get; set; } = new();
    public SchreibpfadKonfig Schreibpfad { get; set; } = new();
    public LebenszyklusKonfig Lebenszyklus { get; set; } = new();
    public KonsumKonfig Konsum { get; set; } = new();
    public TransportKonfig Transport { get; set; } = new();
    /// <summary>Die Instanzen der Graphen (Dateien im Ordner <c>graphen</c> der Umgebung): Name der Instanz (Dateiname) → Konfig.</summary>
    public Dictionary<string, GraphInstanzKonfig> Graphen { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ClientKonfig> Clients { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, StoreKonfig> Stores { get; set; } = new(StringComparer.Ordinal);

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
        Schreibpfad = "Schreibpfad", Lebenszyklus = "Lebenszyklus", Konsum = "Konsum", Transport = "Transport", Identitaeten = "Identitaeten",
        Clients = "Clients", Stores = "Stores", Logging = "Logging";

    /// <summary>Der Abschnitt, unter den der Lader die Instanz-Dateien hängt (<c>Graphen:&lt;instanz&gt;:…</c>) — er steht nie in <c>umgebung.json</c>.</summary>
    public const string Graphen = "Graphen";

    public static readonly IReadOnlyList<string> Alle =
        [Umgebung, EventStore, Cluster, Schreibpfad, Lebenszyklus, Konsum, Transport, Identitaeten, Clients, Stores, Logging];
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
