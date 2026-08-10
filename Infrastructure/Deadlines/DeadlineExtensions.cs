using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Deadlines;

/// <summary>
/// Opt-in-Verdrahtung des Deadline-Schedulers. <see cref="IDbClock"/> und <see cref="IFristplan"/> registriert
/// bereits der Framework-Kern (durabler Fristplan + DB-Uhr sind harmlos ohne Scheduler); diese Erweiterung
/// startet den aktiv feuernden <see cref="FristScheduler"/> und legt fest, WELCHER Command eine fällige Frist
/// zustellt (die Domänen-Wahl trifft der Host).
/// </summary>
public static class DeadlineExtensions
{
    /// <param name="baueCommand">Baut aus einer fälligen Frist den zuzustellenden Command (Ziel-Aggregat + Kontext).</param>
    public static IServiceCollection AddDeadlines(
        this IServiceCollection services,
        Func<Frist, ICommand> baueCommand)
    {
        services.AddHostedService(sp => new FristScheduler(
            sp.GetRequiredService<IDbClock>(),
            sp.GetRequiredService<IFristplan>(),
            sp.GetRequiredService<ActorSystem>(),
            baueCommand,
            sp.GetService<ILogger<FristScheduler>>()));
        return services;
    }
}
