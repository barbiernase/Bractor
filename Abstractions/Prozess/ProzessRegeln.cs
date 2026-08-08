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
        Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>> sende,
        Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>>? rückgängigDurch,
        SammelBedingung? sammel = null,
        IReadOnlyList<Type>? produziertCommands = null)
    {
        if (bedingung is null || bedingung.Count == 0)
            throw new ArgumentException("Eine Regel braucht mindestens einen Bedingungs-Event-Typ.", nameof(bedingung));
        Bedingung = bedingung;
        Sende = sende ?? throw new ArgumentNullException(nameof(sende));
        RückgängigDurch = rückgängigDurch;
        Sammel = sammel;
        ProduziertCommands = produziertCommands ?? Array.Empty<Type>();
    }

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

    /// <summary>Baut aus den gematchten Event-Payloads (in <see cref="Bedingung"/>-Reihenfolge) die zu feuernden Commands.</summary>
    public Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>> Sende { get; }

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
    {
        Typ = typ;
        Anzahl = anzahl;
    }

    public Type Typ { get; }
    public Func<IEvent, int> Anzahl { get; }
}

/// <summary>
/// Die Regeln eines Prozess-Typs (der DAG-Deskriptor, Spec §4). STRUKTUR aus Code, pure Funktion — sie wird
/// nie pro Instanz gespeichert. Der generische Manager kombiniert sie bei jeder Weckung mit dem aus dem Log
/// gefalteten Marking, um die aktivierten Transitionen zu feuern.
/// </summary>
public sealed class ProzessRegeln
{
    public ProzessRegeln(Type auslöserTyp, IReadOnlyList<Regel> regeln)
    {
        AuslöserTyp = auslöserTyp ?? throw new ArgumentNullException(nameof(auslöserTyp));
        Regeln = regeln ?? throw new ArgumentNullException(nameof(regeln));
        TeilnehmendeEvents = regeln
            .SelectMany(r => r.Bedingung)
            .Distinct()
            .ToList();
    }

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
