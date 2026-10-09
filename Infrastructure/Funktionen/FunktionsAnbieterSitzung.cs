using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Aggregate;   // KommandoAbgelehnt — die durable Fehlschlag-Marke
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

/// <summary>Was mit einem gemeldeten Ergebnis eines externen Ausführers geschah.</summary>
public enum ErgebnisAusgang
{
    /// <summary>Der Vorgang läuft in dieser Sitzung nicht (schon erledigt, nie zugeteilt, alte Sitzung) — nichts geschrieben.</summary>
    Unbekannt,
    /// <summary>Das Ergebnis-Event liegt im Ausführungs-Stream.</summary>
    Geschrieben,
    /// <summary>Gescheitert (Fehler gemeldet oder Ergebnis außerhalb des Vertrags) — die Fehlschlag-Marke liegt im Stream.</summary>
    Abgelehnt,
    /// <summary>Nicht schreibbar (Store) — nicht „erledigt“ gemeldet; die Lease läuft ab, der Auftrag geht neu raus.</summary>
    NichtGeschrieben,
}

/// <summary>
/// Die Anbieter-Seite EINER gRPC-Sitzung (docs/konzept-editor-pipelines.md §14.5): ein externer Worker (Python) meldet „ich biete
/// Funktion F mit n Slots“; der Server holt für ihn Aufträge beim Vermittler (Pull — so viele, wie Slots frei sind), reicht sie
/// als <c>ArbeitsAuftrag</c> weiter, nimmt Lebenszeichen und Ergebnisse entgegen und schreibt das Ergebnis GENAU EINMAL in den
/// Ausführungs-Stream (derselbe Abschluss wie bei C#-Ausführung: <see cref="FunktionsAusfuehrer.SchreibeErgebnisAsync"/>).
/// Pipeline und Prozess merken davon nichts — wo eine Funktion rechnet, ist eine Bindung.
///
/// Das Gegenstück zum <see cref="FunktionsAbholer"/> eines Knotens, nur dass hier der Worker rechnet. Transport-frei: Vermittler,
/// Weiterreichen an den Client und Schreiben sind Delegaten — im Prüfstand in-memory beweisbar.
///
/// Endet die Sitzung (<see cref="DisposeAsync"/>), enden die Hol-Schleifen; was in Arbeit war, wird nicht „erledigt“ gemeldet —
/// seine Lease läuft ab und der Vermittler gibt es neu aus (an einen anderen Worker oder die neue Sitzung nach Reconnect).
/// </summary>
public sealed class FunktionsAnbieterSitzung : IAsyncDisposable
{
    private sealed record Laufend(Type Funktion, AuftragAnbieten Auftrag, SemaphoreSlim Slots);

    private readonly string _sitzung;
    private readonly Func<string, Type?> _loese;
    private readonly Func<Type, IReadOnlyCollection<Type>> _ergebnisse;
    private readonly Func<Type, HoleArbeit, CancellationToken, Task<ArbeitZugeteilt>> _hole;
    private readonly Func<Type, object, Task> _melde;
    private readonly Func<AuftragAnbieten, Type, Task> _sende;
    private readonly Func<Guid, Guid, IEvent, string?, string, CancellationToken, Task<bool>> _schreibe;
    private readonly ILogger? _logger;
    private readonly int _warteMs;
    private readonly TimeSpan _fehlerPause;

    private readonly CancellationTokenSource _ende = new();
    private readonly ConcurrentDictionary<Type, Task> _schleifen = new();
    private readonly ConcurrentDictionary<Guid, Laufend> _laufend = new();

    /// <param name="sitzung">Die Session-Id — Teil des Arbeiter-Namens am Vermittler (<c>{sitzung}/{Funktion}</c>).</param>
    /// <param name="loese">Name der Funktion (einfach, z. B. „IBildVerkleinerung“, oder voll) → Typ; unbekannt = null.</param>
    /// <param name="ergebnisse">Die erlaubten Ergebnis-Events einer Funktion (ihre OneOf-Signatur).</param>
    /// <param name="hole">Arbeit beim Vermittler der Funktion holen (Long-Poll).</param>
    /// <param name="melde">Lebenszeichen/Erledigt an den Vermittler der Funktion — bounded, verlierbar (die Lease heilt).</param>
    /// <param name="sende">Einen zugeteilten Auftrag an den Worker weiterreichen.</param>
    /// <param name="schreibe">Ergebnis genau einmal schreiben + Prozess wecken (korrelation, vorgang, ergebnis, akteur, name).</param>
    public FunktionsAnbieterSitzung(
        string sitzung,
        Func<string, Type?> loese,
        Func<Type, IReadOnlyCollection<Type>> ergebnisse,
        Func<Type, HoleArbeit, CancellationToken, Task<ArbeitZugeteilt>> hole,
        Func<Type, object, Task> melde,
        Func<AuftragAnbieten, Type, Task> sende,
        Func<Guid, Guid, IEvent, string?, string, CancellationToken, Task<bool>> schreibe,
        ILogger? logger = null,
        int warteMs = 10_000,
        TimeSpan? fehlerPause = null)
    {
        _sitzung = sitzung;
        _loese = loese;
        _ergebnisse = ergebnisse;
        _hole = hole;
        _melde = melde;
        _sende = sende;
        _schreibe = schreibe;
        _logger = logger;
        _warteMs = warteMs;
        _fehlerPause = fehlerPause ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>Wie viele Aufträge gerade beim Worker liegen (Beobachtung/Test).</summary>
    public int Laufende => _laufend.Count;

    /// <summary>Die Funktionen, die diese Sitzung anbietet.</summary>
    public IReadOnlyCollection<Type> Angeboten => _schleifen.Keys.ToList();

    /// <summary>Der Arbeiter-Name dieser Sitzung für eine Funktion (Lease-Besitzer am Vermittler).</summary>
    public string Arbeiter(Type funktion) => $"{_sitzung}/{funktion.Name}";

    /// <summary>
    /// Nimmt Angebote auf und startet je NEUER Funktion eine Hol-Schleife (ein erneutes Angebot derselben Funktion ändert
    /// nichts). Liefert die Namen, die keine bekannte Funktion sind.
    /// </summary>
    public IReadOnlyList<string> Biete(IEnumerable<(string Funktion, int Slots)> angebote) => Biete(angebote, null, out _);

    /// <summary>
    /// Wie <see cref="Biete(IEnumerable{ValueTuple{string, int}})"/>, mit dem AKTEUR-TOR: eine Funktion, die <paramref name="darf"/> nicht
    /// erlaubt (der angemeldete Akteur hat kein <c>IDarf&lt;F&gt;</c>), wird nicht angenommen — sie steht in <paramref name="nichtBefugt"/>.
    /// <paramref name="darf"/> = null: kein Tor (alles offen, wie ohne Akteur-Token).
    /// </summary>
    public IReadOnlyList<string> Biete(IEnumerable<(string Funktion, int Slots)> angebote, Func<Type, bool>? darf, out IReadOnlyList<string> nichtBefugt)
    {
        var unbekannt = new List<string>();
        var abgewiesen = new List<string>();
        nichtBefugt = abgewiesen;
        foreach (var (name, slots) in angebote)
        {
            if (_loese(name) is not { } funktion)
            {
                unbekannt.Add(name);
                continue;
            }
            if (darf is not null && !darf(funktion))
            {
                abgewiesen.Add(name);
                _logger?.LogWarning("[Funktion] {Sitzung} darf {Funktion} nicht rechnen (kein IDarf<{Funktion}> am Akteur)", _sitzung, funktion.Name, funktion.Name);
                continue;
            }
            var anzahl = Math.Max(1, slots);
            if (_schleifen.TryAdd(funktion, Task.CompletedTask))
            {
                _schleifen[funktion] = Task.Run(() => HoleSchleifeAsync(funktion, anzahl, _ende.Token));
                _logger?.LogInformation("[Funktion] {Sitzung} bietet {Funktion} an ({Slots} Slots)", _sitzung, funktion.Name, anzahl);
            }
        }
        return unbekannt;
    }

    private async Task HoleSchleifeAsync(Type funktion, int anzahl, CancellationToken ct)
    {
        var slots = new SemaphoreSlim(anzahl, anzahl);
        var arbeiter = Arbeiter(funktion);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await slots.WaitAsync(ct);
                var frei = 1;
                while (slots.Wait(0)) frei++;   // alle gerade freien Slots auf einmal anfragen

                ArbeitZugeteilt? zugeteilt = null;
                try
                {
                    zugeteilt = await _hole(funktion, new HoleArbeit(arbeiter, frei, _warteMs), ct);
                }
                finally
                {
                    var genutzt = zugeteilt?.Auftraege.Count ?? 0;
                    if (frei - genutzt > 0) slots.Release(frei - genutzt);
                }

                foreach (var a in zugeteilt?.Auftraege ?? Array.Empty<AuftragAnbieten>())
                    await GibWeiterAsync(funktion, a, slots);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[Funktion] {Sitzung}: Abholen von {Funktion} fehlgeschlagen — neuer Versuch", _sitzung, funktion.Name);
                try { await Task.Delay(_fehlerPause, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Einen zugeteilten Auftrag an den Worker reichen; sein Slot bleibt belegt, bis das Ergebnis kommt.</summary>
    private async Task GibWeiterAsync(Type funktion, AuftragAnbieten a, SemaphoreSlim slots)
    {
        // Derselbe Vorgang noch einmal (Lease abgelaufen, an uns neu ausgegeben): der Worker hat ihn schon — Slot zurück.
        if (!_laufend.TryAdd(a.Vorgang, new Laufend(funktion, a, slots)))
        {
            slots.Release();
            return;
        }
        try
        {
            await _sende(a, funktion);
        }
        catch (Exception ex)
        {
            // Nicht zustellbar: vergessen, Slot frei; die Lease läuft ab und der Auftrag geht neu raus.
            if (_laufend.TryRemove(a.Vorgang, out _)) slots.Release();
            _logger?.LogWarning(ex, "[Funktion] {Sitzung}: Auftrag {Vorgang} nicht zustellbar", _sitzung, a.Vorgang);
        }
    }

    /// <summary>Lebenszeichen des Workers: verlängert die Lease des laufenden Auftrags.</summary>
    public async Task LebtAsync(Guid vorgang)
    {
        if (_laufend.TryGetValue(vorgang, out var l))
            await MeldeAsync(l.Funktion, new ArbeitLebt(vorgang, Arbeiter(l.Funktion)));
    }

    /// <summary>
    /// Das Ergebnis des Workers: <paramref name="ergebnis"/> muss einer der OneOf-Fälle der Funktion sein, sonst (oder bei
    /// <paramref name="fehler"/>) wird die Fehlschlag-Marke <see cref="KommandoAbgelehnt"/> geschrieben — der Prozess sieht dann
    /// einen gescheiterten Schritt (Kompensation/Zweig), genau wie bei einer werfenden C#-Funktion. Danach „erledigt“ an den
    /// Vermittler und der Slot ist frei.
    /// </summary>
    public async Task<ErgebnisAusgang> ErgebnisAsync(Guid vorgang, IEvent? ergebnis, string? fehler, CancellationToken ct)
    {
        if (!_laufend.TryRemove(vorgang, out var l))
        {
            _logger?.LogWarning("[Funktion] {Sitzung}: Ergebnis für unbekannten Vorgang {Vorgang} verworfen", _sitzung, vorgang);
            return ErgebnisAusgang.Unbekannt;
        }
        try
        {
            var name = l.Funktion.Name;
            IEvent geschrieben;
            ErgebnisAusgang ausgang;
            if (!string.IsNullOrEmpty(fehler))
            {
                geschrieben = new KommandoAbgelehnt(vorgang, $"{name}: {fehler}");
                ausgang = ErgebnisAusgang.Abgelehnt;
            }
            else if (ergebnis is null)
            {
                geschrieben = new KommandoAbgelehnt(vorgang, $"{name}: kein Ergebnis geliefert");
                ausgang = ErgebnisAusgang.Abgelehnt;
            }
            else if (!_ergebnisse(l.Funktion).Contains(ergebnis.GetType()))
            {
                var erlaubt = string.Join(", ", _ergebnisse(l.Funktion).Select(t => t.Name));
                geschrieben = new KommandoAbgelehnt(vorgang, $"{name}: Ergebnis {ergebnis.GetType().Name} liegt außerhalb des Vertrags ({erlaubt})");
                ausgang = ErgebnisAusgang.Abgelehnt;
            }
            else
            {
                geschrieben = ergebnis;
                ausgang = ErgebnisAusgang.Geschrieben;
            }

            var a = l.Auftrag;
            if (!await _schreibe(a.Korrelation, vorgang, geschrieben, a.Akteur, name, ct))
                return ErgebnisAusgang.NichtGeschrieben;   // nicht erledigt melden: die Lease heilt

            await MeldeAsync(l.Funktion, new ArbeitErledigt(vorgang));
            return ausgang;
        }
        finally
        {
            l.Slots.Release();
        }
    }

    private async Task MeldeAsync(Type funktion, object nachricht)
    {
        try { await _melde(funktion, nachricht); }
        catch (Exception ex) { _logger?.LogDebug(ex, "[Funktion] {Sitzung}: Meldung an den Vermittler verloren — die Lease heilt", _sitzung); }
    }

    /// <summary>Sitzung endet: Hol-Schleifen stoppen. Laufendes wird nicht erledigt gemeldet — die Leases laufen ab.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_ende.IsCancellationRequested) return;
        _ende.Cancel();
        try { await Task.WhenAll(_schleifen.Values); } catch { /* Schleifen-Ende ist erwartbar */ }
        _laufend.Clear();
    }

    /// <summary>
    /// Die Live-Auflösung eines Funktionsnamens gegen den generierten Katalog (<c>GeneratedFunktionen</c>): einfacher Name
    /// („IBildVerkleinerung“) oder voller Name.
    /// </summary>
    public static Type? LoeseImKatalog(string name, IEnumerable<Type> katalog)
        => katalog.FirstOrDefault(t => t.Name == name) ?? katalog.FirstOrDefault(t => t.FullName == name);
}
