using System.Collections.Concurrent;
using Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

/// <summary>Was mit einer gemeldeten Antwort eines Ausführers draußen geschah.</summary>
public enum ErgebnisAusgang
{
    /// <summary>Der Eingang läuft in dieser Sitzung nicht (schon erledigt, nie zugeteilt, alte Sitzung) — nichts festgehalten.</summary>
    Unbekannt,
    /// <summary>Die Antwort (bzw. das Stück) ist festgehalten.</summary>
    Geschrieben,
    /// <summary>Gescheitert (Fehler gemeldet oder Antwort außerhalb der Signatur) — der Funktions-Actor zählt es gegen die Wiederholungen.</summary>
    Abgelehnt,
    /// <summary>Nicht festhaltbar (Store) — nicht „erledigt“ gemeldet; die Lease läuft ab, der Eingang geht neu raus.</summary>
    NichtGeschrieben,
    /// <summary>Eine andere Antwort war zuerst festgehalten — sie gilt, diese verfällt.</summary>
    Ueberholt,
}

/// <summary>
/// Ein Ausführer draußen für Funktionen (Plan Pub/Sub-Neustrukturierung §2.2, Laufort Hand oder Extern — ein Mensch an der Werkbank, ein Worker oder
/// ein Client, der die Funktion trägt): holt Eingänge beim Vermittler der Funktion (Pull, so viele wie Slots frei sind, mit Lease), reicht sie weiter,
/// hält Lebenszeichen und nimmt die Antwort an. Die Antwort wird unter dem Schlüssel „F, X“ festgehalten — die ERSTE gewinnt (zwei Worker mit
/// verschiedenen Antworten: eine gilt); ein STROM (Training) kommt in Stücken, jedes wird an seiner Stelle festgehalten und dem Funktions-Actor
/// gemeldet, der es sendet, bevor das nächste kommt. Mit dem letzten Stück „erledigt“ an den Vermittler. Die Garantie bleibt beim Actor: geht
/// die Sitzung verloren, läuft die Lease ab und der Eingang geht an den nächsten Ausführer, der beim nächsten nicht festgehaltenen Stück weitermacht.
///
/// Transport-frei: Vermittler, Weiterreichen, Festhalten und die Meldung an den Actor sind Nähte (live über gRPC bzw. die Werkbank).
/// </summary>
public sealed class BausteinSitzung : IAsyncDisposable
{
    private sealed record Laufend(FunktionsBaustein Funktion, EingangAnbieten Eingang, SemaphoreSlim Slots);

    private readonly string _sitzung;
    private readonly Func<string, FunktionsBaustein?> _loese;
    private readonly Func<FunktionsBaustein, HoleEingaenge, CancellationToken, Task<EingaengeZugeteilt?>> _hole;
    private readonly Func<FunktionsBaustein, object, Task> _melde;
    private readonly Func<EingangAnbieten, FunktionsBaustein, int, Task> _sende;
    private readonly IAusgabeAblage _ablage;
    private readonly Func<FunktionsBaustein, EingangAnbieten, string?, bool, Task> _beantwortet;
    private readonly ILogger? _logger;
    private readonly int _warteMs;
    private readonly CancellationTokenSource _ende = new();
    private readonly ConcurrentDictionary<string, Task> _schleifen = new();
    private readonly ConcurrentDictionary<Guid, Laufend> _laufend = new();

    /// <param name="loese">Name der Funktion → Baustein; unbekannt = null.</param>
    /// <param name="hole">Eingänge beim Vermittler der Funktion holen (Long-Poll).</param>
    /// <param name="melde">Lebenszeichen/Erledigt an den Vermittler — verlierbar (die Lease heilt).</param>
    /// <param name="sende">Einen zugeteilten Eingang weiterreichen (an den Menschen, den Worker) — mit der Zahl der schon festgehaltenen Ausgaben
    /// (Strom: ein neuer Träger macht beim nächsten nicht festgehaltenen Stück weiter, Fortschritt wie im Host).</param>
    /// <param name="beantwortet">An den Funktions-Actor der Partition: die Antwort liegt fest (bzw. Fehler; mit <c>teil</c> nur ein Stück eines
    /// Stroms) — verlierbar.</param>
    public BausteinSitzung(string sitzung, Func<string, FunktionsBaustein?> loese,
        Func<FunktionsBaustein, HoleEingaenge, CancellationToken, Task<EingaengeZugeteilt?>> hole, Func<FunktionsBaustein, object, Task> melde,
        Func<EingangAnbieten, FunktionsBaustein, int, Task> sende, IAusgabeAblage ablage, Func<FunktionsBaustein, EingangAnbieten, string?, bool, Task> beantwortet,
        ILogger? logger = null, int warteMs = 4_000)
    {
        _sitzung = sitzung;
        _loese = loese;
        _hole = hole;
        _melde = melde;
        _sende = sende;
        _ablage = ablage;
        _beantwortet = beantwortet;
        _logger = logger;
        _warteMs = warteMs;
    }

    /// <summary>Die Eingänge, die gerade hier auf eine Antwort warten.</summary>
    public IReadOnlyList<(FunktionsBaustein Funktion, EingangAnbieten Eingang)> Laufende => _laufend.Values.Select(l => (l.Funktion, l.Eingang)).ToList();

    public string Arbeiter(FunktionsBaustein f) => $"{_sitzung}/{f.Name}";

    /// <summary>
    /// Bietet Funktionen an (je neuer Funktion eine Hol-Schleife); liefert die unbekannten Namen. Mit <paramref name="darf"/> nur, was der Akteur
    /// der Sitzung rechnen darf — der Rest steht in <paramref name="nichtBefugt"/>.
    /// </summary>
    public IReadOnlyList<string> Biete(IEnumerable<(string Funktion, int Slots)> angebote, Func<Type, bool>? darf, out IReadOnlyList<string> nichtBefugt)
    {
        var unbekannt = new List<string>();
        var verweigert = new List<string>();
        nichtBefugt = verweigert;
        foreach (var (name, slots) in angebote)
        {
            if (_loese(name) is not { } f) { unbekannt.Add(name); continue; }
            if (darf != null && !darf(f.Typ)) { verweigert.Add(name); continue; }
            if (_schleifen.TryAdd(f.Name, Task.CompletedTask))
                _schleifen[f.Name] = HoleSchleifeAsync(f, Math.Max(1, slots), _ende.Token);
        }
        return unbekannt;
    }

    /// <summary>Bietet Funktionen an, ohne Prüfung der Befugnis (die Werkbank: der Mensch am Laufort Hand).</summary>
    public IReadOnlyList<string> Biete(IEnumerable<(string Funktion, int Slots)> angebote) => Biete(angebote, null, out _);

    private async Task HoleSchleifeAsync(FunktionsBaustein f, int anzahl, CancellationToken ct)
    {
        await Task.Yield();
        var slots = new SemaphoreSlim(anzahl, anzahl);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await slots.WaitAsync(ct);
                var frei = 1;
                while (slots.Wait(0)) frei++;
                EingaengeZugeteilt? zugeteilt = null;
                try { zugeteilt = await _hole(f, new HoleEingaenge(Arbeiter(f), frei, _warteMs), ct); }
                finally
                {
                    var genutzt = zugeteilt?.Eingaenge.Count(e => !_laufend.ContainsKey(e.Vorgang)) ?? 0;
                    if (frei - genutzt > 0) slots.Release(frei - genutzt);
                }
                foreach (var e in zugeteilt?.Eingaenge ?? Array.Empty<EingangAnbieten>())
                {
                    if (!_laufend.TryAdd(e.Vorgang, new Laufend(f, e, slots))) continue;   // nach Lease-Ablauf erneut zugeteilt: läuft schon hier
                    try
                    {
                        // Ein Strom, den ein früherer Träger begonnen hat: weiter ab dem, was schon festgehalten ist.
                        var schon = f.Eingang(e.Eingang.GetType()) is { Strom: true }
                            ? (await _ablage.LiesAsync(new FunktionsEingabe(f.Name, e.Stream, e.Version, e.KorrelationsText, e.Akteur), ct)).Ausgaben.Count
                            : 0;
                        await _sende(e, f, schon);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[Funktion] {Funktion}: Eingang {Vorgang} nicht weitergereicht — die Lease läuft ab", f.Name, e.Vorgang);
                        if (_laufend.TryRemove(e.Vorgang, out _)) slots.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[Funktion] Abholen von {Funktion} fehlgeschlagen — neuer Versuch", f.Name);
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Lebenszeichen eines wartenden Eingangs (der Mensch überlegt noch) — verlängert seine Lease.</summary>
    public Task LebtAsync(Guid vorgang) =>
        _laufend.TryGetValue(vorgang, out var l) ? _melde(l.Funktion, new ArbeitLebt(vorgang, Arbeiter(l.Funktion))) : Task.CompletedTask;

    /// <summary>
    /// Die Antwort auf einen Eingang: Ausgaben der Funktion (nur Typen ihrer Signatur) oder ein Fehler — ganz oder als Stück eines Stroms
    /// (<paramref name="ab"/> = Stelle seiner ersten Ausgabe, <paramref name="fertig"/> = der Lauf ist vollständig). Festgehalten wird unter „F, X“
    /// an dieser Stelle — wer zuerst festhält, gewinnt (<see cref="ErgebnisAusgang.Ueberholt"/>: eine andere Antwort war schneller, sie gilt).
    /// </summary>
    public async Task<ErgebnisAusgang> AntwortAsync(Guid vorgang, IReadOnlyList<IPipelineOutput>? ausgaben, string? fehler, CancellationToken ct,
        int ab = 0, bool fertig = true)
    {
        if (!_laufend.TryGetValue(vorgang, out var l)) return ErgebnisAusgang.Unbekannt;
        var vorbei = true;   // die Arbeit an diesem Eingang endet hier (Fehler, letztes Stück, überholt) — sonst kommt noch ein Stück
        try
        {
            var eingang = l.Funktion.Eingang(l.Eingang.Eingang.GetType());
            if (fehler is null && (ausgaben is null || eingang is null || ausgaben.Any(a => !eingang.Ausgaben.Contains(a.GetType()))))
                fehler = $"Antwort außerhalb der Signatur von {l.Funktion.Name} ({string.Join(", ", eingang?.Ausgaben.Select(t => t.Name) ?? [])})";
            if (fehler is null && !fertig && eingang is { Strom: false })
                fehler = $"{l.Funktion.Name} antwortet auf {eingang.Typ.Name} mit genau einer Ausgabe, nicht mit einem Strom";
            if (fehler is not null)
            {
                await _beantwortet(l.Funktion, l.Eingang, fehler, false);
                await _melde(l.Funktion, new ArbeitErledigt(vorgang));
                return ErgebnisAusgang.Abgelehnt;
            }
            var eingabe = new FunktionsEingabe(l.Funktion.Name, l.Eingang.Stream, l.Eingang.Version, l.Eingang.KorrelationsText, l.Eingang.Akteur);
            bool gewonnen;
            using (Spur.Von(l.Eingang.Ursache, l.Funktion.Wirker))
            {
                try { gewonnen = await _ablage.HalteFestAsync(eingabe, ab, ausgaben!.Where(a => a is not ITransientEvent).ToList(), abgeschlossen: fertig, ct); }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[Funktion] {Funktion}: Antwort auf {Vorgang} nicht festgehalten — die Lease läuft ab", l.Funktion.Name, vorgang);
                    return ErgebnisAusgang.NichtGeschrieben;
                }
            }
            vorbei = fertig || !gewonnen;
            if (vorbei) await _melde(l.Funktion, new ArbeitErledigt(vorgang));
            else await _melde(l.Funktion, new ArbeitLebt(vorgang, Arbeiter(l.Funktion)));
            await _beantwortet(l.Funktion, l.Eingang, null, !vorbei);
            return gewonnen ? ErgebnisAusgang.Geschrieben : ErgebnisAusgang.Ueberholt;
        }
        finally
        {
            if (vorbei && _laufend.TryRemove(vorgang, out var weg)) weg.Slots.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _ende.Cancel();
        try { await Task.WhenAll(_schleifen.Values); } catch { /* gemeldet */ }
        _laufend.Clear();
    }
}
