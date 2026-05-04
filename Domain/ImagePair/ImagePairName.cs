using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Domain.ImagePair;

/// <summary>
/// Parst Dateinamen nach der Aufnahme-Convention und erzeugt
/// deterministische Aggregate-IDs aus dem 7-Segment-Schlüssel.
///
/// Convention: {YYYY}_{MM}_{DD}_{HH}_{mm}_{ss}_{mmm}_{VERSION}.{ext}
///   Beispiel:  2025_06_16_17_46_20_293_DC2.tiff
///
///   PairKey     = "2025_06_16_17_46_20_293" (die ersten 7 Segmente)
///   Version     = DC2
///   ProduziertAm = 2025-06-16T17:46:20.293 (aus dem PairKey abgeleitet)
///
/// Der PairKey ist die fachliche Identität eines Bildpaares.
/// Zwei Dateien mit gleichem PairKey (aber unterschiedlicher Version)
/// gehören zum selben ImagePair-Aggregate.
///
/// Die deterministische Guid wird per MD5 aus dem PairKey erzeugt —
/// identischer Input → identische Guid, immer.
/// </summary>
public static class ImagePairFileName
{
    private const int PairKeySegments = 7;

    /// <summary>
    /// Parst einen Dateinamen und extrahiert PairKey, BildVersion und ProduziertAm.
    /// Gibt null zurück wenn der Dateiname nicht der Convention entspricht.
    /// </summary>
    public static ParseResult? Parse(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var segments = nameWithoutExt.Split('_');

        // Mindestens 8 Segmente: 7 für PairKey + 1 für Version
        if (segments.Length < PairKeySegments + 1)
            return null;

        // Letzte Segment = Version (DC0, DC2)
        var versionSegment = segments[PairKeySegments];
        if (!Enum.TryParse<BildVersion>(versionSegment, ignoreCase: true, out var version))
            return null;

        // Erste 7 Segmente = PairKey
        var pairKey = string.Join("_", segments.Take(PairKeySegments));

        // Timestamp aus den 7 Segmenten parsen
        var produziertAm = ParseTimestamp(segments);
        if (produziertAm == null)
            return null;

        return new ParseResult(pairKey, version, produziertAm.Value);
    }

    /// <summary>
    /// Parst die 7 Segmente als Zeitstempel: YYYY, MM, DD, HH, mm, ss, mmm
    /// </summary>
    private static DateTimeOffset? ParseTimestamp(string[] segments)
    {
        if (segments.Length < 7) return null;

        if (int.TryParse(segments[0], out var year) &&
            int.TryParse(segments[1], out var month) &&
            int.TryParse(segments[2], out var day) &&
            int.TryParse(segments[3], out var hour) &&
            int.TryParse(segments[4], out var minute) &&
            int.TryParse(segments[5], out var second) &&
            int.TryParse(segments[6], out var millis))
        {
            try
            {
                return new DateTimeOffset(
                    year, month, day, hour, minute, second, millis,
                    TimeSpan.Zero);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Erzeugt eine deterministische Guid aus einem PairKey.
    /// Gleicher PairKey → gleiche Guid. Immer.
    /// </summary>
    public static Guid ToDeterministicGuid(string pairKey)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(pairKey));
        return new Guid(hash);
    }

    /// <summary>
    /// Convenience: Parst den Dateinamen und gibt PairKey, Version,
    /// deterministischer Guid und ProduziertAm zurück. Null bei ungültigem Format.
    /// </summary>
    public static ResolvedPair? Resolve(string fileName)
    {
        var parsed = Parse(fileName);
        if (parsed == null) return null;

        var aggregateId = ToDeterministicGuid(parsed.PairKey);
        return new ResolvedPair(
            parsed.PairKey, parsed.Version, aggregateId, parsed.ProduziertAm);
    }

    // ─── Result-Typen ───

    public record ParseResult(string PairKey, BildVersion Version, DateTimeOffset ProduziertAm);

    public record ResolvedPair(
        string PairKey, BildVersion Version, Guid AggregateId, DateTimeOffset ProduziertAm);
}