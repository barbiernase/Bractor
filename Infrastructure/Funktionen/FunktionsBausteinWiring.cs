using Abstractions;
using Infrastructure.Komposition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

/// <summary>
/// Wo und wie ein Knoten läuft (aus seiner Konfig in der Instanz, sonst der Standard): Laufort, die Umsetzung im Host (bei mehreren nach Name),
/// Wiederholungen, Parallel, Warteschlange und Vorwärmen (0 = der Standard, <see cref="FunktionsOrtKonfig"/>).
/// </summary>
public sealed record BausteinBindung(string Funktion, FunktionsLaufort Laufort = FunktionsLaufort.Host, string? Umsetzung = null,
    int Wiederholungen = 0, int Parallel = 0, int Warteschlange = 0, bool Vorwaermen = false)
{
    public static BausteinBindung Aus(string funktion, KnotenKonfig k) =>
        new(funktion, k.Laufort, k.Implementierung, k.Wiederholungen, k.Parallel, k.Warteschlange, k.Vorwaermen);
}

/// <summary>
/// Die Verdrahtung der Funktionen, für jede Anwendung gleich — aus den generierten Tabellen <c>Anwendung.Funktionen</c> (Bausteine, Graphen), ohne
/// Domänen-Typ im Framework: die Instanzen (ohne Umgebung je Graph eine, <see cref="GraphInstanzen.JeGraphEine"/>), ihre Köpfe, die Laufzeit vor dem
/// Aggregat (<see cref="FunktionsLaufzeitWiring"/>: Wurzel, Graph-Actors, Parents, Kinder, Werkstatt).
/// </summary>
public static class FunktionsBausteinWiring
{
    public static IServiceCollection AddFunktionsBausteine(this IServiceCollection services)
    {
        services.TryAddSingleton(_ => GraphInstanzen.JeGraphEine(Anwendung.Funktionen.Graphen));
        services.TryAddSingleton(sp => sp.GetRequiredService<GraphInstanzen>().Koepfe());
        FunktionsLaufzeitWiring.AddFunktionsLaufzeit(services);
        return services;
    }

    /// <summary>
    /// Die Bindung eines Knotens ohne Prüfung: aus seiner Konfig, sonst der Standard — eine Klasse läuft im Host, eine Schnittstelle mit genau einer
    /// Umsetzung ebenso (wie ein Dienst mit einer Implementierung); eine Schnittstelle ohne Umsetzung, die ein Client trägt (er verkörpert einen Akteur
    /// mit <c>IDarf&lt;F&gt;</c>), läuft draußen (Laufort Extern); sonst keine.
    /// </summary>
    public static BausteinBindung? Gebunden(FunktionsBaustein b, KnotenKonfig? k) =>
        k is not null && (k.Laufort != FunktionsLaufort.Host || k.Implementierung is not null || k.Parallel + k.Warteschlange + k.Wiederholungen > 0 || k.Vorwaermen)
            ? BausteinBindung.Aus(b.Name, k)
            : !b.Draussen || b.Umsetzungen.Count == 1 ? BausteinBindung.Aus(b.Name, k ?? new KnotenKonfig())
            : b.Umsetzungen.Count == 0 && Getragen(b) ? BausteinBindung.Aus(b.Name, k ?? new KnotenKonfig()) with { Laufort = FunktionsLaufort.Extern }
            : null;

    /// <summary>Trägt irgendein Client die Funktion (verkörpert er einen Akteur, der sie rechnen darf: <c>IDarf&lt;F&gt;</c>)?</summary>
    public static bool Getragen(FunktionsBaustein b) => Anwendung.Akteure.ClientVertraege.Values.Any(c => c.Traegt.Contains(b.Typ));

    /// <summary>
    /// Die geprüfte Bindung eines Knotens (<see cref="Gebunden"/>). Eine Funktion draußen ohne Bindung, ohne eindeutige Umsetzung und ohne Träger
    /// ist ein Boot-Fehler (R17: sie braucht einen Träger oder eine Bindung), ebenso eine Funktion mit Lese-Fähigkeit, die nicht im Host läuft.
    /// </summary>
    public static BausteinBindung BindungVon(FunktionsBaustein b, KnotenKonfig? k, string ort = "")
    {
        var wo = ort.Length > 0 ? ort : b.Name;
        var bindung = Gebunden(b, k)
            ?? throw new Infrastructure.Umgebung.BootFehler($"{wo}: die Funktion '{b.Name}' ist eine Schnittstelle (läuft draußen), hat keine Bindung und keinen "
                + $"Träger — in der Instanz fehlt am Knoten der Laufort (Hand oder Extern, oder Host mit Implementierung), oder ein Client-Vertrag "
                + $"trägt sie nicht (kein verkörperter Akteur mit IDarf<{b.Name}>).", "funktion:" + b.Name);
        if (bindung.Laufort != FunktionsLaufort.Host && b.Liest)
            throw new Infrastructure.Umgebung.BootFehler($"{wo}: eine Funktion mit Lese-Fähigkeit läuft nur im Host (Laufort Host).", "funktion:" + b.Name);
        if (bindung.Laufort == FunktionsLaufort.Extern && !b.Draussen)
            throw new Infrastructure.Umgebung.BootFehler($"{wo}: Laufort Extern braucht eine Schnittstelle — eine Klasse rechnet im Host.", "funktion:" + b.Name);
        if (bindung.Laufort == FunktionsLaufort.Host)
        {
            var passend = b.Umsetzungen.Where(u => bindung.Umsetzung is null || u.Name == bindung.Umsetzung).ToList();
            if (passend.Count != 1)
                throw new Infrastructure.Umgebung.BootFehler($"{wo}: Laufort Host braucht genau eine Umsetzung von {b.Name}"
                    + (bindung.Umsetzung is null ? "" : $" namens '{bindung.Umsetzung}'") + " — gefunden: "
                    + string.Join(", ", b.Umsetzungen.Select(u => u.Name).DefaultIfEmpty("keine")) + ".", "funktion:" + b.Name);
        }
        return bindung;
    }

    /// <summary>Die Umsetzung im Host nach der Bindung (gebaut je Aktivierung über die generierte Fabrik, <see cref="FunktionsAktivierung"/>).</summary>
    public static FunktionsUmsetzung Umsetzung(FunktionsBaustein b, BausteinBindung bindung) =>
        b.Umsetzungen.Single(u => bindung.Umsetzung is null || u.Name == bindung.Umsetzung);

    /// <summary>Jeder Knoten jeder Instanz mit seiner Funktion und Konfig.</summary>
    public static IEnumerable<(GraphInstanz Instanz, KnotenPlan Knoten, FunktionsBaustein Baustein, KnotenKonfig Konfig)> Knoten(GraphInstanzen instanzen) =>
        from i in instanzen.Alle
        from k in i.Graph.Knoten
        let b = Anwendung.Funktionen.Bausteine.FirstOrDefault(x => x.Name == k.Funktion)
        where b is not null
        select (i, k, b, i.Konfig(k.Name));
}
