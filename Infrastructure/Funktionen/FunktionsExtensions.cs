using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

public static class FunktionsLaufortExtensions
{
    /// <summary>
    /// Bindet die Funktionen nach dem Abschnitt <c>Funktionen</c> der Umgebung (docs/06 §6.5): je Funktion der Laufort (<c>Host</c> mit
    /// Umsetzung, <c>Extern</c>, <c>Hand</c>), Slots und Wiederholungen. Gelesen und geprüft (Umsetzung eindeutig, eine Funktion mit Lese-Fähigkeit nur im
    /// Host, eine getragene Schnittstelle ohne Umsetzung draußen) wird die Bindung vom Funktions-Actor (<see cref="FunktionsBausteinWiring"/>); ein Name,
    /// den es nicht gibt, ist ein Boot-Fehler.
    /// </summary>
    public static IServiceCollection BindeFunktionen(this IServiceCollection services, IReadOnlyDictionary<string, FunktionsKonfig> konfig)
    {
        var bausteine = Anwendung.Funktionen.Bausteine.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (name, k) in konfig)
        {
            if (!bausteine.Contains(name))
                throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{name} — keine Funktion dieses Namens (bekannt: "
                    + string.Join(", ", bausteine.OrderBy(n => n, StringComparer.Ordinal)) + ").", "host:Funktionen");
            services.AddSingleton(new BausteinBindung(name, k.Laufort, k.Implementierung, Math.Max(1, k.Slots), Math.Max(0, k.Wiederholungen),
                k.Zustand is null ? null : ZustandsBindung.Aus(k.Zustand)));
            if (k.Laufort == FunktionsLaufort.Hand) services.AddHandAusfuehrer();
        }
        return services;
    }
}
