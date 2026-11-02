using System.Collections.Concurrent;
using Abstractions;
using Infrastructure.Komposition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

/// <summary>Ein Eingang einer Funktion, der auf eine menschliche Antwort wartet: sein Schlüssel, die Nachricht und woher sie kommt.</summary>
public sealed record HandEingang(Guid Vorgang, FunktionsBaustein Funktion, object Eingang, Guid Stream, int Version, string? Akteur, DateTimeOffset Seit);

/// <summary>
/// Laufort <c>Hand</c> (CLAUDE.md W6): ein MENSCH beantwortet die Eingänge einer Funktion — die Werkbank ist der Träger. Derselbe Weg wie ein
/// Python-Worker: der Ausführer ist ein Arbeiter in der <see cref="Werkstatt"/> des Knotens (unbegrenzte Geduld), ein Kind der Funktion legt den
/// Eingang hier ab und wartet auf die Antwort; die erste gewinnt, weil der Lauf mit ihr endet. Nicht maßgeblich: startet der Host neu, ist die Ablage
/// leer — der durable Rand bringt den Eingang erneut, er liegt wieder hier.
/// </summary>
public sealed class HandAusfuehrer : BackgroundService, IArbeiter
{
    /// <summary>Wie viele Eingänge je Funktion gleichzeitig auf eine Antwort warten dürfen.</summary>
    public const int Slots = 1000;

    private readonly string _name;
    private readonly ILogger<HandAusfuehrer>? _logger;
    private readonly IReadOnlyList<FunktionsBaustein> _bausteine;
    private readonly Werkstatt? _werkstatt;
    private readonly ConcurrentDictionary<Guid, HandEingang> _eingaenge = new();

    public HandAusfuehrer(Proto.ActorSystem system, ILogger<HandAusfuehrer>? logger = null, GraphInstanzen? instanzen = null, Werkstatt? werkstatt = null)
    {
        _name = $"{system.Address}/hand";
        _logger = logger;
        // Die Funktionen, die an einem Knoten einer Instanz mit Laufort Hand rechnen (je Funktion einmal angemeldet).
        _bausteine = FunktionsBausteinWiring.Knoten(instanzen ?? GraphInstanzen.Keine)
            .Where(x => FunktionsBausteinWiring.BindungVon(x.Baustein, x.Konfig, x.Instanz.Ort(x.Knoten.Name)).Laufort == FunktionsLaufort.Hand)
            .Select(x => x.Baustein).DistinctBy(b => b.Name).ToList();
        _werkstatt = werkstatt;
    }

    /// <summary>Die Funktionen mit Laufort Hand.</summary>
    public IReadOnlyList<FunktionsBaustein> Bausteine => _bausteine;

    /// <summary>Die wartenden Eingänge, älteste zuerst.</summary>
    public IReadOnlyList<HandEingang> OffeneEingaenge => _eingaenge.Values.OrderBy(a => a.Seit).ToList();

    /// <summary>
    /// Die Antwort des Menschen auf einen Eingang: die Ausgaben der Funktion (nur Typen ihrer Signatur) oder ein Fehler. Die erste Antwort gewinnt — eine
    /// spätere (oder eine auf einen Eingang, dessen Lauf schon endete) ist <see cref="Werkstattausgang.Unbekannt"/>.
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
        _logger?.LogInformation("[Funktion] {Funktion}: Eingang {Stream}:{Version} wartet auf eine Antwort von Hand", a.Funktion.Name, a.Herkunft.Stream,
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
