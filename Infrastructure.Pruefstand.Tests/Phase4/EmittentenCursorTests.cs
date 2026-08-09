using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Infrastructure.Testing;
using Xunit;

namespace Infrastructure.Pruefstand.Phase4;

/// <summary>
/// P4.1 — der Emittenten-Cursor (Achse B = emittierend). Store-freie Logik des In-Memory-Profils, die
/// exakt der Vertrag <c>IEmittentenCursor</c> fordert: ungeschriebene Partition = 0, Load liest den
/// zuletzt geschriebenen Stand, Partitionen sind unabhängig, und out-of-order-Schreiber senken den
/// Cursor NICHT (Monotonie — sonst re-faltete ein verspäteter Wake unnötig ab tiefer Position).
/// Der Cursor ist bewusst best-effort: er trägt NIE die Korrektheit (das tut die Empfänger-Inbox),
/// nur die O(N²)→O(Tail)-Ersparnis.
/// </summary>
public class EmittentenCursorTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Ungeschriebene_Partition_ist_0()
    {
        var cursor = new InMemoryEmittentenCursor();
        (await cursor.LadeAsync("reaktion:stream-a", Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Load_liest_den_zuletzt_geschriebenen_Stand()
    {
        var cursor = new InMemoryEmittentenCursor();
        await cursor.SchreibeAsync("reaktion:stream-a", 7, Ct);
        (await cursor.LadeAsync("reaktion:stream-a", Ct)).Should().Be(7);
    }

    [Fact]
    public async Task Partitionen_sind_unabhaengig()
    {
        var cursor = new InMemoryEmittentenCursor();
        await cursor.SchreibeAsync("reaktion:stream-a", 3, Ct);
        await cursor.SchreibeAsync("andere-reaktion:stream-a", 9, Ct);

        (await cursor.LadeAsync("reaktion:stream-a", Ct)).Should().Be(3);
        (await cursor.LadeAsync("andere-reaktion:stream-a", Ct)).Should().Be(9);
    }

    [Fact]
    public async Task Out_of_order_Schreiber_senkt_den_Cursor_nicht()
    {
        var cursor = new InMemoryEmittentenCursor();
        await cursor.SchreibeAsync("reaktion:stream-a", 10, Ct);
        await cursor.SchreibeAsync("reaktion:stream-a", 4, Ct);   // verspäteter Wake, kleinere Position

        (await cursor.LadeAsync("reaktion:stream-a", Ct)).Should().Be(10);   // bleibt beim Maximum
    }
}
