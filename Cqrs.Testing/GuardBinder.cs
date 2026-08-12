using Abstractions;

namespace Cqrs.Testing;

/// <summary>
/// Bindet einen Guard-Ausdruck aus der Decider-Syntax (z.B. <c>State.Verfuegbar &lt; cmd.Betrag</c>)
/// mit echten Werten zu einem lesbaren „<c>70 &lt; 200</c>". Rein TEXTUELL — keine Auswertung, keine
/// Roslyn-Skript-Engine: <c>State.X</c>/<c>this.State.X</c> → Zustandswert, <c>cmd.Y</c> → Command-Wert.
/// Damit wird das statisch gehobene „Warum" (aus dem Graphen) am konkreten Fall sichtbar.
/// </summary>
public static class GuardBinder
{
    public static string Binde(string ausdruck, IReadOnlyDictionary<string, object?> zustand, IReadOnlyDictionary<string, object?> command)
    {
        var s = ausdruck;
        // Längere Feldnamen zuerst ersetzen, damit z.B. „Verfuegbar" nicht von „Ver" zerlegt wird.
        foreach (var kv in zustand.OrderByDescending(k => k.Key.Length))
        {
            s = s.Replace("this.State." + kv.Key, Fmt(kv.Value));
            s = s.Replace("State." + kv.Key, Fmt(kv.Value));
        }
        foreach (var kv in command.OrderByDescending(k => k.Key.Length))
            s = s.Replace("cmd." + kv.Key, Fmt(kv.Value));
        return s;
    }

    /// <summary>Bequemlichkeit: Command-Werte per Reflection ziehen (Tooling — Anzeige, nicht Dispatch).</summary>
    public static string Binde(string ausdruck, IReadOnlyDictionary<string, object?> zustand, ICommand command)
        => Binde(ausdruck, zustand, Felder(command));

    private static IReadOnlyDictionary<string, object?> Felder(object o)
    {
        var map = new Dictionary<string, object?>();
        foreach (var p in o.GetType().GetProperties())
            if (p.GetIndexParameters().Length == 0)
                try { map[p.Name] = p.GetValue(o); } catch { /* ignore */ }
        return map;
    }

    private static string Fmt(object? v) => v switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        _ => v.ToString() ?? "null"
    };
}
