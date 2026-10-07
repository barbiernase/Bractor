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
/// / {Aggregat}.cs / Decider.cs / Applier.cs).
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
    public IReadOnlyList<Saga> Sagas { get; init; } = [];
    /// <summary>
    /// Katalog-Funktionen (<c>interface IX : IFunktion</c>): ein Auftrag hinein, OneOf-Ergebnis-Events heraus. Ein Prozess ruft sie mit
    /// <c>Rufe&lt;IX&gt;</c> wie ein Aggregat mit <c>Sende&lt;Cmd&gt;</c>; die Implementierung (C#, Python, extern) ist Bindung, kein Modell.
    /// </summary>
    public IReadOnlyList<Funktion> Funktionen { get; init; } = [];
    /// <summary>
    /// Pipelines als FLUSS (docs/konzept-editor-pipelines.md §14): eine Quelle, Katalog-Funktionen und Commands, frei verdrahtet
    /// (<c>class X : IPipeline { PipelineFluss Fluss =&gt; PipelineFluss.Definiere(p =&gt; { var a = …; }) }</c>). Jede Geste im
    /// Editor ist genau ein Code-Fakt dieser Form — entwerfbar auf leerem Board, 1:1 zurückgelesen.
    /// </summary>
    public IReadOnlyList<FlussPipeline> Fluesse { get; init; } = [];
    /// <summary>
    /// Akteure (<c>docs/konzept-akteure.md</c>): wer von außen hineingibt — je Akteur die Typen, die er darf
    /// (<c>IDarf&lt;T&gt;</c>: Commands, Queries, Trigger, Transient-Events). Was er hören darf, ist abgeleitet, nicht Modell.
    /// </summary>
    public IReadOnlyList<Akteur> Akteure { get; init; } = [];
    /// <summary>
    /// Clients (<c>docs/konzept-akteure.md</c> §4): die Software an der Leitung — je Client EIN Vertrag
    /// (<c>interface IX : IClientVertrag, …</c>): getragene Akteur-Vertrags-Teile, Sendet/Fragt, eigene Kenntnis. Welche Akteure er
    /// verkörpert, ist abgeleitet, nicht Modell.
    /// </summary>
    public IReadOnlyList<Client> Clients { get; init; } = [];
    /// <summary>
    /// Die LESESEITE als Signatur-Fakten: Stores (Fähigkeiten + Bündel + Impl-Klasse), Projektionen/Reaktionen, Reader und
    /// die Fähigkeits-Parameter der Pipeline-Handles. ReadModels/Queries/Responses sind <see cref="Record"/>s (eigene Arten).
    /// Null = das Modell trägt keine Leseseite (z. B. Simulation) — der Scaffolder erzeugt dann keine Leseseiten-Dateien.
    /// </summary>
    public Leseseite? Lesen { get; init; }
    /// <summary>Ingress-Bindungen der Composition Root (Trigger → Webhook/Timer/Datei). Null = nicht Teil des Modells.</summary>
    public IReadOnlyList<IngressBindung>? Ingress { get; init; }
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

    /// <summary>Ein einzelner Record aus JSON (Board-Sammlungen, die Records tragen: triggers[].code, selbstNachrichten).</summary>
    public static Record AusJsonRecord(string json) =>
        JsonSerializer.Deserialize<Record>(json, JsonOptionen) ?? throw new FormatException("Konnte Record nicht aus JSON lesen (null).");

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
    public const string Konfig = "konfig";          // reiner Record, per Ctor in einen Konsumenten injiziert → Konfigs.cs
    public const string Trigger = "trigger";        // : IPipelineTrigger → Triggers.cs
    public const string Selbst = "selbst";          // : IPipelineSelfMessage (Selbst<T>-Nachricht einer Pipeline) → SelbstNachrichten.cs
    public const string Auftrag = "auftrag";        // : IAuftrag<F> (der eine Eingang einer Katalog-Funktion) → Auftraege.cs

    public static readonly IReadOnlyList<string> Alle = [Command, Event, Rejection, ValueObject, Query, Antwort, ReadModel, Konfig, Trigger, Selbst, Auftrag];
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
    /// <summary>Nur auftrag: die Katalog-Funktion, deren Eingang er ist (Marker <c>IAuftrag&lt;Funktion&gt;</c>).</summary>
    public string? Funktion { get; init; }
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
    /// Das Aggregat, zu dem der Record GEHÖRT — aus dem Code: der Command, den genau ein Decider entscheidet; das Event,
    /// das genau ein Aggregat erzeugt/faltet. Null = keinem (Value Object, geteilt, oder im Editor noch nicht verdrahtet).
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
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
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

/// <summary>
/// Eine Saga/ein Prozess: mehrere Aggregate / asynchron / mit Kompensation. Der
/// <see cref="TriggerEvent"/> ist das <c>Prozess&lt;T&gt;</c>-Argument.
/// </summary>
public sealed record Saga
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string TriggerEvent { get; init; }
    public IReadOnlyList<SagaSchritt> Schritte { get; init; } = [];
    public string? Doku { get; init; }
    public IReadOnlyList<string> ExtraUsings { get; init; } = [];
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>Eine Saga-Transition: <c>Auf/Und (→ UndAlle) → Sende/SendeJe → RückgängigDurch</c>.</summary>
public sealed record SagaSchritt
{
    /// <summary>Der Join (Konjunktion): <c>Auf&lt;Wenn[0]&gt;().Und&lt;Wenn[1]&gt;()…</c>.</summary>
    public IReadOnlyList<string> Wenn { get; init; } = [];
    /// <summary>Optional Count-Join: <c>.UndAlle&lt;SammelEvent&gt;(t => SammelAnzahl)</c> — feuert erst nach ALLEN N.</summary>
    public string? SammelEvent { get; init; }
    /// <summary>Der Anzahl-Ausdruck des Count-Joins, z. B. <c>t.Ziele.Count</c>.</summary>
    public string? SammelAnzahl { get; init; }
    /// <summary>Der echte Count-Join-Lambda-Ausdruck verbatim (z. B. <c>t => t.Anzahl</c>); Vorrang vor <see cref="SammelAnzahl"/>.</summary>
    public string? SammelAusdruck { get; init; }
    /// <summary>Der gesendete Command (<c>Sende&lt;Cmd&gt;</c>). Leer, wenn der Schritt eine Funktion ruft (<see cref="Rufe"/>).</summary>
    public string Sende { get; init; } = "";
    /// <summary>
    /// Statt eines Commands: die gerufene Katalog-Funktion (<c>Rufe&lt;F&gt;</c>) — der Aufruf-Knoten im Graph. Der Lambda (Auftrag aus dem
    /// Match) steht in <see cref="SendeAusdruck"/>; ohne ihn schreibt der Scaffolder einen Stub mit dem Auftrag der Funktion.
    /// </summary>
    public string? Rufe { get; init; }
    /// <summary>Zeitlimit des Aufrufs als C#-Ausdruck verbatim (z. B. <c>TimeSpan.FromSeconds(30)</c>); null = keins.</summary>
    public string? Zeitlimit { get; init; }
    /// <summary>Fan-out: <c>SendeJe</c> statt <c>Sende</c> — iteriert <see cref="SendeJeCollection"/> mit <see cref="SendeJeElement"/>.</summary>
    public bool SendeJe { get; init; }
    public string? SendeJeCollection { get; init; }
    public string? SendeJeElement { get; init; }
    public IReadOnlyList<string>? SendeArgumente { get; init; }
    /// <summary>
    /// Der ECHTE Sende-Lambda-Ausdruck verbatim (z. B. <c>e => new X(e.Id)</c>) — aus dem Code gelesen.
    /// Gesetzt ⇒ hat Vorrang vor <see cref="SendeArgumente"/>/<see cref="SendeJeCollection"/> (verlustfreier Round-trip).
    /// </summary>
    public string? SendeAusdruck { get; init; }
    public string? Kompensation { get; init; }
    public IReadOnlyList<string>? KompensationArgumente { get; init; }
    /// <summary>Der echte Kompensations-Lambda-Ausdruck verbatim; Vorrang vor <see cref="KompensationArgumente"/>.</summary>
    public string? KompensationAusdruck { get; init; }
    /// <summary>Kompensation als Fan-out (<c>RückgängigDurchJe</c>).</summary>
    public bool KompensationJe { get; init; }
}

/// <summary>
/// Eine Katalog-Funktion — nur ihre Signatur: <c>public interface {Name} : IFunktion { Task&lt;OneOf&lt;Ergebnisse…&gt;&gt; RufeAsync({Auftrag} a,
/// IAusfuehrung x); }</c>. Der Auftrag ist ein <see cref="Record"/> der Art <see cref="RecordArt.Auftrag"/>, die Ergebnisse sind Event-Records.
/// </summary>
public sealed record Funktion
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    /// <summary>Der Auftrag (Record-Name, Art <c>auftrag</c>) — der eine Eingang.</summary>
    public required string Auftrag { get; init; }
    /// <summary>Die Ergebnis-Events (OneOf-Fälle, Record-Namen) — die Ausgänge der Funktion im Graph.</summary>
    public IReadOnlyList<string> Ergebnisse { get; init; } = [];
    public string? Doku { get; init; }
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>
/// Eine Pipeline als Fluss (§14): Knoten in Deklarations-Reihenfolge — die Reihenfolge IST die Code-Reihenfolge (ein Draht
/// kommt nur von einem früheren Knoten, also ist der Fluss per Konstruktion azyklisch).
/// </summary>
public sealed record FlussPipeline
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public IReadOnlyList<FlussSchritt> Knoten { get; init; } = [];
    public string? Doku { get; init; }
    public IReadOnlyList<string> ExtraUsings { get; init; } = [];
    public string? Datei { get; init; }
    /// <summary>Der Name des Bauer-Parameters im Lambda (<c>p</c>) — aus dem Code, sonst <c>p</c>.</summary>
    public string Bauer { get; init; } = "p";
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>Die Art eines Fluss-Knotens.</summary>
public static class FlussArt
{
    /// <summary>Katalog-Quelle: <c>p.Quelle&lt;Nachricht&gt;()</c>.</summary>
    public const string Quelle = "quelle";
    /// <summary>Event aus dem Log als Quelle: <c>p.Auf&lt;Event&gt;()</c>.</summary>
    public const string Auf = "auf";
    /// <summary>Katalog-Funktion: <c>….Rufe&lt;IF&gt;(λ)</c>.</summary>
    public const string Funktion = "funktion";
    /// <summary>Command an ein Aggregat: <c>….Sende&lt;Cmd&gt;(λ)</c>.</summary>
    public const string Command = "command";
    /// <summary>Je-Rahmen: <c>draht.Je(x =&gt; x.Liste)</c>.</summary>
    public const string Je = "je";
}

/// <summary>
/// Ein Knoten des Flusses: <c>var {Name} = …;</c>. <see cref="Typ"/> = Nachricht (Quelle/Auf), Funktions-Interface, Command bzw.
/// Element-Typ (Je; nur Anzeige). Ein Aufruf-Knoten hat ≥ 1 Eingang — der erste ist der Hauptaufruf, jeder weitere ein ∨
/// (<c>.Oder(…)</c>). Ein Je-Knoten hat genau einen Eingang (der Draht mit der Liste) und den Listen-Ausdruck.
/// </summary>
public sealed record FlussSchritt
{
    public required string Name { get; init; }
    public required string Art { get; init; }
    public required string Typ { get; init; }
    public IReadOnlyList<FlussEingang> Eingaenge { get; init; } = [];
    /// <summary>Zeitlimit als C#-Ausdruck verbatim (z. B. <c>TimeSpan.FromMinutes(5)</c>); null = keins.</summary>
    public string? Zeitlimit { get; init; }
    /// <summary>Nur Je: der Listen-Lambda verbatim (<c>z =&gt; z.Bilder</c>).</summary>
    public string? Liste { get; init; }
    /// <summary>Im Code ohne Variable (<c>p.Alle(…).Sende&lt;X&gt;(…);</c>) — der Name ist dann nur die Editor-Identität.</summary>
    public bool OhneVariable { get; init; }
    public string? Doku { get; init; }
}

/// <summary>
/// Ein Eingang eines Aufruf-Knotens: die Drähte (1 = einfach, mehr = ∧ <c>p.Alle(…)</c>), oder ein Je-Element (<see cref="Je"/>
/// ohne Drähte), oder das Sammeln eines Je-Rahmens (<see cref="Je"/> + <see cref="Sammle"/>: die Drähte sind die gesammelten).
/// <see cref="Ausdruck"/> ist der Bau-Lambda verbatim; fehlt er, schreibt der Scaffolder ihn aus <see cref="Argumente"/>
/// (je Konstruktor-Parameter des Ziels ein Ausdruck in den Lambda-Parametern).
/// </summary>
public sealed record FlussEingang
{
    public IReadOnlyList<FlussDraht> Draehte { get; init; } = [];
    /// <summary>Gesetzt: der Eingang gehört zum Je-Rahmen dieses Namens — Element (ohne <see cref="Sammle"/>) bzw. gesammelte Liste.</summary>
    public string? Je { get; init; }
    public bool Sammle { get; init; }
    public string? Ausdruck { get; init; }
    public IReadOnlyList<string>? Argumente { get; init; }
}

/// <summary>Ein Draht: Ausgang <see cref="Fall"/> (bzw. Port) des Knotens <see cref="Von"/>.</summary>
public sealed record FlussDraht
{
    public required string Von { get; init; }
    /// <summary>Der Fall (Typ-Name: Ergebnis der Funktion, Event des Aggregats). Null = der Ausgang einer Quelle selbst.</summary>
    public string? Fall { get; init; }
    /// <summary><c>fall</c> (Standard), <c>zeitlimit</c> (⏳) oder <c>abgelehnt</c> (✕).</summary>
    public string Port { get; init; } = FlussPort.Fall;
}

public static class FlussPort
{
    public const string Fall = "fall";
    public const string Zeitlimit = "zeitlimit";
    public const string Abgelehnt = "abgelehnt";
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

    /// <summary>Kennung der Solution, aus der das Modell stammt — trennt Browser-Zwischenstände verschiedener Repos.</summary>
    public string? Kennung { get; init; }
    /// <summary>Die Skalar-Typen, die der Wire (Proto-Codegen, <c>ProtoScalarSpecs</c>) trägt — Vorschläge + Validierung.</summary>
    public IReadOnlyList<string> Skalare { get; init; } = [];
    /// <summary>Das Identitäts-Feld, das <c>ICommand</c> verlangt (<c>nameof(ICommand.AggregateId)</c>).</summary>
    public string AggregatIdFeld { get; init; } = nameof(Abstractions.ICommand.AggregateId);
    /// <summary>Größte Stelligkeit von <c>OneOf&lt;…&gt;</c> im Vertrag (aus der Compilation gezählt; 0 = unbekannt).</summary>
    public int OneOfMax { get; init; }
    /// <summary>Größte Join-Stelligkeit der Prozess-DSL (<c>RegelBauer&lt;…&gt;</c>, aus der Compilation gezählt; 0 = unbekannt).</summary>
    public int UndMax { get; init; }

    // ── Leseseite (CQRS057: Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…) / Handle(TQuery, IMessageEnvelope, ReadContext, Fähigkeit…)) ──
    /// <summary>
    /// Der Schreiber-Typ der Projektions-Handles (3. Parameter) + sein Namespace — aus den Handles im Code gelesen (er liegt
    /// außerhalb des Vertrags). Null = kein Handle im Code: dann kann der Scaffolder keine NEUE Projektion schreiben.
    /// </summary>
    public string? ProjektionsSchreiber { get; init; }
    public string? ProjektionsSchreiberNamespace { get; init; }
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
    /// <summary>Projektionen UND Reaktionen (dieselbe Form; Reaktion = ein Handle gibt Commands aus).</summary>
    public IReadOnlyList<Konsument> Konsumenten { get; init; } = [];
    public IReadOnlyList<Leser> Reader { get; init; } = [];
    /// <summary>Pipelines — hier NUR ihre Handles mit Fähigkeits-Parametern (verbinden/lösen); keine Pipeline-Scaffoldung.</summary>
    public IReadOnlyList<PipelineKarte> Pipelines { get; init; } = [];
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

/// <summary>Ein Handle einer Projektion/Reaktion, eines Readers oder einer Pipeline: Eingang, Kontext, Fähigkeiten, Rückgabe.</summary>
public sealed record Handle
{
    /// <summary>Einfacher Typname des Eingangs (Event / Query / Trigger / Selbst-Nachricht) — identifiziert den Handle.</summary>
    public required string Eingang { get; init; }
    /// <summary>
    /// Der Eingang, wie er im Code steht, wenn er im Editor umverdrahtet/umbenannt wurde (sonst null) — damit „C# schreiben"
    /// die bestehende Methode findet und ihren Eingangs-Typ umschreibt, statt sie als „nicht gefunden" zu übergehen.
    /// </summary>
    public string? EingangImCode { get; init; }
    /// <summary>Parametername des Eingangs.</summary>
    public string Parameter { get; init; } = "evt";
    /// <summary>Namen der Kontext-Parameter (Umschlag, Schreiber/Lese-Kontext) verbatim; null = Standardnamen.</summary>
    public IReadOnlyList<string>? Kontext { get; init; }
    /// <summary>Die Fähigkeits-Parameter (Typ = Fähigkeits-Interface): die Obergrenze dessen, was der Rumpf am Store darf.</summary>
    public IReadOnlyList<Parameter> Faehigkeiten { get; init; } = [];
    /// <summary>Was der Handle erzeugen KANN (OneOf-Varianten): Commands/Events (Konsument), Responses (Reader).</summary>
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

/// <summary>Eine Projektion oder Reaktion (<c>ISubscriber</c>), Handles <c>Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…)</c>.</summary>
public sealed record Konsument
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? SubscriberId { get; init; }
    public bool Pull { get; init; } = true;
    public bool Append { get; init; }
    public IReadOnlyList<Handle> Handles { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>Deklarationsform verbatim ohne <c>partial</c> (z. B. <c>public</c>); null = <c>public</c>.</summary>
    public string? Typart { get; init; }
    /// <summary>Basisliste verbatim; null = aus den Markern (<c>ISubscriber, IPullSubscriber[, IAppendProjektion]</c>).</summary>
    public IReadOnlyList<string>? Basen { get; init; }
    public string? Attribute { get; init; }
    /// <summary>Alle übrigen Member (SubscriberId, Konstanten, Ctor, Helfer) verbatim. Null ⇒ nur <c>SubscriberId</c> wird erzeugt.</summary>
    public string? Zusatz { get; init; }
    /// <summary>using-Direktiven der Datei verbatim (ohne <c>using</c>/<c>;</c>, auch Aliase).</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
    /// <summary>Regel Z (Code-Fakt, nur gelesen): Instanzfelder, die Zustand halten (siehe <see cref="PipelineKarte.Zustand"/>).</summary>
    public IReadOnlyList<string>? Zustand { get; init; }
}

/// <summary>Ein Reader (<c>IReader&lt;TProjektion&gt;</c>), Handles <c>Handle(TQuery, IMessageEnvelope, ReadContext, Fähigkeit…)</c>.</summary>
public sealed record Leser
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    /// <summary>Name der Projektion (<c>IReader&lt;T&gt;</c>).</summary>
    public required string Projektion { get; init; }
    public bool TrackDeps { get; init; } = true;
    public IReadOnlyList<Handle> Handles { get; init; } = [];
    public string? Doku { get; init; }
    public string? Typart { get; init; }
    public IReadOnlyList<string>? Basen { get; init; }
    /// <summary>Weitere Attribute AUSSER <c>[ProjectionReader]</c> (das kommt aus <see cref="TrackDeps"/>).</summary>
    public string? Attribute { get; init; }
    public string? Zusatz { get; init; }
    public IReadOnlyList<string> Usings { get; init; } = [];
    public string? Datei { get; init; }
    /// <summary>Herkunfts-Stempel: Hash des Inhalts beim Einlesen aus dem Code (<see cref="DomainEditor.Herkunft"/>). Abweichung = im Editor geändert; null = neu.</summary>
    public string? Herkunft { get; init; }
}

/// <summary>
/// Eine Pipeline (<c>IPipelineHandler</c>), Handles <c>Handle(TEingang, PipelineContext, Fähigkeit…)</c> → <c>IAsyncEnumerable&lt;OneOf&lt;…&gt;&gt;</c>
/// (Commands, Trigger, <c>Selbst&lt;T&gt;</c>, <c>Frist&lt;TCmd&gt;</c>). Konfigs = per Konstruktor injizierte Konfigurations-Records.
/// </summary>
public sealed record PipelineKarte
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? PipelineId { get; init; }
    public IReadOnlyList<string> Konfigs { get; init; } = [];
    public IReadOnlyList<Handle> Handles { get; init; } = [];
    public string? Doku { get; init; }
    public string? Typart { get; init; }
    public IReadOnlyList<string>? Basen { get; init; }
    public string? Attribute { get; init; }
    /// <summary>Übrige Member verbatim (PipelineId, Ctor, Felder, Helfer). Null ⇒ PipelineId + Konfig-Ctor werden erzeugt.</summary>
    public string? Zusatz { get; init; }
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
/// Eine Ingress-Bindung der Composition Root: welcher Trigger über welchen Ingress (Webhook/Timer/Datei) an welchem Ort
/// ankommt. Aus dem Code: der Aufruf einer <c>[Ingress]</c>-Methode (Datei + Anweisung verbatim). Eine NEUE Bindung wird nach
/// dem Vorbild einer bestehenden Bindung desselben Modus geschrieben (Anweisung kopiert, Trigger-Typ und Ort ersetzt).
/// </summary>
public sealed record IngressBindung
{
    public required string Trigger { get; init; }
    /// <summary>webhook | timer | filewatch.</summary>
    public required string Modus { get; init; }
    /// <summary>Der Ort (Route/Intervall/Pfad) als Wert.</summary>
    public string? Ort { get; init; }
    /// <summary>Nur aus dem Code: Datei, die Anweisung verbatim, der Typ-Argument-Text und der Ort-Argument-Text darin.</summary>
    public string? Datei { get; init; }
    public string? Anweisung { get; init; }
    public string? TypArgument { get; init; }
    public string? OrtArgument { get; init; }
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
    /// <summary>
    /// Die Dienste dieses Akteurs (<c>interface X : IAkteurDienst&lt;Akteur&gt;</c>, einfache Namen). Ein Dienst ist kein Akteur;
    /// ein Pipeline-Handle, der ihn als Parameter nimmt, entscheidet im Auftrag dieses Akteurs (CQRS060). Nur Code-Fakt (gelesen).
    /// </summary>
    public IReadOnlyList<string> Dienste { get; init; } = [];
    /// <summary>
    /// Der VERTRAG des Akteurs (<c>interface IX : IAkteurVertrag&lt;X&gt;</c>, <c>docs/konzept-akteure.md</c> §3): je <c>Auf(Event)</c> eine
    /// Zusage mit ihren Ausgängen. Was er so hineingibt, darf er (kein IDarf), und er hört genau diese Eingänge. Leer = rein spontan.
    /// </summary>
    public IReadOnlyList<AkteurZusage> Vertrag { get; init; } = [];
    /// <summary>Name des Vertrags-Interfaces (null = <c>I{Name}</c>, sobald es Zusagen gibt).</summary>
    public string? VertragName { get; init; }
    /// <summary>Datei des Vertrags, wenn sie nicht die des Akteurs ist (relativ zur Solution).</summary>
    public string? VertragDatei { get; init; }
    public string? Doku { get; init; }
    /// <summary>Datei der Deklaration (relativ zur Solution); null = neu im Editor.</summary>
    public string? Datei { get; init; }
    public string? Herkunft { get; init; }

    /// <summary>Der Name des Vertrags-Interfaces, wie er im Code steht bzw. geschrieben wird.</summary>
    public string VertragTyp => VertragName ?? "I" + Name;
}

/// <summary>
/// Eine Zusage im Akteur-Vertrag: <c>Auf(Eingang e)</c> mit dem Ausgabe-Vertrag als Rückgabetyp — keine Ausgänge = <c>void</c> (nur zur
/// Kenntnis), sonst <c>OneOf&lt;…&gt;</c>, bei <see cref="Strom"/> <c>IAsyncEnumerable&lt;OneOf&lt;…&gt;&gt;</c> (mehrere Commands je Zusage).
/// </summary>
public sealed record AkteurZusage
{
    public string Eingang { get; init; } = "";
    public IReadOnlyList<string> Ausgaenge { get; init; } = [];
    public bool Strom { get; init; }
    public string? Doku { get; init; }
    /// <summary>
    /// Der Vertrags-TEIL, in dem die Zusage steht, wenn es nicht der Haupt-Vertrag (<see cref="Akteur.VertragTyp"/>) ist — ein Akteur darf
    /// seinen Vertrag in Teile schneiden, die Clients einzeln tragen (CQRS061). Null = Haupt-Vertrag. Teile liest der Editor nur.
    /// </summary>
    public string? Teil { get; init; }
}

/// <summary>
/// Ein CLIENT — die Software an der Leitung (Python-Worker, Blazor-Arbeitsplatz …), <c>docs/konzept-akteure.md</c> §4:
/// <c>public interface {Name} : IClientVertrag, ITeil…, ISendet&lt;C&gt;, IFragt&lt;Q&gt; { void Auf(E e); }</c>. Ein Client kann mehrere
/// Akteure verkörpern, ein Akteur über mehrere Clients laufen; je Client genau dieser eine Vertrag. Nur der RAND steht hier — die
/// client-internen Commands/Events (Intents, ClientEvents, Python-State) nie.
/// </summary>
public sealed record Client
{
    /// <summary>Der Interface-Name wie im Code (<c>IArbeitsplatz</c>); der Handshake-Name ist er ohne führendes I.</summary>
    public string Name { get; init; } = "";
    public string Namespace { get; init; } = "";
    /// <summary>Getragene Akteur-Vertrags-Teile (Interface-Namen, z. B. <c>IKlassifizierer</c>) — ihre Zusagen im Namen des Akteurs.</summary>
    public IReadOnlyList<string> Traegt { get; init; } = [];
    /// <summary><c>ISendet&lt;T&gt;</c>: Command/Trigger/Transient, von sich aus (muss ein Akteur dürfen).</summary>
    public IReadOnlyList<string> Sendet { get; init; } = [];
    /// <summary><c>IFragt&lt;T&gt;</c>: die Queries des Clients (muss ein Akteur dürfen).</summary>
    public IReadOnlyList<string> Fragt { get; init; } = [];
    /// <summary>Eigene <c>void Auf(E e)</c>: nur zur Kenntnis (UI-Aktualisierung), keine Ausgabe.</summary>
    public IReadOnlyList<string> Kenntnis { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>Datei der Deklaration (relativ zur Solution); null = neu im Editor.</summary>
    public string? Datei { get; init; }
    public string? Herkunft { get; init; }
}
