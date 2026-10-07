using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Eine <b>Transition</b> des Prozess-Petri-Netzes (Spec §2/§3): ihre <see cref="Bedingung"/> ist eine
/// KONJUNKTION von Event-Typen (Tokens) — sind alle für eine Korrelation eingetroffen, ist die Transition
/// aktiviert und feuert die von <see cref="Sende"/> gebauten Commands. <see cref="RückgängigDurch"/> ist
/// ihr Gegenzug in der Kompensation (läuft, wenn das Erfolgs-Event dieses Schritts beobachtet wurde).
///
/// Der <see cref="Sende"/>-Rückgabewert ist eine LISTE — so ist Fan-out gratis: eine Transition darf aus
/// einem Match mehrere Commands erzeugen (N <c>SchreibeGut</c> an N Ziele). Jedes bekommt beim Feuern eine
/// deterministische, EINDEUTIGE Vorgang-Id aus der Kausalität (Spec §6, <see cref="ProzessId.FürTransition"/>).
/// </summary>
public sealed class Regel
{
    public Regel(
        IReadOnlyList<Type> bedingung,
        Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>>? sende,
        Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>>? rückgängigDurch,
        SammelBedingung? sammel = null,
        IReadOnlyList<Type>? produziertCommands = null,
        Func<IReadOnlyList<IEvent>, IReadOnlyList<IAuftrag>>? ruft = null,
        IReadOnlyList<Type>? gerufeneFunktionen = null,
        TimeSpan? zeitlimit = null,
        int? knoten = null,
        IReadOnlyList<int?>? vonKnoten = null,
        int? jeKnoten = null)
    {
        if (bedingung is null || bedingung.Count == 0)
            throw new ArgumentException("Eine Regel braucht mindestens einen Bedingungs-Event-Typ.", nameof(bedingung));
        if ((sende is null) == (ruft is null))
            throw new ArgumentException("Eine Regel ruft GENAU EIN Ziel: entweder Sende (Command an ein Aggregat) oder Rufe (Katalog-Funktion).");
        if (zeitlimit is { } z && z <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(zeitlimit), "Ein Zeitlimit muss positiv sein.");
        if (vonKnoten is not null && vonKnoten.Count != bedingung.Count)
            throw new ArgumentException("Je Bedingungs-Typ genau eine Herkunft (oder null = beliebig).", nameof(vonKnoten));
        Bedingung = bedingung;
        Knoten = knoten;
        VonKnoten = vonKnoten ?? bedingung.Select(_ => (int?)null).ToList();
        JeKnoten = jeKnoten;
        Sende = sende;
        Ruft = ruft;
        RückgängigDurch = rückgängigDurch;
        Sammel = sammel;
        ProduziertCommands = produziertCommands ?? Array.Empty<Type>();
        GerufeneFunktionen = gerufeneFunktionen ?? Array.Empty<Type>();
        Zeitlimit = zeitlimit;
    }

    /// <summary>
    /// Pipeline-Fluss (docs/konzept-editor-pipelines.md §14): der KNOTEN, zu dem diese Regel gehört (Deklarations-Index im Fluss).
    /// Die Wirkung ihres Aufrufs wird zum Token MIT dieser Herkunft — so unterscheidet ein Draht „Ergebnis von Knoten 3“ von
    /// „irgendein Event dieses Typs“, und dieselbe Funktion darf mehrfach im Fluss stehen. Mehrere Regeln können zu einem Knoten
    /// gehören (∨: je eingehender Draht eine Regel). <c>null</c> = klassische Prozess-Regel (Herkunft egal).
    /// </summary>
    public int? Knoten { get; }

    /// <summary>
    /// Je Bedingungs-Typ die verlangte Herkunft (Knoten-Index) des Tokens; <c>null</c> = beliebig (klassisches Typ-Matching).
    /// Gleiche Länge wie <see cref="Bedingung"/>.
    /// </summary>
    public IReadOnlyList<int?> VonKnoten { get; }

    /// <summary>
    /// Gesetzt, wenn diese Regel ein JE-Auffächern ist (ein Aufruf je Listen-Element): jedes erzeugte Token trägt dann den Teil
    /// <c>(JeKnoten → Element-Index)</c>. Ein ∧ verbindet nur Tokens mit verträglichen Teilen (dasselbe Element); <c>Sammle</c>
    /// fasst alle Teile wieder zusammen.
    /// </summary>
    public int? JeKnoten { get; }

    /// <summary>
    /// Die Katalog-Funktionen, die <see cref="Ruft"/> aufruft (Typ-Argument von <c>Rufe&lt;TFunktion&gt;</c>, ohne Laufzeit-Invoke
    /// erfasst). Für den Azyklizitäts-Check ist eine Funktion ein Knoten wie ein Command: Bedingungs-Event → Funktion →
    /// ihre Ergebnis-Events (aus der Signatur, generiert).
    /// </summary>
    public IReadOnlyList<Type> GerufeneFunktionen { get; }

    /// <summary>
    /// Baut aus den gematchten Events die Aufträge an Katalog-Funktionen (statt Commands an Aggregate). Genau eines von
    /// <see cref="Sende"/>/<see cref="Ruft"/> ist gesetzt — beide sind ein „Aufruf" mit OneOf-Ergebnis, nur das Ziel
    /// unterscheidet sich (zustandsbehaftet vs. zustandslos).
    /// </summary>
    public Func<IReadOnlyList<IEvent>, IReadOnlyList<IAuftrag>>? Ruft { get; }

    /// <summary>
    /// Optionales Zeitlimit des Aufrufs (Command oder Funktion), gemessen ab dem Zeitpunkt, an dem die Transition
    /// aktiviert wurde (das jüngste gematchte Event, DB-Zeit). Liegt bis dahin kein Ergebnis vor, gilt der Schritt als
    /// gescheitert (Grund „Zeitlimit") → Kompensation bzw. <c>ProzessBeendet(false)</c>. Ein später eintreffendes
    /// Ergebnis ändert daran nichts.
    /// </summary>
    public TimeSpan? Zeitlimit { get; }

    /// <summary>
    /// Die Command-Typen, die <see cref="Sende"/> feuert (beim Bauen über die generische Signatur erfasst,
    /// ohne Laufzeit-Invoke). Grundlage des Azyklizitäts-Checks: Bedingung-Event → Command → produzierte
    /// Events (aus den Decidern) → nächster Command muss ein DAG bleiben.
    /// </summary>
    public IReadOnlyList<Type> ProduziertCommands { get; }

    /// <summary>Die Event-Typen, die ALLE (für eine Korrelation) da sein müssen, damit die Transition feuert.</summary>
    public IReadOnlyList<Type> Bedingung { get; }

    /// <summary>
    /// Optionaler COUNT-JOIN (dynamische Breite): die Transition feuert erst, wenn <see cref="SammelBedingung.Anzahl"/>
    /// Instanzen von <see cref="SammelBedingung.Typ"/> eingetroffen sind (z.B. „buche erst, wenn ALLE N Gutschriften
    /// da sind"). Die erwartete Zahl liest sie aus dem ersten Bedingungs-Event (dem Auslöser mit der Fan-out-Breite).
    /// <see cref="Sende"/> erhält dann <c>[bedingung…, sammel…]</c>. Kein Zähler im Log — die Breite steht im Auslöser.
    /// </summary>
    public SammelBedingung? Sammel { get; }

    /// <summary>
    /// Baut aus den gematchten Event-Payloads (in <see cref="Bedingung"/>-Reihenfolge) die zu feuernden Commands.
    /// <c>null</c> genau dann, wenn die Regel eine Funktion ruft (<see cref="Ruft"/>).
    /// </summary>
    public Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>>? Sende { get; }

    /// <summary>
    /// Optionaler Gegenzug (Kompensation) — bekommt dieselben gematchten Events wie die Vorwärts-Regel und
    /// baut daraus das fachliche Gegen-Command (z.B. die reservierte Menge wieder freigeben). Das Ziel
    /// dedupliziert den Gegenzug über seine eigene deterministische CommandId (Framework-Inbox).
    /// </summary>
    public Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>>? RückgängigDurch { get; }
}

/// <summary>
/// Beschreibt einen COUNT-JOIN: sammle ALLE Instanzen von <see cref="Typ"/> und feuere erst, wenn ihre Zahl
/// <see cref="Anzahl"/> (aus dem Auslöser abgeleitet) erreicht. Hält den Fan-out zusammen (buche erst nach
/// allen N Gutschriften), ohne einen Zähler zu speichern — die Breite ist eine Funktion des Auslöse-Events.
/// </summary>
public sealed class SammelBedingung
{
    public SammelBedingung(Type typ, Func<IEvent, int> anzahl)
        : this(new[] { (typ, (int?)null) }, anzahl, null) { }

    /// <summary>
    /// Pipeline-Fluss (§14): sammle die Tokens mehrerer Drähte (Typ + Herkunfts-Knoten) — bei einem JE-Rahmen
    /// (<paramref name="jeKnoten"/>) genau ein Token je Element, geordnet nach Element-Index; eine leere Liste sammelt sofort.
    /// </summary>
    public SammelBedingung(IReadOnlyList<(Type Typ, int? Von)> drähte, Func<IEvent, int> anzahl, int? jeKnoten)
    {
        if (drähte is null || drähte.Count == 0) throw new ArgumentException("Sammeln braucht mindestens einen Draht.", nameof(drähte));
        Drähte = drähte;
        Anzahl = anzahl;
        JeKnoten = jeKnoten;
    }

    /// <summary>Der (erste) gesammelte Typ — die klassische Count-Join-Sicht.</summary>
    public Type Typ => Drähte[0].Typ;
    public IReadOnlyList<(Type Typ, int? Von)> Drähte { get; }
    public Func<IEvent, int> Anzahl { get; }
    /// <summary>Gesetzt: Sammeln über die Elemente dieses JE-Rahmens (Anzahl 0 erlaubt, Ordnung = Element-Index).</summary>
    public int? JeKnoten { get; }
}

/// <summary>
/// Die Regeln eines Prozess-Typs (der DAG-Deskriptor, Spec §4). STRUKTUR aus Code, pure Funktion — sie wird
/// nie pro Instanz gespeichert. Der generische Manager kombiniert sie bei jeder Weckung mit dem aus dem Log
/// gefalteten Marking, um die aktivierten Transitionen zu feuern.
/// </summary>
public sealed class ProzessRegeln
{
    public ProzessRegeln(Type auslöserTyp, IReadOnlyList<Regel> regeln)
        : this(auslöserTyp, regeln, quellKnoten: null, umleitenZeitlimit: null, umleitenAbgelehnt: null) { }

    /// <summary>
    /// Pipeline-Fluss (§14): Regeln aus einem <c>Pipeline.Definiere</c>. <paramref name="quellKnoten"/> ist die Herkunft des
    /// Auslöser-Tokens; die Umleitungs-Mengen nennen die Knoten, deren Fehler-Port (⏳ Zeitlimit / ✕ abgelehnt) verdrahtet ist —
    /// dort wird ein Fehlschlag zum Token statt den Vorgang scheitern zu lassen.
    /// </summary>
    public ProzessRegeln(Type auslöserTyp, IReadOnlyList<Regel> regeln, int? quellKnoten,
        IReadOnlyCollection<int>? umleitenZeitlimit, IReadOnlyCollection<int>? umleitenAbgelehnt)
    {
        AuslöserTyp = auslöserTyp ?? throw new ArgumentNullException(nameof(auslöserTyp));
        Regeln = regeln ?? throw new ArgumentNullException(nameof(regeln));
        QuellKnoten = quellKnoten;
        UmleitenZeitlimit = new HashSet<int>(umleitenZeitlimit ?? Array.Empty<int>());
        UmleitenAbgelehnt = new HashSet<int>(umleitenAbgelehnt ?? Array.Empty<int>());
        TeilnehmendeEvents = regeln
            .SelectMany(r => r.Bedingung.Concat(r.Sammel?.Drähte.Select(d => d.Typ) ?? Enumerable.Empty<Type>()))
            .Append(auslöserTyp)
            .Where(t => t != typeof(ZeitlimitAbgelaufen) && t != typeof(SchrittAbgelehnt))
            .Distinct()
            .ToList();
    }

    /// <summary>Herkunft (Knoten-Index) des Auslöser-Tokens — gesetzt genau für Pipeline-Flüsse.</summary>
    public int? QuellKnoten { get; }

    /// <summary>Ein Pipeline-Fluss (aus <c>Pipeline.Definiere</c>) — per Konstruktion azyklisch, mit Knoten-Herkunft.</summary>
    public bool IstFluss => QuellKnoten is not null;

    /// <summary>Knoten, deren ⏳-Port verdrahtet ist (Zeitlimit wird Token <see cref="ZeitlimitAbgelaufen"/>).</summary>
    public IReadOnlySet<int> UmleitenZeitlimit { get; }

    /// <summary>Knoten, deren ✕-Port verdrahtet ist (Ablehnung/Fehler wird Token <see cref="SchrittAbgelehnt"/>).</summary>
    public IReadOnlySet<int> UmleitenAbgelehnt { get; }

    /// <summary>Der Auslöser-Event-Typ (bindet den Start; <c>Prozess&lt;TAuslöser&gt;</c>).</summary>
    public Type AuslöserTyp { get; }

    /// <summary>Die Transitionen des Netzes.</summary>
    public IReadOnlyList<Regel> Regeln { get; }

    /// <summary>
    /// Alle teilnehmenden Event-Typen (Union der Bedingungen inkl. Auslöser). Der <c>KorrelationsRouter</c>
    /// abonniert genau ihre <c>StateChangeVia</c>-Signale — jede Ankunft weckt den zuständigen Manager.
    /// </summary>
    public IReadOnlyList<Type> TeilnehmendeEvents { get; }
}

/// <summary>
/// Was der Anwender schreibt (Spec §3): ein Typ, der die <see cref="Regeln"/> eines Prozesses liefert.
/// Ersetzt <c>IProzessPlan</c> (Schrittliste) durch die typisierten Event→Command-Regeln. Der
/// <c>ProzessRegelnGenerator</c> liest alle <see cref="IProzessDefinition"/> und emittiert daraus die
/// Regel-Registry; der generische Manager führt sie aus.
/// </summary>
public interface IProzessDefinition
{
    ProzessRegeln Regeln { get; }
}
