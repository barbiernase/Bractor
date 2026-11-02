using Abstractions;
using Infrastructure.Komposition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure.Funktionen;

/// <summary>
/// Wie ein Knoten läuft (aus seiner Konfig in der Instanz, sonst der Standard): Wiederholungen, Parallel, Warteschlange und Vorwärmen (0 = der
/// Standard, <see cref="FunktionsOrtKonfig"/>). Wo er rechnet, sagt sein Baustein: eine Funktion im Host, die Arbeit eines Akteurs draußen.
/// </summary>
public sealed record BausteinBindung(string Funktion, int Wiederholungen = 0, int Parallel = 0, int Warteschlange = 0, bool Vorwaermen = false)
{
    public static BausteinBindung Aus(string funktion, KnotenKonfig? k) =>
        k is null ? new(funktion) : new(funktion, k.Wiederholungen, k.Parallel, k.Warteschlange, k.Vorwaermen);
}

/// <summary>
/// Die Verdrahtung der Funktionen, für jede Anwendung gleich — aus den generierten Tabellen <c>Anwendung.Funktionen</c> (Bausteine, Graphen) und
/// <c>Anwendung.Akteure</c> (ihre Arbeit), ohne Domänen-Typ im Framework: die Instanzen (ohne Umgebung je Graph eine,
/// <see cref="GraphInstanzen.JeGraphEine"/>, dazu je Akteur mit Arbeit eine, <see cref="Arbeit"/>), ihre Köpfe, die Laufzeit vor dem Aggregat
/// (<see cref="FunktionsLaufzeitWiring"/>: Wurzel, Graph-Actors, Parents, Kinder, Werkstatt).
/// </summary>
public static class FunktionsBausteinWiring
{
    public static IServiceCollection AddFunktionsBausteine(this IServiceCollection services)
    {
        services.TryAddSingleton(_ => GraphInstanzen.JeGraphEine(Anwendung.Funktionen.Graphen, akteure: Anwendung.Akteure.Rechte));
        services.TryAddSingleton(sp => sp.GetRequiredService<GraphInstanzen>().Koepfe());
        FunktionsLaufzeitWiring.AddFunktionsLaufzeit(services);
        return services;
    }

    /// <summary>Die Bindung eines Knotens: aus seiner Konfig, sonst der Standard.</summary>
    public static BausteinBindung BindungVon(FunktionsBaustein b, KnotenKonfig? k, string ort = "") => BausteinBindung.Aus(b.Name, k);

    /// <summary>Der Baustein eines Knotens: eine generierte Funktion oder die Arbeit eines Akteurs (gleich benannt wie er).</summary>
    public static FunktionsBaustein? Baustein(string name) =>
        Anwendung.Funktionen.Bausteine.FirstOrDefault(x => x.Name == name)
        ?? (Anwendung.Akteure.Rechte.TryGetValue(name, out var a) && a.Arbeit.Count > 0 ? Arbeit.Baustein(a) : null);

    /// <summary>Jeder Knoten jeder Instanz mit seiner Funktion und Konfig.</summary>
    public static IEnumerable<(GraphInstanz Instanz, KnotenPlan Knoten, FunktionsBaustein Baustein, KnotenKonfig Konfig)> Knoten(GraphInstanzen instanzen) =>
        from i in instanzen.Alle
        from k in i.Graph.Knoten
        let b = Baustein(k.Funktion)
        where b is not null
        select (i, k, b, i.Konfig(k.Name));
}
