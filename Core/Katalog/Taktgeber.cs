using System.Runtime.CompilerServices;
using Abstractions;

namespace Core.Katalog;

/// <summary>Die Konfig eines Taktgebers: das Intervall (mindestens eine Sekunde; <see cref="TimeSpan.Zero"/> = eine Minute).</summary>
public sealed record Taktkonfig(TimeSpan Intervall = default)
{
    public TimeSpan WirksamesIntervall => Intervall <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : Intervall < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : Intervall;
}

/// <summary>
/// Der TAKTGEBER (Katalog des Frameworks, docs/04 §4.7): eine Quellart (IQuellart), eine Quelle einer App — je Intervall ein <see cref="Takt"/> mit dem Beginn des
/// Intervalls (gezählt ab der Unix-Epoche, UTC). Er meldet ab dem laufenden Intervall; ein Intervall, das während einer Pause verstrich, kommt nicht
/// nach. Nach einem Neustart meldet er das laufende Intervall erneut — mit derselben Kennung (dem Beginn), das Backend verwirft es.
/// </summary>
public sealed class Taktgeber(Taktkonfig konfig) : IQuellart
{
    private DateTimeOffset? _letzter;

    /// <summary>Der Beginn des Intervalls, in dem <paramref name="zeit"/> liegt.</summary>
    public static DateTimeOffset IntervallBeginn(DateTimeOffset zeit, TimeSpan intervall) =>
        new(zeit.UtcTicks - (zeit.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) % intervall.Ticks, TimeSpan.Zero);

    public async IAsyncEnumerable<Takt> Lausche([EnumeratorCancellation] CancellationToken ct)
    {
        var intervall = konfig.WirksamesIntervall;
        while (!ct.IsCancellationRequested)
        {
            var jetzt = DateTimeOffset.UtcNow;
            var beginn = IntervallBeginn(jetzt, intervall);
            if (_letzter != beginn)
            {
                _letzter = beginn;
                yield return new Takt(beginn);
            }
            try { await Task.Delay(TimeSpan.FromTicks(Math.Max(0, (beginn + intervall - DateTimeOffset.UtcNow).Ticks)) + TimeSpan.FromMilliseconds(10), ct); }
            catch (OperationCanceledException) { yield break; }
        }
    }
}
