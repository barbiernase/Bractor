using System.Security.Cryptography;
using System.Text;

namespace Abstractions;

/// <summary>
/// Eine durable DEADLINE (Frist): „stelle zum Zeitpunkt <see cref="Fällig"/> den Command
/// <c>FristFällig(<see cref="ZielAggregatId"/>, <see cref="Kontext"/>)</c> zu." Das eigenständige Zeit-Primitiv
/// (Zielbild §12, Weg „Zeit-Event/-Command"): der Scheduler vergleicht <see cref="Fällig"/> gegen die DB-Uhr
/// (<see cref="IDbClock"/>) und feuert überfällige Fristen über das bestehende Emit-Primitiv (deterministische
/// CommandId → Empfänger-Inbox dedupliziert → exactly-once wirksam). Bewusst KEIN beliebiger serialisierter
/// Command (die DLQ speichert auch keinen) — nur die primitiven Felder + der feste <c>FristFällig</c>-Typ.
///
/// Prozess-Kopplung (Timeout → Kompensation im Marking) ist bewusst NICHT Teil dieser Scheibe — sie hängt am
/// zurückgestellten P5(b)-Marking-Cursor. Dieses Primitiv ist der Unterbau, den sie später nutzt.
/// </summary>
public sealed record Frist(
    Guid Id,
    DateTimeOffset Fällig,
    Guid ZielAggregatId,
    string Kontext);

/// <summary>
/// Durabler Fristplan: Fristen anlegen, die fälligen abfragen (gegen die DB-Uhr) und nach dem Feuern
/// entfernen. Vertragsinterface; die Ablage ist Store-Sache (Marten / InMemory).
/// </summary>
public interface IFristplan
{
    /// <summary>Legt eine Frist an (idempotent über <see cref="Frist.Id"/> — erneutes Planen überschreibt).</summary>
    Task PlaneAsync(Frist frist, CancellationToken ct = default);

    /// <summary>Alle Fristen, deren <see cref="Frist.Fällig"/> ≤ <paramref name="jetzt"/> (die DB-Uhr) ist.</summary>
    Task<IReadOnlyList<Frist>> FälligeAsync(DateTimeOffset jetzt, CancellationToken ct = default);

    /// <summary>Entfernt eine gefeuerte/stornierte Frist. Folgenlos, wenn es sie nicht (mehr) gibt.</summary>
    Task EntferneAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Deterministische Ableitungen rund um Fristen (analog <see cref="ProzessId"/>).</summary>
public static class FristId
{
    /// <summary>
    /// Die CommandId der <c>FristFällig</c>-Zustellung — deterministisch aus der Frist-Id, damit ein
    /// Re-Feuern (Crash zwischen Emit und Entfernen, Doppel-Tick) am Empfänger als Duplikat verpufft.
    /// </summary>
    public static Guid FürZustellung(Guid fristId)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes($"frist:{fristId:N}"), hash);
        return new Guid(hash);
    }
}
