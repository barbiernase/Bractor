namespace Abstractions;

/// <summary>
/// Eine PLANUNG als Pipeline-Ausgang — wie ein Command oder Trigger steht sie im <c>OneOf</c> der Signatur
/// (CQRS050), nicht als Aufruf im Rumpf. Der generierte Dispatch reicht sie an die Framework-Senke weiter:
/// <see cref="Selbst{T}"/> an die eigene Mailbox, <see cref="Frist{TCmd}"/>/<see cref="FristStorno{TCmd}"/> an den
/// durablen Fristplan. So sieht jeder Leser (Generator, Graph, Editor) WAS eine Pipeline plant, ohne den Rumpf zu lesen.
/// </summary>
public interface IPlanung : IPipelineOutput { }

/// <summary>Plant die Selbst-Nachricht <typeparamref name="T"/> für die eigene Pipeline nach <see cref="Verzoegerung"/>
/// (mailbox-sicher, verlierbar — Inv. 6). Gleiches <see cref="Token"/> ersetzt eine noch offene Planung.</summary>
public sealed record Selbst<T>(T Nachricht, TimeSpan Verzoegerung, string? Token = null) : ISelbstPlanung
    where T : IPipelineSelfMessage
{
    IPipelineSelfMessage ISelbstPlanung.Nachricht => Nachricht;
}

/// <summary>Nicht-generische Sicht auf <see cref="Selbst{T}"/> — für die Senke im Pipeline-Actor.</summary>
public interface ISelbstPlanung : IPlanung
{
    IPipelineSelfMessage Nachricht { get; }
    TimeSpan Verzoegerung { get; }
    string? Token { get; }
}

/// <summary>Fabrik: <c>yield return Selbst.In(new PollTick(), intervall)</c>.</summary>
public static class Selbst
{
    public static Selbst<T> In<T>(T nachricht, TimeSpan verzoegerung, string? token = null) where T : IPipelineSelfMessage =>
        new(nachricht, verzoegerung, token);
}

/// <summary>
/// Plant eine durable Frist: nach <see cref="Dauer"/> (gegen die DB-Uhr) wird <typeparamref name="TCmd"/> an
/// <see cref="ZielAggregatId"/> zugestellt. Der Command-Typ steht im Typ — der Fristen-Router ist generiert
/// (<typeparamref name="TCmd"/> braucht einen Konstruktor mit genau der Ziel-Id). Erneutes Planen überschreibt
/// idempotent (Frist-Id deterministisch aus Command-Typ + Ziel).
/// </summary>
public sealed record Frist<TCmd>(Guid ZielAggregatId, TimeSpan Dauer) : IPlanung where TCmd : ICommand;

/// <summary>Räumt die Frist <typeparamref name="TCmd"/> für <see cref="ZielAggregatId"/> ab (folgenlos, wenn schon gefeuert).</summary>
public sealed record FristStorno<TCmd>(Guid ZielAggregatId) : IPlanung where TCmd : ICommand;

/// <summary>
/// Die vom generierten Dispatch aufgelöste Form einer Frist-Planung: der Kontext ist der voll qualifizierte
/// Command-Typname (vom Generator als Konstante eingesetzt, keine Reflection). <see cref="Dauer"/> null = Storno.
/// <see cref="Ab"/> = die Basis der Fälligkeit: die Log-Zeit (DB) des auslösenden Events, wenn es eines gibt — so ergibt
/// jedes erneute Lesen desselben Events (Poll ab 0, Redelivery) dieselbe Fälligkeit, statt die Frist nach hinten zu
/// schieben. null (Trigger-/Selbst-Pfad, kein Log-Event) = die DB-Uhr beim Planen.
/// </summary>
public sealed record FristAuftrag(string Kontext, Guid ZielAggregatId, TimeSpan? Dauer, DateTimeOffset? Ab = null) : IPlanung
{
    /// <summary>Deterministische Frist-Id aus Kontext + Ziel — Planen und Stornieren treffen dieselbe Frist.</summary>
    public Guid FristId => Abstractions.FristId.Für(Kontext, ZielAggregatId);
}

/// <summary>
/// Framework-Selbst-Nachricht beim Start einer Pipeline — der typisierte Ort für die erste Planung
/// (z. B. <c>Handle(PipelineGestartet, ctx) → OneOf&lt;Selbst&lt;PollTick&gt;&gt;</c>).
/// </summary>
public sealed record PipelineGestartet : IPipelineSelfMessage;
