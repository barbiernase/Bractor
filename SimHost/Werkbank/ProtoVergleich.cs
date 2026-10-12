using System.Text.RegularExpressions;

namespace SimHost.Werkbank;

/// <summary>
/// Passen die Daten der Sim-DB noch zum Code? (docs/konzept-werkbank.md §4.6) — Vergleich des generierten <c>domain.proto</c> des
/// letzten Laufs mit dem neuen: ein Event-Typ, der verschwindet, oder ein Feld, das entfällt oder seinen Typ wechselt, macht die
/// gespeicherten Events unlesbar → die Sim-DB wird zurückgesetzt (Schema-Evolution ist kein Ziel). Neue Typen/Felder sind harmlos.
/// </summary>
public static partial class ProtoVergleich
{
    [GeneratedRegex(@"message\s+(?<name>\w+)\s*\{(?<rumpf>[^{}]*)\}", RegexOptions.Singleline)]
    private static partial Regex Nachricht();

    [GeneratedRegex(@"^\s*(?<typ>(optional\s+|repeated\s+)?[\w.<>, ]+?)\s+(?<feld>\w+)\s*=\s*\d+\s*;", RegexOptions.Multiline)]
    private static partial Regex Feld();

    [GeneratedRegex(@"message\s+EventEnvelopeDto\s*\{.*?oneof\s+payload\s*\{(?<liste>[^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex EventListe();

    /// <summary>Die Event-Nachrichten (Payload-Fälle des Event-Umschlags) mit ihren Feldern (Name → Typ).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Events(string proto)
    {
        var liste = EventListe().Match(proto);
        var events = liste.Success
            ? Feld().Matches(liste.Groups["liste"].Value).Select(m => m.Groups["typ"].Value.Trim()).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var aus = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (Match m in Nachricht().Matches(proto))
            if (events.Contains(m.Groups["name"].Value))
                aus[m.Groups["name"].Value] = Feld().Matches(m.Groups["rumpf"].Value)
                    .ToDictionary(f => f.Groups["feld"].Value, f => f.Groups["typ"].Value.Trim(), StringComparer.Ordinal);
        return aus;
    }

    /// <summary>Die Unverträglichkeiten von <paramref name="alt"/> nach <paramref name="neu"/> (leer = die Daten passen).</summary>
    public static IReadOnlyList<string> Unvertraeglich(string alt, string neu)
    {
        var a = Events(alt);
        var n = Events(neu);
        var aus = new List<string>();
        foreach (var (ev, felder) in a)
        {
            if (!n.TryGetValue(ev, out var neuFelder)) { aus.Add($"Event {ev.Replace("Dto", "")} gibt es nicht mehr"); continue; }
            foreach (var (feld, typ) in felder)
            {
                if (!neuFelder.TryGetValue(feld, out var neuTyp)) aus.Add($"{ev.Replace("Dto", "")}.{feld} entfällt");
                else if (neuTyp != typ) aus.Add($"{ev.Replace("Dto", "")}.{feld}: {typ} → {neuTyp}");
            }
        }
        return aus;
    }
}
