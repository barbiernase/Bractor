using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

public static class FunktionsLaufortExtensions
{
    /// <summary>
    /// Bindet die Funktionen nach dem Abschnitt <c>Funktionen</c> der Umgebung (docs/06 §6.5): je Funktion mit Eingängen der Laufort (<c>Host</c> mit
    /// Umsetzung, <c>Extern</c>, <c>Hand</c>), Slots und Wiederholungen. Gelesen und geprüft (Umsetzung eindeutig, eine Funktion mit Lese-Fähigkeit nur im
    /// Host, eine getragene Schnittstelle ohne Umsetzung draußen) wird die Bindung vom Funktions-Actor (<see cref="FunktionsBausteinWiring"/>). Eine
    /// gerufene Funktion nennt hier nur ihre Implementierung (gebunden von <see cref="GerufeneExtensions.BindeGerufene(IServiceCollection, UmgebungKonfig, Microsoft.Extensions.Configuration.IConfiguration)"/>);
    /// ein Name, den es nicht gibt, ist ein Boot-Fehler.
    /// </summary>
    public static IServiceCollection BindeFunktionen(this IServiceCollection services, IReadOnlyDictionary<string, FunktionsKonfig> konfig)
    {
        var bausteine = Anwendung.Funktionen.Bausteine.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        var gerufen = Anwendung.Gerufene.Namen;
        foreach (var (name, k) in konfig)
        {
            if (gerufen.ContainsKey(name))
            {
                // Gerufen läuft im Prozess ihres Halters: kein eigener Laufort, kein Worker.
                if (k.Laufort != FunktionsLaufort.Host || k.Worker is not null)
                    throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{name} ist eine gerufene Funktion (ohne Auf) — sie läuft im Prozess ihres "
                        + "Halters; hier steht nur ihre Implementierung (kein Laufort oder Worker).", "funktion:" + name);
                continue;
            }
            if (!bausteine.Contains(name))
                throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{name} — keine Funktion dieses Namens (bekannt: "
                    + string.Join(", ", bausteine.Concat(gerufen.Keys).OrderBy(n => n, StringComparer.Ordinal)) + ").", "host:Funktionen");
            services.AddSingleton(new BausteinBindung(name, k.Laufort, k.Implementierung, Math.Max(0, k.Wiederholungen), Math.Max(0, k.Parallel),
                Math.Max(0, k.Warteschlange), k.Vorwaermen));
            if (k.Laufort == FunktionsLaufort.Hand) services.AddHandAusfuehrer();
        }
        return services;
    }
}
