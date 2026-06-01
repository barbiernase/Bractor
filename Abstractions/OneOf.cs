using Abstractions;

namespace Abstractions;

/// <summary>
/// Typsicherer Union-Typ für Message-Payload-Rückgaben.
/// 
/// Ermöglicht Compile-Time-Sicherheit für Decider (Events), Reader (Responses),
/// reaktive Subscriber (Events) und Pipeline-Outputs (Commands, Trigger, TransientEvents).
/// Nur die deklarierten Typen können ge-yielded/returned werden. Der private
/// Konstruktor + implizite Konversionen erzwingen den Kontrakt.
///
/// Constraint: IPipelineOutput — deckt IEvent, ICommand, IQueryResponse,
/// IPipelineTrigger und alle Subtypen ab. IMessagePayload erbt von IPipelineOutput,
/// daher kompilieren bestehende Decider/Reader-Signaturen unverändert.
///
/// Verwendung Decider:
///   IEnumerable&lt;OneOf&lt;WarenabgangGebucht, AbgangAbgelehnt&gt;&gt; Decide(...)
///
/// Verwendung Pipeline (neu):
///   IAsyncEnumerable&lt;OneOf&lt;SomeCommand, SomeTrigger&gt;&gt; Handle(...)
///
/// Verwendung Pipeline (nur ein Output-Typ):
///   IAsyncEnumerable&lt;OneOf&lt;DateiErkannt&gt;&gt; Handle(...)
///
/// Verwendung Reader:
///   Task&lt;OneOf&lt;LagerbestandAntwort, LagerbestandNichtGefunden&gt;&gt; Handle(...)
/// </summary>
public readonly struct OneOf<T1>
    where T1 : IPipelineOutput
{
    public IPipelineOutput Value { get; }

    private OneOf(IPipelineOutput value) => Value = value;

    public static implicit operator OneOf<T1>(T1 v) => new(v);
}

public readonly struct OneOf<T1, T2>
    where T1 : IPipelineOutput
    where T2 : IPipelineOutput
{
    public IPipelineOutput Value { get; }

    private OneOf(IPipelineOutput value) => Value = value;

    public static implicit operator OneOf<T1, T2>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2>(T2 v) => new(v);
}

public readonly struct OneOf<T1, T2, T3>
    where T1 : IPipelineOutput
    where T2 : IPipelineOutput
    where T3 : IPipelineOutput
{
    public IPipelineOutput Value { get; }

    private OneOf(IPipelineOutput value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3>(T3 v) => new(v);
}

public readonly struct OneOf<T1, T2, T3, T4>
    where T1 : IPipelineOutput
    where T2 : IPipelineOutput
    where T3 : IPipelineOutput
    where T4 : IPipelineOutput
{
    public IPipelineOutput Value { get; }

    private OneOf(IPipelineOutput value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3, T4>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T3 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T4 v) => new(v);
}


public readonly struct OneOf<T1, T2, T3, T4, T5>
    where T1 : IPipelineOutput
    where T2 : IPipelineOutput
    where T3 : IPipelineOutput
    where T4 : IPipelineOutput
    where T5 : IPipelineOutput
{
    public IPipelineOutput Value { get; }

    private OneOf(IPipelineOutput value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T3 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T4 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T5 v) => new(v);
}