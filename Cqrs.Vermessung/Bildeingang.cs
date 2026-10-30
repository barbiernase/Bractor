using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Domain.ImagePair;

namespace Cqrs.Vermessung;

/// <summary>
/// S1/S2 Bildeingang (Beispieldomäne): je Vorgang ein TIFF-Paar (<c>_DC0</c>, <c>_DC2</c>) ins Quell-Verzeichnis — erst in einen
/// Zwischenordner geschrieben, dann hineinverschoben, dann die Fertig-Marke (Quelle <c>Fertig:Marker</c>). Sichtbar ist der Vorgang, wenn die
/// Query <c>GetImagePair</c> das Paar als komplett liefert — Funktionen (die Kette der Bildaufbereitung, Bildeingang), Aggregat und Projektion durchlaufen. S2 ist derselbe
/// Eingang mit der Kette der Bildaufbereitung im Python-Worker (Laufort Extern).
/// </summary>
internal sealed class Bildeingang : ILast, IAsyncDisposable
{
    public const string TokenInspekteur = "vermessung-inspekteur";

    private readonly Eingang _eingang;
    private readonly string _quelle;
    private readonly string _zwischen;
    private readonly DateTimeOffset _basis;
    private readonly byte[] _tiff = Tiff(96, 64);
    private readonly ConcurrentDictionary<Guid, (Vorgang V, DateTimeOffset Produziert)> _offen = new();
    private readonly ConcurrentBag<Guid> _paare = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _beobachter;
    private long _n;

    /// <param name="jahr">Trennt die Paare der Szenarien im Log (S1: 2030, S2: 2031); <paramref name="wiederholung"/> den Tag.</param>
    public Bildeingang(Eingang eingang, string quelle, int jahr, int wiederholung)
    {
        _eingang = eingang;
        _quelle = quelle;
        _zwischen = Path.Combine(Path.GetDirectoryName(quelle)!, "zwischen");
        Directory.CreateDirectory(_quelle);
        Directory.CreateDirectory(_zwischen);
        _basis = new DateTimeOffset(jahr, 1, wiederholung, 0, 0, 0, TimeSpan.Zero);
        Jahr = jahr;
        _beobachter = Task.Run(BeobachteAsync);
    }

    public int Jahr { get; }
    public IReadOnlyCollection<Guid> Paare => _paare;

    public async Task StarteAsync(Vorgang v, CancellationToken ct)
    {
        var t = _basis.AddMilliseconds(Interlocked.Increment(ref _n));
        var schluessel = t.ToString("yyyy_MM_dd_HH_mm_ss_fff", CultureInfo.InvariantCulture);
        var id = ImagePairFileName.ToDeterministicGuid(schluessel);
        foreach (var version in new[] { "DC0", "DC2" })
        {
            var name = $"{schluessel}_{version}.tiff";
            var zwischen = Path.Combine(_zwischen, name);
            await File.WriteAllBytesAsync(zwischen, _tiff, ct);
            File.Move(zwischen, Path.Combine(_quelle, name));
            await File.WriteAllBytesAsync(Path.Combine(_quelle, name + ".fertig"), Array.Empty<byte>(), ct);
        }
        v.Start = DateTimeOffset.UtcNow;
        _paare.Add(id);
        _offen[id] = (v, t);
    }

    /// <summary>
    /// Fragt alle 100 ms je offenes Paar <c>GetImagePair</c> (Zugriff über die Id, höchstens 32 gleichzeitig): sichtbar, sobald das Paar
    /// komplett ist. Eine Query je offenem Paar und Runde — die Kosten wachsen mit dem Rückstand, nicht mit dem Datenbestand.
    /// </summary>
    private async Task BeobachteAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (!_offen.IsEmpty)
                    await Parallel.ForEachAsync(_offen.Keys.Take(512).ToList(), new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = _cts.Token },
                        async (id, ct) =>
                        {
                            try
                            {
                                var a = await _eingang.QueryAsync("GetImagePair", new JsonObject { ["PairId"] = id.ToString() }, TokenInspekteur, ct);
                                if ((string?)a?["typ"] == "ImagePairAntwort" && (bool?)a?["daten"]?["IstKomplett"] == true && _offen.TryRemove(id, out var o))
                                    o.V.Sichtbar = DateTimeOffset.UtcNow;
                            }
                            catch (Exception) when (!ct.IsCancellationRequested) { /* Host gerade weg (Störungslauf) — nächste Runde */ }
                        });
            }
            catch (OperationCanceledException) { }
            try { await Task.Delay(100, _cts.Token); } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Exactly-once (§0.2): je Datei genau ein Vorgang, je Paar genau ein ImagePair, BildVerfuegbar genau einmal je Bild, Read-Model = Log.</summary>
    public async Task<List<Pruefung>> PruefeAsync(Datenbank db, int dateien, bool vollstaendig = true)
    {
        Pruefung P(string name, long soll, long ist) => Pruefung.Erwartet(name, soll, ist, vollstaendig);
        var ids = _paare.ToArray();
        var s = db.Schema;
        var praefix = Jahr + "%";
        var p = new List<Pruefung>
        {
            P("je Datei genau eine Quell-Nachricht im Log", dateien,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'datei_erkannt' and data->>'Name' like @p", ("p", praefix))),
            Pruefung.Gleich("keine Datei zweimal gemeldet", 0, await db.ZahlAsync(
                $"select count(*) from (select data->>'Name' from {s}.mt_events where type = 'datei_erkannt' and data->>'Name' like @p group by 1 having count(*) > 1) x",
                ("p", praefix))),
            P("je Paar genau ein ImagePairErstellt", ids.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'image_pair_erstellt' and stream_id = any(@ids)", ("ids", ids))),
            P("BildVerfuegbar genau einmal je Bild (2 je Paar)", 2L * ids.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'bild_verfuegbar' and stream_id = any(@ids)", ("ids", ids))),
            Pruefung.Gleich("kein Paar mit mehr als 2 BildVerfuegbar", 0, await db.ZahlAsync(
                $"select count(*) from (select stream_id from {s}.mt_events where type = 'bild_verfuegbar' and stream_id = any(@ids) group by 1 having count(*) > 2) x",
                ("ids", ids))),
            P("je Paar genau ein ImagePairKomplett", ids.Length,
                await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'image_pair_komplett' and stream_id = any(@ids)", ("ids", ids))),
        };
        // Erst das Read-Model, dann der Log: der Log wächst nur, also gilt Read-Model ≤ Log auch bei offenem Rückstand; gleich erst nach dem Abbau.
        var stat = await _eingang.QueryAsync("GetImagePairStatistik", new JsonObject(), TokenInspekteur);
        long rmKomplett = (long?)stat?["daten"]?["Komplett"] ?? -1, rmGesamt = (long?)stat?["daten"]?["Gesamt"] ?? -1;
        p.Add(P("Read-Model: komplette Paare = ImagePairKomplett im Log",
            await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'image_pair_komplett'"), rmKomplett));
        p.Add(P("Read-Model: Paare = ImagePairErstellt im Log",
            await db.ZahlAsync($"select count(*) from {s}.mt_events where type = 'image_pair_erstellt'"), rmGesamt));
        return p;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _beobachter; } catch { }
    }

    /// <summary>Ein kleines Graustufen-TIFF (unkomprimiert, ein Streifen) — wie <c>Werkbank.Abnahme.Tests/Werkbanklauf.Tiff</c>; OpenCV liest es.</summary>
    public static byte[] Tiff(int breite, int hoehe)
    {
        const int eintraege = 9;
        var ifd = 8;
        var daten = ifd + 2 + eintraege * 12 + 4;
        using var m = new MemoryStream();
        using var w = new BinaryWriter(m, Encoding.ASCII);
        w.Write((byte)'I'); w.Write((byte)'I'); w.Write((ushort)42); w.Write((uint)ifd);
        w.Write((ushort)eintraege);
        void Eintrag(ushort tag, ushort typ, uint wert) { w.Write(tag); w.Write(typ); w.Write(1u); if (typ == 3) { w.Write((ushort)wert); w.Write((ushort)0); } else w.Write(wert); }
        Eintrag(256, 4, (uint)breite);
        Eintrag(257, 4, (uint)hoehe);
        Eintrag(258, 3, 8);
        Eintrag(259, 3, 1);
        Eintrag(262, 3, 1);
        Eintrag(273, 4, (uint)daten);
        Eintrag(277, 3, 1);
        Eintrag(278, 4, (uint)hoehe);
        Eintrag(279, 4, (uint)(breite * hoehe));
        w.Write(0u);
        for (var y = 0; y < hoehe; y++)
            for (var x = 0; x < breite; x++) w.Write((byte)((x * 255 / Math.Max(1, breite - 1) + y) & 0xFF));
        w.Flush();
        return m.ToArray();
    }
}
