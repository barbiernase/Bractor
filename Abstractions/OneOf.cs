using Abstractions;

namespace Abstractions;

/// <summary>
/// Typsicherer Union-Typ für Rückgaben: was ein Decide, eine Funktion oder ein Reader erzeugen kann, steht in der Signatur (CQRS050, CQRS100).
/// Nur die deklarierten Typen können ge-yielded/returned werden; der private Konstruktor und die impliziten Konversionen erzwingen den Kontrakt.
/// Ohne Schranke: Commands, Events, Hinweise, Antworten und Fristen stehen hier nebeneinander (docs/03 §3.1). Welche Typen wo erlaubt sind, prüfen die Analyzer (CQRS050, CQRS100), nicht der Compiler.
///
/// Decide:   IEnumerable&lt;OneOf&lt;WarenabgangGebucht, AbgangAbgelehnt&gt;&gt; Decide(...)
/// Funktion: IEnumerable&lt;OneOf&lt;ReservierePosten, BelegeKontingent&gt;&gt; Auf(AuftragGestartet e)
/// Reader:   Task&lt;OneOf&lt;LagerbestandAntwort, LagerbestandNichtGefunden&gt;&gt; Handle(...)
/// </summary>
public readonly struct OneOf<T1>
{
    public object Value { get; }

    private OneOf(object value) => Value = value;

    public static implicit operator OneOf<T1>(T1 v) => new(v);
}

public readonly struct OneOf<T1, T2>
{
    public object Value { get; }

    private OneOf(object value) => Value = value;

    public static implicit operator OneOf<T1, T2>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2>(T2 v) => new(v);
}

public readonly struct OneOf<T1, T2, T3>
{
    public object Value { get; }

    private OneOf(object value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3>(T3 v) => new(v);
}

public readonly struct OneOf<T1, T2, T3, T4>
{
    public object Value { get; }

    private OneOf(object value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3, T4>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T3 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4>(T4 v) => new(v);
}


public readonly struct OneOf<T1, T2, T3, T4, T5>
{
    public object Value { get; }

    private OneOf(object value) => Value = value;

    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T1 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T2 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T3 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T4 v) => new(v);
    public static implicit operator OneOf<T1, T2, T3, T4, T5>(T5 v) => new(v);
}