using System.Globalization;
using System.Text;
using Domain.ImagePair;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Der Eingang der Beispieldomäne von außen: ein TIFF-Paar (<c>_DC0</c>, <c>_DC2</c>) ins Quell-Verzeichnis, erst in einen Zwischenordner
/// geschrieben, dann hineinverschoben — der Datei-Treiber meldet jede Datei, sobald sie stabil ist. Die Zeitstempel im Namen sind je Lauf
/// zufällig, damit kein Paar eines früheren Laufs (Versions-Index) mitspielt.
/// </summary>
public sealed class Bildpaare(Testumgebung u)
{
    private static readonly byte[] TiffBytes = Tiff(48, 32);
    private readonly DateTimeOffset _basis = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        .AddMinutes(Random.Shared.Next(0, 5_000_000));
    private int _n;

    public sealed record Paar(Guid Id, string Schluessel)
    {
        public IEnumerable<string> Dateien => new[] { "DC0", "DC2" }.Select(v => $"{Schluessel}_{v}.tiff");
    }

    /// <summary>Legt ein Paar ab; seine Id folgt deterministisch aus dem Namen, wie die Domäne ihn deutet.</summary>
    public async Task<Paar> LegeAbAsync()
    {
        var schluessel = _basis.AddSeconds(Interlocked.Increment(ref _n)).ToString("yyyy_MM_dd_HH_mm_ss_fff", CultureInfo.InvariantCulture);
        var paar = new Paar(ImagePairFileName.ToDeterministicGuid(schluessel), schluessel);
        foreach (var datei in paar.Dateien) await LegeDateiAbAsync(datei);
        return paar;
    }

    /// <summary>Eine Datei (noch einmal) ablegen — gleicher Name, gleiche Größe ist für die Quelle dieselbe Kennung.</summary>
    public async Task LegeDateiAbAsync(string name)
    {
        var zwischen = Path.Combine(u.Zwischenpfad, name);
        await File.WriteAllBytesAsync(zwischen, TiffBytes);
        File.Move(zwischen, Path.Combine(u.Quellpfad, name), overwrite: true);
    }

    /// <summary>Ein unkomprimiertes 8-Bit-Graustufen-TIFF — echte Bildfunktionen (OpenCV) lesen es.</summary>
    private static byte[] Tiff(int breite, int hoehe)
    {
        const int eintraege = 9;
        const int ifd = 8;
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
