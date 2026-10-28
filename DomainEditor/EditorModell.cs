using System.Text.Json;
using System.Text.Json.Serialization;

namespace DomainEditor;

/// <summary>
/// Das Domänen-Modell als AUSFÜHRBARE DATEN — RECORD-ZENTRISCH (v2).
///
/// Die Kern-Einsicht: Commands, Events, Ablehnungen und Value Objects sind ALLE Records
/// (Name + typisierte Felder). Deshalb gibt es EINEN uniformen Record-Editor statt bespoke
/// Formulare je Bausteinart. Ein Feldtyp darf ein anderer Record/Enum sein — so KOMPONIEREN
/// sich Records (z. B. <c>BildInfo</c> enthält <c>BildMeta</c> und <c>List&lt;RegionBewertung&gt;</c>).
///
/// Aus den getaggten Records KOMPONIERT man dann die Aggregate: State (Felder) + Decider
/// (Command → OneOf-Events) + Applier (Event → State). Der <see cref="Scaffolder"/> erzeugt daraus
/// deterministisch C# in kanonischer Gestalt (Commands.cs / Events.cs / ValueObjects.cs / Enums.cs
/// / {Aggregat}.cs / {Aggregat}.Decider.cs / {Aggregat}.Applier.cs, docs/07 §7.4).
/// </summary>
public sealed record EditorModell
{
    public string SchemaVersion { get; init; } = "2";

    /// <summary>Alle Records der Domäne — Commands, Events, Ablehnungen, Value Objects (kind-getaggt).</summary>
    public IReadOnlyList<Record> Records { get; init; } = [];
    /// <summary>Enums der Domäne (auch Feldtypen), z. B. <c>BildVersion { Dc0, Dc2 }</c>.</summary>
    public IReadOnlyList<Enumeration> Enums { get; init; } = [];
    /// <summary>Aggregate = Konsistenzgrenzen; halten den State und nehmen Decider/Applier entgegen.</summary>
    public IReadOnlyList<Aggregat> Aggregate { get; init; } = [];
    /// <summary>Decider (Command → OneOf-Events) — je Regel EIGENSTÄNDIG, referenziert sein Aggregat.</summary>
    public IReadOnlyList<DecideRegel> Decider { get; init; } = [];
    /// <summary>Applier (Event → State-Faltung) — je Regel EIGENSTÄNDIG, referenziert sein Aggregat.</summary>
    public IReadOnlyList<ApplyRegel> Applier { get; init; } = [];
    /// <summary>
    /// Domänen-Dienste (<c>interface IX : IDienst</c>, konzept-werkbank §12.3 E11): die Technik hinter einer Funktion — Signatur (Methoden) und Implementierungen
    /// (Klassen, die das Interface implementieren). Eine Funktion im Host nimmt sie im Konstruktor (<see cref="BausteinFunktion.Konstruktor"/>);
    /// welche Implementierung läuft, wählt die Umgebung (<c>Dienste:&lt;IX&gt;</c>).
    /// </summary>
    public IReadOnlyList<Dienst> Dienste { get; init; } = [];
    /// <summary>
    /// Funktionen (Plan Pub/Sub-Neustrukturierung §3.3): je Eingang ein <c>Auf</c> mit freier Ausgabe; als Klasse im Host, als
    /// Schnittstelle draußen. Nur gelesen — Gesten und Darstellung dafür kommen mit der Editor-Arbeit (Plan E4); der Scaffolder schreibt sie nicht.
    /// </summary>
    public IReadOnlyList<BausteinFunktion> BausteinFunktionen { get; init; } = [];
    /// <summary>
    /// Akteure (<c>docs/konzept-akteure.md</c>): wer von außen hineingibt — je Akteur die Typen, die er darf
    /// (<c>IDarf&lt;T&gt;</c>: Commands, Queries, Quell-Nachrichten, Hinweise, Funktionen, die er draußen rechnet). Was er hören darf, ist
    /// abgeleitet, nicht Modell.
    /// </summary>
    public IReadOnlyList<Akteur> Akteure { get; init; } = [];
    /// <summary>
    /// Clients (<c>docs/konzept-akteure.md</c> §4): die Software an der Leitung — je Client EIN Vertrag
    /// (<c>interface IX : IClientVertrag, …</c>): getragene Funktionen, Sendet/Fragt, Kenntnis (Hört). Welche Akteure er
    /// verkörpert, ist abgeleitet, nicht Modell.
    /// </summary>
    public IReadOnlyList<Client> Clients { get; init; } = [];
    /// <summary>
    /// Die LESESEITE als Signatur-Fakten: Stores (Fähigkeiten + Bündel + Impl-Klasse), Projektionen und Reader.
    /// ReadModels/Queries/Responses sind <see cref="Record"/>s (eigene Arten).
    /// Null = das Modell trägt keine Leseseite (z. B. Simulation) — der Scaffolder erzeugt dann keine Leseseiten-Dateien.
    /// </summary>
    public Leseseite? Lesen { get; init; }
    /// <summary>Der aus dem Code abgeleitete Rahmen (Vertrags-Namespace, globale usings, Namenskonvention, Verzeichnisse).</summary>
    public Rahmen Rahmen { get; init; } = new();

    // Serialisierung: camelCase, Enums als String, Nulls weglassen — wie knowledge-graph.json.
    public static readonly JsonSerializerOptions JsonOptionen = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string AlsJson() => JsonSerializer.Serialize(this, JsonOptionen);

    public static EditorModell AusJson(string json) =>
        JsonSerializer.Deserialize<EditorModell>(json, JsonOptionen)
        ?? throw new FormatException("Konnte EditorModell nicht aus JSON lesen (null).");
}

/// <summary>Die Rolle eines Records — bestimmt Interface und Zieldatei.</summary>
public static class RecordArt
{
    public const string Command = "command";        // : ICommand → Commands.cs
    public const string Event = "event";            // : IEvent → Events.cs (persistent)
    public const string Rejection = "rejection";    // : ITransientEvent → Events.cs (Ablehnung)
    public const string ValueObject = "valueobject";// reiner Record → ValueObjects.cs
    public const string Query = "query";            // : IQuery → Queries.cs
    public const string Antwort = "queryresponse";  // : IQueryResponse → Responses.cs
    public const string ReadModel = "readmodel";    // : IReadModel → ReadModels.cs
    public const string Konfig = "konfig";          // reiner Record, per Ctor in eine Funktion/Dienst-Implementierung injiziert → Konfigs.cs

    public static readonly IReadOnlyList<string> Alle = [Command, Event, Rejection, ValueObject, Query, Antwort, ReadModel, Konfig];
}

/// <summary>
/// Ein Record: Name + Kind (<see cref="RecordArt"/>) + typisierte Felder. Der uniforme Baustein für
/// Commands, Events, Ablehnungen und Value Objects. Felder dürfen andere Records/Enums referenzieren.
/// </summary>
public sealed record Record
{
    public required string Name { get; init; }
    /// <summary>Siehe <see cref="RecordArt"/>: command | event | rejection | valueobject.</summary>
    public required string Kind { get; init; }
    /// <summary>Voller Namespace, z. B. <c>Domain.ImagePair</c> — bestimmt die Zieldatei-Gruppierung.</summary>
    public required string Namespace { get; init; }
    public IReadOnlyList<Feld> Felder { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>Nur command: erzeugt das Aggregat (<c>ICreationCommand</c> statt nur <c>ICommand</c>).</summary>
    public bool IstErzeugung { get; init; }
    /// <summary>
    /// Nur readmodel: GETEILTES Dokument (<c>IGeteiltesReadModel</c>) — mehrere Streams schreiben hinein. Folge: Optimistic
    /// Concurrency (generiert) und Ändern nur über den konfliktgeprüften Weg der Store-Basis.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Geteilt { get; init; }
    /// <summary>
    /// Zusätzliche Member im Record-Rumpf als roher C#-Text (z. B. <c>static Default</c>, abgeleitete
    /// Props) — Handcode, den der Scaffolder verbatim in <c>{ … }</c> einhängt. Null ⇒ <c>record X(…);</c>.
    /// </summary>
    public string? Zusatz { get; init; }
    /// <summary>Zusätzliche <c>using</c>-Namespaces, die der Handcode (Zusatz) braucht (ohne <c>using</c>/<c>;</c>).</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];
    /// <summary>Die Quelldatei (relativ zur Solution), in der der Record steht — null = noch nicht geschrieben.</summary>
    public string? Datei { get; init; }
    /// <summary>
    /// Das Aggregat, zu dem der Record GEHÖRT — aus dem Code: beim Command das Ziel in seinem Typ (<c>ICommand&lt;TAggregat&gt;</c>); beim Event
    /// das Aggregat, das es als einziges erzeugt/faltet. Null = keinem (Value Object, geteilt, oder im Editor noch nicht verdrahtet).
    /// </summary>
    public string? Aggregat { get; init; }
    /// <summary>Die Deklarationsform verbatim ohne Namen, z. B. <c>public sealed record</c>, <c>public record struct</c>. Null = <c>public record</c>.</summary>
    public string? Typart { get; init; }
    /// <summary>Record ohne Parameterliste (<c>record X : I { … }</c> statt <c>record X(…) : I</c>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool OhneParameterliste { get; init; }
    /// <summary>Basistypen AUSSER dem Rollen-Marker, verbatim (z. B. weitere Interfaces).</summary>
    public IReadOnlyList<string>? Basen { get; init; }
    /// <summary>Attribut-Listen des Typs verbatim (mehrere zeilengetrennt).</summary>
    public string? Attribute { get; init; }
    /// <summary>
    /// Nur Quellen-Typen (docs/konzept-werkbank.md §3): der fest codierte Treiber, gewählt über den Basistyp (<c>Datei</c> für
    /// <c>DateiGemeldet</c>). Gesetzt ⇒ Event der Sorte „Quelle", geschrieben als <c>public sealed record X : DateiGemeldet;</c>.
    /// </summary>
    public string? Treiber { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>
/// Ein Feld = Name + Typ. Der Typ ist C#-Text: ein Skalar (<see cref="Skalar"/>), ein anderer
/// Record/Enum der Domäne (Komposition), ein Framework-Typ (<c>DateTimeOffset</c> …), nullable
/// (<c>Klassifikation?</c>) oder eine Collection (<c>List&lt;RegionBewertung&gt;</c>). Verbatim übernommen.
/// </summary>
public sealed record Feld
{
    public required string Name { get; init; }
    public required string Typ { get; init; }
    /// <summary>Optionaler Default (nur Command-Felder), z. B. <c>false</c> — roher C#-Literal.</summary>
    public string? Standard { get; init; }
    /// <summary>Nur State-Felder: gesetzt ⇒ abgeleitete Read-only-Property (<c>=> Ausdruck</c>), kein gespeichertes Feld.</summary>
    public string? Ausdruck { get; init; }
    /// <summary>Nur State-Felder: <c>{ get; }</c> statt <c>{ get; set; }</c> (z. B. mit <see cref="Standard"/> <c>new()</c>).</summary>
    public bool NurGet { get; init; }
    /// <summary>Nur Record-Felder: als Property deklariert, Accessor-Satz z. B. <c>{ get; init; }</c>. Null = Positions-Parameter.</summary>
    public string? Zugriff { get; init; }
    /// <summary>Property mit <c>required</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Pflicht { get; init; }
    /// <summary>Sammlungs-Feld: der Element-Typ (vom Extractor per Symbol bestimmt). Null = keine Sammlung bzw. noch nicht übersetzt.</summary>
    public string? ElementTyp { get; init; }
    /// <summary>Von der Treiber-Basis eines Quellen-Typs geerbt: zum Lesen und Zuordnen da, nie geschrieben.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Geerbt { get; init; }
}

/// <summary>Ein Enum-Typ der Domäne (→ <c>Enums.cs</c>).</summary>
public sealed record Enumeration
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public IReadOnlyList<string> Werte { get; init; } = [];
    public string? Doku { get; init; }
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>
/// Ein Aggregat = eine Konsistenzgrenze. Hält NUR den State (Felder). Decider und Applier sind
/// eigenständige Bausteine (<see cref="DecideRegel"/>/<see cref="ApplyRegel"/>), die IHR Aggregat per
/// Namen referenzieren — das Aggregat „nimmt sie entgegen".
/// </summary>
public sealed record Aggregat
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Doku { get; init; }
    /// <summary>State-Felder (mit optionalem <see cref="Feld.Ausdruck"/> für abgeleitete Props).</summary>
    public IReadOnlyList<Feld> State { get; init; } = [];
    /// <summary>Zusätzliche State-Member als roher C#-Text (Hilfsmethoden), in die Klasse eingehängt.</summary>
    public string? StateZusatz { get; init; }
    /// <summary>Private Hilfs-Member der Decider-Klasse (roher C#-Text), hinter den Decide-Methoden eingehängt.</summary>
    public string? DeciderZusatz { get; init; }
    /// <summary>Private Hilfs-Member der Applier-Klasse (roher C#-Text), hinter den Apply-Methoden eingehängt.</summary>
    public string? ApplierZusatz { get; init; }
    /// <summary>Zusätzliche <c>using</c>-Namespaces der Aggregat-Dateien (State/Decider/Applier), z. B. für Hilfstypen in Rümpfen.</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];
    /// <summary>Quelldateien (relativ zur Solution): State, Decider, Applier — null = noch nicht geschrieben.</summary>
    public string? Datei { get; init; }
    public string? DeciderDatei { get; init; }
    public string? ApplierDatei { get; init; }
    /// <summary>Die Fragen an das Aggregat (Plan Pub/Sub-Neustrukturierung §2.6) — Signatur-Fakt der <c>Frage</c> im Decider, nur gelesen; ihr
    /// Quelltext steht im <see cref="DeciderZusatz"/>.</summary>
    public IReadOnlyList<AggregatFrage> Fragen { get; init; } = [];
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>Eine Frage an ein Aggregat: die Query (Record-Name) und ihre möglichen Antworten (Record-Namen) aus der Signatur.</summary>
public sealed record AggregatFrage
{
    public string Query { get; init; } = "";
    public IReadOnlyList<string> Antworten { get; init; } = [];
}

/// <summary>
/// Ein Decider (eigenständig): entscheidet für sein <see cref="Aggregat"/> einen Command zu OneOf-
/// Events. Command → Decider → Events, und Decider → Aggregat.
/// </summary>
public sealed record DecideRegel
{
    /// <summary>Name des Aggregats, zu dem dieser Decider gehört.</summary>
    public required string Aggregat { get; init; }
    /// <summary>Name des Command-Records.</summary>
    public string Command { get; init; } = "";
    /// <summary>Die OneOf-Ausgänge (Event-/Ablehnungs-Record-Namen) in Reihenfolge.</summary>
    public IReadOnlyList<Ausgang> Ergibt { get; init; } = [];
    /// <summary>Optionaler Decide-Körper; null ⇒ kompilierbarer <c>throw</c>-Platzhalter, <c>""</c> ⇒ bewusst leer.</summary>
    public string? Rumpf { get; init; }
    /// <summary>Parametername des Commands in der Signatur (der Rumpf bezieht sich darauf).</summary>
    public string Parameter { get; init; } = "cmd";
    /// <summary>Die Quelldatei der Methode (relativ zur Solution) — Anker für Code-Sync/IDE; null = noch nicht geschrieben.</summary>
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>Ein OneOf-Ausgang: der Event-Record-Name (Signatur-Fakt; das „Wann" bleibt freier Rumpf-Code).</summary>
public sealed record Ausgang
{
    public required string Event { get; init; }
}

/// <summary>
/// Ein Applier (eigenständig): faltet für sein <see cref="Aggregat"/> einen (persistenten) Event in
/// den State. Event → Applier → Aggregat.
/// </summary>
public sealed record ApplyRegel
{
    /// <summary>Name des Aggregats, zu dem dieser Applier gehört.</summary>
    public required string Aggregat { get; init; }
    /// <summary>Name des Event-Records.</summary>
    public string Event { get; init; } = "";
    /// <summary>Optionaler Apply-Körper; null ⇒ <c>throw</c>-Platzhalter, <c>""</c> ⇒ bewusst leerer No-op.</summary>
    public string? Rumpf { get; init; }
    /// <summary>Parametername des Events in der Signatur (der Rumpf bezieht sich darauf).</summary>
    public string Parameter { get; init; } = "evt";
    public string? Datei { get; init; }
}

/// <summary>Eine Funktion (nur gelesen, Plan E4): Klasse (im Host) oder Schnittstelle (draußen), je Eingang die Signatur.</summary>
public sealed record BausteinFunktion
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    /// <summary>Als Klasse deklariert: läuft im Host. Sonst Schnittstelle: draußen oder mit einer Umsetzung im Host (Laufort aus der Umgebung).</summary>
    public bool Klasse { get; init; }
    public IReadOnlyList<BausteinEingang> Eingaenge { get; init; } = [];
    /// <summary>Konstruktor der Klasse (Dienste, Konfig-Records, Logger).</summary>
    public IReadOnlyList<Parameter> Konstruktor { get; init; } = [];
    /// <summary>Bei einer Schnittstelle: ihre Umsetzungen im Host (Klasse und Konstruktor).</summary>
    public IReadOnlyList<FunktionsUmsetzung> Umsetzungen { get; init; } = [];
    /// <summary>Der gekapselte Zustand (<c>IZustand&lt;T&gt;</c> im Konstruktor, Plan §2.9): der einfache Name von T, sonst null.</summary>
    public string? Zustand { get; init; }
    /// <summary><c>[EineAdresse]</c>: ein Funktions-Actor für alles statt einer je Partition (A6).</summary>
    public bool EineAdresse { get; init; }
    /// <summary>Die Klasse hat einen Lebenszyklus (<c>ILebenszyklus</c>, E10): sie lädt beim Aktivieren etwas.</summary>
    public bool Lebenszyklus { get; init; }
    public string? Doku { get; init; }
    public string? Datei { get; init; }
}

/// <summary>Eine Umsetzung einer Funktion im Host: die Klasse und ihr Konstruktor (ein Dienst darin ist die Kante Funktion → Dienst).</summary>
public sealed record FunktionsUmsetzung
{
    public required string Name { get; init; }
    public IReadOnlyList<Parameter> Konstruktor { get; init; } = [];
}

/// <summary>Ein Eingang (ein <c>Auf</c>): Eingangstyp, ob Hinweis, ob mit Herkunft, ob Strom, Lese-Fähigkeiten, Ausgaben (einfache Namen).</summary>
public sealed record BausteinEingang
{
    public required string Eingang { get; init; }
    public bool Hinweis { get; init; }
    public bool Herkunft { get; init; }
    public bool Strom { get; init; }
    public IReadOnlyList<Parameter> Faehigkeiten { get; init; } = [];
    public IReadOnlyList<string> Ausgaben { get; init; } = [];
}

/// <summary>
/// Ein Domänen-Dienst (konzept-werkbank §12.3 E11): <c>public interface {Name} : IDienst { {Rückgabe} {Methode}({Parameter}); … }</c> und seine Implementierungen
/// (<c>public sealed class X({Konstruktor}) : {Name} { … }</c>). Deklariert wird er nur an seiner Karte; eine Funktion nimmt ihn per ⊕.
/// </summary>
public sealed record Dienst
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    /// <summary>Die Methoden des Interfaces (Signatur, Deklarations-Reihenfolge).</summary>
    public IReadOnlyList<DienstMethode> Methoden { get; init; } = [];
    /// <summary>Die Implementierungen aus dem Code (Klassen, die das Interface implementieren).</summary>
    public IReadOnlyList<DienstImpl> Implementierungen { get; init; } = [];
    public string? Doku { get; init; }
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel (<see cref="DomainEditor.Herkunft"/>): Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>Eine Methode eines Dienstes: <c>{Rueckgabe} {Name}({Parameter});</c> — Typen wie geschrieben.</summary>
public sealed record DienstMethode
{
    public required string Name { get; init; }
    public string Rueckgabe { get; init; } = "void";
    public IReadOnlyList<Parameter> Parameter { get; init; } = [];
    public string? Doku { get; init; }
}

/// <summary>Eine Implementierung eines Dienstes: Klasse, Datei, Konstruktor (Dienste, Konfig-Records, Logger) und je Methode ihr Rumpf.</summary>
public sealed record DienstImpl
{
    public required string Name { get; init; }
    public string? Datei { get; init; }
    public IReadOnlyList<Parameter> Konstruktor { get; init; } = [];
    /// <summary>Je Methode des Dienstes der Rumpf der Implementierung (Spiegel der Datei; geändert wird er mit ✎ am Rumpf).</summary>
    public IReadOnlyList<DienstRumpf> Rumpfe { get; init; } = [];
}

/// <summary>Der Rumpf einer Dienst-Methode in einer Implementierung.</summary>
public sealed record DienstRumpf
{
    public required string Methode { get; init; }
    public string? Rumpf { get; init; }
}

/// <summary>
/// Der Rahmen, in den geschrieben wird — vom Extractor AUS DEM CODE abgeleitet, nicht im Scaffolder festgelegt:
/// Vertrags-Namespace, globale usings der Domänen-Projekte, die Namen der inneren Decider/Applier-Klassen und ihrer
/// Methoden (wie der Code sie benennt) und die Verzeichnisse je Namespace (wo die Typen tatsächlich liegen).
/// Die Klassen-/Methodennamen sind der Vertrag des Framework-Generators (<c>Abstractions.Aggregatvertrag</c>); die
/// Verzeichnisse stehen nur für Namespaces, deren Typen in genau EINEM Verzeichnis liegen.
/// </summary>
public sealed record Rahmen
{
    public string VertragsNamespace { get; init; } = typeof(Abstractions.IState).Namespace!;
    /// <summary>global usings der Domänen-Compilations (ImplicitUsings) — für In-Memory-Übersetzungen.</summary>
    public IReadOnlyList<string> GlobaleUsings { get; init; } = [];
    // Die Aggregat-Namensregel des Framework-Generators — Vertrag (Abstractions.Aggregatvertrag), nicht geschätzt.
    public string DeciderKlasse { get; init; } = Abstractions.Aggregatvertrag.Decider;
    public string ApplierKlasse { get; init; } = Abstractions.Aggregatvertrag.Applier;
    public string DecideMethode { get; init; } = Abstractions.Aggregatvertrag.Decide;
    public string ApplyMethode { get; init; } = Abstractions.Aggregatvertrag.Apply;
    /// <summary>Namespace → Verzeichnis (relativ zur Solution, „/"-getrennt). Enthält auch die Wurzel-Namespaces der Projekte.</summary>
    public IReadOnlyDictionary<string, string> Verzeichnisse { get; init; } = new Dictionary<string, string>();
    /// <summary>
    /// Das Python-Paket der Clients (relativ zur Solution, z. B. <c>Domain.Client.Worker.Python.ML/domain_client</c>) — Code-Fakt: dort liegt
    /// die generierte <c>generated/vertraege.py</c>. Hier legt „C# schreiben" eine neue Client-Implementierung an. Null = keins.
    /// </summary>
    public string? PythonPaket { get; init; }

    /// <summary>Kennung der Solution, aus der das Modell stammt — trennt Browser-Zwischenstände verschiedener Repos.</summary>
    public string? Kennung { get; init; }
    /// <summary>Die Skalar-Typen, die der Wire (Proto-Codegen, <c>ProtoScalarSpecs</c>) trägt — Vorschläge + Validierung.</summary>
    public IReadOnlyList<string> Skalare { get; init; } = [];
    /// <summary>Das Ziel-Feld, das <c>ICommand&lt;TAggregat&gt;</c> verlangt (<c>Id&lt;TAggregat&gt; AggregateId</c>, <see cref="Abstractions.Idvertrag.CommandZiel"/>).</summary>
    public string AggregatIdFeld { get; init; } = Abstractions.Idvertrag.CommandZiel;
    /// <summary>Größte Stelligkeit von <c>OneOf&lt;…&gt;</c> im Vertrag (aus der Compilation gezählt; 0 = unbekannt).</summary>
    public int OneOfMax { get; init; }

    // ── Leseseite (CQRS057: Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeit…[, CancellationToken ct]) / Frage(TQuery q, Lese-Fähigkeit…[, CancellationToken ct])) ──
    /// <summary>Gemeinsame Basisklasse ALLER Store-Implementierungen im Code (nur wenn eindeutig) — Basis einer NEUEN Impl-Klasse.</summary>
    public StoreBasis? StoreBasis { get; init; }
    /// <summary>Namespace, in dem ALLE Store-Implementierungen liegen (nur wenn eindeutig) — Ort einer NEUEN Impl-Klasse.</summary>
    public string? StoreImplNamespace { get; init; }
}

/// <summary>Basisklasse für neue Store-Implementierungen: Name, Namespace und die Parameter ihres (einzigen) Konstruktors.</summary>
public sealed record StoreBasis
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public IReadOnlyList<Parameter> KtorParameter { get; init; } = [];
    /// <summary>Namespaces der Konstruktor-Parametertypen (für die usings der neuen Datei).</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
//  LESESEITE — nur SIGNATUR-Fakten (Fähigkeit, Bündel, Handle-Parameter, Rückgabe-Vertrag). Rümpfe/Handcode reisen
//  verbatim mit (Rumpf/Zusatz), damit der Round-trip ein Fixpunkt ist; geschrieben wird additiv (DateiSchreiber).
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Die Leseseite des Modells.</summary>
public sealed record Leseseite
{
    public IReadOnlyList<Store> Stores { get; init; } = [];
    /// <summary>Die Projektionen.</summary>
    public IReadOnlyList<Konsument> Konsumenten { get; init; } = [];
    public IReadOnlyList<Leser> Reader { get; init; } = [];
}

/// <summary>Ein Methoden-/Konstruktor-Parameter verbatim: Typ, Name, optionaler Default.</summary>
public sealed record Parameter
{
    public required string Typ { get; init; }
    public required string Name { get; init; }
    public string? Standard { get; init; }
}

/// <summary>
/// Ein Store = ein Bündel-Interface (<c>: IStore, IFähigkeitA, …</c>) mit seinen Fähigkeiten. <see cref="IstBuendel"/> = false:
/// eine einzelne Fähigkeit ohne Bündel (dann ist sie ihr eigener Store, <see cref="Name"/> = Fähigkeit).
/// </summary>
public sealed record Store
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Doku { get; init; }
    public bool IstBuendel { get; init; } = true;
    /// <summary>Die Fähigkeiten in Deklarations-Reihenfolge der Basisliste.</summary>
    public IReadOnlyList<Faehigkeit> Fns { get; init; } = [];
    /// <summary>Die (einzige) Implementierungs-Klasse; null = keine bzw. mehrdeutig (dann wird dort nichts ergänzt).</summary>
    public StoreImpl? Impl { get; init; }
    /// <summary>Datei des Bündel-Interfaces (relativ zur Solution) — null = noch nicht geschrieben.</summary>
    public string? Datei { get; init; }
    /// <summary>
    /// Die Store-Klassen (1 atomar, 2 wiederholungsfest, 3 mindestens einmal; Plan §2.4, R14), die seine Implementierungen tragen — Code-Fakt der
    /// Basisliste (<c>IAtomaresZiel</c>, <c>IWiederholungsfestesZiel</c>, <c>INurMindestensEinmal</c>, auch über die Basisklasse), nur gelesen, nie
    /// geschrieben. Richtig ist genau eine (CQRS109); leer = keine Implementierung bzw. keine Klasse.
    /// </summary>
    public IReadOnlyList<int> Klassen { get; init; } = [];
}

/// <summary>Eine Fähigkeit = Interface mit GENAU EINER Funktion (CQRS051), <c>: IWriteStore</c> bzw. <c>: IReadStore</c>.</summary>
public sealed record Faehigkeit
{
    /// <summary>Name des Fähigkeits-Interfaces (z. B. <c>IUpsertModell</c>) — Code-Fakt, im Editor editierbar.</summary>
    public required string Name { get; init; }
    /// <summary>Namespace der Fähigkeit; null = der des Stores.</summary>
    public string? Namespace { get; init; }
    public required string Methode { get; init; }
    public bool Lesen { get; init; }
    /// <summary>Rückgabetyp verbatim, z. B. <c>Task</c> oder <c>Task&lt;ModellReadModel?&gt;</c>.</summary>
    public string Rueckgabe { get; init; } = "Task";
    public IReadOnlyList<Parameter> Parameter { get; init; } = [];
    public string? Doku { get; init; }
    public string? Datei { get; init; }
    /// <summary>Rumpf-Entwurf der Impl-Methode (Code-/LLM-Knoten im Editor) — nur für eine NEUE Fähigkeit; null ⇒ throw-Platzhalter.</summary>
    public string? ImplRumpf { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>Die Implementierungs-Klasse eines Stores: Name, Namespace und wo ihre Teile liegen (partial über Dateien).</summary>
public sealed record StoreImpl
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    /// <summary>Datei der Deklaration mit Basisliste; null = neue Klasse (wird voll geschrieben).</summary>
    public string? Datei { get; init; }
    /// <summary>Datei, in der die Schreib- bzw. Lese-Methoden der Klasse liegen (nur wenn eindeutig; sonst <see cref="Datei"/>).</summary>
    public string? SchreibDatei { get; init; }
    public string? LeseDatei { get; init; }
}

/// <summary>
/// Ein Eingang einer Projektion (<c>Auf</c>) oder eines Readers (<c>Frage</c>): Eingang, optional Herkunft und Abbruch, Fähigkeiten, Rückgabe.
/// Der Methodenname ist Vertrag (<see cref="Abstractions.Projektionsvertrag.Auf"/>, <see cref="Abstractions.Projektionsvertrag.Frage"/>).
/// </summary>
public sealed record Handle
{
    /// <summary>Einfacher Typname des Eingangs (Event / Query) — identifiziert den Handle.</summary>
    public required string Eingang { get; init; }
    /// <summary>
    /// Der Eingang, wie er im Code steht, wenn er im Editor umverdrahtet/umbenannt wurde (sonst null) — damit „C# schreiben"
    /// die bestehende Methode findet und ihren Eingangs-Typ umschreibt, statt sie als „nicht gefunden" zu übergehen.
    /// </summary>
    public string? EingangImCode { get; init; }
    /// <summary>Parametername des Eingangs.</summary>
    public string Parameter { get; init; } = "e";
    /// <summary>Name des <c>Herkunft</c>-Parameters (nur Projektion, direkt nach dem Eingang); null = keiner.</summary>
    public string? HerkunftParameter { get; init; }
    /// <summary>Name des <c>CancellationToken</c>-Parameters (am Ende); null = keiner.</summary>
    public string? AbbruchParameter { get; init; }
    /// <summary>Die Fähigkeits-Parameter (Typ = Fähigkeits-Interface): die Obergrenze dessen, was der Rumpf am Store darf.</summary>
    public IReadOnlyList<Parameter> Faehigkeiten { get; init; } = [];
    /// <summary>Was der Eingang erzeugen KANN (OneOf-Varianten): die Antworten eines Readers; eine Projektion gibt nichts aus (leer).</summary>
    public IReadOnlyList<string> Ausgaenge { get; init; } = [];
    /// <summary>Rückgabetyp verbatim; null = aus <see cref="Ausgaenge"/> abgeleitet.</summary>
    public string? Rueckgabe { get; init; }
    /// <summary>Modifizierer verbatim (z. B. <c>public async</c>); null = <c>public</c>.</summary>
    public string? Modifikatoren { get; init; }
    /// <summary>Block-Rumpf (ohne Klammern); null ⇒ <c>throw</c>-Platzhalter.</summary>
    public string? Rumpf { get; init; }
    /// <summary>Ausdrucks-Rumpf (<c>=&gt; …;</c> ohne Pfeil/Semikolon); hat Vorrang vor <see cref="Rumpf"/>.</summary>
    public string? Ausdruck { get; init; }
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>Eine Projektion (<c>IProjektion</c>), je gehörtem Event ein <c>Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeit…[, CancellationToken ct])</c>.</summary>
public sealed record Konsument
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    /// <summary><c>[MindestensEinmal]</c>: ausdrücklich erlaubt, in einen Store der Klasse 3 zu schreiben (R14).</summary>
    public bool MindestensEinmal { get; init; }
    public IReadOnlyList<Handle> Handles { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>Deklarationsform verbatim (z. B. <c>public sealed class</c>); null = <c>public sealed class</c>.</summary>
    public string? Typart { get; init; }
    /// <summary>Basisliste verbatim; null = der Marker <c>IProjektion</c>.</summary>
    public IReadOnlyList<string>? Basen { get; init; }
    /// <summary>Weitere Attribute AUSSER <c>[MindestensEinmal]</c> (das kommt aus <see cref="MindestensEinmal"/>).</summary>
    public string? Attribute { get; init; }
    /// <summary>Alle übrigen Member (Konstanten, Ctor, Helfer) verbatim. Null ⇒ keine.</summary>
    public string? Zusatz { get; init; }
    /// <summary>using-Direktiven der Datei verbatim (ohne <c>using</c>/<c>;</c>, auch Aliase).</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
    /// <summary>
    /// Regel Z (Code-Fakt, nur gelesen, nie geschrieben — die Felder stehen verbatim im <see cref="Zusatz"/>): die Instanzfelder, die
    /// ZUSTAND halten — nicht <c>readonly</c>, oder <c>readonly</c> mit einem Referenztyp, der weder Konstruktor-Parameter-Typ
    /// (injiziert: Konfig, Dienst, Logger) noch <c>string</c> ist (z. B. <c>HashSet&lt;string&gt; _seen = new()</c>). Null/leer = zustandslos.
    /// </summary>
    public IReadOnlyList<string>? Zustand { get; init; }
}

/// <summary>
/// Ein Reader (<c>IReader</c>), je Query eine <c>Frage(TQuery q, Lese-Fähigkeit…[, CancellationToken ct])</c> → Antwort bzw. <c>OneOf&lt;…&gt;</c>. Welche
/// Projektionen hinter ihm stehen, folgt aus seinen Lese-Fähigkeiten (die Projektionen, die in denselben Store schreiben) — kein eigenes Feld.
/// </summary>
public sealed record Leser
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public IReadOnlyList<Handle> Handles { get; init; } = [];
    public string? Doku { get; init; }
    public string? Typart { get; init; }
    /// <summary>Basisliste verbatim; null = der Marker <c>IReader</c>.</summary>
    public IReadOnlyList<string>? Basen { get; init; }
    public string? Attribute { get; init; }
    public string? Zusatz { get; init; }
    public IReadOnlyList<string> Usings { get; init; } = [];
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>
/// Ein Akteur — ein Domänen-Experte: <c>public sealed record {Name} : IMensch, IDarf&lt;A&gt;, IDarf&lt;B&gt;;</c>
/// (<c>docs/konzept-akteure.md</c> §2). <see cref="Darf"/> = einfache Typnamen in Deklarations-Reihenfolge — nur, was er SELBST
/// hineingibt; was eine Kette in seinem Namen erzeugt, wird abgeleitet (<see cref="AkteurAnteile"/>).
/// </summary>
public sealed record Akteur
{
    public string Name { get; init; } = "";
    public string Namespace { get; init; } = "";
    public IReadOnlyList<string> Darf { get; init; } = [];
    /// <summary>Art aus der Basisliste: „Mensch" (<c>IMensch</c>), „Maschine" (<c>IMaschine</c>), „Ki" (<c>IKi</c>); null = nur <c>IAkteur</c>.</summary>
    public string? Art { get; init; }
    public string? Doku { get; init; }
    /// <summary>Datei der Deklaration (relativ zur Solution); null = neu im Editor.</summary>
    public string? Datei { get; init; }
    public string? Herkunft { get; init; }
}

/// <summary>
/// Ein CLIENT — die Software an der Leitung (Python-Worker, Blazor-Arbeitsplatz …), <c>docs/konzept-akteure.md</c> §4:
/// <c>public interface {Name} : IClientVertrag, ITraegt&lt;F&gt;…, ISendet&lt;C&gt;…, IFragt&lt;Q&gt;…, IHoert&lt;E&gt;…;</c>. Ein Client kann mehrere
/// Akteure verkörpern, ein Akteur über mehrere Clients laufen; je Client genau dieser eine Vertrag. Nur der RAND steht hier — die
/// client-internen Commands/Events (Intents, ClientEvents, Python-State) nie.
/// </summary>
public sealed record Client
{
    /// <summary>Der Interface-Name wie im Code (<c>IArbeitsplatz</c>); der Handshake-Name ist er ohne führendes I.</summary>
    public string Name { get; init; } = "";
    public string Namespace { get; init; } = "";
    /// <summary><c>ITraegt&lt;F&gt;</c>: getragene Funktionen (Schnittstellen-Namen) — der Client führt ihren Code aus.</summary>
    public IReadOnlyList<string> Traegt { get; init; } = [];
    /// <summary><c>ISendet&lt;T&gt;</c>: Command, Quell-Nachricht oder Hinweis, von sich aus (muss ein Akteur dürfen).</summary>
    public IReadOnlyList<string> Sendet { get; init; } = [];
    /// <summary><c>IFragt&lt;T&gt;</c>: die Queries des Clients (muss ein Akteur dürfen).</summary>
    public IReadOnlyList<string> Fragt { get; init; } = [];
    /// <summary><c>IHoert&lt;E&gt;</c>: nur zur Kenntnis (UI-Aktualisierung), keine Ausgabe.</summary>
    public IReadOnlyList<string> Kenntnis { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>Datei der Deklaration (relativ zur Solution); null = neu im Editor.</summary>
    public string? Datei { get; init; }
    public string? Herkunft { get; init; }
    /// <summary>
    /// Die Software, die den Vertrag umsetzt (Code-Fakt: eine Klasse, die die generierte Basis <c>&lt;Client&gt;Basis</c> erbt) — je
    /// getragenem Eingang und je Kenntnis ihr Rumpf. Das ist die Logik draußen: im Editor ein 📝-Block (+ 🤖) je Methode. Nicht Teil der
    /// Signatur (Herkunft), geändert wird sie über ✓ Übernehmen am 🤖 bzw. die IDE.
    /// </summary>
    public IReadOnlyList<ClientImpl> Implementierungen { get; init; } = [];
}

/// <summary>Eine Implementierung eines Clients: Sprache (heute <c>python</c>), Klasse, Datei und je Methode der Basis der Rumpf.</summary>
public sealed record ClientImpl
{
    public string Sprache { get; init; } = "python";
    public string Klasse { get; init; } = "";
    public string Datei { get; init; } = "";
    public IReadOnlyList<ClientRumpf> Rumpfe { get; init; } = [];
}

/// <summary>
/// Der Rumpf eines getragenen Funktions-Eingangs bzw. einer Kenntnis in der Implementierung (<see cref="Methode"/> wie die generierte Basis sie
/// nennt; null Rumpf = fehlt).
/// </summary>
public sealed record ClientRumpf
{
    public string Event { get; init; } = "";
    public string Methode { get; init; } = "";
    public string? Rumpf { get; init; }
    /// <summary>Die Signatur aus der generierten Basis (<c>async def …(self, e: …Dto, …, state) -&gt; …:</c>) — gilt auch für einen fehlenden Rumpf.</summary>
    public string? Signatur { get; init; }
    /// <summary>Die Ausgaben des getragenen Eingangs (aus der Signatur der Funktion; leer = Kenntnis) und die getragene Funktion (null = Kenntnis).</summary>
    public IReadOnlyList<string> Ausgaenge { get; init; } = [];
    public string? Funktion { get; init; }
}
