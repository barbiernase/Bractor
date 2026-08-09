namespace Abstractions;

/// <summary>
/// Durables Cursor-Dokument eines EMITTIERENDEN Konsumenten (Reaktion / Pipeline-Event) pro Partition.
/// Best-effort/Cache (siehe <see cref="IEmittentenCursor"/>): außerhalb der Entscheidungs-Transaktion,
/// Verlust heilt der Voll-Fold (Read ab 0 + Empfänger-Inbox-Dedup). Reines POCO ohne Persistenz-
/// Abhängigkeit (analog <see cref="PollCursor"/> / <see cref="ProjectionCheckpoint"/>); die
/// Marten-Schema-Registrierung erfolgt in der Host-/Store-Konfiguration.
///
/// <see cref="Id"/> = die Partition (der <c>partition</c>-Schlüssel aus <see cref="IEmittentenCursor"/>),
/// vom Framework als <c>{SubscriberId}:{StreamId}</c> gebildet — so haben zwei Konsumenten desselben
/// Streams unabhängige Cursor.
/// </summary>
public class EmittentenCursorDoc
{
    public string Id { get; set; } = default!;
    public long Position { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
