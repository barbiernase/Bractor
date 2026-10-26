using Abstractions;

namespace Cqrs.Leitung;

/// <summary>
/// Die Nutzlast-Tabelle einer Anwendung: zwischen Domänen-Nachricht und <see cref="Nutzlast"/> (Typname + Bytes der {Typ}Dto-Message aus dem
/// Proto der Anwendung). Generiert im Leitungs-Projekt der Anwendung (<c>&lt;Wurzel&gt;.Leitung</c>, <c>GeneratedNutzlast</c>, docs/07); das Framework
/// (Server-Hüllen in <c>ProtoMessageMapper</c>, C#-Client) kennt nur diese Schnittstelle, keinen Domänen-Typ.
/// </summary>
public interface INutzlastTabelle
{
    ICommand Command(Nutzlast n);
    IEvent Event(Nutzlast n);
    IQuery Query(Nutzlast n);
    IQueryResponse Antwort(Nutzlast n);

    Nutzlast PackeCommand(ICommand command);
    Nutzlast PackeEvent(IEvent evt);
    Nutzlast PackeQuery(IQuery query);
    Nutzlast PackeAntwort(IQueryResponse antwort);
}
