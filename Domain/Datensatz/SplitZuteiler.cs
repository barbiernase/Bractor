using Domain.ImagePair;

namespace Domain.Datensatz;

/// <summary>
/// Reine, deterministische Split-Zuteilung (train/val/test) beim Einfrieren.
///
/// Stratifiziert nach Klasse: jede Klasse wird für sich im konfigurierten Verhältnis
/// aufgeteilt, damit die Balance in allen drei Mengen erhalten bleibt. Die Reihenfolge
/// innerhalb einer Klasse kommt aus einem <em>stabilen</em> Hash von (Seed, ImagePairId) —
/// nicht aus <c>Guid.GetHashCode</c> (prozess-instabil). So liefert dieselbe Mitglieder-Menge
/// mit demselben Seed <em>bit-genau</em> denselben Split — zwei Trainings auf v1 sind
/// vergleichbar (Konzept §3.3, §10).
///
/// Keinerlei I/O — der Resolver (Meilenstein 5) reicht die aus dem Read-Model gelesenen
/// (Id, Label)-Paare herein.
/// </summary>
public static class SplitZuteiler
{
    public static IReadOnlyDictionary<Guid, Split> Zuteilen(
        IEnumerable<(Guid Id, Klassifikation Label)> mitglieder,
        SplitKonfig konfig)
    {
        var result = new Dictionary<Guid, Split>();

        foreach (var gruppe in mitglieder.GroupBy(m => m.Label))
        {
            var sortiert = gruppe
                .OrderBy(m => StabilerAnteil(m.Id, konfig.Seed))
                .ThenBy(m => m.Id)          // stabiler Tie-Break
                .ToList();

            var n = sortiert.Count;

            var trainN = (int)Math.Round(n * konfig.TrainProzent / 100.0, MidpointRounding.AwayFromZero);
            var valN = (int)Math.Round(n * konfig.ValProzent / 100.0, MidpointRounding.AwayFromZero);

            // Rundung darf nie mehr als n vergeben — Test bekommt den Rest.
            if (trainN > n) trainN = n;
            if (trainN + valN > n) valN = n - trainN;

            for (var i = 0; i < n; i++)
            {
                var split = i < trainN ? Split.Train
                    : i < trainN + valN ? Split.Val
                    : Split.Test;
                result[sortiert[i].Id] = split;
            }
        }

        return result;
    }

    /// <summary>
    /// Stabiler Anteil in [0,1) aus (Seed, ImagePairId) via FNV-1a-64.
    /// Prozess- und lauf-unabhängig — Grundlage der Reproduzierbarkeit.
    /// </summary>
    private static double StabilerAnteil(Guid id, int seed)
    {
        const ulong FnvOffset = 1469598103934665603UL;
        const ulong FnvPrime = 1099511628211UL;

        var hash = FnvOffset;

        void Mix(byte b)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        foreach (var b in BitConverter.GetBytes(seed)) Mix(b);
        foreach (var b in id.ToByteArray()) Mix(b);

        // Obere 53 Bits → double in [0,1).
        return (hash >> 11) * (1.0 / (1UL << 53));
    }
}
