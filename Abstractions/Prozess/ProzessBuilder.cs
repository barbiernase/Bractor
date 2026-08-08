using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Einstiegspunkt der Regel-Notation (Spec §3):
/// <code>
/// public ProzessRegeln Regeln => Prozess&lt;UeberweisungBeauftragt&gt;.Definiere(p =>
/// {
///     p.Auf&lt;UeberweisungBeauftragt&gt;().Sende(e => new ReserviereBetrag(e.Quelle, e.Betrag))
///         .RückgängigDurch(e => new GebeReservierungFrei(e.Quelle));
///     p.Auf&lt;BetragReserviert&gt;().Und&lt;Gutgeschrieben&gt;()
///         .Sende((r, g) => new BucheReservierung(r.AggregateId));
/// });
/// </code>
/// <typeparamref name="TAuslöser"/> bindet den Auslöser-Typ (der Start ist ein Event, kein Sonder-Command).
/// </summary>
public static class Prozess<TAuslöser> where TAuslöser : IEvent
{
    public static ProzessRegeln Definiere(Action<RegelBauer> konfiguriere)
    {
        var bauer = new RegelBauer();
        konfiguriere(bauer);
        return new ProzessRegeln(typeof(TAuslöser), bauer.Regeln);
    }
}

/// <summary>Sammelt die Regeln während des <c>Definiere</c>-Aufrufs. <c>Auf&lt;E&gt;()</c> beginnt eine Transition.</summary>
public sealed class RegelBauer
{
    private readonly List<Regel> _regeln = new();
    internal IReadOnlyList<Regel> Regeln => _regeln;

    /// <summary>Die Regel wird beim Abschluss (<c>Sende</c>/<c>SendeJe</c>) registriert; hier gibt es nur ihren Index.</summary>
    internal RegelHandle Registriere(Regel r)
    {
        _regeln.Add(r);
        return new RegelHandle(_regeln, _regeln.Count - 1);
    }

    /// <summary>Beginnt eine Transition mit dem ersten Bedingungs-Event.</summary>
    public RegelBauer<TEvent> Auf<TEvent>() where TEvent : IEvent
        => new(this, new[] { typeof(TEvent) });
}

/// <summary>Erlaubt es, nach <c>Sende</c> noch <c>RückgängigDurch</c> an DIESELBE Regel zu hängen (mutiert sie).</summary>
internal readonly struct RegelHandle
{
    private readonly List<Regel> _regeln;
    private readonly int _index;
    public RegelHandle(List<Regel> regeln, int index) { _regeln = regeln; _index = index; }

    public void SetzeRückgängig(Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>> rückgängig)
    {
        var alt = _regeln[_index];
        _regeln[_index] = new Regel(alt.Bedingung, alt.Sende, rückgängig, alt.Sammel, alt.ProduziertCommands);
    }
}

// ── Arität 1: Auf<E1> ──

/// <summary>Eine begonnene Transition mit einem Bedingungs-Event. <c>Und&lt;E2&gt;</c> macht daraus einen Join.</summary>
public sealed class RegelBauer<TE1> where TE1 : IEvent
{
    private readonly RegelBauer _bauer;
    private readonly Type[] _bedingung;
    internal RegelBauer(RegelBauer bauer, Type[] bedingung) { _bauer = bauer; _bedingung = bedingung; }

    /// <summary>Join (Konjunktion, Spec §3): feuert erst, wenn AUCH <typeparamref name="TE2"/> da ist.</summary>
    public RegelBauer<TE1, TE2> Und<TE2>() where TE2 : IEvent
        => new(_bauer, _bedingung.Append(typeof(TE2)).ToArray());

    /// <summary>
    /// Count-Join (dynamische Breite): feuert erst, wenn ALLE <paramref name="anzahl"/> Instanzen von
    /// <typeparamref name="TSammel"/> da sind (z.B. buche erst nach allen N Gutschriften). Die erwartete
    /// Breite liest <paramref name="anzahl"/> aus dem Auslöser — kein Zähler.
    /// </summary>
    public RegelBauerSammel<TE1, TSammel> UndAlle<TSammel>(Func<TE1, int> anzahl) where TSammel : IEvent
        => new(_bauer, _bedingung, typeof(TSammel), e => anzahl((TE1)e));

    /// <summary>Ein Command aus dem Match (Command-Typ per Inferenz für den Azyklizitäts-Check erfasst).</summary>
    public RegelAbschluss<TE1> Sende<TCmd>(Func<TE1, TCmd> baue) where TCmd : ICommand
        => Abschluss(new[] { typeof(TCmd) }, evts => new ICommand[] { baue((TE1)evts[0]) });

    /// <summary>Mehrere Commands aus dem Match (Fan-out).</summary>
    public RegelAbschluss<TE1> SendeJe<TCmd>(Func<TE1, IEnumerable<TCmd>> baue) where TCmd : ICommand
        => Abschluss(new[] { typeof(TCmd) }, evts => baue((TE1)evts[0]).Cast<ICommand>().ToArray());

    private RegelAbschluss<TE1> Abschluss(Type[] cmdTypen, Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>> sende)
        => new(_bauer.Registriere(new Regel(_bedingung, sende, null, produziertCommands: cmdTypen)));
}

/// <summary>Nach <c>Sende</c>: erlaubt optional <c>RückgängigDurch</c> mit derselben Event-Signatur.</summary>
public readonly struct RegelAbschluss<TE1> where TE1 : IEvent
{
    private readonly RegelHandle _handle;
    internal RegelAbschluss(RegelHandle handle) { _handle = handle; }

    public void RückgängigDurch<TCmd>(Func<TE1, TCmd> baue) where TCmd : ICommand
        => _handle.SetzeRückgängig(evts => new ICommand[] { baue((TE1)evts[0]) });

    public void RückgängigDurchJe<TCmd>(Func<TE1, IEnumerable<TCmd>> baue) where TCmd : ICommand
        => _handle.SetzeRückgängig(evts => baue((TE1)evts[0]).Cast<ICommand>().ToArray());
}

// ── Count-Join: Auf<E1>().UndAlle<ESammel>(n) ──

/// <summary>Eine Transition mit Count-Join: Auslöser <typeparamref name="TE1"/> + ALLE <typeparamref name="TSammel"/>.</summary>
public sealed class RegelBauerSammel<TE1, TSammel>
    where TE1 : IEvent
    where TSammel : IEvent
{
    private readonly RegelBauer _bauer;
    private readonly Type[] _bedingung;
    private readonly Type _sammelTyp;
    private readonly Func<IEvent, int> _anzahl;

    internal RegelBauerSammel(RegelBauer bauer, Type[] bedingung, Type sammelTyp, Func<IEvent, int> anzahl)
    {
        _bauer = bauer; _bedingung = bedingung; _sammelTyp = sammelTyp; _anzahl = anzahl;
    }

    /// <summary>Baut den Command aus dem Auslöser + der vollständigen Liste der gesammelten Events.</summary>
    public void Sende<TCmd>(Func<TE1, IReadOnlyList<TSammel>, TCmd> baue) where TCmd : ICommand
        => _bauer.Registriere(new Regel(
            _bedingung,
            evts => new ICommand[] { baue((TE1)evts[0], evts.Skip(_bedingung.Length).Cast<TSammel>().ToList()) },
            null,
            new SammelBedingung(_sammelTyp, _anzahl),
            produziertCommands: new[] { typeof(TCmd) }));
}

/// <summary>Count-Join nach Zwei-Wege-Join: Auslöser <typeparamref name="TE1"/> + <typeparamref name="TE2"/> + ALLE <typeparamref name="TSammel"/>.</summary>
public sealed class RegelBauerSammel<TE1, TE2, TSammel>
    where TE1 : IEvent
    where TE2 : IEvent
    where TSammel : IEvent
{
    private readonly RegelBauer _bauer;
    private readonly Type[] _bedingung;
    private readonly Type _sammelTyp;
    private readonly Func<IEvent, int> _anzahl;

    internal RegelBauerSammel(RegelBauer bauer, Type[] bedingung, Type sammelTyp, Func<IEvent, int> anzahl)
    {
        _bauer = bauer; _bedingung = bedingung; _sammelTyp = sammelTyp; _anzahl = anzahl;
    }

    public void Sende<TCmd>(Func<TE1, TE2, IReadOnlyList<TSammel>, TCmd> baue) where TCmd : ICommand
        => _bauer.Registriere(new Regel(
            _bedingung,
            evts => new ICommand[] { baue((TE1)evts[0], (TE2)evts[1], evts.Skip(_bedingung.Length).Cast<TSammel>().ToList()) },
            null,
            new SammelBedingung(_sammelTyp, _anzahl),
            produziertCommands: new[] { typeof(TCmd) }));
}

// ── Arität 2: Auf<E1>().Und<E2> ──

/// <summary>Ein Join zweier Bedingungs-Events (hält Diamanten/maximale Parallelität offen, Spec §3).</summary>
public sealed class RegelBauer<TE1, TE2>
    where TE1 : IEvent
    where TE2 : IEvent
{
    private readonly RegelBauer _bauer;
    private readonly Type[] _bedingung;
    internal RegelBauer(RegelBauer bauer, Type[] bedingung) { _bauer = bauer; _bedingung = bedingung; }

    public RegelBauer<TE1, TE2, TE3> Und<TE3>() where TE3 : IEvent
        => new(_bauer, _bedingung.Append(typeof(TE3)).ToArray());

    /// <summary>Count-Join nach einem Zwei-Wege-Join: Auslöser + Und-Event + ALLE N <typeparamref name="TSammel"/>.</summary>
    public RegelBauerSammel<TE1, TE2, TSammel> UndAlle<TSammel>(Func<TE1, int> anzahl) where TSammel : IEvent
        => new(_bauer, _bedingung, typeof(TSammel), e => anzahl((TE1)e));

    public RegelAbschluss<TE1, TE2> Sende<TCmd>(Func<TE1, TE2, TCmd> baue) where TCmd : ICommand
        => Abschluss(new[] { typeof(TCmd) }, evts => new ICommand[] { baue((TE1)evts[0], (TE2)evts[1]) });

    public RegelAbschluss<TE1, TE2> SendeJe<TCmd>(Func<TE1, TE2, IEnumerable<TCmd>> baue) where TCmd : ICommand
        => Abschluss(new[] { typeof(TCmd) }, evts => baue((TE1)evts[0], (TE2)evts[1]).Cast<ICommand>().ToArray());

    private RegelAbschluss<TE1, TE2> Abschluss(Type[] cmdTypen, Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>> sende)
        => new(_bauer.Registriere(new Regel(_bedingung, sende, null, produziertCommands: cmdTypen)));
}

public readonly struct RegelAbschluss<TE1, TE2>
    where TE1 : IEvent
    where TE2 : IEvent
{
    private readonly RegelHandle _handle;
    internal RegelAbschluss(RegelHandle handle) { _handle = handle; }

    public void RückgängigDurch<TCmd>(Func<TE1, TE2, TCmd> baue) where TCmd : ICommand
        => _handle.SetzeRückgängig(evts => new ICommand[] { baue((TE1)evts[0], (TE2)evts[1]) });
}

// ── Arität 3: Auf<E1>().Und<E2>().Und<E3> (z.B. Buchen erst nach Reservieren UND Gutschreiben) ──

/// <summary>Ein Join dreier Bedingungs-Events.</summary>
public sealed class RegelBauer<TE1, TE2, TE3>
    where TE1 : IEvent
    where TE2 : IEvent
    where TE3 : IEvent
{
    private readonly RegelBauer _bauer;
    private readonly Type[] _bedingung;
    internal RegelBauer(RegelBauer bauer, Type[] bedingung) { _bauer = bauer; _bedingung = bedingung; }

    public RegelAbschluss<TE1, TE2, TE3> Sende<TCmd>(Func<TE1, TE2, TE3, TCmd> baue) where TCmd : ICommand
        => Abschluss(new[] { typeof(TCmd) }, evts => new ICommand[] { baue((TE1)evts[0], (TE2)evts[1], (TE3)evts[2]) });

    public RegelAbschluss<TE1, TE2, TE3> SendeJe<TCmd>(Func<TE1, TE2, TE3, IEnumerable<TCmd>> baue) where TCmd : ICommand
        => Abschluss(new[] { typeof(TCmd) }, evts => baue((TE1)evts[0], (TE2)evts[1], (TE3)evts[2]).Cast<ICommand>().ToArray());

    private RegelAbschluss<TE1, TE2, TE3> Abschluss(Type[] cmdTypen, Func<IReadOnlyList<IEvent>, IReadOnlyList<ICommand>> sende)
        => new(_bauer.Registriere(new Regel(_bedingung, sende, null, produziertCommands: cmdTypen)));
}

public readonly struct RegelAbschluss<TE1, TE2, TE3>
    where TE1 : IEvent
    where TE2 : IEvent
    where TE3 : IEvent
{
    private readonly RegelHandle _handle;
    internal RegelAbschluss(RegelHandle handle) { _handle = handle; }

    public void RückgängigDurch<TCmd>(Func<TE1, TE2, TE3, TCmd> baue) where TCmd : ICommand
        => _handle.SetzeRückgängig(evts => new ICommand[] { baue((TE1)evts[0], (TE2)evts[1], (TE3)evts[2]) });
}
