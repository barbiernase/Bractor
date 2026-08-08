using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Zyklus-Erkennung auf dem Regel-Graph (Spec §3, letzter Absatz): <c>Command → produziert → Event →
/// triggert → Command</c> muss AZYKLISCH sein — das ersetzt die „rückwärts-only"-Compile-Regel der alten
/// Schrittliste durch eine statische Analyse (der eine Preis der höheren Ausdruckskraft).
///
/// Die Kanten-EXTRAKTION (welcher Command welches Event produziert) liest der Roslyn-Analyzer (Build-Zeit)
/// aus den Decidern; DIESE Klasse ist der pure, testbare Kern der Prüfung: gegeben ein gerichteter Graph
/// (Knoten = Typnamen, Kanten = „führt zu"), meldet sie jeden Zyklus. So ist der Algorithmus im Prüfstand
/// beweisbar, unabhängig von der Syntax-Extraktion.
/// </summary>
public static class ProzessGraph
{
    /// <summary>
    /// Liefert einen gefundenen Zyklus als Knotenfolge (leer ⇒ azyklisch). <paramref name="kanten"/> bildet
    /// jeden Knoten auf seine direkten Nachfolger ab (Event→Commands und Command→Events zusammengeführt).
    /// </summary>
    public static IReadOnlyList<string> FindeZyklus(IReadOnlyDictionary<string, IReadOnlyList<string>> kanten)
    {
        var farbe = new Dictionary<string, int>();   // 0=weiß, 1=grau(im Stack), 2=schwarz
        var stack = new List<string>();

        var knoten = kanten.Keys.Concat(kanten.Values.SelectMany(v => v)).Distinct();
        foreach (var start in knoten)
        {
            var zyklus = Besuche(start, kanten, farbe, stack);
            if (zyklus.Count > 0) return zyklus;
        }
        return Array.Empty<string>();
    }

    private static List<string> Besuche(
        string knoten,
        IReadOnlyDictionary<string, IReadOnlyList<string>> kanten,
        Dictionary<string, int> farbe,
        List<string> stack)
    {
        farbe.TryGetValue(knoten, out var f);
        if (f == 2) return new List<string>();
        if (f == 1)
        {
            // Zurück-Kante: Zyklus vom ersten Auftreten bis jetzt.
            var ab = stack.IndexOf(knoten);
            var z = stack.Skip(ab).ToList();
            z.Add(knoten);
            return z;
        }

        farbe[knoten] = 1;
        stack.Add(knoten);
        if (kanten.TryGetValue(knoten, out var nachfolger))
            foreach (var n in nachfolger)
            {
                var z = Besuche(n, kanten, farbe, stack);
                if (z.Count > 0) return z;
            }
        stack.RemoveAt(stack.Count - 1);
        farbe[knoten] = 2;
        return new List<string>();
    }

    /// <summary>
    /// Baut die Graph-Kanten aus den Regeln + einer Command→Events-Abbildung (die der Analyzer aus den
    /// Decidern kennt): Event→Command (die Regel triggert) und Command→Event (der Decider produziert).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> BaueKanten(
        IEnumerable<(IReadOnlyList<string> Bedingung, string Command)> regeln,
        IReadOnlyDictionary<string, IReadOnlyList<string>> commandProduziert)
    {
        var kanten = new Dictionary<string, HashSet<string>>();
        void Add(string von, string nach)
        {
            if (!kanten.TryGetValue(von, out var s)) { s = new HashSet<string>(); kanten[von] = s; }
            s.Add(nach);
        }

        foreach (var (bedingung, command) in regeln)
        {
            foreach (var evt in bedingung) Add(evt, command);          // Event triggert Command
            if (commandProduziert.TryGetValue(command, out var evts))
                foreach (var e in evts) Add(command, e);               // Command produziert Event
        }
        return kanten.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.ToList());
    }
}
