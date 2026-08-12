namespace Cqrs.Testing;

/// <summary>
/// Sammelt aus gefahrenen Szenarien die berührten Graph-Elemente (Command-Knoten, Event-Knoten,
/// produces-Zweige) als stabile Ids — <c>cmd:X</c>, <c>evt:Y</c>, <c>produces:X-&gt;Y</c>. Über den
/// Wissensgraphen gelegt ergibt das die Abdeckung: welche Entscheidungs-Zweige ein Test/eine
/// Board-Session je gefeuert hat (grün) — und welche nie (grau). Keine Prozente, sondern das
/// erleuchtete Domänenmodell.
/// </summary>
public sealed class Abdeckung
{
    private readonly HashSet<string> _ids = new();

    public IReadOnlyCollection<string> Ids => _ids;

    public Abdeckung Erfasse(SzenarioTrace trace)
    {
        foreach (var s in trace.Schritte)
            ErfasseSchritt(s.Command.GetType().Name, s.Ausgang);
        return this;
    }

    public Abdeckung Erfasse(SagaTrace trace)
    {
        foreach (var s in trace.Schritte)
            if (!s.Unrouted)
                ErfasseSchritt(s.Command.GetType().Name, s.Ausgang);
        return this;
    }

    private void ErfasseSchritt(string command, IReadOnlyList<Ereignis> ausgang)
    {
        _ids.Add("cmd:" + command);
        foreach (var e in ausgang)
        {
            _ids.Add("evt:" + e.Typ);
            _ids.Add($"produces:{command}->{e.Typ}");
        }
    }

    /// <summary>Die berührten Ids als JSON-Array (für die Board-Overlay-Datei / den /api/coverage-Endpunkt).</summary>
    public string AlsJson()
        => "[" + string.Join(",", _ids.OrderBy(x => x, StringComparer.Ordinal).Select(x => "\"" + x + "\"")) + "]";
}
