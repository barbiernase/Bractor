using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DER REINE KERN DES DIRIGENTEN (docs/konzept-editor-pipelines.md §14.5): welche Tokens eine Regel belegen. Herkunft (ein Draht
// kommt von genau einem Knoten), Je-Teile (ein ∧ verbindet nur Tokens desselben Elements) und Sammeln (ein Token je Element,
// geordnet). Store-frei und ohne Zeit — der Prozess-Manager faltet seine Tokens aus dem Log, die Simulation (Cqrs.Testing) hält sie
// im Speicher; beide entscheiden hier, mit DERSELBEN Semantik, was feuert.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Token des Prozess-Netzes: Nachricht, Herkunft (Knoten-Index, −1 = klassisch) und seine Je-Teile.</summary>
public interface IFlussToken
{
    IEvent Payload { get; }
    int Herkunft { get; }
    JeTeile Teile { get; }
}

/// <summary>
/// Die Je-Teile eines Tokens: je JE-Rahmen (Knoten-Index) der Element-Index, aus dem es stammt. Zwei Tokens sind verträglich,
/// wenn sie in jedem gemeinsamen Rahmen vom selben Element kommen. Unveränderlich; leer für klassische Prozesse.
/// </summary>
public sealed class JeTeile
{
    public static readonly JeTeile Leer = new(new SortedDictionary<int, int>());
    private readonly SortedDictionary<int, int> _d;
    private JeTeile(SortedDictionary<int, int> d) { _d = d; }

    public bool Hat(int je) => _d.ContainsKey(je);
    public int Von(int je) => _d[je];
    public IReadOnlyDictionary<int, int> Elemente => _d;

    public JeTeile Mit(int je, int element)
        => new(new SortedDictionary<int, int>(_d) { [je] = element });

    public JeTeile Ohne(int je)
    {
        if (!_d.ContainsKey(je)) return this;
        var d = new SortedDictionary<int, int>(_d);
        d.Remove(je);
        return new JeTeile(d);
    }

    public bool VerträglichMit(JeTeile andere)
        => _d.All(kv => !andere._d.TryGetValue(kv.Key, out var w) || w == kv.Value);

    public static bool AlleVerträglich<T>(IReadOnlyList<T> tokens) where T : IFlussToken
    {
        for (var i = 0; i < tokens.Count; i++)
            for (var j = i + 1; j < tokens.Count; j++)
                if (!tokens[i].Teile.VerträglichMit(tokens[j].Teile)) return false;
        return true;
    }

    public static JeTeile Vereinige<T>(IEnumerable<T> tokens) where T : IFlussToken
    {
        SortedDictionary<int, int>? d = null;
        foreach (var t in tokens)
            foreach (var kv in t.Teile._d)
                (d ??= new SortedDictionary<int, int>())[kv.Key] = kv.Value;
        return d is null ? Leer : new JeTeile(d);
    }
}

/// <summary>Welche Tokens eine <see cref="Regel"/> belegen und welche Teile ihre Aufrufe tragen.</summary>
public static class FlussBelegung
{
    /// <summary>
    /// Alle Belegungen einer Regel: für normale Regeln die kartesischen Konjunktions-Matches; für einen COUNT-JOIN die
    /// Bedingungs-Matches, an die ALLE Sammel-Tokens angehängt werden — aber nur, wenn ihre Zahl die aus dem Auslöser abgeleitete
    /// Breite erreicht (buche erst nach allen N). Beim Sammeln eines JE-Rahmens (Pipeline-Fluss) zählt genau ein Token je Element,
    /// geordnet nach Element-Index; eine leere Liste sammelt sofort.
    /// </summary>
    public static IEnumerable<IReadOnlyList<T>> Belegungen<T>(Regel regel, IReadOnlyList<T> tokens) where T : IFlussToken
    {
        if (regel.Sammel is null)
        {
            foreach (var m in Matches(regel, tokens)) yield return m;
            yield break;
        }

        var je = regel.Sammel.JeKnoten;
        foreach (var trig in Matches(regel, tokens))   // Matches nutzt regel.Bedingung (nur der/die Auslöser)
        {
            var trigTeile = JeTeile.Vereinige(trig);
            var sammel = tokens.Where(t =>
                    regel.Sammel.Drähte.Any(d => d.Typ.IsInstanceOfType(t.Payload) && (d.Von is null || d.Von == t.Herkunft)) &&
                    (je is null || t.Teile.Hat(je.Value)) &&
                    (je is null ? t.Teile : t.Teile.Ohne(je.Value)).VerträglichMit(trigTeile))
                .ToList();
            var erwartet = regel.Sammel.Anzahl(trig[0].Payload);
            if (je is int j)
            {
                var jeElement = sammel.GroupBy(t => t.Teile.Von(j)).Select(g => g.First()).OrderBy(t => t.Teile.Von(j)).ToList();
                if (erwartet >= 0 && jeElement.Count >= erwartet)
                    yield return trig.Concat(jeElement.Take(erwartet)).ToList();
            }
            else if (erwartet > 0 && sammel.Count >= erwartet)
                yield return trig.Concat(sammel).ToList();
        }
    }

    /// <summary>
    /// Kartesische Konjunktions-Matches über <see cref="Regel.Bedingung"/> (pro Typ die passenden Tokens). Im Pipeline-Fluss
    /// zählt zusätzlich die HERKUNFT (ein Draht kommt von genau einem Knoten) und die Verträglichkeit der Teile (ein ∧ verbindet
    /// nur Tokens desselben Je-Elements).
    /// </summary>
    public static IEnumerable<IReadOnlyList<T>> Matches<T>(Regel regel, IReadOnlyList<T> tokens) where T : IFlussToken
    {
        var perTyp = regel.Bedingung
            .Select((t, i) => tokens.Where(tok => t.IsInstanceOfType(tok.Payload) &&
                                                  (regel.VonKnoten[i] is not int von || tok.Herkunft == von)).ToList())
            .ToList();
        if (perTyp.Any(l => l.Count == 0)) yield break;

        var indizes = new int[perTyp.Count];
        while (true)
        {
            var kombi = Enumerable.Range(0, perTyp.Count).Select(i => perTyp[i][indizes[i]]).ToList();
            if (JeTeile.AlleVerträglich(kombi)) yield return kombi;

            int k = perTyp.Count - 1;
            while (k >= 0 && ++indizes[k] >= perTyp[k].Count) { indizes[k] = 0; k--; }
            if (k < 0) yield break;
        }
    }

    /// <summary>
    /// Die Teile, die der <paramref name="aufruf"/>-te Aufruf einer Belegung (und damit sein Ergebnis-Token) trägt: die der gematchten
    /// Tokens — beim Sammeln nur die des Auslösers (die Element-Teile werden dort zusammengefasst), beim Je-Auffächern plus das eigene
    /// Element.
    /// </summary>
    public static JeTeile TeileDesAufrufs<T>(Regel regel, IReadOnlyList<T> belegung, int aufruf) where T : IFlussToken
    {
        var basis = JeTeile.Vereinige(regel.Sammel is null ? belegung : belegung.Take(regel.Bedingung.Count));
        return regel.JeKnoten is int je ? basis.Mit(je, aufruf) : basis;
    }
}
