using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Komposition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

/// <summary>Arbeit, die auf eine menschliche Antwort wartet: ihr Schlüssel, der Akteur (als Baustein), der Command und woher er kommt.</summary>
public sealed record HandEingang(Guid Vorgang, FunktionsBaustein Funktion, object Eingang, Guid Stream, int Version, string? Akteur, DateTimeOffset Seit);

/// <summary>
/// HAND (CLAUDE.md W6): ein MENSCH an der Werkbank beantwortet die Arbeit von Akteuren (<c>Umgebung:Hand</c>) — die Werkbank verkörpert sie. Derselbe
/// Weg wie ein Python-Worker: der Ausführer ist ein Arbeiter in der <see cref="Werkstatt"/> des Knotens (unbegrenzte Geduld), das Kind der Arbeit legt
/// den Command hier ab und wartet auf die Antwort; die erste gewinnt, weil der Lauf mit ihr endet. Nicht maßgeblich: startet der Host neu, ist die
/// Ablage leer — der durable Rand bringt den Command erneut, er liegt wieder hier.
/// </summary>
public sealed class HandAusfuehrer : BackgroundService, IArbeiter
{
    /// <summary>Wie viele Aufträge je Akteur gleichzeitig auf eine Antwort warten dürfen.</summary>
    public const int Slots = 1000;

    private readonly string _name;
    private readonly ILogger<HandAusfuehrer>? _logger;
    private readonly IReadOnlyList<FunktionsBaustein> _bausteine;
    private readonly Werkstatt? _werkstatt;
    private readonly ConcurrentDictionary<Guid, HandEingang> _eingaenge = new();

    public HandAusfuehrer(Proto.ActorSystem system, HandAkteure akteure, ILogger<HandAusfuehrer>? logger = null, GraphInstanzen? instanzen = null,
        Werkstatt? werkstatt = null)
    {
        _name = $"{system.Address}/hand";
        _logger = logger;
        // Die Arbeit der Akteure, die die Hand beantwortet (je Akteur einmal angemeldet).
        _bausteine = FunktionsBausteinWiring.Knoten(instanzen ?? GraphInstanzen.Keine)
            .Where(x => x.Baustein.Draussen && akteure.Namen.Contains(x.Baustein.Name))
            .Select(x => x.Baustein).DistinctBy(b => b.Name).ToList();
        _werkstatt = werkstatt;
    }

    /// <summary>Die Arbeit der Akteure, die die Hand beantwortet.</summary>
    public IReadOnlyList<FunktionsBaustein> Bausteine => _bausteine;

    /// <summary>Die wartenden Eingänge, älteste zuerst.</summary>
    public IReadOnlyList<HandEingang> OffeneEingaenge => _eingaenge.Values.OrderBy(a => a.Seit).ToList();

    /// <summary>
    /// Die Antwort des Menschen auf einen Auftrag: Commands, die der Akteur darf (sein IDarf), oder ein Fehler. Die erste Antwort gewinnt — eine spätere
    /// (oder eine auf einen Auftrag, dessen Lauf schon endete) ist <see cref="Werkstattausgang.Unbekannt"/>.
    /// </summary>
    public Werkstattausgang Antworte(Guid vorgang, IReadOnlyList<object>? ausgaben, string? fehler)
    {
        if (!_eingaenge.TryRemove(vorgang, out _)) return Werkstattausgang.Unbekannt;
        return _werkstatt?.Ergebnis(this, vorgang, ausgaben, 0, true, fehler) ?? Werkstattausgang.Unbekannt;
    }

    string IArbeiter.Name => _name;

    TimeSpan? IArbeiter.Geduld => null;

    void IArbeiter.Sende(AuftragDraussen a)
    {
        _eingaenge[a.Vorgang] = new HandEingang(a.Vorgang, a.Funktion, a.Eingang, a.Herkunft.Stream, a.Herkunft.Version, a.Herkunft.Akteur, DateTimeOffset.UtcNow);
        _logger?.LogInformation("[Arbeit] {Akteur}: Auftrag {Stream}:{Version} wartet auf eine Antwort von Hand", a.Funktion.Name, a.Herkunft.Stream,
            a.Herkunft.Version);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_werkstatt is null || _bausteine.Count == 0) return Task.CompletedTask;
        foreach (var b in _bausteine) _werkstatt.Anmelden(this, b.Name, Slots);
        stoppingToken.Register(() => _werkstatt.Abmelden(this));
        return Task.CompletedTask;
    }
}

/// <summary>Die Akteure, deren Arbeit die Hand beantwortet (<c>Umgebung:Hand</c>).</summary>
public sealed record HandAkteure(IReadOnlySet<string> Namen);

public static class HandAusfuehrerExtensions
{
    /// <summary>Registriert den Hand-Ausführer für die Arbeit der genannten Akteure (einmal; weitere Aufrufe nehmen Akteure dazu).</summary>
    public static IServiceCollection AddHandAusfuehrer(this IServiceCollection services, params string[] akteure)
    {
        var schon = services.FirstOrDefault(d => d.ServiceType == typeof(HandAkteure))?.ImplementationInstance as HandAkteure;
        services.Replace(ServiceDescriptor.Singleton(new HandAkteure((schon?.Namen ?? new HashSet<string>()).Concat(akteure).ToHashSet(StringComparer.Ordinal))));
        if (services.Any(d => d.ServiceType == typeof(HandAusfuehrer))) return services;
        services.AddSingleton<HandAusfuehrer>();
        services.AddHostedService(sp => sp.GetRequiredService<HandAusfuehrer>());
        return services;
    }
}
