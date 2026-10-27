using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // Partitionierung
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

/// <summary>Ein Eingang einer Funktion, der auf eine menschliche Antwort wartet.</summary>
public sealed record HandEingang(EingangAnbieten Eingang, FunktionsBaustein Funktion, DateTimeOffset Seit);

/// <summary>
/// Laufort <c>Hand</c> (docs/konzept-werkbank.md §6, Werkbank P5.2): ein MENSCH beantwortet die Eingänge einer Funktion — die Werkbank ist der
/// Träger. Derselbe Weg wie ein Python-Worker: eine <see cref="BausteinSitzung"/> holt die Eingänge beim Vermittler (Pull), legt sie hier ab, statt
/// sie über gRPC weiterzureichen, und hält ihre Lease mit Lebenszeichen, solange der Mensch überlegt. Die Antwort wird unter „Funktion F,
/// Eingang X“ festgehalten (die erste gewinnt) und vom Funktions-Actor gesendet — er merkt nicht, dass ein Mensch rechnete.
///
/// Nicht maßgeblich: startet der Host neu, ist die Ablage leer — die Lease läuft ab, der Funktions-Actor bietet neu an, der Eingang liegt wieder hier.
/// </summary>
public sealed class HandAusfuehrer : BackgroundService
{
    private static readonly TimeSpan LebenszeichenAlle = TimeSpan.FromSeconds(10);
    /// <summary>Wie viele Eingänge je Funktion gleichzeitig auf eine Antwort warten dürfen.</summary>
    public const int Slots = 1000;

    private readonly ActorSystem _system;
    private readonly ILogger<HandAusfuehrer>? _logger;
    private readonly IReadOnlyList<FunktionsBaustein> _bausteine;
    private readonly IAusgabeAblage? _ausgaben;
    private readonly Projections.Konsum _konsum;
    private readonly ConcurrentDictionary<Guid, HandEingang> _eingaenge = new();
    private BausteinSitzung? _sitzung;

    public HandAusfuehrer(ActorSystem system, ILogger<HandAusfuehrer>? logger = null, IEnumerable<BausteinBindung>? bausteinBindungen = null,
        IAusgabeAblage? ausgaben = null, Projections.Konsum? konsum = null)
    {
        _system = system;
        _logger = logger;
        var hand = (bausteinBindungen ?? []).Where(b => b.Laufort == FunktionsLaufort.Hand).Select(b => b.Funktion).ToHashSet(StringComparer.Ordinal);
        _bausteine = Anwendung.Funktionen.Bausteine.Where(b => hand.Contains(b.Name)).ToList();
        _ausgaben = ausgaben;
        _konsum = konsum ?? Projections.Konsum.Standard;
    }

    /// <summary>Die Funktionen mit Laufort Hand.</summary>
    public IReadOnlyList<FunktionsBaustein> Bausteine => _bausteine;

    /// <summary>Die wartenden Eingänge, älteste zuerst.</summary>
    public IReadOnlyList<HandEingang> OffeneEingaenge => _eingaenge.Values.OrderBy(a => a.Seit).ToList();

    /// <summary>
    /// Die Antwort des Menschen auf einen Eingang: die Ausgaben der Funktion (nur Typen ihrer Signatur) oder ein Fehler. Festgehalten wird unter
    /// „F, X“ — eine andere, schnellere Antwort gewinnt (<see cref="ErgebnisAusgang.Ueberholt"/>).
    /// </summary>
    public async Task<ErgebnisAusgang> AntworteAsync(Guid vorgang, IReadOnlyList<IAusgabe>? ausgaben, string? fehler, CancellationToken ct)
    {
        if (_sitzung is null || !_eingaenge.ContainsKey(vorgang)) return ErgebnisAusgang.Unbekannt;
        var ausgang = await _sitzung.AntwortAsync(vorgang, ausgaben, fehler, ct);
        _eingaenge.TryRemove(vorgang, out _);
        return ausgang;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_bausteine.Count == 0 || _ausgaben is null) return;
        var bote = new Bote(_system.Cluster(), _logger);
        _sitzung = new BausteinSitzung($"{_system.Address}/hand",
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
            (b, e, fehler, teil, ausgaben) =>
            {
                bote.Melde(FunktionsActor.IdentitaetFuer(b, e.Stream, _konsum), EingangBeantwortet.Aus(e.Vorgang, fehler, teil, ausgaben));
                return Task.CompletedTask;
            },
            _logger);
        _sitzung.Biete(_bausteine.Select(b => (b.Name, Slots)));

        // Lebenszeichen: der Mensch darf überlegen — die Lease jedes wartenden Eingangs wird verlängert.
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(LebenszeichenAlle, stoppingToken);
                foreach (var v in _eingaenge.Keys) await _sitzung.LebtAsync(v);
            }
        }
        catch (OperationCanceledException) { }
        finally { await _sitzung.DisposeAsync(); }
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
