using Abstractions;
using Infrastructure.Komposition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

/// <summary>
/// Wo und wie eine Funktion läuft (Umgebung <c>Funktionen:&lt;Name&gt;</c>, sonst der Standard): Laufort, die Umsetzung im Host (bei mehreren nach
/// Name), Wiederholungen, Parallel, Warteschlange und Vorwärmen je Knoten (0 = der Standard, <see cref="FunktionsOrtKonfig"/>).
/// </summary>
public sealed record BausteinBindung(string Funktion, FunktionsLaufort Laufort = FunktionsLaufort.Host, string? Umsetzung = null,
    int Wiederholungen = 0, int Parallel = 0, int Warteschlange = 0, bool Vorwaermen = false);

/// <summary>
/// Die Verdrahtung der Funktionen, für jede Anwendung gleich — aus der generierten Tabelle <c>Anwendung.Funktionen.Bausteine</c>, ohne Domänen-Typ im
/// Framework: die Laufzeit vor dem Aggregat (<see cref="FunktionsLaufzeitWiring"/>: Wurzel, Parents, Kinder, Werkstatt) und das Hinweis-Netz
/// (<see cref="HinweisNetz"/>), wenn irgendeine Funktion einen Hinweis hört.
/// </summary>
public static class FunktionsBausteinWiring
{
    public static IServiceCollection AddFunktionsBausteine(this IServiceCollection services)
    {
        FunktionsLaufzeitWiring.AddFunktionsLaufzeit(services);
        if (Anwendung.Funktionen.Verkettung.Keys.Any(t => typeof(ITransientEvent).IsAssignableFrom(t)))
            services.TryAddSingleton(sp => new HinweisNetz(sp.GetRequiredService<Verkettung>(), sp.GetRequiredService<IFunktionsWege>,
                sp.GetService<ILoggerFactory>()?.CreateLogger("Hinweisnetz")));
        return services;
    }

    /// <summary>
    /// Die Bindung einer Funktion ohne Prüfung: die letzte registrierte, sonst der Standard — eine Klasse läuft im Host, eine Schnittstelle mit genau
    /// einer Umsetzung ebenso (wie ein Dienst mit einer Implementierung); eine Schnittstelle ohne Umsetzung, die ein Client trägt (er verkörpert
    /// einen Akteur mit <c>IDarf&lt;F&gt;</c>), läuft draußen (Laufort Extern); sonst keine.
    /// </summary>
    public static BausteinBindung? Gebunden(FunktionsBaustein b, IEnumerable<BausteinBindung> bindungen) =>
        bindungen.LastOrDefault(x => x.Funktion == b.Name)
        ?? (!b.Draussen || b.Umsetzungen.Count == 1 ? new BausteinBindung(b.Name)
            : b.Umsetzungen.Count == 0 && Getragen(b) ? new BausteinBindung(b.Name, FunktionsLaufort.Extern)
            : null);

    /// <summary>Trägt irgendein Client die Funktion (verkörpert er einen Akteur, der sie rechnen darf: <c>IDarf&lt;F&gt;</c>)?</summary>
    public static bool Getragen(FunktionsBaustein b) => Anwendung.Akteure.ClientVertraege.Values.Any(c => c.Traegt.Contains(b.Typ));

    /// <summary>
    /// Die geprüfte Bindung einer Funktion (<see cref="Gebunden"/>). Eine Funktion draußen ohne Bindung, ohne eindeutige Umsetzung und ohne Träger
    /// ist ein Boot-Fehler (R17: sie braucht einen Träger oder eine Bindung), ebenso eine Funktion mit Lese-Fähigkeit, die nicht im Host läuft.
    /// </summary>
    public static BausteinBindung BindungVon(FunktionsBaustein b, IEnumerable<BausteinBindung> bindungen)
    {
        var bindung = Gebunden(b, bindungen)
            ?? throw new Infrastructure.Umgebung.BootFehler($"Die Funktion '{b.Name}' ist eine Schnittstelle (läuft draußen), hat keine Bindung und keinen "
                + $"Träger — in der Umgebung fehlt Funktionen:{b.Name} (Laufort Hand oder Extern, oder Host mit Implementierung), oder ein Client-Vertrag "
                + $"trägt sie nicht (kein verkörperter Akteur mit IDarf<{b.Name}>).", "funktion:" + b.Name);
        if (bindung.Laufort != FunktionsLaufort.Host && b.Liest)
            throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: eine Funktion mit Lese-Fähigkeit läuft nur im Host (Laufort Host).", "funktion:" + b.Name);
        if (bindung.Laufort == FunktionsLaufort.Extern && !b.Draussen)
            throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: Laufort Extern braucht eine Schnittstelle — eine Klasse rechnet im Host.", "funktion:" + b.Name);
        if (bindung.Laufort == FunktionsLaufort.Host)
        {
            var passend = b.Umsetzungen.Where(u => bindung.Umsetzung is null || u.Name == bindung.Umsetzung).ToList();
            if (passend.Count != 1)
                throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: Laufort Host braucht genau eine Umsetzung"
                    + (bindung.Umsetzung is null ? "" : $" namens '{bindung.Umsetzung}'") + " — gefunden: "
                    + string.Join(", ", b.Umsetzungen.Select(u => u.Name).DefaultIfEmpty("keine")) + ".", "funktion:" + b.Name);
        }
        return bindung;
    }

    /// <summary>Die Umsetzung im Host nach der Bindung (gebaut je Aktivierung über die generierte Fabrik, <see cref="FunktionsAktivierung"/>).</summary>
    public static FunktionsUmsetzung Umsetzung(FunktionsBaustein b, BausteinBindung bindung) =>
        b.Umsetzungen.Single(u => bindung.Umsetzung is null || u.Name == bindung.Umsetzung);
}
