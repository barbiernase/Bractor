using System.Reflection;
using System.Text;
using Abstractions;

namespace Cqrs.Testing;

/// <summary>
/// Flache Momentaufnahme aller lesbaren öffentlichen Zustands-Felder — inkl. abgeleiteter
/// (z.B. <c>Verfuegbar</c>), weil die gerade beim Debuggen die interessanten sind.
///
/// Nutzt Reflection: das ist bewusst NUR im Werkzeug erlaubt (Anzeige/Inspektion, kein
/// Dispatch, nicht im Produktionspfad, nicht in der Domäne) — dieselbe pragmatische Linie
/// wie SimEngine. Invariante 4 (keine Runtime-Reflection) gilt der DISPATCH-Logik, nicht
/// einem Zustands-Spiegel für die Ausgabe.
/// </summary>
internal static class Zustandsspiegel
{
    public static IReadOnlyDictionary<string, object?> Von(IState state)
    {
        var map = new Dictionary<string, object?>();
        foreach (var p in state.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            if (p.Name is nameof(IState.Id)) continue; // Rauschen: Framework-Pflichtfeld
            object? wert;
            try { wert = p.GetValue(state); }
            catch { wert = "<Lesefehler>"; }
            map[p.Name] = wert;
        }
        return map;
    }
}

/// <summary>
/// Rendert eine <see cref="SzenarioTrace"/> als lesbaren Entscheidungs-Bericht — die erste,
/// GUI-freie Scheibe des Entscheidungs-Debuggers. Zeigt je Schritt: Zustand vorher → der vom
/// Decider gewählte Zweig (persistent/ABGELEHNT) → Zustand nachher.
/// </summary>
internal static class Berichte
{
    public static string Schreibe(SzenarioTrace t)
    {
        var sb = new StringBuilder();
        sb.Append("Szenario ").Append(t.AggregatTyp).Append(" (").Append(t.AggregatId.ToString("N")[..8]).AppendLine(")");

        if (t.Gegeben.Count > 0)
            foreach (var e in t.Gegeben)
                sb.Append("  Gegeben: ").AppendLine(Wert(e));

        foreach (var s in t.Schritte)
        {
            sb.Append("  [").Append(s.Herkunft == SchrittHerkunft.Wenn ? "Wenn " : "Vorab").Append("] ")
              .AppendLine(Wert(s.Command));
            sb.Append("     vorher:  ").AppendLine(Felder(s.ZustandVorher));
            foreach (var a in s.Ausgang)
                sb.Append("     → ").Append(Wert(a.Event))
                  .Append(a.Persistent ? "   (persistent)" : "   (ABGELEHNT)").AppendLine();
            if (s.Ausgang.All(a => a.Ablehnung))
                sb.AppendLine("     nachher: (unverändert)");
            else
                sb.Append("     nachher: ").AppendLine(Felder(s.ZustandNachher));
        }
        return sb.ToString();
    }

    public static string Felder(IReadOnlyDictionary<string, object?> m)
        => string.Join(", ", m.Select(kv => $"{kv.Key}={kv.Value}"));

    public static string Liste(IEnumerable<IEvent> events)
    {
        var s = string.Join(", ", events.Select(Wert));
        return s.Length == 0 ? "(nichts)" : "[" + s + "]";
    }

    public static string Wert(object? o) => o?.ToString() ?? "null";
}
