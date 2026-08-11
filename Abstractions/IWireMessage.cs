namespace Abstractions;

/// <summary>
/// Marker für die <b>Top-Level-Nachrichten des internen Actor-Plane</b> — genau die Typen, die per
/// <c>Cluster().RequestAsync&lt;T&gt;(identity, payload)</c> bzw. <c>context.Respond(...)</c> zwischen
/// (virtuellen) Actors gesendet werden und deshalb cross-node <b>serialisiert</b> über die Leitung gehen.
///
/// Der <c>WireSerializerGenerator</c> sammelt alle Implementierer und erzeugt daraus die Wire-Whitelist
/// (<c>GeneratedWire.CanSerialize</c>/<c>WireMessageTypes</c>); der <c>WireSerializerBootCheck</c> prüft
/// beim Start, dass jeder markierte Typ vom <c>CqrsWireSerializer</c> abgedeckt ist (sonst Start-Abbruch).
///
/// Bewusst NUR die Transport-Hüllen tragen den Marker, nicht die Domänen-<c>ICommand</c>/<c>IEvent</c>:
/// diese reisen ausschließlich als polymorphe Payloads GESCHACHTELT in einer Hülle (Diskriminator über
/// den Typ-Namen), nie als eigenständige Plane-Nachricht. Die Domänen-Typen bleiben so rein.
/// </summary>
public interface IWireMessage { }
