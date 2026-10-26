using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// Zwischen-Repräsentation (raw), die der GraphBuilder zum Property-Graph und der ModellMapper zum
// Editor-Modell zusammensetzt. Grundsatz: ALLES, was der Scaffolder zurückschreiben kann, wird hier
// verlustfrei gelesen (Felder inkl. Defaults, Handcode-Rümpfe, Parameternamen) — sonst
// laufen Code und Editor auseinander. Die ParitaetsPruefung (--check) beweist das als Fixpunkt.

public sealed class DomainModel
{
    public List<AggregateRaw> Aggregates { get; } = new();
    public List<ProjectionRaw> Projections { get; } = new();
    public List<QueryRaw> Queries { get; } = new();
    public List<ReaderRaw> Readers { get; } = new();
    public List<StoreRaw> Stores { get; } = new();
    /// <summary><c>IAkteur</c>-Typen: wer von außen hineingibt, mit seinen <c>IDarf&lt;T&gt;</c>.</summary>
    public List<AkteurRaw> Akteure { get; } = new();
    /// <summary><c>IClientVertrag</c>-Interfaces: die Clients mit ihrem Vertrag (docs/konzept-akteure.md §4).</summary>
    public List<ClientRaw> Clients { get; } = new();

    /// <summary>Records ohne Nachrichten-Marker in Domain-Assemblies (Value Objects).</summary>
    public List<RecordRaw> ValueObjects { get; } = new();
    /// <summary><c>IQueryResponse</c>-Records.</summary>
    public List<RecordRaw> Responses { get; } = new();
    /// <summary><c>IReadModel</c>-Typen (Dokumente der Leseseite).</summary>
    public List<RecordRaw> ReadModels { get; } = new();
    /// <summary>Enums in Domain-Assemblies.</summary>
    public List<EnumRaw> Enums { get; } = new();
    /// <summary>Funktionen (<c>Auf</c>, Plan Pub/Sub-Neustrukturierung §3.3): je Eingang die Ausgaben aus der Signatur.</summary>
    public List<BausteinFunktionRaw> BausteinFunktionen { get; } = new();
    /// <summary>Domänen-Dienste (<c>interface IX : IDienst</c>, konzept-werkbank §12.3 E11): Signatur + Implementierungen.</summary>
    public List<DienstRaw> Dienste { get; } = new();
    /// <summary>
    /// Konfigurations-Records: Records ohne Nachrichten-Rolle, die per DI in den KONSTRUKTOR einer Funktion, Dienst-Implementierung,
    /// Projektion, eines Readers oder einer Store-Impl injiziert werden — keine Value Objects der Domäne.
    /// </summary>
    public List<RecordRaw> Konfigs { get; } = new();
    /// <summary>Konfig-Record-Name → Namen der Klassen, die ihn injizieren.</summary>
    public Dictionary<string, List<string>> KonfigNutzer { get; } = new(StringComparer.Ordinal);

    // ── Der Rahmen, wie der Code ihn vorgibt (für den Scaffolder: wohin, wie heißt es) ──
    /// <summary>Namen der inneren Decider-/Applier-Klassen und ihrer Methoden, wie der Code sie benennt (häufigste).</summary>
    public string? DeciderKlasse, ApplierKlasse, DecideMethode, ApplyMethode;
    /// <summary>Namespace → absolutes Verzeichnis, in dem seine Typen liegen (häufigstes).</summary>
    public Dictionary<string, string> NamespaceVerzeichnisse { get; } = new(StringComparer.Ordinal);
    /// <summary>global usings der Domänen-Compilations.</summary>
    public List<string> GlobaleUsings { get; } = new();
    /// <summary>Größte Stelligkeit von OneOf im Vertrag — aus der Compilation gezählt.</summary>
    public int OneOfMax;
    /// <summary>Wurzel-Namespace je Domänen-Projekt → Projektverzeichnis (absolut) — aus der Projektlage.</summary>
    public Dictionary<string, string> ProjektWurzeln { get; } = new(StringComparer.Ordinal);
    /// <summary>Solution-Verzeichnis (absolut) — Bezug für relative Pfade im Editor-Modell.</summary>
    public string Wurzel { get; set; } = "";
    /// <summary>Die fest codierten Treiber (Name → Basistyp + seine Felder) — der Editor legt damit neue Quellen-Typen an.</summary>
    public Dictionary<string, (string Basis, List<FieldInfo> Felder)> Treiber { get; } = new(StringComparer.Ordinal);

    /// <summary>Event-FullName → (Simple, Persisted, Felder, Meta).</summary>
    public Dictionary<string, EventType> Events { get; } = new(StringComparer.Ordinal);
    /// <summary>Der Schreiber-Typ (3. Parameter) der Projektions-Handles im Code — nur wenn eindeutig.</summary>
    public string? ProjektionsSchreiber, ProjektionsSchreiberNamespace;
    /// <summary>Gemeinsame Basisklasse aller Store-Impls (nur eindeutig): Name, Namespace, Ctor-Parameter, usings.</summary>
    public (string Name, string Ns, List<(string Typ, string Name)> Ktor, List<string> Usings)? StoreBasis;
    /// <summary>Namespace aller Store-Impls (nur eindeutig).</summary>
    public string? StoreImplNamespace;
    /// <summary>Command-FullName → (Simple, IsCreation, Felder, Meta).</summary>
    public Dictionary<string, CommandType> Commands { get; } = new(StringComparer.Ordinal);

    // ── Code mit Fehlern (Werkbank P7.1): nichts verschwindet still ──
    /// <summary>Typ-FullName → seine handgeschriebenen Deklarationen (partial: mehrere), nach Datei geordnet.</summary>
    public Dictionary<string, List<Ort>> TypOrte { get; } = new(StringComparer.Ordinal);
    /// <summary>Handgeschriebene Domänen-Typen mit unaufgelöstem Basistyp — sie werden Code-Karten mit ⚠ (bzw. ein Befund an ihrer Karte).</summary>
    public List<UnaufgeloestRaw> Unaufgeloest { get; } = new();
    /// <summary>
    /// Jede handgeschriebene Deklaration eines Typs oberster Ebene in den Editor-Projekten (partial: je Teil eine) — Grundlage der Code-Karte
    /// (Werkbank P7.4, Zielbild §9): was keine Karte über <c>_ort</c>/<c>_teile</c> abdeckt, wird roh gezeigt.
    /// </summary>
    public List<TypDeklaration> Deklarationen { get; } = new();
}

/// <summary>Eine handgeschriebene Typ-Deklaration oberster Ebene: Name, Namespace, Form, Ort und der Code verbatim.</summary>
public sealed record TypDeklaration(string Name, string Namespace, string Art, Ort Ort, string Code);

/// <summary>
/// Ein Befund des Lesens (kein Compiler-Befund): <paramref name="Karte"/> ist die stabile Karten-Id (<c>agg:A</c>, <c>code:T</c> …),
/// <paramref name="Datei"/> absolut, <paramref name="Zeile"/> 1-basiert.
/// </summary>
public sealed record Lesebefund(string Code, string Text, string? Karte, string? Datei, int Zeile);

/// <summary>
/// Wo eine Karte im Code steht: Datei (absolut), Spanne der Deklaration (Zeichen, ohne führende Trivia) und Zeile (1-basiert). Die Werkbank
/// zeigt damit Rumpf-Ausschnitte und setzt Gesten an der richtigen Stelle an.
/// </summary>
public sealed record Ort(string Datei, int Start, int Ende, int Zeile, int Bis = 0)
{
    /// <param name="n">Die Deklaration bzw. Anweisung; <see cref="Bis"/> = ihre letzte Zeile (1-basiert) — Befunde einer Zeile finden so die innerste Karte.</param>
    public static Ort Von(SyntaxNode n)
    {
        var z = n.GetLocation().GetLineSpan();
        return new(n.SyntaxTree.FilePath, n.Span.Start, n.Span.End, z.StartLinePosition.Line + 1, z.EndLinePosition.Line + 1);
    }
}

/// <summary>Ein handgeschriebener Typ, dessen Basisliste Unaufgelöstes enthält (Tippfehler, gelöschter Typ, fehlendes using).</summary>
public sealed class UnaufgeloestRaw
{
    public string Name = "", Namespace = "", Art = "";
    public string? Datei, Code;
    public int Zeile;
    /// <summary>Die nicht aufgelösten Basis-Namen, wie sie im Code stehen.</summary>
    public List<string> Basen = new();
    /// <summary>Position der Deklaration (für die Karte: Datei + Spanne).</summary>
    public int Start, Ende;
}

/// <summary>Quelltext-Metadaten eines Typs: Doku (summary), Handcode-Rumpf, Datei-usings, Herkunfts-Assembly.</summary>
/// <param name="IstDomäne">Liegt der Typ in einer Domänen-Assembly (abgeleitet, <see cref="Projektlage"/>) — editierbarer Fachcode, kein Framework?</param>
/// <param name="Typart">Die Deklarationsform ohne <c>public</c>, verbatim: <c>record</c>, <c>sealed record</c>, <c>record struct</c>, <c>class</c> …</param>
/// <param name="Basen">Basistypen AUSSER dem Rollen-Marker (z. B. zusätzliche Interfaces) — verbatim aus der Basisliste.</param>
/// <param name="Attribute">Attribut-Listen des Typs verbatim (null = keine).</param>
/// <param name="OhneParameterliste">Record ohne <c>(…)</c> bzw. jede Klasse/Struct (Felder dann nur als Properties).</param>
public sealed record TypMeta(string Namespace, string Assembly, string? Doku, string? Zusatz, List<string> Usings, bool IstDomäne = false, string? Datei = null,
    string Typart = "public record", List<string>? Basen = null, string? Attribute = null, bool OhneParameterliste = false, string? Treiber = null);

public sealed record EventType(string Simple, bool Persisted, List<FieldInfo> Fields, TypMeta Meta);
public sealed record CommandType(string Simple, bool IsCreation, List<FieldInfo> Fields, TypMeta Meta, string? Ziel = null);

public sealed class RecordRaw
{
    public string Name = "", Full = "";
    public List<FieldInfo> Fields = new();
    public TypMeta Meta = new("", "", null, null, new());
    /// <summary>Nur ReadModel: der Store, dessen Funktionen diesen Typ lesen/schreiben (typisiert gematcht, eindeutig).</summary>
    public string? Store;
    /// <summary>Nur ReadModel: mehrere Stores nennen den Typ — keine Zuordnung geraten, sondern alle Kandidaten.</summary>
    public List<string>? StoreKandidaten;
    /// <summary>Nur ReadModel: geteiltes Dokument (Marker <c>IGeteiltesReadModel</c>) — aus mehreren Streams beschrieben.</summary>
    public bool Geteilt;
}

/// <summary>
/// Eine Funktion (Plan Pub/Sub-Neustrukturierung §3.3): als Klasse im Host, als Schnittstelle draußen. Gelesen wird nur die
/// Signatur (<see cref="Infrastructure.SourceGeneration.Funktionsform"/>, dieselbe Lesart wie der Generator): je <c>Auf</c> Eingang, Herkunft,
/// Lese-Fähigkeiten, Ausgaben, Strom. Nur gelesen — die Editor-Oberfläche dafür ist zurückgestellt (Plan E4).
/// </summary>
public sealed class BausteinFunktionRaw
{
    public string Name = "", Full = "", Namespace = "";
    /// <summary>Als Klasse deklariert (läuft im Host); sonst Schnittstelle (draußen).</summary>
    public bool Klasse;
    public List<BausteinEingangRaw> Eingaenge = new();
    /// <summary>Konstruktor der Klasse (Typ wie geschrieben, Name): Dienste, Konfig-Records, Logger.</summary>
    public List<(string Typ, string Name)> Konstruktor = new();
    /// <summary>Bei einer Schnittstelle: die Klassen der Domänen-Quellen, die sie implementieren (Umsetzungen im Host).</summary>
    public List<string> UmsetzungenFull = new();
    /// <summary>Je Umsetzung ihr Konstruktor (Typ wie geschrieben, Name) — ein Dienst darin ist die Kante Funktion → Dienst.</summary>
    public Dictionary<string, List<(string Typ, string Name)>> UmsetzungsKonstruktoren = new(StringComparer.Ordinal);
    public string? Doku, Datei;
}

/// <summary>Ein Eingang (ein <c>Auf</c>) einer Funktion.</summary>
public sealed class BausteinEingangRaw
{
    public string EingangFull = "";
    public bool Hinweis, Herkunft, Strom;
    public List<(string Typ, string Name)> Faehigkeiten = new();
    public List<string> AusgabenFull = new();
}

/// <summary>
/// Ein Domänen-Dienst (<c>interface IX : IDienst</c>) — Signatur (Methoden, Typen wie geschrieben) und seine Implementierungen: jede Klasse der
/// Domänen-Quellen, die das Interface implementiert (Code-Fakt über das Interface, nie über Namen).
/// </summary>
public sealed class DienstRaw
{
    public string Name = "", Full = "", Namespace = "";
    public string? Doku, Datei;
    public List<DienstMethodeRaw> Methoden = new();
    public List<DienstImplRaw> Impls = new();
}

public sealed class DienstMethodeRaw
{
    public string Name = "", Rueckgabe = "";
    public List<(string Typ, string Name)> Parameter = new();
    public string? Doku;
    /// <summary>Die Deklaration am Interface (Umbenennen per Renamer).</summary>
    public Ort? Ort;
}

public sealed class DienstImplRaw
{
    public string Name = "", Full = "";
    public string? Datei;
    /// <summary>Die Klassen-Deklaration (Umbenennen per Renamer).</summary>
    public Ort? Ort;
    public List<(string Typ, string Name)> Konstruktor = new();
    /// <summary>Je Methode des Dienstes: der Rumpf der Implementierung und der Ort ihrer Methode (Rumpf-Edit).</summary>
    public List<(string Methode, string? Rumpf, Ort? Ort)> Rumpfe = new();
}

public sealed class EnumRaw
{
    public string Name = "", Full = "";
    public List<string> Werte = new();
    public TypMeta Meta = new("", "", null, null, new());
    /// <summary>Hat ein Member den Wert 0 (implizit oder explizit)? Der Proto-Wire verliert 0 → Diagnose.</summary>
    public bool HatNull;
}

public sealed class AggregateRaw
{
    public string Name = "", Namespace = "", Full = "";
    public List<FieldInfo> State = new();
    public string? Doku, StateZusatz, DeciderZusatz, ApplierZusatz;
    /// <summary>Absolute Quelldateien: State-Deklaration, innerer Decider, innerer Applier; je Command/Event die Methode.</summary>
    public string? Datei, DeciderDatei, ApplierDatei;
    public Dictionary<string, string> DecideDateien = new(), ApplyDateien = new();
    /// <summary>Wo Decide(Cmd) bzw. Apply(Evt) steht — Schlüssel ist der einfache Name des Commands bzw. Events.</summary>
    public Dictionary<string, Ort> DecideOrte = new(), ApplyOrte = new();
    /// <summary>Die Decider-/Applier-Typen des Aggregats (FullName) — beim Löschen gehen sie mit.</summary>
    public List<string> TeilTypen = new();
    /// <summary>Wie der Code sie benennt (null = nicht genau eine Klasse); Geschachtelt = innere Klasse des States.</summary>
    public string? DeciderKlasse, ApplierKlasse;
    public bool DeciderGeschachtelt, ApplierGeschachtelt;
    /// <summary>Methodenname je Command- bzw. Event-FullName (wie im Code).</summary>
    public Dictionary<string, string> DecideMethoden = new(StringComparer.Ordinal), ApplyMethoden = new(StringComparer.Ordinal);
    public List<string> Usings = new();
    public List<string> HandlesCommandsFull = new();  // FullNames der entschiedenen Commands (Quelltext-Reihenfolge)
    /// <summary>Ablehnungen (ITransientEvent) je Command — der Sad-Path, den GeneratedCommandRouting bewusst auslässt.</summary>
    public List<(string CmdFull, string EvtFull)> Rejects = new();
    /// <summary>Die OneOf-Ausgänge je Command IN SIGNATUR-REIHENFOLGE (Event-FullNames, Erfolg + Ablehnung).</summary>
    public Dictionary<string, List<string>> DecideOutcomes = new();
    /// <summary>Parametername je Command-FullName (der Rumpf referenziert ihn).</summary>
    public Dictionary<string, string> DecideParams = new();

    /// <summary>Der ECHTE Decide-Rumpf je Command (FullName → dedenteter Body-Text; "" = leer).</summary>
    public Dictionary<string, string> DecideBodies = new();
    /// <summary>Die ECHTEN Apply-Methoden in Quelltext-Reihenfolge (Event-FullName).</summary>
    public List<string> ApplyEventsFull = new();
    /// <summary>Der ECHTE Apply-Rumpf je Event (SimpleName → dedenteter Body-Text; "" = bewusster No-op).</summary>
    public Dictionary<string, string> ApplyBodies = new();
    /// <summary>Parametername je Event-SimpleName.</summary>
    public Dictionary<string, string> ApplyParams = new();
}

/// <summary>
/// Quelltext einer Konsumenten-/Reader-Klasse verbatim (für den verlustfreien Round-trip): Datei, Doku, Form, Basisliste,
/// Attribute, die übrigen Member (Zusatz) und die using-Direktiven der Datei (auch Aliase).
/// </summary>
public sealed class KlassenQuelle
{
    public string? Datei, Doku, Typart, Attribute, Zusatz;
    public List<string> Basen = new(), Usings = new();
    /// <summary>Regel Z: Instanzfelder, die Zustand halten (Symbol-Fakt, siehe <c>Konsument.Zustand</c>); leer = zustandslos.</summary>
    public List<string> Zustand = new();
}

/// <summary>Die Signatur eines Handles verbatim: Parameternamen, Fähigkeits-Parameter, Rückgabe, Modifizierer, Rumpf.</summary>
public sealed class HandleSigRaw
{
    public string Parameter = "";
    public List<string> Kontext = new();
    public List<(string Typ, string Name)> Faehigkeiten = new();
    public string Rueckgabe = "", Modifikatoren = "";
    public string? Rumpf, Ausdruck, Datei;
    public Ort? Ort;
}

public sealed class ProjectionRaw
{
    /// <summary>Die Klasse verbatim (Round-trip) und je Handle (Event-Simple-Name) die Signatur verbatim.</summary>
    public KlassenQuelle Quelle = new();
    public Dictionary<string, HandleSigRaw> HandleSigs = new();
    public string Name = "", Full = "", Namespace = "";
    /// <summary>Der Wert der SubscriberId, wenn er zur Compile-Zeit konstant ist; sonst null (nicht geraten).</summary>
    public string? SubscriberId;
    /// <summary>Geordneter Pull (<c>IPullSubscriber</c>)?</summary>
    public bool Pull;
    /// <summary>Append-artig (<c>IAppendProjektion</c> ⇒ Co-Commit-Pflicht, GA-1)?</summary>
    public bool Append;
    public List<string> ConsumesFull = new();
    /// <summary>Handle-Rumpf je konsumiertem Event (Simple-Name → Body-Text).</summary>
    public Dictionary<string, string> HandleBodies = new();
    /// <summary>Fähigkeits-Parameter je Event (Simple-Name → [(Store, Funktion, Lesen?)]) — aus der SIGNATUR des Handles.</summary>
    public Dictionary<string, List<(string Store, string Method, bool IsRead)>> HandleFaehigkeiten = new();
    /// <summary>Ge-yieldete (veröffentlichte) Events je Event (Simple-Name → Event-Simple-Namen).</summary>
    public Dictionary<string, List<string>> HandlePublishes = new();
    /// <summary>Rückgabe-Vertrag + Ausgänge (mit Guard) je Event (Simple-Name).</summary>
    public Dictionary<string, HandleVertragRaw> HandleVertraege = new();
}

public sealed class QueryRaw
{
    public string Name = "", Full = "";
    public List<FieldInfo> Fields = new();
    public TypMeta Meta = new("", "", null, null, new());
}

public sealed class ReaderRaw
{
    public KlassenQuelle Quelle = new();
    /// <summary>Signatur verbatim je Query (Simple-Name).</summary>
    public Dictionary<string, HandleSigRaw> HandleSigs = new();
    public string Name = "", Full = "", Namespace = "", ProjectionName = "";
    public bool TrackDeps = true;
    public List<string> QueryNames = new();
    /// <summary>Handle-Rumpf je Query (Query-Simple-Name → Body-Text).</summary>
    public Dictionary<string, string> HandleBodies = new();
    /// <summary>Fähigkeits-Parameter je Query (Query-Simple-Name → [(Store, Funktion, Lesen?)]) — aus der SIGNATUR.</summary>
    public Dictionary<string, List<(string Store, string Method, bool IsRead)>> HandleFaehigkeiten = new();
    /// <summary>Die (OneOf-)Responses je Query (Query-Simple-Name → Response-Simple-Namen) aus der Rückgabe-Signatur.</summary>
    public Dictionary<string, List<string>> HandleResponses = new();
    /// <summary>Rückgabe-Vertrag + Ausgänge (mit Guard) je Query (Simple-Name).</summary>
    public Dictionary<string, HandleVertragRaw> HandleVertraege = new();
}

/// <summary>
/// Ein möglicher AUSGANG eines Handlers (Projektion/Reader) — das Gegenstück zum OneOf-Ausgang eines
/// Deciders. <see cref="Art"/> aus dem Marker des Typs (response | command | transient | event | typ) — immer aus der
/// SIGNATUR —, bzw. <c>storefn</c> je Fähigkeits-Parameter (ebenfalls Signatur). Der Rumpf liefert nichts.
/// </summary>
public sealed class AusgangRaw
{
    public string Typ = "", Art = "";
    /// <summary>Voller Typname (Signatur-Ausgänge und Fähigkeiten).</summary>
    public string Full = "";
    /// <summary>Nur bei <c>storefn</c>: der Store (Bündel) der Fähigkeit und ob Lese-Fähigkeit.</summary>
    public string? Store;
    public bool IstLesen;
}

/// <summary>Der Rückgabe-Vertrag eines Handlers: Form, ob die Signatur offen ist (Marker statt konkreter Typen), die Ausgänge.</summary>
public sealed class HandleVertragRaw
{
    /// <summary>nichts (<c>Task</c>/<c>void</c>) · einzeln (<c>Task&lt;T&gt;</c>) · strom (<c>IAsyncEnumerable&lt;T&gt;</c>/<c>IEnumerable&lt;T&gt;</c>).</summary>
    public string Form = "nichts";
    /// <summary>Das Element-Typargument ist ein Interface (z. B. <c>ICommand</c>) — der Vertrag steht nur im Rumpf.</summary>
    public bool SignaturOffen;
    /// <summary>Die Rückgabe-Signatur verbatim (z. B. <c>Task&lt;OneOf&lt;A, B&gt;&gt;</c>).</summary>
    public string Signatur = "";
    public List<AusgangRaw> Ausgaenge = new();
}

/// <summary>Ein Akteur (<c>IAkteur</c>) und was er darf (<c>IDarf&lt;T&gt;</c>, Deklarations-Reihenfolge, einfache Namen).</summary>
public sealed class AkteurRaw
{
    public string Name = "", Namespace = "", Full = "";
    public List<string> Darf = new();
    /// <summary>„Mensch" / „Maschine" / „Ki" aus der Basisliste, sonst null.</summary>
    public string? Art;
    public string? Datei, Doku;
}

/// <summary>Ein Client (<c>IClientVertrag</c>): getragene Funktionen, Sendet/Fragt, Kenntnis — alles aus der Basisliste.</summary>
public sealed class ClientRaw
{
    public string Name = "", Namespace = "", Full = "";
    public List<string> Traegt = new(), Sendet = new(), Fragt = new(), Kenntnis = new();
    public string? Datei, Doku;
}

/// <summary>Eine Store-Funktion (aus dem Store-Interface + Impl-Rumpf).</summary>
public sealed class StoreFnRaw
{
    public string Name = "";
    public bool IsRead;
    public List<(string Name, string Type)> Params = new();
    public string? Return;
    public string? Body;
    /// <summary>Das Fähigkeits-Interface (Simple-Name / FullName), das genau diese Funktion trägt.</summary>
    public string Faehigkeit = "", FaehigkeitFull = "";
    /// <summary>Alle Typen (FullNames), die die Signatur nennt — auch in Typ-Argumenten (für ReadModel → Store).</summary>
    public HashSet<string> TypRefs = new(StringComparer.Ordinal);
    /// <summary>Die Signatur verbatim (Rückgabe, Parameter mit Default) + Ort/Doku der Fähigkeit — für den Round-trip.</summary>
    public string RueckgabeVoll = "Task";
    public List<(string Typ, string Name, string? Standard)> ParamsVoll = new();
    public string? FaehigkeitNamespace, FaehigkeitDatei, FaehigkeitDoku;
    /// <summary>Die Methode der Implementierung, deren Rumpf <see cref="Body"/> ist (Rumpf-Edit, Werkbank P7.4).</summary>
    public Ort? BodyOrt;
}

/// <summary>
/// Ein Store = ein Bündel-Interface mit Marker <c>IStore</c>; seine Funktionen = die Fähigkeits-Interfaces
/// (<c>IWriteStore</c>/<c>IReadStore</c>, je genau eine Funktion), die es erbt. Eine Fähigkeit ohne Bündel ist ihr
/// eigener Store. Name = das Bündel-Interface (frei benannt).
/// </summary>
public sealed class StoreRaw
{
    public string Name = "", Namespace = "";
    /// <summary>FullName des Bündel-Interfaces.</summary>
    public string? BuendelFull;
    public List<StoreFnRaw> Fns = new();
    /// <summary>Die konkreten Implementierungen (FullNames) — Quelle der Fn-Rümpfe (📝-Slots).</summary>
    public List<string> ImplsFull = new();
    /// <summary>Mehr als eine konkrete Klasse implementiert dasselbe Interface → DI-Auflösung mehrdeutig.</summary>
    public List<string> MehrdeutigeImpls = new();
    /// <summary>Datei + Doku des Bündels; false = Fähigkeit ohne Bündel (eigener Store).</summary>
    public string? Datei, Doku;
    public bool IstBuendel = true;
    /// <summary>Die EINZIGE Impl-Klasse (sonst null): Name, Namespace, Datei der Deklaration, Datei der Schreib-/Lese-Methoden.</summary>
    public (string Name, string Ns, string? Datei, string? SchreibDatei, string? LeseDatei)? Impl;
}

/// <summary>
/// Extrahiert das gesamte Domänen-Modell semantisch (über Marker-Interfaces, nicht textuell) — inklusive der
/// Handcode-Anteile, die der Scaffolder für den Round-trip braucht.
/// </summary>
public sealed partial class DomainExtractor
{
    private readonly List<Compilation> _comps;
    private readonly List<INamedTypeSymbol> _types;

    private readonly INamedTypeSymbol? _iState, _iCommand, _iCreation, _iCommandZiel, _iEvent, _iTransient, _frist,
        _iSubscriber, _iPull, _iAppend, _iReader, _iQuery, _iQueryResponse, _iReadModel, _iGeteilt, _iPayload, _iPipelineOutput;


    /// <summary>Die Domänen-Assemblies (abgeleitet aus der <see cref="Projektlage"/>).</summary>
    private readonly HashSet<string> _domänen;
    /// <summary>Die globalen usings der Domänen-Compilations (ImplicitUsings + global using) + der Vertrags-Namespace — nie „explizit".</summary>
    private readonly HashSet<string> _impliziteUsings;
    private readonly INamedTypeSymbol? _iDecider, _iApplier, _iAggEnvelope,
        _iWriteStore, _iReadStore, _iStore, _iWertobjekt, _iAkteur, _iDarf, _iHoert, _iTraegt,
        _iClient, _iSendet, _iFragt, _iDienst;
    /// <summary>State-FullName → die Typen, die <c>IDecider&lt;State&gt;</c> bzw. <c>IApplier&lt;State&gt;</c> implementieren (egal wo deklariert).</summary>
    private readonly Dictionary<string, List<INamedTypeSymbol>> _deciderJeState = new(StringComparer.Ordinal), _applierJeState = new(StringComparer.Ordinal);

    /// <summary>Welche Syntaxbäume handgeschrieben sind (je Solution-Stand).</summary>
    private readonly Quelltexte _quelltexte;

    public DomainExtractor(IEnumerable<Compilation> comps, ISet<string> domänenAssemblies, Quelltexte quelltexte)
    {
        _quelltexte = quelltexte;
        _comps = comps.ToList();
        _types = Sym.AllConcreteTypes(_comps);
        _domänen = new HashSet<string>(domänenAssemblies, StringComparer.Ordinal);

        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iState = Get(Vertrag.IState);
        _iCommand = Get(Vertrag.ICommand);
        _iCreation = Get(Vertrag.ICreationCommand);
        _iCommandZiel = Get(Vertrag.ICommandZiel);
        _iEvent = Get(Vertrag.IEvent);
        _iTransient = Get(Vertrag.ITransientEvent);
        _frist = Get(Vertrag.Frist);
        _iSubscriber = Get(Vertrag.ISubscriber);
        _iPull = Get(Vertrag.IPullSubscriber);
        _iAppend = Get(Vertrag.IAppendProjektion);
        _iReader = Get(Vertrag.IReader);
        _iQuery = Get(Vertrag.IQuery);
        _iQueryResponse = Get(Vertrag.IQueryResponse);
        _iReadModel = Get(Vertrag.IReadModel);
        _iGeteilt = Get(Vertrag.IGeteiltesReadModel);
        _iPayload = Get(Vertrag.IMessagePayload);
        _iPipelineOutput = Get(Vertrag.IAusgabe);
        _iDecider = Get(Vertrag.IDecider);
        _iApplier = Get(Vertrag.IApplier);
        _iAggEnvelope = Get(Vertrag.IAggregateEnvelope);
        _iWriteStore = Get(Vertrag.IWriteStore);
        _iReadStore = Get(Vertrag.IReadStore);
        _iStore = Get(Vertrag.IStore);
        _iAkteur = Get(Vertrag.IAkteur);
        _iHoert = Get(Vertrag.IHoert);
        _iTraegt = Get(Vertrag.ITraegt);
        _iClient = Get(Vertrag.IClientVertrag);
        _iSendet = Get(Vertrag.ISendet);
        _iFragt = Get(Vertrag.IFragt);
        _iDarf = Get(Vertrag.IDarf);
        _iWertobjekt = Get(Vertrag.IWertobjekt);
        _iDienst = Get(Abstractions.Dienstvertrag.Marker);

        // Aggregat-Komposition über den TYP: wer IDecider<T>/IApplier<T> implementiert, gehört zum State T.
        foreach (var t in _types)
            foreach (var i in t.AllInterfaces)
            {
                var def = i.OriginalDefinition.Fq();
                var ziel = def == _iDecider?.Fq() ? _deciderJeState : def == _iApplier?.Fq() ? _applierJeState : null;
                if (ziel == null || i.TypeArguments.Length != 1) continue;
                var state = i.TypeArguments[0].Fq();
                if (!ziel.TryGetValue(state, out var liste)) ziel[state] = liste = new();
                if (!liste.Any(x => x.Fq() == t.Fq())) liste.Add(t);
            }

        _impliziteUsings = new HashSet<string>(StringComparer.Ordinal) { Vertrag.VertragsNamespace };
        foreach (var c in _comps.Where(c => _domänen.Contains(c.AssemblyName ?? "")))
            foreach (var t in c.SyntaxTrees)
                foreach (var u in t.GetRoot().DescendantNodes(n => n is CompilationUnitSyntax).OfType<UsingDirectiveSyntax>())
                    if (u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && u.Alias == null && u.Name != null)
                        _impliziteUsings.Add(u.Name.ToString().Replace("global::", ""));
    }

    private bool IstDomänenAssembly(IAssemblySymbol? a) => a != null && _domänen.Contains(a.Name);
    private static bool Implementiert(INamedTypeSymbol t, INamedTypeSymbol? generischesIface) =>
        generischesIface != null && t.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == generischesIface.ToDisplayString());

    private SemanticModel Model(SyntaxTree tree) =>
        _comps.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    public DomainModel Extract()
    {
        var m = new DomainModel();
        if (_comps.Select(c => c.GetTypeByMetadataName(Abstractions.Treibervertrag.DateiBasis)).FirstOrDefault(x => x != null) is { } dateiBasis)
            m.Treiber[Abstractions.Treibervertrag.Datei] = (dateiBasis.Name, GeerbteFelder(dateiBasis, mitSelbst: true));

        CatalogEventsAndCommands(m);
        // Stores ZUERST: die Konsumenten-Handler lösen ihre Fähigkeits-Parameter gegen die erkannten Fähigkeiten auf.
        ExtractStores(m);
        foreach (var t in _types)
        {
            if (Sym.Implements(t, _iState) && _deciderJeState.ContainsKey(t.Fq())) m.Aggregates.Add(ReadAggregate(t));
            if (Sym.Implements(t, _iSubscriber)) { var p = TryReadProjection(t); if (p != null) m.Projections.Add(p); }
            if (Sym.Implements(t, _iQuery)) m.Queries.Add(ReadQuery(t));
            var reader = TryReadReader(t);
            if (reader != null) m.Readers.Add(reader);
        }
        // Der Schreiber-Typ der Projektions-Handles (CQRS057: 3. Parameter) — aus dem Code, nur wenn eindeutig.
        var schreiber = _types.Where(t => Sym.Implements(t, _iSubscriber)).SelectMany(OeffentlicheMethoden)
            .Where(mm => mm.Parameters.Length >= 3 && Sym.Implements(mm.Parameters[0].Type, _iEvent)
                         && mm.Parameters[1].Type.ToDisplayString() == _iAggEnvelope?.ToDisplayString())
            .Select(mm => mm.Parameters[2].Type).GroupBy(x => x.Fq()).Select(g => g.First()).ToList();
        if (schreiber.Count == 1) { m.ProjektionsSchreiber = schreiber[0].Name; m.ProjektionsSchreiberNamespace = schreiber[0].ContainingNamespace.Fq(); }

        CatalogDomainTypes(m);
        LinkDienstImpls(m);
        SeparateKonfigs(m);
        LinkReadModelStores(m);

        LeseRahmen(m);
        LeseUnaufgeloeste(m);
        foreach (var t in DomainQuellTypen())
        {
            m.TypOrte[t.Fq()] = QuellDeklarationen(t).Select(Ort.Von).ToList();
            if (t.ContainingType == null)
                foreach (var d in QuellDeklarationen(t).OfType<BaseTypeDeclarationSyntax>())
                    m.Deklarationen.Add(new TypDeklaration(t.Name, t.ContainingNamespace.Fq(), Form(d), Ort.Von(d), d.ToString()));
        }

        // Deterministische Reihenfolge (die Symbol-Enumeration ist es nicht garantiert).
        m.Aggregates.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        return m;
    }

    /// <summary>
    /// Der Rahmen aus dem Code: Verzeichnis je Namespace (nur wenn EINDEUTIG — alle Typen des Namespace liegen in
    /// genau einem Verzeichnis; sonst kein Eintrag statt einer Mehrheits-Schätzung) und die globalen usings.
    /// Die Klassen-/Methodennamen der Aggregate sind Vertrag (<see cref="Vertrag.DeciderKlasse"/> …), nicht geschätzt.
    /// </summary>
    private void LeseRahmen(DomainModel m)
    {
        m.DeciderKlasse = Vertrag.DeciderKlasse;
        m.ApplierKlasse = Vertrag.ApplierKlasse;
        m.DecideMethode = Vertrag.DecideMethode;
        m.ApplyMethode = Vertrag.ApplyMethode;

        foreach (var g in DomainQuellTypen()
                     .SelectMany(t => QuellDeklarationen(t).Select(d => (Ns: t.ContainingNamespace.Fq(), Dir: Path.GetDirectoryName(d.SyntaxTree.FilePath) ?? "")))
                     .GroupBy(x => x.Ns))
        {
            var dirs = g.Select(x => x.Dir).Distinct(StringComparer.Ordinal).ToList();
            if (dirs.Count == 1) m.NamespaceVerzeichnisse[g.Key] = dirs[0];
        }

        m.GlobaleUsings.AddRange(_impliziteUsings.Where(u => u != Vertrag.VertragsNamespace).OrderBy(u => u, StringComparer.Ordinal));

        // Stelligkeiten des Vertrags: die Compilation fragen, welche generischen Varianten es gibt.
        int Max(string ns, string name)
        {
            var n = 0;
            for (var k = 1; k <= 32; k++)
                if (_comps.Any(c => c.GetTypeByMetadataName($"{ns}.{name}`{k}") != null)) n = k;
            return n;
        }
        m.OneOfMax = Max(typeof(Abstractions.OneOf<>).Namespace!, Vertrag.OneOf);
    }

    // ── Domänen-Typen ohne Nachrichten-Rolle: Value Objects, Responses, ReadModels, Enums ──

    /// <summary>
    /// Code mit Fehlern (Werkbank P7.1): ein handgeschriebener Domänen-Typ, dessen Basisliste nicht aufgelöst ist, darf nicht still
    /// verschwinden (ohne Basis erkennt ihn kein Marker). Er wird festgehalten — als Code-Karte mit ⚠ bzw. als Befund an seiner Karte.
    /// </summary>
    private void LeseUnaufgeloeste(DomainModel m)
    {
        foreach (var t in DomainQuellTypen())
        {
            var decl = QuellDeklarationen(t).OfType<BaseTypeDeclarationSyntax>().FirstOrDefault(d => d.BaseList != null);
            if (decl?.BaseList == null) continue;
            var model = Model(decl.SyntaxTree);
            var offen = decl.BaseList.Types
                .Where(b => model.GetTypeInfo(b.Type).Type is not { } bt || bt.TypeKind == TypeKind.Error)
                .Select(b => b.Type.ToString()).ToList();
            if (offen.Count == 0) continue;
            var ort = decl.GetLocation().GetLineSpan();
            m.Unaufgeloest.Add(new UnaufgeloestRaw
            {
                Name = t.Name, Namespace = t.ContainingNamespace.Fq(), Datei = decl.SyntaxTree.FilePath, Zeile = ort.StartLinePosition.Line + 1,
                Art = Form(decl),
                Code = decl.ToString(), Basen = offen, Start = decl.Span.Start, Ende = decl.Span.End,
            });
        }
        m.Unaufgeloest.Sort((a, b) => string.CompareOrdinal(a.Namespace + "." + a.Name, b.Namespace + "." + b.Name));
    }

    /// <summary>Die Deklarationsform als Wort (für die Code-Karte).</summary>
    private static string Form(BaseTypeDeclarationSyntax decl) => decl switch
    {
        RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
        InterfaceDeclarationSyntax => "interface", StructDeclarationSyntax => "struct", EnumDeclarationSyntax => "enum", _ => "class",
    };

    /// <summary>Steht in der Basisliste etwas, das der Compiler nicht auflöst (Tippfehler, gelöschter Typ, fehlendes using)?</summary>
    private static bool HatUnaufgeloesteBasis(INamedTypeSymbol t) =>
        t.BaseType?.TypeKind == TypeKind.Error || t.Interfaces.Any(i => i.TypeKind == TypeKind.Error);

    /// <summary>Quelltext-Typen der Domain*-Assemblies (nicht Framework, nicht Metadaten, nicht generiert).</summary>
    private IEnumerable<INamedTypeSymbol> DomainQuellTypen()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var comp in _comps.Where(c => _domänen.Contains(c.AssemblyName ?? "")))
            foreach (var t in AlleTypen(comp.Assembly.GlobalNamespace))
                if (t.DeclaringSyntaxReferences.Any(r => !IstGeneriert(r.SyntaxTree)) && seen.Add(t.Fq()))
                    yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> AlleTypen(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
            foreach (var x in MitGeschachtelten(t)) yield return x;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in AlleTypen(sub)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
    {
        yield return t;
        foreach (var n in t.GetTypeMembers())
            foreach (var x in MitGeschachtelten(n)) yield return x;
    }

    private void CatalogDomainTypes(DomainModel m)
    {
        foreach (var t in DomainQuellTypen())
        {
            if (t.TypeKind == TypeKind.Enum)
            {
                m.Enums.Add(ReadEnum(t));
                continue;
            }
            // Funktion (Auf): Klasse im Host oder Schnittstelle draußen — nur ihre Signatur zählt.
            if (Infrastructure.SourceGeneration.Funktionsform.IstBaustein(t)) { m.BausteinFunktionen.Add(ReadBausteinFunktion(t)); continue; }
            // Domänen-Dienst (konzept-werkbank §12.3 E11): ein Interface mit IDienst — Signatur; die Implementierungen hängt LinkDienstImpls an.
            if (t.TypeKind == TypeKind.Interface && _iDienst != null && Sym.Implements(t, _iDienst)) { m.Dienste.Add(ReadDienst(t)); continue; }
            // Client-Vertrag (IClientVertrag): die Software an der Leitung.
            if (t.TypeKind == TypeKind.Interface && IstClient(t)) { m.Clients.Add(ReadClient(t)); continue; }
            // Geschachtelte Typen gehören zum Handcode ihres Containers (Zusatz), keine eigenen Bausteine.
            if (t.ContainingType != null || t.TypeKind is not (TypeKind.Class or TypeKind.Struct) || t.IsStatic || t.IsAbstract) continue;

            var raw = new RecordRaw { Name = t.Name, Full = t.Fq(), Fields = Felder(t), Meta = Meta(t) };
            if (_iAkteur != null && Sym.Implements(t, _iAkteur)) m.Akteure.Add(ReadAkteur(t));
            else if (Sym.Implements(t, _iQueryResponse)) m.Responses.Add(raw);
            else if (Sym.Implements(t, _iReadModel)) { raw.Geteilt = _iGeteilt != null && Sym.Implements(t, _iGeteilt); m.ReadModels.Add(raw); }
            // Value Object = als Wertobjekt markiert ODER ein Record (Datenträger per Sprachkonstrukt) ohne jede Rolle.
            // Klassen ohne Marker sind Dienste/Helfer (z. B. Store-Implementierungen), keine Werte. Ein Record mit UNAUFGELÖSTER Basis
            //   ist kein Wert, sondern unbekannt (die fehlende Basis kann seine Rolle sein) — er wird eine Code-Karte (LeseUnaufgeloeste).
            else if (!HatUnaufgeloesteBasis(t) && (Sym.Implements(t, _iWertobjekt) || t.IsRecord)
                     && !Sym.Implements(t, _iPayload) && !Sym.Implements(t, _iPipelineOutput) && !Sym.Implements(t, _iQuery)
                     && !Sym.Implements(t, _iState)
                     && !Sym.Implements(t, _iWriteStore) && !Sym.Implements(t, _iReadStore))
                m.ValueObjects.Add(raw);
        }
        m.Enums.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.Akteure.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.Clients.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.ValueObjects.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.Responses.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.ReadModels.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.BausteinFunktionen.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
        m.Dienste.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
    }

    /// <summary>
    /// Je Domänen-Dienst seine Implementierungen: jede konkrete Klasse der Domänen-Quellen, die das Interface implementiert — mit Konstruktor und
    /// je Interface-Methode dem Rumpf der Implementierung (Ort für den Rumpf-Edit).
    /// </summary>
    private void LinkDienstImpls(DomainModel m)
    {
        if (m.Dienste.Count == 0) return;
        var klassen = DomainQuellTypen().Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract).OrderBy(t => t.Fq(), StringComparer.Ordinal).ToList();
        foreach (var d in m.Dienste)
            foreach (var impl in klassen.Where(k => k.AllInterfaces.Any(i => i.Fq() == d.Full)))
            {
                var iface = impl.AllInterfaces.First(i => i.Fq() == d.Full);
                var raw = new DienstImplRaw
                {
                    Name = impl.Name, Full = impl.Fq(), Konstruktor = Konstruktor(impl),
                    Datei = QuellDeklarationen(impl).FirstOrDefault()?.SyntaxTree.FilePath,
                    Ort = QuellDeklarationen(impl).OfType<BaseTypeDeclarationSyntax>().FirstOrDefault() is { } idecl ? Ort.Von(idecl) : null,
                };
                foreach (var me in iface.GetMembers().OfType<IMethodSymbol>().Where(x => x.MethodKind == MethodKind.Ordinary))
                {
                    var im = Sym.Implementierung(impl, me) as IMethodSymbol;
                    var syn = im?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
                    raw.Rumpfe.Add((me.Name, im == null ? null : MethodBody(im), syn == null ? null : Ort.Von(syn)));
                }
                d.Impls.Add(raw);
            }
    }

    /// <summary>
    /// Der Konstruktor einer Implementierung, wie die generierte Bindung ihn ruft (der öffentliche mit den meisten Parametern; Primär-Konstruktor
    /// eingeschlossen) — Typ so, wie er im Code steht.
    /// </summary>
    private static List<(string Typ, string Name)> Konstruktor(INamedTypeSymbol t)
    {
        var ctor = t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).OrderByDescending(c => c.Parameters.Length).FirstOrDefault();
        if (ctor == null) return new();
        return ctor.Parameters.Select(p => (
            (p.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as ParameterSyntax)?.Type?.ToString() ?? ShortType(p.Type), p.Name)).ToList();
    }

    /// <summary>Die Signatur eines Dienstes: seine Methoden (Deklarations-Reihenfolge), Typen wie geschrieben.</summary>
    private DienstRaw ReadDienst(INamedTypeSymbol t)
    {
        var decl = QuellDeklarationen(t).FirstOrDefault();
        var d = new DienstRaw
        {
            Name = t.Name, Full = t.Fq(), Namespace = t.ContainingNamespace.Fq(),
            Doku = decl == null ? null : Summary(decl), Datei = decl?.SyntaxTree.FilePath,
        };
        foreach (var me in t.GetMembers().OfType<IMethodSymbol>().Where(x => x.MethodKind == MethodKind.Ordinary)
                     .OrderBy(x => x.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0))
        {
            var syn = me.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<MethodDeclarationSyntax>().FirstOrDefault();
            d.Methoden.Add(new DienstMethodeRaw
            {
                Name = me.Name, Rueckgabe = syn?.ReturnType.ToString() ?? ShortType(me.ReturnType),
                Parameter = me.Parameters.Select((p, i) => (syn?.ParameterList.Parameters.ElementAtOrDefault(i)?.Type?.ToString() ?? ShortType(p.Type), p.Name)).ToList(),
                Doku = syn == null ? null : Summary(syn), Ort = syn == null ? null : Ort.Von(syn),
            });
        }
        return d;
    }

    /// <summary>Die Signatur einer Funktion (Funktionsform: dieselbe Lesart wie der FunktionsBausteinGenerator).</summary>
    private BausteinFunktionRaw ReadBausteinFunktion(INamedTypeSymbol t)
    {
        var form = Infrastructure.SourceGeneration.Funktionsform.Lies(t);
        var decl = QuellDeklarationen(t).FirstOrDefault();
        string Geschrieben(IMethodSymbol m, int i, ITypeSymbol typ) =>
            m.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<MethodDeclarationSyntax>().FirstOrDefault()
                ?.ParameterList.Parameters.ElementAtOrDefault(i)?.Type?.ToString() ?? typ.Name;
        var f = new BausteinFunktionRaw
        {
            Name = t.Name, Full = t.Fq(), Namespace = t.ContainingNamespace.Fq(), Klasse = !form.Draussen,
            Doku = decl == null ? null : Summary(decl), Datei = decl?.SyntaxTree.FilePath,
        };
        foreach (var a in form.Aufs)
        {
            var e = new BausteinEingangRaw
            {
                EingangFull = a.Eingang.Fq(), Hinweis = a.Hinweis, Herkunft = a.MitHerkunft, Strom = a.Strom,
                AusgabenFull = a.Ausgaben.Select(x => x.Fq()).ToList(),
            };
            for (var i = 0; i < a.Methode.Parameters.Length; i++)
                if (a.Parameter[i].Art == "faehigkeit") e.Faehigkeiten.Add((Geschrieben(a.Methode, i, a.Methode.Parameters[i].Type), a.Methode.Parameters[i].Name));
            f.Eingaenge.Add(e);
        }
        if (!form.Draussen) f.Konstruktor = KonstruktorWieGeschrieben(t);
        if (form.Draussen)
        {
            var umsetzungen = _types.Where(c => c.TypeKind == TypeKind.Class && !c.IsAbstract && c.AllInterfaces.Contains(t, SymbolEqualityComparer.Default))
                .OrderBy(c => c.Fq(), StringComparer.Ordinal).ToList();
            f.UmsetzungenFull = umsetzungen.Select(c => c.Fq()).ToList();
            foreach (var c in umsetzungen) f.UmsetzungsKonstruktoren[c.Fq()] = KonstruktorWieGeschrieben(c);
        }
        return f;
    }

    /// <summary>Der bindende Konstruktor einer Klasse (Ctorbindung, wie die Laufzeit ihn wählt): Typ wie geschrieben und Name je Parameter.</summary>
    private static List<(string Typ, string Name)> KonstruktorWieGeschrieben(INamedTypeSymbol t)
    {
        if (Infrastructure.SourceGeneration.Ctorbindung.Konstruktor(t) is not { } k) return new();
        var ks = k.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).FirstOrDefault();
        var ps = (ks as ConstructorDeclarationSyntax)?.ParameterList.Parameters ?? (ks as TypeDeclarationSyntax)?.ParameterList?.Parameters;
        return k.Parameters.Select((p, i) => (ps?.ElementAtOrDefault(i)?.Type?.ToString() ?? p.Type.Name, p.Name)).ToList();
    }

    private EnumRaw ReadEnum(INamedTypeSymbol t)
    {
        var e = new EnumRaw { Name = t.Name, Full = t.Fq(), Meta = Meta(t, mitZusatz: false) };
        var decl = t.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<EnumDeclarationSyntax>().FirstOrDefault();
        foreach (var f in t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue))
        {
            var syn = decl?.Members.FirstOrDefault(x => x.Identifier.Text == f.Name);
            e.Werte.Add(syn != null ? Codeformen.Feldregeln.EnumWert(syn) : f.Name);
            if (Convert.ToInt64(f.ConstantValue) == 0) e.HatNull = true;
        }
        return e;
    }

    /// <summary>
    /// ReadModel → Store: der Store, dessen Fähigkeits-Signaturen den Typ nennen — nur Signatur, nie die Implementierung.
    /// </summary>
    private void LinkReadModelStores(DomainModel m)
    {
        foreach (var rm in m.ReadModels)
        {
            // Eindeutig oder gar nicht: nennen mehrere Stores den Typ, wird NICHT geraten (→ Diagnose im Graph).
            var kandidaten = m.Stores.Where(s => s.Fns.Any(f => f.TypRefs.Contains(rm.Full))).ToList();
            if (kandidaten.Count == 1) rm.Store = kandidaten[0].Name;
            else if (kandidaten.Count > 1) rm.StoreKandidaten = kandidaten.Select(s => s.Name).ToList();
        }
    }

    /// <summary>
    /// Konfigurations-Records von Value Objects trennen: ein Record, der im Konstruktor einer Funktion, Dienst-Implementierung,
    /// Projektion, eines Readers oder einer Store-Impl steht, ist per DI injizierte Konfiguration — kein Fachwert.
    /// </summary>
    private void SeparateKonfigs(DomainModel m)
    {
        var voByFull = m.ValueObjects.GroupBy(v => v.Full, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var storeImpls = m.Stores.SelectMany(s => s.ImplsFull).ToHashSet(StringComparer.Ordinal);
        // Funktionen und Dienst-Implementierungen (konzept-werkbank §12.3 E11): ihre Konstruktor-Records sind Konfig aus Konfig:<Record> —
        //   bei einer Funktion die Klasse selbst bzw. die Umsetzungen der Schnittstelle.
        storeImpls.UnionWith(m.BausteinFunktionen.Where(f => f.Klasse).Select(f => f.Full));
        storeImpls.UnionWith(m.BausteinFunktionen.SelectMany(f => f.UmsetzungenFull));
        storeImpls.UnionWith(m.Dienste.SelectMany(d => d.Impls).Select(i => i.Full));
        var injiziert = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in _types)
        {
            var istKonsument = Sym.Implements(t, _iSubscriber)
                || t.AllInterfaces.Any(i => _iReader != null && i.OriginalDefinition.Fq() == _iReader.Fq())
                || storeImpls.Contains(t.Fq());
            if (!istKonsument) continue;
            foreach (var ctor in t.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public))
                foreach (var p in ctor.Parameters)
                {
                    if (p.Type is not INamedTypeSymbol pt || !voByFull.ContainsKey(pt.Fq())) continue;
                    injiziert.Add(pt.Fq());
                    if (!m.KonfigNutzer.TryGetValue(pt.Name, out var nutzer)) m.KonfigNutzer[pt.Name] = nutzer = new();
                    if (!nutzer.Contains(t.Name)) nutzer.Add(t.Name);
                }
        }
        // Genau die injizierten Typen (per FullName) — nicht alles, was zufällig gleich heißt.
        foreach (var vo in m.ValueObjects.Where(v => injiziert.Contains(v.Full)).ToList())
        {
            m.ValueObjects.Remove(vo);
            m.Konfigs.Add(vo);
        }
        m.Konfigs.Sort((a, b) => string.CompareOrdinal(a.Full, b.Full));
    }

    private bool IstClient(INamedTypeSymbol t) => _iClient != null && Sym.Implements(t, _iClient);

    /// <summary>
    /// Ein Client-Vertrag — nur Code-Fakten der Basisliste: <c>ITraegt&lt;F&gt;</c> (→ <c>Traegt</c>), <c>ISendet&lt;T&gt;</c>/<c>IFragt&lt;T&gt;</c>,
    /// <c>IHoert&lt;T&gt;</c> (→ Kenntnis). Geerbte Client-Verträge liest der Editor nicht auf.
    /// </summary>
    private ClientRaw ReadClient(INamedTypeSymbol t)
    {
        var decl = QuellDeklarationen(t).FirstOrDefault();
        bool Gen(INamedTypeSymbol i, INamedTypeSymbol? g) => g != null && i.OriginalDefinition.Fq() == g.Fq() && i.TypeArguments.Length == 1;
        return new ClientRaw
        {
            Name = t.Name, Namespace = t.ContainingNamespace.Fq(), Full = t.Fq(),
            Traegt = t.Interfaces.Where(i => Gen(i, _iTraegt)).Select(i => i.TypeArguments[0].Name).ToList(),
            Sendet = t.Interfaces.Where(i => Gen(i, _iSendet)).Select(i => i.TypeArguments[0].Name).ToList(),
            Fragt = t.Interfaces.Where(i => Gen(i, _iFragt)).Select(i => i.TypeArguments[0].Name).ToList(),
            Kenntnis = t.Interfaces.Where(i => Gen(i, _iHoert)).Select(i => i.TypeArguments[0].Name).ToList(),
            Datei = decl?.SyntaxTree.FilePath, Doku = decl == null ? null : Summary(decl),
        };
    }

    // ── Akteure: IAkteur + IDarf<T> in der Basisliste (Code-Fakt; Reihenfolge = Deklaration, Geerbtes danach) ───────
    private AkteurRaw ReadAkteur(INamedTypeSymbol t)
    {
        var decl = QuellDeklarationen(t).FirstOrDefault();
        bool IstDarf(INamedTypeSymbol i) => _iDarf != null && i.OriginalDefinition.Fq() == _iDarf.Fq() && i.TypeArguments.Length == 1;
        var darf = t.Interfaces.Where(IstDarf).Concat(t.AllInterfaces.Where(IstDarf))
            .Select(i => i.TypeArguments[0].Name).Distinct(StringComparer.Ordinal).ToList();
        var art = t.AllInterfaces.Select(i => i.Fq()).ToList() is var basen
            ? basen.Contains(Vertrag.IMensch) ? "Mensch" : basen.Contains(Vertrag.IMaschine) ? "Maschine" : basen.Contains(Vertrag.IKi) ? "Ki" : null
            : null;
        return new AkteurRaw { Name = t.Name, Namespace = t.ContainingNamespace.Fq(), Full = t.Fq(), Darf = darf, Art = art,
            Datei = decl?.SyntaxTree.FilePath, Doku = decl == null ? null : Summary(decl) };
    }

    // ── Stores: Fähigkeiten (IWriteStore/IReadStore, je EINE Funktion) + Bündel (IStore) — Namen frei ──────────────

    /// <summary>Fähigkeits-Interface (FullName) → (Store, Funktion, Lesen?).</summary>
    private readonly Dictionary<string, (string Store, string Method, bool IsRead)> _faehigkeiten = new(StringComparer.Ordinal);

    private bool IstFaehigkeit(ITypeSymbol t) => t is INamedTypeSymbol { TypeKind: TypeKind.Interface } i
        && (Sym.Implements(i, _iWriteStore) || Sym.Implements(i, _iReadStore)) && !Sym.Implements(i, _iStore);

    private Dictionary<string, List<INamedTypeSymbol>>? _implsJeInterface;

    /// <summary>
    /// Die konkreten Typen der analysierten Quell-Assemblies, die ein Interface implementieren (Index über alle Interfaces, einmal je Lesen) —
    /// statt je Fähigkeit über alle Typen samt Metadaten zu laufen.
    /// </summary>
    private List<INamedTypeSymbol> ImplsVon(INamedTypeSymbol iface)
    {
        if (_implsJeInterface == null)
        {
            _implsJeInterface = new(StringComparer.Ordinal);
            var quellen = _comps.Select(c => c.AssemblyName ?? "").ToHashSet(StringComparer.Ordinal);
            foreach (var t in _types.Where(t => t.TypeKind is TypeKind.Class or TypeKind.Struct && !t.IsAbstract && quellen.Contains(t.ContainingAssembly?.Name ?? "")))
                foreach (var i in t.AllInterfaces.Select(i => i.Fq()).Distinct(StringComparer.Ordinal))
                {
                    if (!_implsJeInterface.TryGetValue(i, out var l)) _implsJeInterface[i] = l = new();
                    l.Add(t);
                }
        }
        return _implsJeInterface.GetValueOrDefault(iface.Fq()) ?? [];
    }

    private void ExtractStores(DomainModel m)
    {
        var faehigkeiten = DomainQuellTypen().Where(IstFaehigkeit).ToList();
        var buendel = DomainQuellTypen().Where(t => t.TypeKind == TypeKind.Interface && Sym.Implements(t, _iStore)).ToList();
        var stores = new List<StoreRaw>();
        var implsJeFaehigkeit = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Nimm(StoreRaw store, INamedTypeSymbol f)
        {
            if (_faehigkeiten.ContainsKey(f.Fq())) return;   // schon in einem (anderen) Bündel
            var isRead = Sym.Implements(f, _iReadStore);
            var impls = ImplsVon(f);
            implsJeFaehigkeit[f.Fq()] = impls.Select(x => x.Name).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var impl in impls) if (!store.ImplsFull.Contains(impl.Fq())) store.ImplsFull.Add(impl.Fq());
            // Genau EINE Funktion je Fähigkeit (CQRS051); mehrere wären ein Analyzer-Fehler — hier: jede wird eine Fn.
            foreach (var im in f.GetMembers().OfType<IMethodSymbol>().Where(x => x.MethodKind == MethodKind.Ordinary))
            {
                var fn = new StoreFnRaw
                {
                    Name = im.Name, IsRead = isRead, Faehigkeit = f.Name, FaehigkeitFull = f.Fq(),
                    Params = im.Parameters.Select(p => (p.Name, ShortType(p.Type))).ToList(),
                    Return = isRead ? UnwrapTask(im.ReturnType) : null,
                };
                foreach (var tr in im.Parameters.Select(p => p.Type).Append(im.ReturnType).SelectMany(TypUndArgumente)) fn.TypRefs.Add(tr);
                // Verbatim aus der Deklaration (Rückgabe/Parameter wie geschrieben) — der Scaffolder schreibt sie genau so zurück.
                if (im.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax ms)
                {
                    fn.RueckgabeVoll = ms.ReturnType.ToString();
                    fn.ParamsVoll = ms.ParameterList.Parameters.Select(p => (p.Type?.ToString() ?? "", p.Identifier.Text, p.Default?.Value.ToString())).ToList();
                }
                var fDecl = QuellDeklarationen(f).FirstOrDefault();
                fn.FaehigkeitNamespace = f.ContainingNamespace.Fq();
                fn.FaehigkeitDatei = fDecl?.SyntaxTree.FilePath;
                fn.FaehigkeitDoku = fDecl == null ? null : Summary(fDecl);
                foreach (var impl in impls)
                    if ((fn.Body == null || IstRegistriert(impl))
                        && Sym.Implementierung(impl, im) is IMethodSymbol implM && MethodBody(implM) is { } body)
                    {
                        fn.Body = body;
                        fn.BodyOrt = implM.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax bd ? Ort.Von(bd) : null;
                    }
                store.Fns.Add(fn);
                _faehigkeiten[f.Fq()] = (store.Name, im.Name, isRead);
            }
        }

        foreach (var b in buendel.OrderBy(x => x.Fq(), StringComparer.Ordinal))
        {
            var bDecl = QuellDeklarationen(b).FirstOrDefault();
            var store = new StoreRaw { Name = b.Name, Namespace = b.ContainingNamespace.Fq(), BuendelFull = b.Fq(),
                Datei = bDecl?.SyntaxTree.FilePath, Doku = bDecl == null ? null : Summary(bDecl) };
            // Reihenfolge = Deklarations-Reihenfolge der Basisliste (Code-Fakt), nicht alphabetisch.
            foreach (var f in b.Interfaces.Where(IstFaehigkeit).Concat(b.AllInterfaces.Where(IstFaehigkeit)))
                Nimm(store, f);
            stores.Add(store);
        }
        foreach (var f in faehigkeiten.Where(f => !_faehigkeiten.ContainsKey(f.Fq())))
        {
            var fDecl = QuellDeklarationen(f).FirstOrDefault();
            var store = new StoreRaw { Name = f.Name, Namespace = f.ContainingNamespace.Fq(), BuendelFull = f.Fq(), IstBuendel = false,
                Datei = fDecl?.SyntaxTree.FilePath };
            Nimm(store, f);
            stores.Add(store);
        }
        foreach (var (f, impls) in implsJeFaehigkeit)
            if (impls.Count > 1 && _faehigkeiten.TryGetValue(f, out var z))
                stores.First(s => s.Name == z.Store).MehrdeutigeImpls.Add($"{f[(f.LastIndexOf('.') + 1)..]}: {string.Join(", ", impls)}");
        m.Stores.AddRange(stores.OrderBy(s => s.Name, StringComparer.Ordinal));
        LeseStoreImpls(m);
    }

    /// <summary>
    /// Je Store die EINZIGE Impl-Klasse (mehrere/keine ⇒ null, nichts geraten) und wo ihre Teile liegen: Deklaration mit
    /// Basisliste, Datei der Schreib- bzw. Lese-Methoden (nur wenn eindeutig). Dazu die Rahmen-Fakten für NEUE Impls:
    /// gemeinsame Basisklasse und gemeinsamer Namespace aller Impls — nur wenn eindeutig.
    /// </summary>
    private void LeseStoreImpls(DomainModel m)
    {
        var impls = new List<INamedTypeSymbol>();
        foreach (var s in m.Stores)
        {
            if (s.ImplsFull.Count != 1) continue;
            var impl = _types.FirstOrDefault(t => t.Fq() == s.ImplsFull[0]);
            if (impl == null || !IstDomänenAssembly(impl.ContainingAssembly)) continue;
            impls.Add(impl);
            var decls = QuellDeklarationen(impl).OfType<TypeDeclarationSyntax>().ToList();
            string? Datei(bool lesen)
            {
                var dateien = s.Fns.Where(f => f.IsRead == lesen)
                    .Select(f => _comps.Select(c => c.GetTypeByMetadataName(f.FaehigkeitFull)).FirstOrDefault(x => x != null)?.GetMembers(f.Name).OfType<IMethodSymbol>().FirstOrDefault())
                    .Where(x => x != null).Select(x => Sym.Implementierung(impl, x!)?.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath)
                    .Where(x => x != null).Distinct(StringComparer.Ordinal).ToList();
                return dateien.Count == 1 ? dateien[0] : null;
            }
            s.Impl = (impl.Name, impl.ContainingNamespace.Fq(), (decls.FirstOrDefault(d => d.BaseList != null) ?? decls.FirstOrDefault())?.SyntaxTree.FilePath,
                Datei(false), Datei(true));
        }
        var nss = impls.Select(i => i.ContainingNamespace.Fq()).Distinct(StringComparer.Ordinal).ToList();
        if (nss.Count == 1) m.StoreImplNamespace = nss[0];
        var basen = impls.Select(i => i.BaseType).Where(b => b != null && b.SpecialType != SpecialType.System_Object).Select(b => b!.Fq()).Distinct(StringComparer.Ordinal).ToList();
        if (impls.Count > 0 && basen.Count == 1 && impls.All(i => i.BaseType?.Fq() == basen[0]))
        {
            var b = impls[0].BaseType!;
            var ktors = b.InstanceConstructors.Where(c => c.DeclaredAccessibility is Accessibility.Protected or Accessibility.Public).ToList();
            if (ktors.Count == 1)
                m.StoreBasis = (b.Name, b.ContainingNamespace.Fq(),
                    ktors[0].Parameters.Select(p => (ShortType(p.Type), p.Name)).ToList(),
                    ktors[0].Parameters.Select(p => p.Type.ContainingNamespace?.Fq()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct(StringComparer.Ordinal).ToList());
        }
    }

    /// <summary>Die Fähigkeits-Parameter einer Handle-Methode → (Store, Funktion, Lesen?) — reine Signatur.</summary>
    private List<(string Store, string Method, bool IsRead)> FaehigkeitenVon(IMethodSymbol mm) =>
        mm.Parameters.Select(p => _faehigkeiten.TryGetValue(p.Type.OriginalDefinition.Fq(), out var f) ? f : default)
            .Where(f => f.Store != null).Distinct().ToList();

    /// <summary>Ein Typ und alle Typ-Argumente darin, rekursiv (FullNames der Original-Definitionen und Argumente).</summary>
    private static IEnumerable<string> TypUndArgumente(ITypeSymbol t)
    {
        if (t is IArrayTypeSymbol a) { foreach (var x in TypUndArgumente(a.ElementType)) yield return x; yield break; }
        yield return t.OriginalDefinition.Fq();
        if (t is INamedTypeSymbol n)
            foreach (var ta in n.TypeArguments)
                foreach (var x in TypUndArgumente(ta)) yield return x;
    }

    private static string UnwrapTask(ITypeSymbol t) =>
        IstTask(t) ? ShortType(((INamedTypeSymbol)t).TypeArguments[0]) : ShortType(t);

    private static bool IstTask(ITypeSymbol t) => Vertrag.Ist(t, typeof(Task<>)) || Vertrag.Ist(t, typeof(ValueTask<>));

    /// <summary>Kandidaten im Generat je einfachem Typnamen: <c>new X(…)</c> und Typ-Argumente <c>&lt;…, X, …&gt;</c> (nur Syntax, gebunden wird bei Bedarf).</summary>
    private Dictionary<string, List<(Compilation Comp, SyntaxNode Knoten)>>? _generatStellen;
    private readonly Dictionary<string, bool> _registriert = new(StringComparer.Ordinal);

    /// <summary>
    /// Erzeugt bzw. registriert das GENERAT (DI-Registrierung des Framework-Generators) diesen konkreten Domänen-Typ — welche
    /// Store-/Funktions-Implementierung die Laufzeit wirklich nimmt, statt einer Namens-Vorliebe. Gebunden wird nur an den Stellen, deren
    /// Name passt (das warme Lesen der Werkbank bindet sonst jedes Generat jeder Compilation).
    /// </summary>
    private bool IstRegistriert(INamedTypeSymbol impl)
    {
        var fq = impl.Fq();
        if (_registriert.TryGetValue(fq, out var ja)) return ja;
        if (_generatStellen == null)
        {
            _generatStellen = new(StringComparer.Ordinal);
            static string? Rechts(TypeSyntax t) => t switch
            {
                SimpleNameSyntax s => s.Identifier.ValueText,
                QualifiedNameSyntax q => q.Right.Identifier.ValueText,
                AliasQualifiedNameSyntax al => al.Name.Identifier.ValueText,
                _ => null,
            };
            foreach (var c in _comps)
                foreach (var tree in c.SyntaxTrees.Where(IstGeneriert))
                    foreach (var n in tree.GetRoot().DescendantNodes())
                    {
                        var (name, knoten) = n switch
                        {
                            ObjectCreationExpressionSyntax oc => (Rechts(oc.Type), (SyntaxNode)oc),
                            TypeSyntax ta when ta.Parent is TypeArgumentListSyntax => (Rechts(ta), ta),
                            _ => (null, n),
                        };
                        if (name == null) continue;
                        if (!_generatStellen.TryGetValue(name, out var l)) _generatStellen[name] = l = new();
                        l.Add((c, knoten));
                    }
        }
        ja = false;
        foreach (var (c, knoten) in _generatStellen.GetValueOrDefault(impl.Name) ?? [])
            if (c.GetSemanticModel(knoten.SyntaxTree).GetTypeInfo(knoten).Type is INamedTypeSymbol nt && IstDomänenAssembly(nt.ContainingAssembly)
                && (knoten is ObjectCreationExpressionSyntax || nt.TypeKind == TypeKind.Class) && nt.Fq() == fq)
            { ja = true; break; }
        _registriert[fq] = ja;
        return ja;
    }


    // ── Kataloge: alle Events + Commands mit Klassifikation ──────────────────

    private void CatalogEventsAndCommands(DomainModel m)
    {
        foreach (var t in _types)
        {
            if (Sym.Implements(t, _iEvent))
            {
                var persisted = !Sym.Implements(t, _iTransient);
                m.Events[t.Fq()] = new EventType(t.Name, persisted, Felder(t), Meta(t));
            }
            if (Sym.Implements(t, _iCommand))
            {
                var isCreation = Sym.Implements(t, _iCreation);
                // Das Ziel-Aggregat steht im Typ (ICommand<TAggregat>) — ein Code-Fakt, nicht aus dem Decider geraten.
                var ziel = _iCommandZiel == null ? null
                    : t.AllInterfaces.FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, _iCommandZiel))?.TypeArguments[0].Name;
                m.Commands[t.Fq()] = new CommandType(t.Name, isCreation, Felder(t), Meta(t), ziel);
            }
        }
    }

    // ── Aggregate ────────────────────────────────────────────────────────────

    private List<INamedTypeSymbol> DeciderVon(INamedTypeSymbol t) => _deciderJeState.GetValueOrDefault(t.Fq()) ?? new();
    private List<INamedTypeSymbol> ApplierVon(INamedTypeSymbol t) => _applierJeState.GetValueOrDefault(t.Fq()) ?? new();

    private AggregateRaw ReadAggregate(INamedTypeSymbol t)
    {
        var agg = new AggregateRaw { Name = t.Name, Namespace = t.ContainingNamespace.Fq(), Full = t.Fq() };
        var decls = QuellDeklarationen(t).OfType<TypeDeclarationSyntax>().ToList();
        // State-Datei = die Deklaration, die mehr trägt als nur geschachtelte Typen (Decider/Applier).
        agg.Datei = decls.FirstOrDefault(d => d.Members.Any(m => m is not BaseTypeDeclarationSyntax))?.SyntaxTree.FilePath ?? decls.FirstOrDefault()?.SyntaxTree.FilePath;
        agg.Doku = decls.Select(Summary).FirstOrDefault(d => d != null);
        agg.Usings = decls.SelectMany(d => DateiUsings(d)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

        // State: einfache Auto-Properties + abgeleitete (=> expr) werden Felder; alles andere ist Handcode (Zusatz).
        var zusatz = new List<MemberDeclarationSyntax>();
        foreach (var decl in decls)
            foreach (var member in decl.Members)
            {
                if (member is BaseTypeDeclarationSyntax nested && Model(nested.SyntaxTree).GetDeclaredSymbol(nested) is INamedTypeSymbol ns
                    && (Implementiert(ns, _iDecider) || Implementiert(ns, _iApplier))) continue;
                if (member is PropertyDeclarationSyntax prop && TryStateFeld(prop) is { } feld)
                {
                    if (feld.Name == Vertrag.StateId || feld.Name == Vertrag.StateVersion) continue;
                    if (Model(prop.SyntaxTree).GetDeclaredSymbol(prop) is IPropertySymbol ps) feld.ElementTyp = ElementTyp(ps.Type);
                    agg.State.Add(feld);
                    continue;
                }
                zusatz.Add(member);
            }
        agg.StateZusatz = MemberText(zusatz);

        var deciders = DeciderVon(t);
        agg.TeilTypen.AddRange(deciders.Concat(ApplierVon(t)).Select(x => x.Fq()).Distinct());
        agg.DeciderKlasse = deciders.Count == 1 ? deciders[0].Name : null;
        agg.DeciderGeschachtelt = deciders.Count == 1 && deciders[0].ContainingType?.Fq() == t.Fq();
        agg.DeciderDatei = deciders.SelectMany(QuellDeklarationen).FirstOrDefault()?.SyntaxTree.FilePath;
        var deciderZusatz = new List<MemberDeclarationSyntax>();
        foreach (var dd in deciders.SelectMany(QuellDeklarationen).OfType<TypeDeclarationSyntax>())
            foreach (var member in dd.Members)
            {
                // Entscheidung = öffentliche Methode mit Command als erstem Parameter (Name egal — der Typ trägt die Rolle).
                if (member is MethodDeclarationSyntax md && md.Modifiers.Any(SyntaxKind.PublicKeyword)
                    && Model(md.SyntaxTree).GetDeclaredSymbol(md) is IMethodSymbol method
                    && method.Parameters.Length >= 1 && method.Parameters[0].Type is INamedTypeSymbol cmd
                    && Sym.Implements(cmd, _iCommand))
                {
                    var cmdFull = cmd.Fq();
                    agg.HandlesCommandsFull.Add(cmdFull);
                    agg.DecideMethoden[cmdFull] = method.Name;
                    agg.DecideDateien[cmdFull] = md.SyntaxTree.FilePath;
                    agg.DecideOrte[cmd.Name] = Ort.Von(md);
                    agg.DecideParams[cmdFull] = method.Parameters[0].Name;
                    var ausgaenge = UniverseEvents(method.ReturnType).ToList();
                    agg.DecideOutcomes[cmdFull] = DecideAusgaenge(method.ReturnType);
                    foreach (var reject in ausgaenge.Where(e => Sym.Implements(e, _iTransient)))
                        agg.Rejects.Add((cmdFull, reject.Fq()));
                    var db = MethodBody(method);
                    if (db != null) agg.DecideBodies[cmdFull] = db;
                    continue;
                }
                deciderZusatz.Add(member);
            }
        agg.DeciderZusatz = MemberText(deciderZusatz);

        // Apply-Methoden: die ECHTEN (nicht aus den Commands abgeleitet) — inkl. bewusst leerer No-ops.
        var appliers = ApplierVon(t);
        agg.ApplierKlasse = appliers.Count == 1 ? appliers[0].Name : null;
        agg.ApplierGeschachtelt = appliers.Count == 1 && appliers[0].ContainingType?.Fq() == t.Fq();
        agg.ApplierDatei = appliers.SelectMany(QuellDeklarationen).FirstOrDefault()?.SyntaxTree.FilePath;
        var applierZusatz = new List<MemberDeclarationSyntax>();
        foreach (var ad in appliers.SelectMany(QuellDeklarationen).OfType<TypeDeclarationSyntax>())
            foreach (var member in ad.Members)
            {
                // Faltung = öffentliche void-Methode mit Event als erstem Parameter.
                if (member is MethodDeclarationSyntax md && md.Modifiers.Any(SyntaxKind.PublicKeyword)
                    && Model(md.SyntaxTree).GetDeclaredSymbol(md) is IMethodSymbol { ReturnsVoid: true } method
                    && method.Parameters.Length >= 1 && method.Parameters[0].Type is INamedTypeSymbol evt
                    && Sym.Implements(evt, _iEvent))
                {
                    agg.ApplyEventsFull.Add(evt.Fq());
                    agg.ApplyMethoden[evt.Fq()] = method.Name;
                    agg.ApplyDateien[evt.Name] = md.SyntaxTree.FilePath;
                    agg.ApplyOrte[evt.Name] = Ort.Von(md);
                    agg.ApplyParams[evt.Name] = method.Parameters[0].Name;
                    var ab = MethodBody(method);
                    if (ab != null) agg.ApplyBodies[evt.Name] = ab;
                    continue;
                }
                applierZusatz.Add(member);
            }
        agg.ApplierZusatz = MemberText(applierZusatz);
        return agg;
    }

    /// <summary>
    /// Eine State-Property als Feld, wenn der Scaffolder sie verlustfrei zurückschreiben kann:
    /// <c>public T X { get; set; } [= init;]</c>, <c>public T X { get; } [= init;]</c> oder <c>public T X => expr;</c>.
    /// Alles andere (Rumpf-Accessoren, <c>private set</c>, <c>init</c>, static, Attribute) bleibt Handcode.
    /// </summary>
    private static FieldInfo? TryStateFeld(PropertyDeclarationSyntax p) =>
        Codeformen.Feldregeln.State(p) is { } f
            ? new FieldInfo { Name = f.Name, Type = f.Typ, Default = f.Standard, Expr = f.Ausdruck, NurGet = f.NurGet }
            : null;

    // ── Quelltext-Helfer ─────────────────────────────────────────────────────

    private bool IstGeneriert(SyntaxTree tree) => _quelltexte.IstGeneriert(tree);

    /// <summary>Die handgeschriebenen Deklarationen eines Typs (partial über Dateien), deterministisch nach Dateipfad.</summary>
    private IEnumerable<SyntaxNode> QuellDeklarationen(INamedTypeSymbol t) =>
        t.DeclaringSyntaxReferences
            .Where(r => !IstGeneriert(r.SyntaxTree))
            .OrderBy(r => r.SyntaxTree.FilePath, StringComparer.Ordinal).ThenBy(r => r.Span.Start)
            .Select(r => r.GetSyntax());

    /// <summary>Doku, Handcode-Rumpf und Datei-usings eines Typs.</summary>
    private TypMeta Meta(INamedTypeSymbol t, bool mitZusatz = true)
    {
        var decl = QuellDeklarationen(t).OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        string? zusatz = null;
        // Handcode = alle Member AUSSER den Property-Feldern (die sind Felder, siehe Felder()).
        if (mitZusatz && decl is TypeDeclarationSyntax td && td.OpenBraceToken.IsKind(SyntaxKind.OpenBraceToken))
            zusatz = MemberText(td.Members.Where(mm => !(mm is PropertyDeclarationSyntax pp && EigenschaftsFeld(pp) != null)));

        // Deklarationsform verbatim (Modifizierer + Schlüsselwort), Basistypen außer dem Rollen-Marker, Attribute.
        var typart = decl switch
        {
            RecordDeclarationSyntax r => string.Join(" ", r.Modifiers.Where(x => !x.IsKind(SyntaxKind.PartialKeyword)).Select(x => x.Text)
                .Append(r.Keyword.Text).Concat(r.ClassOrStructKeyword.IsKind(SyntaxKind.None) ? [] : new[] { r.ClassOrStructKeyword.Text })),
            TypeDeclarationSyntax c => string.Join(" ", c.Modifiers.Where(x => !x.IsKind(SyntaxKind.PartialKeyword)).Select(x => x.Text).Append(c.Keyword.Text)),
            _ => "public record",
        };
        List<string>? basen = null;
        if (decl?.BaseList is { } bl)
        {
            var model = Model(decl.SyntaxTree);
            // Die Rollen-Marker (sie bestimmen die Art und werden aus ihr geschrieben) — alle übrigen Basen sind Code-Fakt.
            var rollen = new[] { _iCommand, _iCreation, _iEvent, _iTransient, _iQuery, _iQueryResponse, _iReadModel, _iGeteilt }
                .Where(x => x != null).Select(x => x!.Fq()).ToHashSet(StringComparer.Ordinal);
            basen = bl.Types.Where(b => model.GetTypeInfo(b.Type).Type is not INamedTypeSymbol bt
                                        || !(rollen.Contains(bt.Fq()) || (_iCommandZiel != null && bt.OriginalDefinition.Fq() == _iCommandZiel.Fq())))
                .Select(b => b.ToString()).ToList();
            if (basen.Count == 0) basen = null;
        }
        var attribute = decl is { AttributeLists.Count: > 0 } ? string.Join("\n", decl.AttributeLists.Select(a => a.ToString())) : null;

        return new TypMeta(t.ContainingNamespace.Fq(), t.ContainingAssembly?.Name ?? "",
            decl == null ? null : Summary(decl), zusatz, decl == null ? new() : DateiUsings(decl).ToList(),
            IstDomänenAssembly(t.ContainingAssembly), decl?.SyntaxTree.FilePath, typart, basen, attribute,
            decl is TypeDeclarationSyntax { ParameterList: null }, TreiberVon(t));
    }

    /// <summary>Quellen-Typ: der Treiber seiner Basis (Code-Fakt über den Basistyp, <see cref="Abstractions.Treibervertrag"/>), sonst null.</summary>
    private static string? TreiberVon(INamedTypeSymbol t)
    {
        for (var b = t.BaseType; b != null; b = b.BaseType)
            if (Abstractions.Treibervertrag.TreiberVon(b.Fq()) is { } treiber) return treiber;
        return null;
    }

    /// <summary>Die Felder der Treiber-Basis eines Quellen-Typs (public init, Basis zuerst) — sichtbar, aber nie zurückgeschrieben.</summary>
    private static List<FieldInfo> GeerbteFelder(INamedTypeSymbol t, bool mitSelbst = false)
    {
        var kette = new List<INamedTypeSymbol>();
        for (var b = mitSelbst ? t : t.BaseType; b != null && b.SpecialType != SpecialType.System_Object; b = b.BaseType) kette.Insert(0, b);
        return kette.SelectMany(b => b.GetMembers().OfType<IPropertySymbol>())
            .Where(p => !p.IsStatic && !p.IsIndexer && p.DeclaredAccessibility == Accessibility.Public && p.SetMethod != null && p.Name != "EqualityContract")
            .Select(p => new FieldInfo { Name = p.Name, Type = ShortType(p.Type), Zugriff = "{ get; init; }", Pflicht = p.IsRequired, Geerbt = true })
            .ToList();
    }

    private IEnumerable<string> DateiUsings(SyntaxNode decl)
    {
        var usings = decl.SyntaxTree.GetRoot().DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .OfType<UsingDirectiveSyntax>()
            .Where(u => u.Alias == null && u.StaticKeyword.IsKind(SyntaxKind.None) && u.GlobalKeyword.IsKind(SyntaxKind.None))
            .Select(u => u.Name?.ToString() ?? "")
            .Where(n => n.Length > 0 && !_impliziteUsings.Contains(n));
        return usings.Distinct(StringComparer.Ordinal);
    }

    /// <summary>Der Inhalt des <c>&lt;summary&gt;</c>-Doku-Kommentars als Text (Zeilen ohne <c>///</c>), sonst null.</summary>
    private static string? Summary(SyntaxNode decl)
    {
        var doc = decl.GetLeadingTrivia().Select(tr => tr.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().LastOrDefault();
        var summary = doc?.Content.OfType<XmlElementSyntax>().FirstOrDefault(x => x.StartTag.Name.ToString() == "summary");
        if (summary == null) return null;
        var zeilen = string.Concat(summary.Content.Select(c => c.ToFullString()))
            .Replace("\r\n", "\n").Split('\n')
            .Select(z => z.TrimStart())
            .Select(z => z.StartsWith("///", StringComparison.Ordinal) ? z[3..] : z)
            .Select(z => z.StartsWith(' ') ? z[1..] : z)
            .Select(z => z.TrimEnd())
            .ToList();
        while (zeilen.Count > 0 && zeilen[0].Length == 0) zeilen.RemoveAt(0);
        while (zeilen.Count > 0 && zeilen[^1].Length == 0) zeilen.RemoveAt(zeilen.Count - 1);
        return zeilen.Count == 0 ? null : string.Join("\n", zeilen);
    }

    /// <summary>Member-Deklarationen als dedenteter Quelltext (inkl. Kommentare) — der Handcode-„Zusatz".</summary>
    private static string? MemberText(IEnumerable<MemberDeclarationSyntax> members)
    {
        var list = members.ToList();
        if (list.Count == 0) return null;
        var text = string.Concat(list.Select(m => m.ToFullString()));
        var d = Dedent(text.Replace("\r\n", "\n")).Trim('\n', '\r', ' ', '\t');
        return d.Length == 0 ? null : d;
    }

    /// <summary>
    /// Den Methoden-Rumpf als dedenteten Text heben: ALLES zwischen den Klammern (inkl. Kommentare wie die
    /// <c>// 🤖 Prompt:</c>-Zeile). <c>""</c> = bewusst leerer Rumpf; null = kein Quelltext.
    /// </summary>
    private static string? MethodBody(IMethodSymbol m)
    {
        if (m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax s) return null;
        if (s.Body is { } b)
        {
            var text = b.SyntaxTree.GetText().ToString(
                Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(b.OpenBraceToken.Span.End, b.CloseBraceToken.SpanStart));
            return Dedent(text.Replace("\r\n", "\n")).Trim('\n', '\r', ' ', '\t');
        }
        if (s.ExpressionBody != null) return Rumpf.AlsAnweisung(s);
        return null;
    }

    /// <summary>Gemeinsame führende Einrückung entfernen — der Body war je nach Datei tief eingerückt.</summary>
    private static string Dedent(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var min = int.MaxValue;
        foreach (var l in lines)
        {
            if (l.Trim().Length == 0) continue;
            var lead = l.Length - l.TrimStart().Length;
            if (lead < min) min = lead;
        }
        if (min is int.MaxValue or 0) return string.Join("\n", lines.Select(l => l.TrimEnd()));
        return string.Join("\n", lines.Select(l => (l.Length >= min ? l[min..] : l.TrimStart()).TrimEnd()));
    }

    // ── Projektionen / Queries / Reader ─────────────

    /// <summary>
    /// Die Klasse verbatim: Datei/Form/Basisliste aus der Deklaration mit Basisliste, Attribute (ohne die ausgenommenen),
    /// Doku, die übrigen Member aller handgeschriebenen Teile (ohne die Handles) und die using-Direktiven (auch Aliase).
    /// </summary>
    private KlassenQuelle LiesKlasse(INamedTypeSymbol t, IEnumerable<IMethodSymbol> handles, string? ohneAttribut = null)
    {
        var decls = QuellDeklarationen(t).OfType<TypeDeclarationSyntax>().ToList();
        var haupt = decls.FirstOrDefault(d => d.BaseList != null) ?? decls.FirstOrDefault();
        var q = new KlassenQuelle();
        if (haupt == null) return q;
        q.Datei = haupt.SyntaxTree.FilePath;
        q.Doku = decls.Select(Summary).FirstOrDefault(d => d != null);
        q.Typart = string.Join(" ", haupt.Modifiers.Where(x => !x.IsKind(SyntaxKind.PartialKeyword)).Select(x => x.Text).Append(haupt.Keyword.Text));
        q.Basen = decls.Where(d => d.BaseList != null).SelectMany(d => d.BaseList!.Types.Select(b => b.ToString())).Distinct(StringComparer.Ordinal).ToList();
        var attrs = decls.SelectMany(d => d.AttributeLists)
            .Where(al => ohneAttribut == null || !al.Attributes.Any(a => Model(a.SyntaxTree).GetTypeInfo(a).Type?.ToDisplayString() == ohneAttribut))
            .Select(al => al.ToString()).ToList();
        q.Attribute = attrs.Count == 0 ? null : string.Join("\n", attrs);
        var handleSyntax = handles.SelectMany(h => h.DeclaringSyntaxReferences).Select(r => r.GetSyntax()).ToHashSet();
        q.Zusatz = MemberText(decls.SelectMany(d => d.Members).Where(mm => !handleSyntax.Contains(mm)));
        q.Zustand = ZustandsFelder(t);
        q.Usings = decls.SelectMany(d => d.SyntaxTree.GetRoot().DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                .OfType<UsingDirectiveSyntax>().Where(u => u.GlobalKeyword.IsKind(SyntaxKind.None)))
            .Select(u => u.ToString().Trim()[("using ".Length)..].TrimEnd(';').Trim())
            .Where(u => !_impliziteUsings.Contains(u)).Distinct(StringComparer.Ordinal).OrderBy(u => u, StringComparer.Ordinal).ToList();
        return q;
    }

    /// <summary>
    /// Regel Z als Symbol-Fakt (kein Rumpf): Instanzfelder, die ZUSTAND halten — nicht <c>readonly</c>, oder <c>readonly</c> mit einem
    /// Referenztyp, der weder der Typ eines Konstruktor-Parameters (injiziert: Konfig, Dienst, Logger) noch <c>string</c> ist.
    /// Compiler-erzeugte Felder (Auto-Properties, primäre Konstruktoren) zählen nicht.
    /// </summary>
    private static List<string> ZustandsFelder(INamedTypeSymbol t)
    {
        var injiziert = t.InstanceConstructors.SelectMany(c => c.Parameters).Select(p => p.Type)
            .ToHashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        return t.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsConst && !f.IsImplicitlyDeclared && f.AssociatedSymbol == null)
            .Where(f => !f.IsReadOnly
                        || (!f.Type.IsValueType && f.Type.SpecialType != SpecialType.System_String && !injiziert.Contains(f.Type)))
            .Select(f => f.Name).ToList();
    }

    /// <summary>Die Signatur eines Handles verbatim; <paramref name="kontext"/> = Anzahl Kontext-Parameter nach dem Eingang (CQRS057).</summary>
    private static HandleSigRaw LiesHandleSig(IMethodSymbol mm, int kontext)
    {
        var sig = new HandleSigRaw { Parameter = mm.Parameters[0].Name };
        if (mm.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax s) return sig;
        var ps = s.ParameterList.Parameters;
        sig.Kontext = ps.Skip(1).Take(kontext).Select(p => p.Identifier.Text).ToList();
        sig.Faehigkeiten = ps.Skip(1 + kontext).Select(p => (p.Type?.ToString() ?? "", p.Identifier.Text)).ToList();
        sig.Rueckgabe = s.ReturnType.ToString();
        sig.Modifikatoren = string.Join(" ", s.Modifiers.Select(x => x.Text));
        sig.Datei = s.SyntaxTree.FilePath;
        sig.Ort = Ort.Von(s);
        if (s.ExpressionBody is { } e) sig.Ausdruck = Dedent(e.Expression.ToString().Replace("\r\n", "\n")).Trim();
        else sig.Rumpf = MethodBody(mm);
        return sig;
    }

    private ProjectionRaw? TryReadProjection(INamedTypeSymbol t)
    {
        // Handler = öffentliche Methode: Event als erster Parameter + Aggregat-Umschlag (Name egal, die Typen tragen die Rolle).
        var methods = OeffentlicheMethoden(t)
            .Where(mm => mm.Parameters.Length >= 1 && Sym.Implements(mm.Parameters[0].Type, _iEvent)
                         && mm.Parameters.Skip(1).Any(p => p.Type.ToDisplayString() == _iAggEnvelope?.ToDisplayString())).ToList();
        if (methods.Count == 0) return null;
        var handles = methods.Select(mm => mm.Parameters[0].Type.Fq()).Distinct().ToList();

        var subId = KonstanterWert(t, _iSubscriber, Vertrag.SubscriberId);

        var raw = new ProjectionRaw
        {
            Name = t.Name, Full = t.Fq(), Namespace = t.ContainingNamespace.Fq(), SubscriberId = subId, ConsumesFull = handles,
            Pull = Sym.Implements(t, _iPull), Append = _iAppend != null && Sym.Implements(t, _iAppend),
        };
        foreach (var mm in methods)
        {
            var evt = mm.Parameters[0].Type.Name;
            var body = MethodBody(mm);
            if (body != null) raw.HandleBodies[evt] = body;
            var fs = FaehigkeitenVon(mm);
            if (fs.Count > 0) raw.HandleFaehigkeiten[evt] = fs;

            // Rückgabe IAsyncEnumerable<T | OneOf<…>>: veröffentlichte (transiente) Events.
            var pubs = YieldTypen(mm.ReturnType).Where(x => Sym.Implements(x, _iEvent)).Select(x => x.Name).ToList();
            if (pubs.Count > 0) raw.HandlePublishes[evt] = pubs;
            raw.HandleVertraege[evt] = LiesHandleVertrag(mm);
            raw.HandleSigs[evt] = LiesHandleSig(mm, 2);
        }
        raw.Quelle = LiesKlasse(t, methods);
        return raw;
    }

    /// <summary>Die Element-Typen einer <c>IAsyncEnumerable&lt;T&gt;</c>/<c>IEnumerable&lt;T&gt;</c>-Rückgabe (OneOf aufgefächert).</summary>
    private static IEnumerable<INamedTypeSymbol> YieldTypen(ITypeSymbol rt)
    {
        if (!(Vertrag.Ist(rt, typeof(IAsyncEnumerable<>)) || Vertrag.Ist(rt, typeof(IEnumerable<>)))) yield break;
        if (((INamedTypeSymbol)rt).TypeArguments[0] is not INamedTypeSymbol el) yield break;
        if (Vertrag.IstOneOf(el))
        {
            foreach (var a in el.TypeArguments.OfType<INamedTypeSymbol>()) yield return a;
        }
        else yield return el;
    }

    private QueryRaw ReadQuery(INamedTypeSymbol t) =>
        new() { Name = t.Name, Full = t.Fq(), Fields = Felder(t), Meta = Meta(t) };

    private ReaderRaw? TryReadReader(INamedTypeSymbol t)
    {
        var readerIface = t.AllInterfaces.FirstOrDefault(i => _iReader != null && i.OriginalDefinition.Fq() == _iReader.Fq());
        if (readerIface == null || readerIface.TypeArguments.Length != 1) return null;

        var methods = OeffentlicheMethoden(t)
            .Where(mm => mm.Parameters.Length >= 1 && Sym.Implements(mm.Parameters[0].Type, _iQuery)).ToList();
        var queries = methods.Select(mm => mm.Parameters[0].Type.Name).Distinct().ToList();

        // [ProjectionReader(TrackDeps = false)] — Default true.
        var attr = t.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Vertrag.ProjectionReaderAttribute);
        var trackDeps = attr?.NamedArguments.FirstOrDefault(a => a.Key == Vertrag.TrackDeps).Value.Value as bool? ?? true;

        var raw = new ReaderRaw
        {
            Name = t.Name, Full = t.Fq(), Namespace = t.ContainingNamespace.Fq(),
            ProjectionName = readerIface.TypeArguments[0].Name, QueryNames = queries, TrackDeps = trackDeps,
        };
        foreach (var mm in methods)
        {
            var q = mm.Parameters[0].Type.Name;
            var body = MethodBody(mm);
            if (body != null) raw.HandleBodies[q] = body;
            var fs = FaehigkeitenVon(mm);
            if (fs.Count > 0) raw.HandleFaehigkeiten[q] = fs;
            // Task<R> / Task<OneOf<R1,R2>> → Responses.
            var inner = IstTask(mm.ReturnType) ? ((INamedTypeSymbol)mm.ReturnType).TypeArguments[0] as INamedTypeSymbol : null;
            if (inner != null)
                raw.HandleResponses[q] = (Vertrag.IstOneOf(inner) ? inner.TypeArguments.OfType<INamedTypeSymbol>() : new[] { inner })
                    .Select(x => x.Name).ToList();
            raw.HandleVertraege[q] = LiesHandleVertrag(mm);
            raw.HandleSigs[q] = LiesHandleSig(mm, 2);
        }
        raw.Quelle = LiesKlasse(t, methods, Vertrag.ProjectionReaderAttribute);
        return raw;
    }

    // ── Handle-Vertrag: WAS ein Handler erzeugen kann und WELCHE Store-Funktionen er benutzen darf, steht in der SIGNATUR
    //    (Typ-Fakten, vom Compiler erzwungen: CQRS050 für die Rückgabe, Fähigkeits-Parameter für die Store-Funktionen).
    //    Der RUMPF liefert nichts — er übersteht Hilfsmethoden, Delegates und fremde Objekte nicht verlässlich. ──

    /// <summary>
    /// Der Vertrag eines Handlers — das Gegenstück zum OneOf eines Deciders, für Projektion/Reader.
    /// Ausgänge: Form (nichts/einzeln/strom) + Typargumente (OneOf aufgefächert); eine offene Signatur (Interface,
    /// Typ-Parameter, object, abstrakte Klasse) liefert KEINE Ausgänge (nicht geraten; CQRS050). Dazu je
    /// Fähigkeits-Parameter ein <c>storefn</c>-Ausgang: die Obergrenze dessen, was der Handle am Store rufen darf.
    /// </summary>
    private HandleVertragRaw LiesHandleVertrag(IMethodSymbol mm)
    {
        var v = new HandleVertragRaw { Signatur = mm.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) };
        var rt = mm.ReturnType;
        ITypeSymbol? el = null;
        if (IstTask(rt)) { v.Form = "einzeln"; el = ((INamedTypeSymbol)rt).TypeArguments[0]; }
        else if (Vertrag.Ist(rt, typeof(IAsyncEnumerable<>)) || Vertrag.Ist(rt, typeof(IEnumerable<>))) { v.Form = "strom"; el = ((INamedTypeSymbol)rt).TypeArguments[0]; }
        var sigTypen = el == null ? new List<ITypeSymbol>() : Vertrag.IstOneOf(el) ? ((INamedTypeSymbol)el).TypeArguments.ToList() : new List<ITypeSymbol> { el };
        static bool Offen(ITypeSymbol t) => t.TypeKind is TypeKind.Interface or TypeKind.TypeParameter
                                            || t.SpecialType == SpecialType.System_Object || (t.TypeKind == TypeKind.Class && t.IsAbstract);
        v.SignaturOffen = sigTypen.Any(Offen);
        var sig = v.SignaturOffen ? new List<INamedTypeSymbol>() : sigTypen.OfType<INamedTypeSymbol>().ToList();
        v.Ausgaenge = sig.Select(t => new AusgangRaw { Typ = t.Name, Full = t.Fq(), Art = ArtVon(t) }).ToList();
        foreach (var p in mm.Parameters)
            if (_faehigkeiten.TryGetValue(p.Type.OriginalDefinition.Fq(), out var f))
                v.Ausgaenge.Add(new AusgangRaw { Typ = f.Method, Full = p.Type.Fq(), Art = "storefn", Store = f.Store, IstLesen = f.IsRead });
        return v;
    }

    /// <summary>Die Art eines Ausgabe-Typs aus seinem Marker (nie aus dem Namen).</summary>
    private string ArtVon(INamedTypeSymbol t) =>
        Sym.Implements(t, _iQueryResponse) ? "response"
        : Sym.Implements(t, _iCommand) ? "command"
        : Sym.Implements(t, _iTransient) ? "transient"   // vor IEvent: ITransientEvent : IEvent (Broker statt Log)
        : Sym.Implements(t, _iEvent) ? "event"
        : "typ";

    /// <summary>
    /// Der Compile-Zeit-Wert eines Vertrags-Members (z. B. <c>SubscriberId</c>) am Typ: über die Interface-Implementierung
    /// (auch explizit), ausgewertet als Konstante (<c>"x"</c>, <c>const</c>, <c>nameof</c>, Verkettung). Nicht konstant ⇒ null.
    /// </summary>
    private string? KonstanterWert(INamedTypeSymbol t, INamedTypeSymbol? iface, string member)
    {
        var vertrag = iface?.GetMembers(member).OfType<IPropertySymbol>().FirstOrDefault();
        if (vertrag == null || Sym.Implementierung(t, vertrag) is not IPropertySymbol impl) return null;
        foreach (var syn in impl.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<PropertyDeclarationSyntax>())
        {
            var expr = syn.ExpressionBody?.Expression ?? syn.Initializer?.Value
                ?? syn.AccessorList?.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) switch
                {
                    { ExpressionBody: { } eb } => eb.Expression,
                    { Body.Statements: [ReturnStatementSyntax { Expression: { } re }] } => re,
                    _ => null,
                };
            if (expr != null && Model(syn.SyntaxTree).GetConstantValue(expr) is { HasValue: true, Value: string wert }) return wert;
        }
        return null;
    }

    // ── Helfer ───────────────────────────────────────────────────────────────

    /// <summary>Die HANDGESCHRIEBENEN öffentlichen Instanz-Methoden (generierte Dispatch-Member sind keine Handler).</summary>
    private IEnumerable<IMethodSymbol> OeffentlicheMethoden(INamedTypeSymbol t) =>
        t.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic
                                                          && m.DeclaringSyntaxReferences.Any(r => !IstGeneriert(r.SyntaxTree)));

    /// <summary>
    /// Die Felder eines Typs, wie er sie DEKLARIERT: Positions-Parameter eines Records (Typ + Default verbatim) und
    /// Auto-Properties der Form <c>public [required] T X { get; [set|init;] } [= v;]</c> (Accessor-Satz festgehalten).
    /// Parameter eines KLASSEN-Konstruktors sind keine Felder (nur Eingaben). Ohne Quelltext (Metadaten): die
    /// öffentlichen Instanz-Properties des Symbols.
    /// </summary>
    private List<FieldInfo> Felder(INamedTypeSymbol t)
    {
        var felder = TreiberVon(t) != null ? GeerbteFelder(t) : new List<FieldInfo>();
        felder.AddRange(DeklarierteFelder(t));
        return felder;
    }

    private List<FieldInfo> DeklarierteFelder(INamedTypeSymbol t)
    {
        var decls = QuellDeklarationen(t).OfType<TypeDeclarationSyntax>().ToList();
        if (decls.Count == 0)
            return t.GetMembers().OfType<IPropertySymbol>()
                .Where(p => !p.IsStatic && !p.IsIndexer && p.DeclaredAccessibility == Accessibility.Public && !p.IsImplicitlyDeclared)
                .Select(p => new FieldInfo { Name = p.Name, Type = ShortType(p.Type), ElementTyp = ElementTyp(p.Type) }).ToList();
        var felder = new List<FieldInfo>();
        foreach (var decl in decls)
        {
            var model = Model(decl.SyntaxTree);
            if (t.IsRecord && decl.ParameterList is { } pl)
                foreach (var p in pl.Parameters)
                    felder.Add(new FieldInfo
                    {
                        Name = p.Identifier.Text, Type = p.Type?.ToString() ?? "", Default = p.Default?.Value.ToString(),
                        ElementTyp = model.GetDeclaredSymbol(p) is IParameterSymbol ps ? ElementTyp(ps.Type) : null,
                    });
            foreach (var prop in decl.Members.OfType<PropertyDeclarationSyntax>())
                if (EigenschaftsFeld(prop) is { } f)
                {
                    if (model.GetDeclaredSymbol(prop) is IPropertySymbol psym) f.ElementTyp = ElementTyp(psym.Type);
                    felder.Add(f);
                }
        }
        return felder;
    }

    /// <summary>Der Element-Typ einer Sammlung: Array-Element bzw. das T von <c>IEnumerable&lt;T&gt;</c> (auch der Typ selbst). <c>string</c> ist keine Sammlung.</summary>
    private static string? ElementTyp(ITypeSymbol t)
    {
        if (t.SpecialType == SpecialType.System_String) return null;
        if (t is IArrayTypeSymbol a) return ShortType(a.ElementType);
        if (t is not INamedTypeSymbol n) return null;
        var ie = new[] { n }.Concat(n.AllInterfaces)
            .FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        return ie == null ? null : ShortType(ie.TypeArguments[0]);
    }

    /// <summary>
    /// Eine Property ist ein FELD, wenn sie verlustfrei als solches zurückschreibbar ist: <c>public</c> (optional
    /// <c>required</c>), ohne Attribute, Auto-Accessoren <c>get;</c> / <c>get; set;</c> / <c>get; init;</c>, optional mit
    /// Initialisierer. Alles andere (berechnet, Rümpfe, Attribute) bleibt Handcode.
    /// </summary>
    private static FieldInfo? EigenschaftsFeld(PropertyDeclarationSyntax p) =>
        Codeformen.Feldregeln.Eigenschaft(p) is { } f
            ? new FieldInfo { Name = f.Name, Type = f.Typ, Default = f.Standard, Zugriff = f.Zugriff, Pflicht = f.Pflicht }
            : null;

    private static string ShortType(ITypeSymbol t) =>
        t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    /// <summary>Alle Event-Typen aus einem Decide-Rückgabetyp <c>IEnumerable&lt;OneOf&lt;…&gt;&gt;</c> / <c>IEnumerable&lt;E&gt;</c> in Signatur-Reihenfolge.</summary>
    /// <summary>
    /// Die Ausgänge einer Entscheidung in Deklarations-Reihenfolge: Events und Hinweise voll qualifiziert, eine Frist als <c>Frist&lt;Command&gt;</c>
    /// (Code-Fakt: <c>Abstractions.Frist&lt;TCmd&gt;</c> im OneOf, Plan §3.1) — die Uhr macht aus ihr den Command an dasselbe Aggregat.
    /// </summary>
    private List<string> DecideAusgaenge(ITypeSymbol returnType)
    {
        var l = new List<string>();
        if (returnType is not INamedTypeSymbol enumerable || enumerable.TypeArguments.Length != 1) return l;
        if (enumerable.TypeArguments[0] is not INamedTypeSymbol element) return l;
        foreach (var ta in Vertrag.IstOneOf(element) ? element.TypeArguments.ToList() : new List<ITypeSymbol> { element })
            if (ta is INamedTypeSymbol f && _frist != null && SymbolEqualityComparer.Default.Equals(f.OriginalDefinition, _frist))
                l.Add($"Frist<{f.TypeArguments[0].Fq()}>");
            else if (ta is INamedTypeSymbol et && Sym.Implements(et, _iEvent))
                l.Add(et.Fq());
        return l;
    }

    private IEnumerable<INamedTypeSymbol> UniverseEvents(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol enumerable || enumerable.TypeArguments.Length != 1) yield break;
        if (enumerable.TypeArguments[0] is not INamedTypeSymbol element) yield break;

        var candidates = Vertrag.IstOneOf(element) ? element.TypeArguments.ToList() : new List<ITypeSymbol> { element };
        foreach (var ta in candidates)
            if (ta is INamedTypeSymbol et && Sym.Implements(et, _iEvent))
                yield return et;
    }
}
