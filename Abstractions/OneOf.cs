using Abstractions;

namespace Abstractions;

/// <summary>
/// Typsicherer Union-Typ für Message-Payload-Rückgaben.
/// 
/// Ermöglicht Compile-Time-Sicherheit für Decider (Events), Reader (Responses)
/// und reaktive Subscriber (Events). Nur die deklarierten Typen können
/// ge-yielded/returned werden. Der private Konstruktor + implizite Konversionen
/// erzwingen den Kontrakt.
///
/// Constraint: IMessagePayload — deckt IEvent, IQueryResponse und ICommand ab.
///
/// Verwendung Decider:
///   IEnumerable&lt;OneOf&lt;WarenabgangGebucht, AbgangAbgelehnt&gt;&gt; Decide(...)
///
/// Verwendung Reader:
///   Task&lt;OneOf&lt;LagerbestandAntwort, LagerbestandNichtGefunden&gt;&gt; Handle(...)
/// </summary>
public readonly struct OneOf<T1, T2>
    where T1 : IMessagePayload
    where T2 : IMessagePayload
{
    public IMessagePayload Value { get; }

    private OneOf(IMessagePayload value) => Value = value;

    public static implicit operator OneOf<T1, T2>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2>(T2 v) => new(v);
}

public readonly struct OneOf<T1, T2, T3>
    where T1 : IMessagePayload
    where T2 : IMessagePayload
    where T3 : IMessagePayload
{
    public IMessagePayload Value { get; }

    private OneOf(IMessagePayload value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3>(T3 v) => new(v);
}

public readonly struct OneOf<T1, T2, T3, T4>
    where T1 : IMessagePayload
    where T2 : IMessagePayload
    where T3 : IMessagePayload
    where T4 : IMessagePayload
{
    public IMessagePayload Value { get; }

    private OneOf(IMessagePayload value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3, T4>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T3 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T4 v) => new(v);
}