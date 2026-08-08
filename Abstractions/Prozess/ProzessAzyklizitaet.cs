using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Der Azyklizitäts-Check über den Regel-Graph (Spec §3, offene Entscheidung 3). Baut aus den Regeln die
/// Kanten <c>Bedingungs-Event → Command</c> (die Regel triggert) und <c>Command → produziertes Event</c>
/// (aus den Decidern, via <paramref name="produziert"/>) und meldet jeden Zyklus. Das ersetzt die
/// „rückwärts-only"-Compile-Regel der alten Schrittliste durch eine echte Graph-Analyse.
///
/// Als BOOT-GUARD gedacht (fail-fast beim Start, statt eines separaten Roslyn-Analyzers): die
/// Command-Typen liest der Check aus <see cref="Regel.ProduziertCommands"/> (beim Bauen per Typinferenz
/// erfasst), die Command→Event-Abbildung liefert der Aufrufer (generiert aus den Decidern). Der reine
/// Zyklus-Kern (<see cref="ProzessGraph"/>) ist separat im Prüfstand bewiesen.
/// </summary>
public static class ProzessAzyklizität
{
    /// <summary>Wirft, wenn der Regel-Graph des Prozesses einen Zyklus enthält.</summary>
    public static void Prüfe(string prozessName, ProzessRegeln regeln, Func<Type, IEnumerable<Type>> produziert)
    {
        var tupel = new List<(IReadOnlyList<string> Bedingung, string Command)>();
        var produziertMap = new Dictionary<string, IReadOnlyList<string>>();

        foreach (var regel in regeln.Regeln)
        {
            var bedingung = regel.Bedingung.Select(Name).ToList();
            foreach (var cmd in regel.ProduziertCommands)
            {
                tupel.Add((bedingung, Name(cmd)));
                if (!produziertMap.ContainsKey(Name(cmd)))
                    produziertMap[Name(cmd)] = produziert(cmd).Select(Name).ToList();
            }
        }

        var kanten = ProzessGraph.BaueKanten(tupel, produziertMap);
        var zyklus = ProzessGraph.FindeZyklus(kanten);
        if (zyklus.Count > 0)
            throw new InvalidOperationException(
                $"Prozess '{prozessName}' ist NICHT azyklisch: {string.Join(" → ", zyklus)}. " +
                "Der Regel-Graph (Command → Event → Command) muss ein DAG sein (Spec §3).");
    }

    /// <summary>Prüft alle Prozesse einer Registry.</summary>
    public static void PrüfeAlle(
        IReadOnlyDictionary<string, ProzessRegeln> registry, Func<Type, IEnumerable<Type>> produziert)
    {
        foreach (var kv in registry)
            Prüfe(kv.Key, kv.Value, produziert);
    }

    private static string Name(Type t) => t.FullName ?? t.Name;
}
