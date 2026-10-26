#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Die feste PROJEKT- UND ORDNERSTRUKTUR einer Anwendung (<c>docs/07-projektstruktur.md</c>) an EINER Stelle: welche Rollen-Projekte es gibt
/// (als Suffix zur Anwendungs-Wurzel), wie sie einander referenzieren, welche Primitive in welches Projekt gehört und wie ihre Datei heißt.
/// Generatoren, Analyzer (per Link, netstandard2.0), Codegen, Extractor, Scaffolder und Werkbank lesen sie von hier; <c>--check</c> und
/// CQRS098 prüfen sie (falscher Ort = Befund, nie still).
/// <para>Die Struktur sagt nur, WO etwas liegt. WAS ein Typ ist (die <see cref="Primitive"/>), bestimmen weiterhin Marker und Signaturen
/// (Leitsatz W2) — die Zuordnung Symbol → Primitive steht in <c>Domain.SourceGeneration/Lageart.cs</c>.</para>
/// <para>In den Generatoren (netstandard2.0, per Link) ist die Klasse <c>internal</c> — sonst sähe ein Projekt, das Abstractions UND
/// einen Generator referenziert (Prüfstand), sie doppelt.</para>
/// </summary>
#if NETSTANDARD2_0
internal
#else
public
#endif
static class Projektstruktur
{
    /// <summary>Die Rolle eines Projekts der Anwendung (docs/07 §7.2).</summary>
    public enum Rolle
    {
        /// <summary><c>&lt;Wurzel&gt;</c>: Aggregate, Commands, Events, Ablehnungen, Wertobjekte, Enums, Quellen-Typen, Prozesse.</summary>
        Schreibseite,
        /// <summary><c>&lt;Wurzel&gt;.Projections</c>: ReadModels, Queries, Antworten, Store-Fähigkeiten, Projektionen, Reaktionen, Reader.</summary>
        Leseseite,
        /// <summary><c>&lt;Wurzel&gt;.Pipeline</c>: Flüsse, Funktionen (Klasse im Host, Schnittstelle draußen), Domänen-Dienste, Konfig-Records.</summary>
        Ablaeufe,
        /// <summary><c>&lt;Wurzel&gt;.Akteure</c>: Akteure, Akteur-Verträge, Client-Verträge — der äußerste Ring.</summary>
        Akteure,
        /// <summary><c>&lt;Wurzel&gt;.Infrastructure</c>: Store-Implementierungen.</summary>
        Speicher,
        /// <summary><c>&lt;Wurzel&gt;.Leitung</c>: die Domänen-DTOs der Leitung (<c>domain.proto</c>, vom Codegen) und ihr generierter Nutzlast-Mapper.</summary>
        Leitung,
        /// <summary><c>&lt;Wurzel&gt;.Laufzeit</c>: die Komposition — Framework + Rollen-Projekte, die generierten Laufzeit-Tabellen.</summary>
        Komposition,
    }

    /// <summary>Was ein Typ ist — bestimmt aus Code-Fakten (Lageart), nie aus Name oder Ordner.</summary>
    public enum Primitive
    {
        Aggregat, Decider, Applier, Command, Event, Quelle, Prozess, Wertobjekt, Enum,
        ReadModel, Query, Antwort, StoreBuendel, StoreFaehigkeit, Projektion, Reader,
        Fluss, Funktion, Auftrag, FunktionsErgebnis, FunktionsImplementierung, Dienst, DienstImplementierung, Konfig,
        /// <summary>Eine Funktion als Klasse (Baustein-Form, <c>Auf</c>): sie läuft im Host; als Schnittstelle ist sie <see cref="Funktion"/> (draußen).</summary>
        HostFunktion,
        Akteur, AkteurVertrag, ClientVertrag,
        StoreImplementierung,
        /// <summary>Kein Rollen-Marker: liegt im Bereichs-Ordner seines Projekts, in eigener Datei oder einer Sammeldatei des Projekts.</summary>
        Hilfscode,
    }

    /// <summary>Die Wurzel der Anwendung, die Codegen und Werkbank bearbeiten (die Beispielanwendung; eine Test-Anwendung hat eine eigene).</summary>
    public const string StandardWurzel = "Domain";

    /// <summary>Der Ordner der Umgebungs-Dateien <c>umgebung.&lt;name&gt;.json</c> (relativ zur Solution, docs/07 §7.2, Leitsatz W7).</summary>
    public const string UmgebungsOrdner = "umgebungen";

    /// <summary>Das Projekt, dessen Host die Werkbank baut und startet (⟳).</summary>
    public const string ServerHost = "Host.Grpc";

    /// <summary>Wohin der Codegen die STJ-Kontexte schreibt: der Ordner Serialization der Komposition (relativ zur Solution).</summary>
    public static string SerialisierungsOrdner(string wurzel) => Projekt(wurzel, Rolle.Komposition) + "/Serialization";

    /// <summary>Das Werkzeug-Projekt des Codegen-Prepass: es muss alle <see cref="CodegenProjekte"/> referenzieren (Build-Reihenfolge).</summary>
    public const string CodegenWerkzeug = "Cqrs.Codegen";

    /// <summary>Die Datei der Domänen-DTOs im Leitungs-Projekt der Anwendung (<see cref="Rolle.Leitung"/>), vom Codegen geschrieben.</summary>
    public const string ProtoDatei = "domain.proto";

    /// <summary>Das Python-Worker-Projekt der Anwendung (Zusagen, externe Funktionen) und darin der Ordner der generierten Basen.</summary>
    public static string PythonWorker(string wurzel) => wurzel + ".Client.Worker.Python.ML";
    public const string PythonGeneriert = "domain_client/generated";

    /// <summary>Der Namespace der generierten Prozess-Registry einer Assembly (<c>GeneratedProzessRegeln</c>).</summary>
    public static string ProzessNamespace(string assembly) => assembly + ".Prozess";
    public const string ProzessRegistry = "GeneratedProzessRegeln";

    // ── Rollen-Projekte ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ein Rollen-Projekt: Suffix zur Wurzel, ob es Bereichs-Ordner hat, welche Rollen es referenziert, seine Sammeldateien und ob es nur
    /// Generiertes trägt (Leitung, Komposition: nichts Handgeschriebenes, docs/07 §7.2).
    /// </summary>
    public sealed class Eintrag
    {
        public Eintrag(Rolle rolle, string suffix, bool mitBereich, Rolle[] referenziert, string[] sammeldateien, bool nurGeneriert = false)
        { Rolle = rolle; Suffix = suffix; MitBereich = mitBereich; Referenziert = referenziert; Sammeldateien = sammeldateien; NurGeneriert = nurGeneriert; }
        public Rolle Rolle { get; }
        public string Suffix { get; }
        public bool MitBereich { get; }
        public IReadOnlyList<Rolle> Referenziert { get; }
        public IReadOnlyList<string> Sammeldateien { get; }
        public bool NurGeneriert { get; }
    }

    /// <summary>Die Rollen in Referenz-Reihenfolge (unten → oben, docs/07 §7.2).</summary>
    public static readonly IReadOnlyList<Eintrag> Rollen = new[]
    {
        new Eintrag(Rolle.Schreibseite, "", true, new Rolle[0],
            new[] { "Commands.cs", "Events.cs", "ValueObjects.cs", "Enums.cs", "Quellen.cs" }),
        new Eintrag(Rolle.Leseseite, ".Projections", true, new[] { Rolle.Schreibseite },
            new[] { "ReadModels.cs", "Queries.cs", "Responses.cs", "Enums.cs" }),
        new Eintrag(Rolle.Ablaeufe, ".Pipeline", true, new[] { Rolle.Schreibseite, Rolle.Leseseite },
            new[] { "Funktionen.cs", "Auftraege.cs", "Events.cs", "Konfigs.cs", "Enums.cs" }),
        new Eintrag(Rolle.Akteure, ".Akteure", false, new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe },
            new[] { "Akteure.cs", "Clients.cs" }),
        new Eintrag(Rolle.Speicher, ".Infrastructure", true, new[] { Rolle.Schreibseite, Rolle.Leseseite },
            new string[0]),
        new Eintrag(Rolle.Leitung, ".Leitung", false, new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe },
            new string[0], nurGeneriert: true),
        new Eintrag(Rolle.Komposition, ".Laufzeit", false,
            new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe, Rolle.Akteure, Rolle.Speicher, Rolle.Leitung },
            new string[0], nurGeneriert: true),
    };

    public static Eintrag Von(Rolle r) => Rollen.First(e => e.Rolle == r);

    /// <summary>Der Projektname einer Rolle: Wurzel + Suffix.</summary>
    public static string Projekt(string wurzel, Rolle r) => wurzel + Von(r).Suffix;

    /// <summary>Alle Rollen-Projekte einer Anwendung (Referenz-Reihenfolge).</summary>
    public static IEnumerable<string> Projekte(string wurzel) => Rollen.Select(e => wurzel + e.Suffix);

    /// <summary>
    /// Die Rollen-Projekte mit handgeschriebenem Code (ohne Leitung und Komposition): über sie läuft der Stand der Werkbank. Was der Build
    /// selbst erzeugt (Proto, STJ-Kontexte), darf den Stand nicht verschieben — sonst wechselte er durch das Bauen (Spur-Stand ≠ Board-Stand).
    /// </summary>
    public static IEnumerable<string> HandgeschriebeneProjekte(string wurzel) =>
        Rollen.Where(e => !e.NurGeneriert).Select(e => wurzel + e.Suffix);

    /// <summary>Die Projekte, aus denen der Codegen Proto, STJ-Kontexte und Python-Basen erzeugt (alles außer den Store-Implementierungen).</summary>
    public static IEnumerable<string> CodegenProjekte(string wurzel) =>
        new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe, Rolle.Akteure }.Select(r => Projekt(wurzel, r));

    /// <summary>
    /// Zerlegt einen Assembly-/Projektnamen in Wurzel und Rolle über das längste passende Suffix; ohne Suffix ist es die Schreibseite der
    /// Wurzel <paramref name="name"/>. Ob das Projekt zu einer Anwendung gehört, entscheidet der Aufrufer (bekannte Wurzel, Domänen-Generator).
    /// </summary>
    public static (string Wurzel, Rolle Rolle) Zerlege(string name)
    {
        foreach (var e in Rollen.Where(e => e.Suffix.Length > 0).OrderByDescending(e => e.Suffix.Length))
            if (name.EndsWith(e.Suffix, StringComparison.Ordinal) && name.Length > e.Suffix.Length)
                return (name.Substring(0, name.Length - e.Suffix.Length), e.Rolle);
        return (name, Rolle.Schreibseite);
    }

    /// <summary>Ist <paramref name="projekt"/> ein Rollen-Projekt der Anwendung <paramref name="wurzel"/>?</summary>
    public static bool IstProjekt(string wurzel, string projekt) => Projekte(wurzel).Contains(projekt, StringComparer.Ordinal);

    // ── Primitive → Rolle und Datei ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>In welchen Rollen-Projekten eine Primitive liegen darf (genau eines, außer Enum und Hilfscode).</summary>
    public static IReadOnlyList<Rolle> RollenVon(Primitive p)
    {
        switch (p)
        {
            case Primitive.Aggregat: case Primitive.Decider: case Primitive.Applier: case Primitive.Command: case Primitive.Event:
            case Primitive.Quelle: case Primitive.Prozess: case Primitive.Wertobjekt:
                return new[] { Rolle.Schreibseite };
            case Primitive.ReadModel: case Primitive.Query: case Primitive.Antwort: case Primitive.StoreBuendel: case Primitive.StoreFaehigkeit:
            case Primitive.Projektion: case Primitive.Reader:
                return new[] { Rolle.Leseseite };
            case Primitive.Fluss: case Primitive.Funktion: case Primitive.Auftrag: case Primitive.FunktionsErgebnis:
            case Primitive.FunktionsImplementierung: case Primitive.Dienst: case Primitive.DienstImplementierung: case Primitive.Konfig:
            case Primitive.HostFunktion:
                return new[] { Rolle.Ablaeufe };
            case Primitive.Akteur: case Primitive.AkteurVertrag: case Primitive.ClientVertrag:
                return new[] { Rolle.Akteure };
            case Primitive.StoreImplementierung:
                return new[] { Rolle.Speicher };
            case Primitive.Enum:
                return new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe };
            default:
                return Rollen.Select(e => e.Rolle).ToArray();
        }
    }

    /// <summary>Die Sammeldatei einer Primitive (<c>Commands.cs</c> …), oder <c>null</c>, wenn der Typ eine eigene Datei hat.</summary>
    public static string? Sammeldatei(Primitive p)
    {
        switch (p)
        {
            case Primitive.Command: return "Commands.cs";
            case Primitive.Event: case Primitive.FunktionsErgebnis: return "Events.cs";
            case Primitive.Quelle: return "Quellen.cs";
            case Primitive.Wertobjekt: return "ValueObjects.cs";
            case Primitive.Enum: return "Enums.cs";
            case Primitive.ReadModel: return "ReadModels.cs";
            case Primitive.Query: return "Queries.cs";
            case Primitive.Antwort: return "Responses.cs";
            case Primitive.Funktion: return "Funktionen.cs";
            case Primitive.Auftrag: return "Auftraege.cs";
            case Primitive.Konfig: return "Konfigs.cs";
            case Primitive.Akteur: case Primitive.AkteurVertrag: return "Akteure.cs";
            case Primitive.ClientVertrag: return "Clients.cs";
            default: return null;
        }
    }

    /// <summary>
    /// Der Dateiname einer NEUEN Primitive (docs/07 §7.4): die Sammeldatei ihrer Art, sonst <c>{Typ}.cs</c>; Decide- und Apply-Methoden eines
    /// Aggregats stehen in <c>{Aggregat}.Decider.cs</c>/<c>{Aggregat}.Applier.cs</c> (<paramref name="typ"/> = Aggregat-Name).
    /// </summary>
    public static string Datei(Primitive p, string typ) =>
        p == Primitive.Decider ? typ + "." + Aggregatvertrag.Decider + ".cs"
        : p == Primitive.Applier ? typ + "." + Aggregatvertrag.Applier + ".cs"
        : Sammeldatei(p) ?? typ + ".cs";

    /// <summary>Der Namespace eines Typs im Bereich <paramref name="bereich"/> eines Projekts: Projekt + Ordner (ohne Bereich: das Projekt).</summary>
    public static string Namespace(string projekt, string? bereich) => string.IsNullOrEmpty(bereich) ? projekt : projekt + "." + bereich;

    /// <summary>Der Bereich eines Namespace im Projekt (<c>Domain.Pipeline.ImagePair</c> in <c>Domain.Pipeline</c> → <c>ImagePair</c>); sonst <c>null</c>.</summary>
    public static string? BereichAus(string projekt, string ns) =>
        ns.StartsWith(projekt + ".", StringComparison.Ordinal) && ns.IndexOf('.', projekt.Length + 1) < 0 ? ns.Substring(projekt.Length + 1) : null;

    // ── Lage-Prüfung (eine Regel für --check und CQRS098) ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Liegt eine Primitive im Projekt ihrer Rolle? Liefert den Befund oder <c>null</c>. <paramref name="projekt"/> muss ein Rollen-Projekt
    /// der Wurzel sein (sonst prüft der Aufrufer das Projekt selbst).
    /// </summary>
    public static string? PruefeProjekt(string wurzel, string projekt, Primitive p, string typ)
    {
        var rolle = Zerlege(projekt).Rolle;
        var erlaubt = RollenVon(p);
        if (erlaubt.Contains(rolle)) return null;
        return $"'{typ}' ist {Text(p)} und gehört nach {string.Join(" oder ", erlaubt.Select(r => Projekt(wurzel, r)))}, nicht nach {projekt} (docs/07 §7.2)";
    }

    /// <summary>
    /// Ordner, Namespace und Datei eines Typs im Projekt (docs/07 §7.3/§7.4). <paramref name="relativerPfad"/> ist der Pfad der Datei relativ
    /// zum Projektverzeichnis mit '/' (<c>ImagePair/Events.cs</c>); <paramref name="aggregat"/> ist bei Decider/Applier der Aggregat-Name;
    /// <paramref name="buendel"/> sind bei einer Store-Fähigkeit die Bündel, die sie enthalten (sie steht in der Datei eines davon).
    /// </summary>
    public static IEnumerable<string> PruefeOrt(string projekt, string relativerPfad, string ns, Primitive p, string typ, string? aggregat,
        IEnumerable<string>? buendel = null)
    {
        var e = Von(Zerlege(projekt).Rolle);
        var teile = relativerPfad.Split('/');
        var datei = teile[teile.Length - 1];
        var bereich = teile.Length == 2 ? teile[0] : null;
        if (e.MitBereich ? teile.Length != 2 : teile.Length != 1)
            yield return $"'{typ}' liegt in {projekt}/{relativerPfad} — erwartet " + (e.MitBereich ? $"{projekt}/<Bereich>/<Datei>" : $"{projekt}/<Datei>") + " (docs/07 §7.3)";
        else if (ns != Namespace(projekt, bereich))
            yield return $"'{typ}' hat den Namespace {ns}, liegt aber in {projekt}/{relativerPfad} — Namespace = Projekt + Ordner: {Namespace(projekt, bereich)} (docs/07 §7.3)";

        string[] erwartet;
        if (p == Primitive.Decider || p == Primitive.Applier) erwartet = new[] { Datei(p, aggregat ?? typ) };
        else if (p == Primitive.StoreFaehigkeit) erwartet = (buendel ?? Enumerable.Empty<string>()).Select(b => b + ".cs").Concat(new[] { typ + ".cs" }).ToArray();
        else if (Sammeldatei(p) is { } s) erwartet = new[] { s };
        else if (p == Primitive.Hilfscode) erwartet = e.Sammeldateien.Concat(new[] { typ + ".cs" }).ToArray();
        else erwartet = new[] { typ + ".cs" };
        var eigene = p != Primitive.Decider && p != Primitive.Applier && p != Primitive.StoreFaehigkeit && Sammeldatei(p) == null;
        if (!erwartet.Contains(datei, StringComparer.Ordinal) && !(eigene && datei.StartsWith(typ + ".", StringComparison.Ordinal)))
            yield return $"'{typ}' ({Text(p)}) steht in {datei} — erwartet {string.Join(" oder ", erwartet)} (docs/07 §7.4)";
    }

    /// <summary>Lesbarer Name einer Primitive für Befunde.</summary>
    public static string Text(Primitive p)
    {
        switch (p)
        {
            case Primitive.Aggregat: return "ein Aggregat";
            case Primitive.Decider: return "ein Decider";
            case Primitive.Applier: return "ein Applier";
            case Primitive.Command: return "ein Command";
            case Primitive.Event: return "ein Event";
            case Primitive.Quelle: return "ein Quellen-Typ";
            case Primitive.Prozess: return "ein Prozess";
            case Primitive.Wertobjekt: return "ein Wertobjekt";
            case Primitive.Enum: return "ein Enum";
            case Primitive.ReadModel: return "ein ReadModel";
            case Primitive.Query: return "eine Query";
            case Primitive.Antwort: return "eine Antwort";
            case Primitive.StoreBuendel: return "ein Store-Bündel";
            case Primitive.StoreFaehigkeit: return "eine Store-Fähigkeit";
            case Primitive.Projektion: return "eine Projektion/Reaktion";
            case Primitive.Reader: return "ein Reader";
            case Primitive.Fluss: return "ein Fluss";
            case Primitive.Funktion: return "eine Funktion (Schnittstelle)";
            case Primitive.HostFunktion: return "eine Funktion im Host";
            case Primitive.Auftrag: return "ein Auftrag";
            case Primitive.FunktionsErgebnis: return "ein Ergebnis-Event einer Funktion";
            case Primitive.FunktionsImplementierung: return "eine Funktions-Implementierung";
            case Primitive.Dienst: return "ein Domänen-Dienst";
            case Primitive.DienstImplementierung: return "eine Dienst-Implementierung";
            case Primitive.Konfig: return "ein Konfig-Record";
            case Primitive.Akteur: return "ein Akteur";
            case Primitive.AkteurVertrag: return "ein Akteur-Vertrag";
            case Primitive.ClientVertrag: return "ein Client-Vertrag";
            case Primitive.StoreImplementierung: return "eine Store-Implementierung";
            default: return "Hilfscode";
        }
    }
}
