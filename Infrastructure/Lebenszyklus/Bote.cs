using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Lebenszyklus;

/// <summary>
/// Die zwei Verben, mit denen Framework-Code einen virtuellen Cluster-Actor anspricht (Plan Pub/Sub-Neustrukturierung §1.7, Phase 2) — es
/// gibt keinen dritten Weg und keine Anfrage ohne Frist:
/// <list type="bullet">
/// <item><see cref="Melde"/> — <b>verlierbar, ohne Antwort.</b> Der Aufrufer wartet nicht; die Zustellung ist intern gebunden
///   (<see cref="StandardMeldeFrist"/>), ein Fehlschlag wird protokolliert und ist folgenlos (Weckruf, Ablehnung an die Sitzung, Abmelden,
///   verlierbarer Push — Invariante 2 und 6).</item>
/// <item><see cref="FrageAsync{T}"/> — <b>mit Antwort und Pflicht-Frist.</b> Kommt in der Frist keine Antwort des erwarteten Typs (Frist
///   abgelaufen, Ziel nicht platzierbar, Transportfehler, Abbruch des Aufrufers), liefert sie <c>null</c>; eine Ausnahme verlässt den Boten nie.
///   „Keine Antwort“ heißt „unbekannt“, nie „nicht gewirkt“ — wer wiederholt, wiederholt mit derselben Identität der Nachricht.</item>
/// </list>
/// Dazu für die Actors mit festem Namen je Knoten (der Verteiler, Plan §2.7) <see cref="MeldeAnKnoten"/> — ebenso verlierbar, ohne Platzierung.
/// Proto.Cluster wiederholt eine Anfrage ohne Frist unbegrenzt (Platzierung, tote PID) — der Grund, warum keine Stelle mehr
/// <c>Cluster.RequestAsync</c> selbst ruft. Die Anfrage selbst ist eine Naht (<see cref="Anfrage"/>): live der Cluster, im Prüfstand ein Fake.
/// </summary>
[HausregelAusnahme("Der Bote ist der eine Berührungspunkt mit dem Cluster: er ruft RequestAsync, immer mit Frist; Melde ist verlierbar " +
    "per Vertrag und kehrt sofort zurück — MeldeAsync fängt jeden Fehler selbst.")]
public sealed class Bote
{
    /// <summary>Der eine Cluster-Berührungspunkt: eine Anfrage an eine Identität, gebunden durch das Token.</summary>
    public delegate Task<object?> Anfrage(ClusterIdentity ziel, object nachricht, CancellationToken ct);

    /// <summary>Wie lange eine Meldung höchstens unterwegs ist (Platzierung inklusive), bevor sie als verloren gilt.</summary>
    public static readonly TimeSpan StandardMeldeFrist = TimeSpan.FromSeconds(10);

    private readonly Anfrage _anfrage;
    private readonly ILogger _logger;
    private readonly TimeSpan _meldeFrist;
    private readonly Action<PID, object>? _sende;

    /// <summary>Live: an den Cluster gebunden.</summary>
    public Bote(Cluster cluster, ILogger? logger = null)
        : this((ziel, nachricht, ct) => cluster.RequestAsync<object?>(ziel, nachricht, ct), logger,
            sende: (pid, nachricht) => cluster.System.Root.Send(pid, nachricht))
    {
        ArgumentNullException.ThrowIfNull(cluster);
    }

    /// <summary>Naht/Test: eine eingesetzte Anfrage (und, für <see cref="MeldeAnKnoten"/>, ein eingesetztes Senden).</summary>
    public Bote(Anfrage anfrage, ILogger? logger = null, TimeSpan? meldeFrist = null, Action<PID, object>? sende = null)
    {
        _anfrage = anfrage ?? throw new ArgumentNullException(nameof(anfrage));
        _logger = logger ?? NullLogger.Instance;
        _meldeFrist = meldeFrist ?? StandardMeldeFrist;
        _sende = sende;
    }

    /// <summary>
    /// Verlierbar, ohne Antwort, an den Actor mit festem Namen <paramref name="name"/> auf dem Knoten <paramref name="adresse"/> (kein virtueller
    /// Actor: nichts wird platziert, nichts wiederholt). Lebt er nicht oder ist der Knoten fort, geht die Meldung verloren.
    /// </summary>
    public void MeldeAnKnoten(string adresse, string name, object nachricht)
    {
        ArgumentNullException.ThrowIfNull(nachricht);
        if (_sende is null) throw new InvalidOperationException("Bote ohne Senden an Knoten (Naht ohne sende)");
        try { _sende(PID.FromAddress(adresse, name), nachricht); }
        catch (Exception ex) { _logger.LogWarning(ex, "[Bote] Meldung {Nachricht} an {Adresse}/{Name} gescheitert (verlierbar)", nachricht.GetType().Name, adresse, name); }
    }

    /// <summary>
    /// Verlierbar, ohne Antwort: kehrt sofort zurück. Eine Antwort des Ziels (etwa eine Quittung) wird verworfen; geht die Meldung verloren,
    /// heilt der Takt des Empfängers bzw. der Absender braucht sie nicht.
    /// </summary>
    public void Melde(ClusterIdentity ziel, object nachricht)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(nachricht);
        _ = MeldeAsync(ziel, nachricht);
    }

    private async Task MeldeAsync(ClusterIdentity ziel, object nachricht)
    {
        try
        {
            using var frist = new CancellationTokenSource(_meldeFrist);
            await _anfrage(ziel, nachricht, frist.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            _logger.LogDebug("[Bote] Meldung {Nachricht} an {Ziel} nach {Frist} s verloren (verlierbar)",
                nachricht.GetType().Name, ziel, _meldeFrist.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Bote] Meldung {Nachricht} an {Ziel} gescheitert (verlierbar)", nachricht.GetType().Name, ziel);
        }
    }

    /// <summary>
    /// Mit Antwort und Pflicht-Frist: die Antwort des Ziels, wenn sie in <paramref name="frist"/> (und vor dem Abbruch von
    /// <paramref name="ct"/>) als <typeparamref name="T"/> kommt — sonst <c>null</c>. Wirft nie; eine Frist ≤ 0 oder unendlich ist ein
    /// Programmierfehler.
    /// </summary>
    public async Task<T?> FrageAsync<T>(ClusterIdentity ziel, object nachricht, TimeSpan frist, CancellationToken ct) where T : class
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(nachricht);
        if (frist <= TimeSpan.Zero || frist == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(frist), frist, "Eine Frage braucht eine endliche Frist (Pflicht-Frist).");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(frist);
        try
        {
            var antwort = await _anfrage(ziel, nachricht, cts.Token).ConfigureAwait(false);
            if (antwort is T erwartet) return erwartet;
            if (antwort is not null)
                _logger.LogWarning("[Bote] {Ziel} beantwortete {Nachricht} mit {Antwort} statt {Erwartet}", ziel, nachricht.GetType().Name,
                    antwort.GetType().Name, typeof(T).Name);
            return null;
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            _logger.LogDebug("[Bote] {Nachricht} an {Ziel}: keine Antwort in {Frist} s", nachricht.GetType().Name, ziel, frist.TotalSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Bote] {Nachricht} an {Ziel} gescheitert — Ausgang unbekannt", nachricht.GetType().Name, ziel);
            return null;
        }
    }
}
