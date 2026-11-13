using System;
using System.Collections.Generic;

namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// UMGEBUNGEN (docs/06 §6.5): je Umgebung EIN Ordner `umgebungen/<name>/` — `umgebung.json`, je Instanz eines Graphen
// `graphen/<instanz>.json` und je Instanz einer App `apps/<instanz>.json`. Struktur ist Code (was
// existiert), Auswahl und Werte sind Konfig (was aktiv ist). Sim und Prod unterscheiden sich nur hier — nie im Code. Die Typen
// unten sind zugleich das Schema, aus dem die Werkbank ihr Formular zeichnet (generischer Formular-Renderer).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Art einer Umgebung: <c>Sim</c> (eigene, zurücksetzbare DB, Uhr verstellbar) oder <c>Prod</c>.</summary>
public enum UmgebungsArt { Sim, Prod }

/// <summary>Wer eine Ressource betreibt: <c>Aus</c> (nicht genutzt), <c>Verwaltet</c> (die Werkbank startet sie per Docker), <c>Vorhanden</c>.</summary>
public enum Betrieb { Aus, Verwaltet, Vorhanden }

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

/// <summary>
/// Der Cluster (Proto.Actor): sein Name und die Consul-Adresse. Ob er ein Einzelknoten ist, folgt aus dem Backend (ein Rechner = Einzelknoten, mehrere
/// = Consul); unter welcher Adresse ein Knoten erreichbar ist, aus seinem Rechner im Pool (<see cref="UmgebungKonfig.Rechner"/>).
/// </summary>
public sealed class ClusterKonfig
{
    /// <summary>Cluster-Name — ein Sim-Knoten darf nie dem Prod-Cluster beitreten (Siegel prüft ihn mit).</summary>
    public string Name { get; set; } = "cqrs";
    public string Consul { get; set; } = "localhost:8500";
    /// <summary>Rolle dieses Prozesses beim Schema (Multi-Node-Kaltstart) — eine Angabe je Prozess (<c>Cluster__Rolle</c>), kein Ort.</summary>
    public KnotenRolle Rolle { get; set; } = KnotenRolle.Allein;
}

/// <summary>Ein Rechner des Pools (Abschnitt <c>Rechner:&lt;Name&gt;</c>, docs/06 §6.5): wie man ihn erreicht.</summary>
public sealed class RechnerKonfig
{
    /// <summary>IP oder Hostname, unter dem der Rechner erreichbar ist (ohne Schema und Port).</summary>
    public string Adresse { get; set; } = "";
}

/// <summary>
/// Das Backend (Abschnitt <c>Backend</c>, docs/06 §6.5): die Rechner des Pools, auf denen der Server-Host läuft. Aggregate und Projektionen verteilen sich
/// selbst über sie; was einen Ort braucht (eine Graph-Instanz mit Zustand oder GPU), nennt einen von ihnen.
/// </summary>
public sealed class BackendKonfig
{
    public string[] Rechner { get; set; } = [Rechnerregeln.Standard];
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
    /// <summary>Der Backend-Rechner, auf dem die Instanz läuft; ohne Angabe auf jedem Rechner des Backends. Pflicht mit Zustand im Cluster.</summary>
    public string? Rechner { get; set; }
    public Dictionary<string, KnotenKonfig> Knoten { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Wie die Werkbank eine App-Instanz als Prozess startet (Durchspielen, W6): ein Python-Skript (<see cref="Skript"/>) oder ein .NET-Projekt
/// (<see cref="Projekt"/>, <c>dotnet run</c>). Die Werkbank setzt <c>CQRS_UMGEBUNG</c>, <c>CQRS_APP</c> (die Instanz), <c>CQRS_RECHNER</c>,
/// <c>CQRS_GRPC_HOST</c>, <c>CQRS_GRPC_PORT</c> und <c>CQRS_AKTEUR_TOKEN</c> — die App meldet sich damit am gRPC-Port an. Ein Host liest es nie.
/// </summary>
public sealed class StartKonfig
{
    /// <summary>Der Interpreter (leer = der der Umgebung, <see cref="UmgebungKonfig.Python"/>).</summary>
    public string Python { get; set; } = "";
    /// <summary>Das Skript, relativ zur Solution.</summary>
    public string Skript { get; set; } = "";
    /// <summary>Statt eines Skripts: ein .NET-Projekt, relativ zur Solution — die Werkbank startet es mit <c>dotnet run --project</c>.</summary>
    public string Projekt { get; set; } = "";
    public string[] Argumente { get; set; } = [];
}

/// <summary>
/// Eine QUELLE einer App-Instanz (docs/04 §4.7; Schlüssel = Name des Parameters der App, <c>record KameraApp([Uebersetzt&lt;U&gt;] Dateiwaechter Kamera)</c>):
/// die Werte ihrer Quellart — die Felder ihrer Konfig-Records, flach wie am Knoten einer Instanz — und die Wahl ihrer Technik. Welche Quellart läuft
/// und welche Übersetzung ihre Meldung zur Nachricht macht, sagt der Code der App; hier stehen nur die Werte (W7). Sie läuft im Rechnerdienst des
/// Rechners der Instanz; einen Gegendruck gibt es nicht — der Läufer gibt ein Element hinein, wartet auf die Quittung und nimmt das nächste.
/// </summary>
public sealed class QuelleKonfig
{
    /// <summary>Die Schlüssel einer Quelle, die keine Felder eines Konfig-Records sind.</summary>
    public static readonly IReadOnlyList<string> Rahmenfelder = [nameof(Technik)];

    /// <summary>Je Technik im Konstruktor der Quellart die gewählte Umsetzung (nötig, wenn es mehrere gibt).</summary>
    public Dictionary<string, string> Technik { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Die Felder der Konfig-Records der Quellart (Schlüssel flach, eine Liste als <c>Feld:0</c>, <c>Feld:1</c> …).</summary>
    public Dictionary<string, string?> Werte { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Wie die Aktivierung der Quellart ihre Werte und ihre Technik-Wahl liest — dieselbe Form wie am Knoten einer Instanz.</summary>
    public KnotenKonfig AlsKnoten() => new() { Technik = Technik, Werte = Werte };
}

/// <summary>
/// Eine Instanz einer App (docs/10 E18, Datei <c>umgebungen/&lt;name&gt;/apps/&lt;instanz&gt;.json</c>): welche App (<c>IApp&lt;TClient&gt;</c>,
/// Name des Records) auf welchem Rechner des Pools läuft, unter welchem Port, mit welchem Akteur-Token, wie die Werkbank sie startet, die Verzeichnisse,
/// aus denen sie Dateien zeigen darf, und je Quelle der App ihre Werte. Eine App kann mehrfach laufen (zwei Linien-PCs), auf einem Rechner mehrere Apps
/// auf verschiedenen Ports. Struktur ist Code (die App), Auswahl und Werte sind Konfig.
/// </summary>
public sealed class AppInstanzKonfig
{
    /// <summary>Die Schlüssel einer Instanz (alles andere ist ein Fehler).</summary>
    public static readonly IReadOnlyList<string> Felder =
        [nameof(App), nameof(Rechner), nameof(Port), nameof(Token), nameof(Start), nameof(Dateien), nameof(Quellen)];

    /// <summary>Die App (Record-Name, <c>record X : IApp&lt;TClient&gt;</c>).</summary>
    public string App { get; set; } = "";
    /// <summary>Der Rechner des Pools, auf dem sie läuft; ohne Angabe <c>localhost</c>.</summary>
    public string Rechner { get; set; } = Rechnerregeln.Standard;
    /// <summary>Der Port, unter dem sie erreichbar ist (eine App mit Oberfläche); 0 = keiner. Je Rechner eindeutig.</summary>
    public int Port { get; set; }
    /// <summary>Das Akteur-Token (Abschnitt <c>Identitaeten</c>), mit dem sie sich anmeldet — es nennt Akteure ihres Clients.</summary>
    public string Token { get; set; } = "";
    /// <summary>Wie die Werkbank sie als Prozess startet (Durchspielen); ohne Angabe startet sie nicht aus der Werkbank.</summary>
    public StartKonfig? Start { get; set; }
    /// <summary>Die Verzeichnisse, aus denen sie Dateien zeigen darf (eine App mit Oberfläche).</summary>
    public string[] Dateien { get; set; } = [];
    /// <summary>Je Quelle der App (Name des Parameters) ihre Werte.</summary>
    public Dictionary<string, QuelleKonfig> Quellen { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Die URL, unter der sie auf ihrem Rechner hört (nur mit Port).</summary>
    public string? Url(UmgebungKonfig u) => Port > 0 ? $"http://{u.Adresse(Rechner)}:{Port}" : null;
}

/// <summary>Welche App-Instanz ein Prozess ist: <c>--app &lt;instanz&gt;</c> bzw. <c>CQRS_APP</c>; ohne Angabe die eine Instanz seiner App.</summary>
public static class AppInstanzwahl
{
    public const string Argument = "--app";
    public const string Variable = "CQRS_APP";

    /// <summary>Der Name aus den Argumenten oder der Variablen; null = keiner angegeben.</summary>
    public static string? NameAus(string[] args)
    {
        var i = Array.IndexOf(args, Argument);
        if (i >= 0 && i + 1 < args.Length) return args[i + 1];
        var g = args.FirstOrDefault(a => a.StartsWith(Argument + "=", StringComparison.Ordinal));
        return g?[(Argument.Length + 1)..] ?? (Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } v ? v : null);
    }

    /// <summary>
    /// Die Instanz der App <paramref name="app"/> dieses Prozesses: die genannte (sie muss diese App sein), sonst die einzige dieser App in der Umgebung.
    /// </summary>
    public static (string Name, AppInstanzKonfig Instanz) Waehle(UmgebungKonfig u, string app, string[] args)
    {
        if (NameAus(args) is { } name)
        {
            if (!u.Apps.TryGetValue(name, out var a))
                throw new InvalidOperationException($"Umgebung {u.Umgebung.Name}: eine App-Instanz {name} gibt es nicht (apps/{name}.json).");
            if (a.App != app)
                throw new InvalidOperationException($"Umgebung {u.Umgebung.Name}: die App-Instanz {name} ist {a.App}, dieser Prozess ist {app}.");
            return (name, a);
        }
        var von = u.Apps.Where(x => x.Value.App == app).ToList();
        return von.Count == 1 ? (von[0].Key, von[0].Value)
            : throw new InvalidOperationException($"Umgebung {u.Umgebung.Name}: {(von.Count == 0 ? "keine Instanz" : von.Count + " Instanzen")} von {app} — "
                + $"{Argument} <instanz> bzw. {Variable} nennt, welche dieser Prozess ist.");
    }
}

/// <summary>
/// Wo ein Store im Dateisystem liegt (Abschnitt <c>Stores:&lt;Store&gt;</c>, Klassenname des Stores; docs/04 §4.3). Ohne Eintrag
/// unter dem Temp-Verzeichnis je Datenbank und Schema des Logs.
/// </summary>
public sealed class StoreKonfig
{
    public string? Verzeichnis { get; set; }
}

/// <summary>Die gebundene Umgebung (ohne Identitäten — die binden ihren eigenen Abschnitt) mit den Instanzen ihrer Graphen und Apps.</summary>
public sealed class UmgebungKonfig
{
    public UmgebungKopf Umgebung { get; set; } = new();
    public EventStoreKonfig EventStore { get; set; } = new();
    public ClusterKonfig Cluster { get; set; } = new();
    public SchreibpfadKonfig Schreibpfad { get; set; } = new();
    public LebenszyklusKonfig Lebenszyklus { get; set; } = new();
    public KonsumKonfig Konsum { get; set; } = new();
    public TransportKonfig Transport { get; set; } = new();
    /// <summary>Der Pool (Abschnitt <c>Rechner</c>): Name → wie man ihn erreicht. Ohne Abschnitt nur <c>localhost</c>.</summary>
    public Dictionary<string, RechnerKonfig> Rechner { get; set; } = new(StringComparer.Ordinal)
    {
        [Rechnerregeln.Standard] = new() { Adresse = Rechnerregeln.Standard },
    };
    /// <summary>Welche Rechner des Pools das Backend bilden.</summary>
    public BackendKonfig Backend { get; set; } = new();
    /// <summary>
    /// Der Rechner dieses Prozesses (kein Abschnitt der Datei): <c>--rechner &lt;name&gt;</c> bzw. <c>CQRS_RECHNER</c>, sonst <c>localhost</c>
    /// (<see cref="Rechnerregeln.Standard"/>). Ein Prozess nimmt sich aus der Umgebung, was diesem Rechner zugewiesen ist.
    /// </summary>
    public string DieserRechner { get; set; } = Rechnerregeln.Standard;
    /// <summary>Die Instanzen der Graphen (Dateien im Ordner <c>graphen</c> der Umgebung): Name der Instanz (Dateiname) → Konfig.</summary>
    public Dictionary<string, GraphInstanzKonfig> Graphen { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Die Instanzen der Apps (Dateien im Ordner <c>apps</c> der Umgebung): Name der Instanz (Dateiname) → Konfig.</summary>
    public Dictionary<string, AppInstanzKonfig> Apps { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, StoreKonfig> Stores { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Der Python-Interpreter — eine Regel für alle Aufrufer: der des Starts einer App, sonst der der Umgebung (<c>Umgebung:Python</c>), sonst
    /// <c>CQRS_PYTHON</c>, sonst <c>python3</c> vom PATH.
    /// </summary>
    public string Python(StartKonfig? start = null) =>
        start?.Python is { Length: > 0 } w ? w
        : Umgebung.Python is { Length: > 0 } u ? u
        : Environment.GetEnvironmentVariable("CQRS_PYTHON") is { Length: > 0 } e ? e
        : "python3";

    /// <summary>Besteht das Backend aus einem Rechner? Dann läuft der Cluster als Einzelknoten (in-memory), sonst über Consul.</summary>
    public bool Einzelknoten => Backend.Rechner.Length <= 1;

    /// <summary>Die Adresse eines Rechners des Pools (sein Name, wenn er keine nennt).</summary>
    public string Adresse(string rechner) => Rechner.TryGetValue(rechner, out var r) && r.Adresse is { Length: > 0 } a ? a : rechner;

    /// <summary>Die gRPC-Adresse des Backends, unter der sich ein Client verbindet: sein erster Rechner und der gRPC-Port der Umgebung.</summary>
    public string GrpcAdresse() => $"http://{Adresse(Backend.Rechner.FirstOrDefault() ?? Rechnerregeln.Standard)}:{Transport.GrpcPort}";
}

/// <summary>Die Abschnitte einer Umgebungs-Datei — ein anderer Abschnitt ist ein Start-Fehler.</summary>
public static class UmgebungsAbschnitte
{
    public const string Umgebung = "Umgebung", EventStore = "EventStore", Cluster = "Cluster", Rechner = "Rechner", Backend = "Backend",
        Schreibpfad = "Schreibpfad", Lebenszyklus = "Lebenszyklus", Konsum = "Konsum", Transport = "Transport", Identitaeten = "Identitaeten",
        Stores = "Stores", Logging = "Logging";

    /// <summary>Der Abschnitt, unter den der Lader die Instanz-Dateien hängt (<c>Graphen:&lt;instanz&gt;:…</c>) — er steht nie in <c>umgebung.json</c>.</summary>
    public const string Graphen = "Graphen";

    /// <summary>Der Abschnitt, unter den der Lader die App-Instanzen hängt (<c>Apps:&lt;instanz&gt;:…</c>) — er steht nie in <c>umgebung.json</c>.</summary>
    public const string Apps = "Apps";

    public static readonly IReadOnlyList<string> Alle =
        [Umgebung, EventStore, Cluster, Rechner, Backend, Schreibpfad, Lebenszyklus, Konsum, Transport, Identitaeten, Stores, Logging];
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
