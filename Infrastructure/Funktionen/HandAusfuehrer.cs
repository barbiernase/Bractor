using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // WakeAck
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

/// <summary>Ein Auftrag, der auf eine menschliche Antwort wartet: der Auftrag, seine Funktion und seit wann er hier liegt.</summary>
public sealed record HandAuftrag(AuftragAnbieten Auftrag, Type Funktion, DateTimeOffset Seit);

/// <summary>Ein Eingang einer Funktion in Baustein-Form, der auf eine menschliche Antwort wartet.</summary>
public sealed record HandEingang(EingangAnbieten Eingang, FunktionsBaustein Funktion, DateTimeOffset Seit);

/// <summary>
/// Laufort <c>Hand</c> (docs/konzept-werkbank.md §6, Werkbank P5.2): ein MENSCH beantwortet die Aufträge einer Funktion — die Werkbank ist
/// der Ausführer. Derselbe Weg wie ein Python-Worker: eine <see cref="FunktionsAnbieterSitzung"/> holt die Aufträge beim Vermittler (Pull),
/// legt sie hier ab, statt sie über gRPC weiterzureichen, und hält ihre Lease mit Lebenszeichen, solange der Mensch überlegt. Die Antwort
/// geht durch denselben Abschluss (<see cref="FunktionsAusfuehrer.SchreibeErgebnisAsync"/>): genau ein Ergebnis im Ausführungs-Stream,
/// Ergebnis außerhalb des Vertrags = Fehlschlag-Marke, danach „erledigt". Fluss und Prozess merken nicht, dass ein Mensch rechnete.
///
/// Nicht maßgeblich: startet der Host neu, ist die Ablage leer — die Lease läuft ab, der Dirigent bietet neu an, der Auftrag liegt wieder hier.
/// </summary>
public sealed class HandAusfuehrer : BackgroundService
{
    private static readonly TimeSpan LebenszeichenAlle = TimeSpan.FromSeconds(10);
    /// <summary>Wie viele Aufträge je Funktion gleichzeitig auf eine Antwort warten dürfen.</summary>
    public const int Slots = 1000;

    private readonly ActorSystem _system;
    private readonly FunktionsAusfuehrer _ausfuehrer;
    private readonly IReadOnlyList<Type> _funktionen;
    private readonly ILogger<HandAusfuehrer>? _logger;
    private readonly ConcurrentDictionary<Guid, HandAuftrag> _ablage = new();
    private FunktionsAnbieterSitzung? _sitzung;
    // Funktionen in Baustein-Form mit Laufort Hand (Plan Pub/Sub-Neustrukturierung Phase 3): dieselbe Rolle, eine eigene Sitzung.
    private readonly IReadOnlyList<FunktionsBaustein> _bausteine;
    private readonly IAusgabeAblage? _ausgaben;
    private readonly Projections.Konsum _konsum;
    private readonly ConcurrentDictionary<Guid, HandEingang> _eingaenge = new();
    private BausteinSitzung? _bausteinSitzung;

    public HandAusfuehrer(ActorSystem system, FunktionsAusfuehrer ausfuehrer, IEnumerable<FunktionsBindung> bindungen,
        ILogger<HandAusfuehrer>? logger = null, IEnumerable<BausteinBindung>? bausteinBindungen = null, IAusgabeAblage? ausgaben = null,
        Projections.Konsum? konsum = null)
    {
        _system = system;
        _ausfuehrer = ausfuehrer;
        _funktionen = bindungen.Where(b => b.Hand).Select(b => b.Funktion).Distinct().ToList();
        _logger = logger;
        var hand = (bausteinBindungen ?? []).Where(b => b.Laufort == FunktionsLaufort.Hand).Select(b => b.Funktion).ToHashSet(StringComparer.Ordinal);
        _bausteine = Anwendung.Funktionen.Bausteine.Where(b => hand.Contains(b.Name)).ToList();
        _ausgaben = ausgaben;
        _konsum = konsum ?? Projections.Konsum.Standard;
    }

    /// <summary>Die Funktionen in Baustein-Form mit Laufort Hand.</summary>
    public IReadOnlyList<FunktionsBaustein> Bausteine => _bausteine;

    /// <summary>Die wartenden Eingänge (Baustein-Form), älteste zuerst.</summary>
    public IReadOnlyList<HandEingang> OffeneEingaenge => _eingaenge.Values.OrderBy(a => a.Seit).ToList();

    /// <summary>
    /// Die Antwort des Menschen auf einen Eingang (Baustein-Form): die Ausgaben der Funktion (nur Typen ihrer Signatur) oder ein Fehler. Festgehalten wird
    /// unter „F, X“ — eine andere, schnellere Antwort gewinnt (<see cref="ErgebnisAusgang.Ueberholt"/>).
    /// </summary>
    public async Task<ErgebnisAusgang> AntworteAsync(Guid vorgang, IReadOnlyList<IPipelineOutput>? ausgaben, string? fehler, CancellationToken ct)
    {
        if (_bausteinSitzung is null || !_eingaenge.ContainsKey(vorgang)) return ErgebnisAusgang.Unbekannt;
        var ausgang = await _bausteinSitzung.AntwortAsync(vorgang, ausgaben, fehler, ct);
        _eingaenge.TryRemove(vorgang, out _);
        return ausgang;
    }

    /// <summary>Die Funktionen mit Laufort Hand.</summary>
    public IReadOnlyList<Type> Funktionen => _funktionen;

    /// <summary>Die wartenden Aufträge, älteste zuerst.</summary>
    public IReadOnlyList<HandAuftrag> Offen => _ablage.Values.OrderBy(a => a.Seit).ToList();

    /// <summary>
    /// Die Antwort des Menschen: ein Ergebnis-Fall der Funktion (sonst schreibt die Sitzung die Fehlschlag-Marke) oder ein Fehler
    /// (<paramref name="fehler"/> = der Schritt scheitert). <see cref="ErgebnisAusgang.Unbekannt"/>: der Auftrag liegt hier nicht (mehr).
    /// </summary>
    public async Task<ErgebnisAusgang> AntworteAsync(Guid vorgang, IEvent? ergebnis, string? fehler, CancellationToken ct)
    {
        if (_sitzung is null || !_ablage.ContainsKey(vorgang)) return ErgebnisAusgang.Unbekannt;
        var ausgang = await _sitzung.ErgebnisAsync(vorgang, ergebnis, fehler, ct);
        _ablage.TryRemove(vorgang, out _);   // NichtGeschrieben: die Lease läuft ab, der Auftrag kommt wieder
        return ausgang;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_funktionen.Count == 0 && _bausteine.Count == 0) return;
        var bote = new Bote(_system.Cluster(), _logger);
        if (_bausteine.Count > 0 && _ausgaben is not null)
        {
            _bausteinSitzung = new BausteinSitzung($"{_system.Address}/hand",
                name => _bausteine.FirstOrDefault(b => b.Name == name),
                (b, anfrage, ct) => bote.FrageAsync<EingaengeZugeteilt>(FunktionsVermittlerActor.Identitaet(b.Typ), anfrage,
                    TimeSpan.FromMilliseconds(Math.Max(0, anfrage.WarteMs)) + TimeSpan.FromSeconds(5), ct),
                (b, nachricht) => { bote.Melde(FunktionsVermittlerActor.Identitaet(b.Typ), nachricht); return Task.CompletedTask; },
                (e, b, _) =>
                {
                    _eingaenge.TryAdd(e.Vorgang, new HandEingang(e, b, DateTimeOffset.UtcNow));
                    _logger?.LogInformation("[Funktion] {Funktion}: Eingang {Stream}:{Version} wartet auf eine Antwort von Hand", b.Name, e.Stream, e.Version);
                    return Task.CompletedTask;
                },
                _ausgaben,
                (b, e, fehler, teil) =>
                {
                    bote.Melde(FunktionsActor.Identitaet(b, Partitionierung.Von(e.Stream, _konsum.Partitionen)), new EingangBeantwortet(e.Vorgang, fehler, teil));
                    return Task.CompletedTask;
                },
                _logger);
            _bausteinSitzung.Biete(_bausteine.Select(b => (b.Name, Slots)));
        }
        if (_funktionen.Count == 0)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(LebenszeichenAlle, stoppingToken);
                    foreach (var v in _eingaenge.Keys) await _bausteinSitzung!.LebtAsync(v);
                }
            }
            catch (OperationCanceledException) { }
            finally { if (_bausteinSitzung is not null) await _bausteinSitzung.DisposeAsync(); }
            return;
        }
        _sitzung = new FunktionsAnbieterSitzung(
            $"{_system.Address}/hand",
            name => FunktionsAnbieterSitzung.LoeseImKatalog(name, _funktionen),
            f => Anwendung.Funktionen.Ergebnisse.TryGetValue(f, out var e) ? e : Array.Empty<Type>(),
            FunktionsAnbieter.Hole(bote),
            FunktionsAnbieter.Melde(bote),
            (a, f) =>
            {
                _ablage.TryAdd(a.Vorgang, new HandAuftrag(a, f, DateTimeOffset.UtcNow));
                _logger?.LogInformation("[Funktion] {Funktion}: Auftrag {Vorgang} wartet auf eine Antwort von Hand", f.Name, a.Vorgang);
                return Task.CompletedTask;
            },
            _ausfuehrer.SchreibeErgebnisAsync,
            _logger);
        _sitzung.Biete(_funktionen.Select(f => (f.Name, Slots)));

        // Lebenszeichen: der Mensch darf überlegen — die Lease jedes wartenden Auftrags wird verlängert.
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(LebenszeichenAlle, stoppingToken);
                foreach (var v in _ablage.Keys) await _sitzung.LebtAsync(v);
                if (_bausteinSitzung is not null) foreach (var v in _eingaenge.Keys) await _bausteinSitzung.LebtAsync(v);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            await _sitzung.DisposeAsync();
            if (_bausteinSitzung is not null) await _bausteinSitzung.DisposeAsync();
        }
    }
}

public static class HandAusfuehrerExtensions
{
    /// <summary>Registriert den Hand-Ausführer einmal (auch wenn mehrere Funktionen den Laufort Hand haben).</summary>
    public static IServiceCollection AddHandAusfuehrer(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(HandAusfuehrer))) return services;
        services.AddSingleton<HandAusfuehrer>();
        services.AddHostedService(sp => sp.GetRequiredService<HandAusfuehrer>());
        return services;
    }
}
