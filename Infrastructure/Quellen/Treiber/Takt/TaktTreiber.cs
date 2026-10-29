using System.Globalization;
using Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Quellen.Treiber.Takt;

/// <summary>
/// Der TAKT-Treiber (ersetzt den Timer-Trigger): je Intervall eine Quell-Nachricht, Kennung = Beginn des
/// Intervalls (UTC, ISO 8601) — dieselbe auf jedem Knoten und nach jedem Neustart, also genau einmal im Log (Version 0 bzw. die Kennung im
/// Verzeichnis der Quelle). Gezählt wird mit der Datenbank-Uhr (eine Uhr, G-10; in Sim samt Versatz). Ein Intervall, das während einer Pause
/// aller Knoten verstrich, kommt nicht nach — es zählt der nächste erreichte Zeitpunkt. Läuft standardmäßig einmal im Cluster (Lease); ein
/// zweiter Knoten ohne Lease meldete dasselbe Intervall nur noch einmal (verpufft an der Kennung).
/// </summary>
public sealed class TaktTreiber
{
    private readonly QuellTyp _quelle;
    private readonly TaktQuellKonfig _konfig;
    private readonly Func<IQuellNachricht, CancellationToken, Task> _melde;
    private readonly Func<CancellationToken, Task<DateTimeOffset>> _jetzt;
    private readonly QuellenZustand _zustand;
    private readonly ILogger? _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _warte;
    private DateTimeOffset? _letzter;

    /// <param name="jetzt">Die Uhr (live die Datenbank-Uhr, <see cref="IDbClock"/>).</param>
    /// <param name="warte">Warten bis zur nächsten Probe (Naht für den Prüfstand).</param>
    public TaktTreiber(QuellTyp quelle, TaktQuellKonfig konfig, Func<IQuellNachricht, CancellationToken, Task> melde,
        Func<CancellationToken, Task<DateTimeOffset>> jetzt, QuellenZustand zustand, ILogger? logger = null,
        Func<TimeSpan, CancellationToken, Task>? warte = null)
    {
        if (konfig.Intervall < TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(konfig), $"Quellen:{quelle.Abschnitt}:Intervall = {konfig.Intervall} — mindestens eine Sekunde.");
        _quelle = quelle;
        _konfig = konfig;
        _melde = melde;
        _jetzt = jetzt;
        _zustand = zustand;
        _logger = logger;
        _warte = warte ?? Task.Delay;
    }

    /// <summary>Der Beginn des Intervalls, in dem <paramref name="zeit"/> liegt (gezählt ab der Unix-Epoche, UTC).</summary>
    public static DateTimeOffset IntervallBeginn(DateTimeOffset zeit, TimeSpan intervall) =>
        new(zeit.UtcTicks - (zeit.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) % intervall.Ticks, TimeSpan.Zero);

    public static string Kennung(DateTimeOffset beginn) => beginn.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>Eine Probe: ist ein neues Intervall erreicht, wird es gemeldet. Liefert, wie lange bis zur nächsten Probe zu warten ist.</summary>
    public async Task<TimeSpan> ProbeAsync(CancellationToken ct)
    {
        var jetzt = await _jetzt(ct);
        var beginn = IntervallBeginn(jetzt, _konfig.Intervall);
        if (_letzter != beginn)
        {
            await _melde(_quelle.TaktFabrik!(new TaktDaten(beginn, Kennung(beginn))), ct);
            _letzter = beginn;
            _zustand.Gemeldet(_quelle.Abschnitt);
        }
        _zustand.Ok(_quelle.Abschnitt);
        // Bis zum nächsten Intervall, höchstens eine Sekunde (die Uhr kann springen: Sim, Zeitumstellung der Datenbank).
        var bisNaechster = beginn + _konfig.Intervall - jetzt;
        return bisNaechster < TimeSpan.FromSeconds(1) ? bisNaechster + TimeSpan.FromMilliseconds(10) : TimeSpan.FromSeconds(1);
    }

    public async Task LaufeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TimeSpan warte;
            try { warte = await ProbeAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _zustand.Gestoert(_quelle.Abschnitt, ex.Message);
                _logger?.LogWarning(ex, "[Quelle] Takt {Quelle}: Probe gescheitert — nächster Versuch in einer Sekunde", _quelle.Abschnitt);
                warte = TimeSpan.FromSeconds(1);
            }
            try { await _warte(warte, ct); } catch (OperationCanceledException) { return; }
        }
    }
}
