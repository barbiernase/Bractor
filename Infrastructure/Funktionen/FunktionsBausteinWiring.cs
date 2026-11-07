using Abstractions;
using Infrastructure.Komposition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure.Funktionen;

/// <summary>
/// Wie ein Knoten läuft (aus seiner Konfig in der Instanz, sonst der Standard): Wiederholungen, Parallel, Warteschlange und Vorwärmen (0 = der
/// Standard, <see cref="FunktionsOrtKonfig"/>).
/// </summary>
public sealed record BausteinBindung(string Funktion, int Wiederholungen = 0, int Parallel = 0, int Warteschlange = 0, bool Vorwaermen = false)
{
    public static BausteinBindung Aus(string funktion, KnotenKonfig? k) =>
        k is null ? new(funktion) : new(funktion, k.Wiederholungen, k.Parallel, k.Warteschlange, k.Vorwaermen);
}

/// <summary>
/// Die Verdrahtung der Funktionen, für jede Anwendung gleich — aus der generierten Tabelle <c>Anwendung.Funktionen</c> (Bausteine, Graphen), ohne
/// Domänen-Typ im Framework: die Instanzen (ohne Umgebung je Graph eine, <see cref="GraphInstanzen.JeGraphEine"/>), ihre Köpfe, die Laufzeit vor dem
/// Aggregat (<see cref="FunktionsLaufzeitWiring"/>: Wurzel, Graph-Actors, Parents, Kinder).
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

    /// <summary>Die Bindung eines Knotens: aus seiner Konfig, sonst der Standard.</summary>
    public static BausteinBindung BindungVon(FunktionsBaustein b, KnotenKonfig? k, string ort = "") => BausteinBindung.Aus(b.Name, k);

    /// <summary>Der Baustein eines Knotens: eine generierte Funktion.</summary>
    public static FunktionsBaustein? Baustein(string name) => Anwendung.Funktionen.Bausteine.FirstOrDefault(x => x.Name == name);

    /// <summary>Jeder Knoten jeder Instanz mit seiner Funktion und Konfig.</summary>
    public static IEnumerable<(GraphInstanz Instanz, KnotenPlan Knoten, FunktionsBaustein Baustein, KnotenKonfig Konfig)> Knoten(GraphInstanzen instanzen) =>
        from i in instanzen.Alle
        from k in i.Graph.Knoten
        let b = Baustein(k.Funktion)
        where b is not null
        select (i, k, b, i.Konfig(k.Name));
}
