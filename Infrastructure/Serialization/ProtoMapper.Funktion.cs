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
    /// Ein Command der Antwort — an ein Aggregat oder ohne Ziel (an den Kopf eines Graphen); nur die Nutzlast: Stream, Korrelation und Akteur stempelt
    /// der Server selbst, ein Client muss sie nicht (und darf sie nicht) setzen.
    /// </summary>
    public object Antwortcommand(Nutzlast n)
    {
        try { return _tabelle.Command(n); }
        catch (NotSupportedException) { return _tabelle.Nachricht(n); }   // kein Command an ein Aggregat: einer ohne Ziel
    }
}
