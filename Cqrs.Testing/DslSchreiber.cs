using System.Globalization;
using System.Reflection;
using System.Text;
using Abstractions;

namespace Cqrs.Testing;

/// <summary>
/// Erzeugt aus einem gefahrenen Szenario den entsprechenden DSL-QUELLTEXT
/// (<c>Szenario.Für&lt;…&gt;()…Gegeben/Vorab/Wenn/Dann</c>). Damit wird die Richtung geschlossen:
/// die GUI/das Board fährt ein Szenario interaktiv, und der Schreiber gießt es in einen
/// Regressionstest — „Board-Session als Test exportieren". Reine Text-Erzeugung, unit-testbar.
///
/// Guids werden auf stabile Variablennamen (<c>id</c>, <c>id2</c>, …) abgebildet: die konkreten
/// Werte sind für einen Test bedeutungslos, die BEZIEHUNGEN (dasselbe Konto) bleiben erhalten.
/// </summary>
public static class DslSchreiber
{
    /// <summary>Aus einer <see cref="SzenarioTrace"/> (die letzte Stufe = das getestete Wenn).</summary>
    public static string AusSzenario(SzenarioTrace trace)
    {
        var vorab = trace.Schritte.Where(s => s.Herkunft == SchrittHerkunft.Vorab).Select(s => s.Command).ToList();
        var wenn = trace.Letzter;
        return Aus(trace.AggregatTyp, trace.Gegeben, vorab, wenn.Command, wenn.Ausgang);
    }

    /// <summary>Die tragende Erzeugung aus den Rohteilen — auch direkt vom SimHost aus aufrufbar.</summary>
    public static string Aus(
        string aggregatTyp,
        IReadOnlyList<IEvent> gegeben,
        IReadOnlyList<ICommand> vorab,
        ICommand wenn,
        IReadOnlyList<Ereignis> wennAusgang)
    {
        var ids = new GuidNamen();
        ids.NameVon(wenn.AggregateId); // sichert, dass das Ziel-Aggregat "id" heißt

        var body = new StringBuilder();
        body.Append("Szenario.Für<").Append(aggregatTyp).AppendLine(">(new AggregateHandlerFactory(), id)");
        if (gegeben.Count > 0)
            body.Append("    .Gegeben(").Append(Liste(gegeben, ids)).AppendLine(")");
        if (vorab.Count > 0)
            body.Append("    .Vorab(").Append(Liste(vorab, ids)).AppendLine(")");
        body.Append("    .Wenn(").Append(Neu(wenn, ids)).AppendLine(")");
        body.AppendLine(Zusicherung(wennAusgang));

        var kopf = new StringBuilder();
        foreach (var name in ids.Namen)
            kopf.Append("var ").Append(name).AppendLine(" = Guid.NewGuid();");
        return kopf.Append(body).ToString();
    }

    private static string Zusicherung(IReadOnlyList<Ereignis> ausgang)
    {
        var ablehnung = ausgang.FirstOrDefault(a => a.Ablehnung);
        if (ablehnung is not null) return $"    .DannAbgelehnt<{ablehnung.Typ}>();";
        var persistent = ausgang.FirstOrDefault(a => a.Persistent);
        if (persistent is not null) return $"    .Dann<{persistent.Typ}>();";
        return "    .DannKeineAblehnung();";
    }

    private static string Liste(IEnumerable<object> msgs, GuidNamen ids)
        => string.Join(", ", msgs.Select(m => Neu(m, ids)));

    private static string Neu(object msg, GuidNamen ids)
    {
        var t = msg.GetType();
        var ctor = t.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var args = ctor.GetParameters().Select(p =>
        {
            var prop = t.GetProperty(p.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return Wert(prop?.GetValue(msg), ids);
        });
        return $"new {t.Name}({string.Join(", ", args)})";
    }

    private static string Wert(object? v, GuidNamen ids) => v switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        Guid g => ids.NameVon(g),
        decimal d => d.ToString(CultureInfo.InvariantCulture) + "m",
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture) + "L",
        double db => db.ToString(CultureInfo.InvariantCulture) + "d",
        string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"",
        Enum e => e.GetType().Name + "." + e,
        _ => $"/* {v.GetType().Name}: {v} */ default"
    };

    /// <summary>Bildet Guids stabil auf <c>id</c>, <c>id2</c>, … in Erst-Sicht-Reihenfolge ab.</summary>
    private sealed class GuidNamen
    {
        private readonly Dictionary<Guid, string> _m = new();

        public string NameVon(Guid g)
        {
            if (_m.TryGetValue(g, out var n)) return n;
            n = _m.Count == 0 ? "id" : $"id{_m.Count + 1}";
            _m[g] = n;
            return n;
        }

        public IEnumerable<string> Namen => _m.Values;
    }
}
