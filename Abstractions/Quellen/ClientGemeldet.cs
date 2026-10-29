namespace Abstractions;

/// <summary>
/// Eine Quell-Nachricht, die ein CLIENT hineingibt (docs/10 §10.2): ein Fakt von draußen ohne Treiber — etwa „Import
/// angefordert“. Die Kennung ist die Command-Id der Nachricht: der Client vergibt sie und schickt sie beim Wiederholen wieder mit (wie bei einem
/// Command, er hält sie vor dem Senden fest) — genau einmal im Log je Kennung. Die Domäne benennt nur ihre Felder:
/// <c>public sealed record ImportAngefordert(int Zeilen) : ClientGemeldet;</c>; die Kennung setzt der Eingang. Wer sie schicken darf, sagt
/// <c>IDarf&lt;T&gt;</c>.
/// </summary>
public abstract record ClientGemeldet : IQuellNachricht
{
    /// <summary>Die Command-Id der Nachricht (vom Eingang gesetzt).</summary>
    public string Kennung { get; init; } = "";
}
