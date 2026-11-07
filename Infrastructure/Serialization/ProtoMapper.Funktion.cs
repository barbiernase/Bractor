using Abstractions;
using Cqrs.Leitung;

namespace Infrastructure.Serialization;

/// <summary>
/// Auftrag und Antwort der Arbeit eines Akteurs draußen über die generierte Nutzlast-Tabelle — reflexionsfrei (Invariante 4).
/// </summary>
public partial class ProtoMessageMapper
{
    /// <summary>Der Auftrag als Nutzlast: ein Command ohne Ziel.</summary>
    public Nutzlast Nachricht(object eingang) => eingang is IEvent e ? _tabelle.PackeEvent(e) : _tabelle.PackeNachricht(eingang);

    /// <summary>
    /// Die Hülle eines Commands ohne Ziel vom Client (an den Kopf eines Graphen): die Command-Id ist die Kennung des offenen Eingangs — wer
    /// wiederholt, schickt dieselbe; ohne Aggregat und ohne erwartete Version.
    /// </summary>
    public CommandEnvelopeDto NachrichtAlsDto(ICommand nachricht, Guid commandId, string korrelation, string? sitzung, string? ursache = null) => new()
    {
        CommandId = commandId.ToString(),
        AggregateId = Guid.Empty.ToString(),
        AggregateType = string.Empty,
        ExpectedVersion = -1,
        CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        CorrelationId = korrelation,
        OriginSessionId = sitzung ?? string.Empty,
        Ursache = ursache ?? string.Empty,
        Payload = _tabelle.PackeNachricht(nachricht),
    };

    /// <summary>Ein Command ohne Ziel in einer Nutzlast (an einen Graph-Kopf oder einen Akteur); null = ein Command an ein Aggregat.</summary>
    public ICommand? CommandOhneZiel(Nutzlast n)
    {
        try { _tabelle.Command(n); return null; }
        catch (NotSupportedException) { return _tabelle.Nachricht(n) as ICommand; }
    }

    /// <summary>
    /// Ein Command der Antwort — an ein Aggregat oder ohne Ziel (an den Kopf eines Graphen); nur die Nutzlast: Stream, Korrelation und Akteur stempelt
    /// der Server selbst, ein Client muss sie nicht (und darf sie nicht) setzen.
    /// </summary>
    public object Antwortcommand(Nutzlast n)
    {
        try { return _tabelle.Command(n); }
        catch (NotSupportedException) { return _tabelle.Nachricht(n); }   // kein Command an ein Aggregat: einer ohne Ziel
    }
}
