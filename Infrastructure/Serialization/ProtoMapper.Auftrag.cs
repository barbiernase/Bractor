using Abstractions;
using Cqrs.Leitung;

namespace Infrastructure.Serialization;

/// <summary>
/// Auftrags-/Ergebnis-Mapping für externe Funktions-Ausführer (docs/konzept-editor-pipelines.md §14.5).
/// Delegiert an die generierten Helfer — reflexionsfrei (Invariante 4).
/// </summary>
public partial class ProtoMessageMapper
{
    // =========================================================================
    // AUFTRAG MAPPING (Katalog-Funktionen)
    // =========================================================================

    public IAuftrag AuftragAus(Nutzlast n)
    {
        return _tabelle.Auftrag(n);
    }

    public Nutzlast MapToDto(IAuftrag auftrag)
    {
        return _tabelle.PackeAuftrag(auftrag);
    }

    /// <summary>Der Eingang einer Funktion draußen als Nutzlast (ein Event aus dem Log, eine Quell-Nachricht, ein Hinweis).</summary>
    public Nutzlast MapToDto(IEvent eingang) => _tabelle.PackeEvent(eingang);

    /// <summary>
    /// Eine Ausgabe einer Funktion draußen — nur die Nutzlast; Stream, Korrelation und Akteur stempelt der Funktions-Actor selbst, ein Träger
    /// muss sie nicht (und darf sie nicht) setzen.
    /// </summary>
    public IPipelineOutput Ausgabe(FunktionsAusgabe a) => a.ArtCase switch
    {
        FunktionsAusgabe.ArtOneofCase.Command => _tabelle.Command(a.Command),
        FunktionsAusgabe.ArtOneofCase.Event => _tabelle.Event(a.Event),
        FunktionsAusgabe.ArtOneofCase.Hinweis => _tabelle.Event(a.Hinweis),
        _ => throw new MappingException("Eine Ausgabe ohne Art (Command, Event oder Hinweis)."),
    };
}
