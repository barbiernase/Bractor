using System.Threading.Channels;
using Abstractions;
using Infrastructure.Umgebung;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Infrastructure.Einblick;

/// <summary>Was nicht im Log steht, aber in die Spur gehört: eine Ablehnung, ein transientes Event (Werkbank P4.5).</summary>
public sealed record SpurHinweis(DateTimeOffset Zeit, string Art, string? Wirker, string? Ursache, string? Korrelation, string Text);

/// <summary>
/// Hinweise an die Spur (<c>docs/konzept-werkbank.md</c> §7.1): best-effort, VERLIERBAR (Invariante 6) — das System liest sie nie, nur die
/// Werkbank zeigt sie (rot bzw. gestrichelt). Wer etwas Verlierbares tut, meldet es hier; ist kein Schreiber aktiv (Prod, Tests), verpufft
/// die Meldung. Die Arten: <see cref="Ablehnung"/> (ein Decide lehnte ab — der Client-Pfad hinterlässt keine Marke im Log) und
/// <see cref="Transient"/> (ein reaktives Event ging nur über den Broker).
/// </summary>
public static class SpurHinweise
{
    public const string Ablehnung = "ablehnung", Transient = "transient";

    private static ChannelWriter<SpurHinweis>? _ziel;

    internal static void Verbinde(ChannelWriter<SpurHinweis>? ziel) => _ziel = ziel;

    public static void Melde(string art, string? wirker, string? ursache, string? korrelation, string text)
        => _ziel?.TryWrite(new SpurHinweis(DateTimeOffset.UtcNow, art, wirker, ursache, korrelation, text));
}

/// <summary>
/// Schreibt die Hinweise gebündelt in <c>&lt;schema&gt;.spur_hinweis</c> und löscht, was älter als die Aufbewahrungsfrist ist. Nur in
/// Umgebungen der Art Sim registriert (die Werkbank liest sie über <c>/einblick/hinweise</c>). Eine volle Warteschlange verwirft (bounded).
/// </summary>
public sealed class SpurHinweisSchreiber(UmgebungKonfig umgebung) : BackgroundService
{
    public static readonly TimeSpan Aufbewahrung = TimeSpan.FromDays(7);

    private readonly Channel<SpurHinweis> _kanal = Channel.CreateBounded<SpurHinweis>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private string Schema => umgebung.EventStore.Schema;

    public override Task StartAsync(CancellationToken ct)
    {
        SpurHinweise.Verbinde(_kanal.Writer);
        return base.StartAsync(ct);
    }

    public override Task StopAsync(CancellationToken ct)
    {
        SpurHinweise.Verbinde(null);
        return base.StopAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var aufgeraeumt = DateTimeOffset.MinValue;
        var los = new List<SpurHinweis>();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!await _kanal.Reader.WaitToReadAsync(ct)) return;
                los.Clear();
                while (los.Count < 500 && _kanal.Reader.TryRead(out var h)) los.Add(h);
                await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(umgebung.EventStore));
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    create table if not exists "{Schema}".spur_hinweis (
                      id bigserial primary key, zeit timestamptz not null, art text not null,
                      wirker text, ursache text, korrelation text, text text not null);
                    insert into "{Schema}".spur_hinweis (zeit, art, wirker, ursache, korrelation, text)
                      select * from unnest(@z, @a, @w, @u, @k, @t);
                    """;
                cmd.Parameters.AddWithValue("z", los.Select(h => h.Zeit).ToArray());
                cmd.Parameters.AddWithValue("a", los.Select(h => h.Art).ToArray());
                cmd.Parameters.AddWithValue("w", los.Select(h => h.Wirker).ToArray());
                cmd.Parameters.AddWithValue("u", los.Select(h => h.Ursache).ToArray());
                cmd.Parameters.AddWithValue("k", los.Select(h => h.Korrelation).ToArray());
                cmd.Parameters.AddWithValue("t", los.Select(h => h.Text).ToArray());
                await cmd.ExecuteNonQueryAsync(ct);
                if (DateTimeOffset.UtcNow - aufgeraeumt > TimeSpan.FromHours(1))
                {
                    await using var weg = conn.CreateCommand();
                    weg.CommandText = $"delete from \"{Schema}\".spur_hinweis where zeit < now() - @frist";
                    weg.Parameters.AddWithValue("frist", Aufbewahrung);
                    await weg.ExecuteNonQueryAsync(ct);
                    aufgeraeumt = DateTimeOffset.UtcNow;
                }
                SpurSignal.Melde();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e)
            {
                // best-effort: verlieren ist erlaubt — nur kurz durchatmen, damit eine fehlende DB nicht die CPU frisst
                Console.WriteLine($"[Spur] Hinweise nicht geschrieben ({los.Count}): {e.Message}");
                try { await Task.Delay(TimeSpan.FromSeconds(2), ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Hinweise mit <c>id &gt; ab</c> (aufsteigend, höchstens <paramref name="n"/>) — für <c>/einblick/hinweise</c>.</summary>
    public static async Task<IReadOnlyList<(long Id, SpurHinweis Hinweis)>> LiesAsync(UmgebungKonfig u, long ab, int n, CancellationToken ct)
    {
        var aus = new List<(long, SpurHinweis)>();
        await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(u.EventStore));
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"select id, zeit, art, wirker, ursache, korrelation, text from \"{u.EventStore.Schema}\".spur_hinweis where id > @ab order by id limit @n";
        cmd.Parameters.AddWithValue("ab", ab);
        cmd.Parameters.AddWithValue("n", Math.Clamp(n, 1, 5000));
        try
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
                aus.Add((r.GetInt64(0), new SpurHinweis(r.GetFieldValue<DateTimeOffset>(1), r.GetString(2), S(3), S(4), S(5), r.GetString(6))));
            }
        }
        catch (PostgresException e) when (e.SqlState == "42P01") { }
        return aus;
    }
}
