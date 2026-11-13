using Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

public static class GraphInstanzExtensions
{
    /// <summary>
    /// Bindet die Instanzen der Graphen aus der Umgebung (docs/04 §4.4, <c>umgebungen/&lt;name&gt;/graphen/*.json</c>), geprüft gegen die Graphen
    /// (<see cref="GraphInstanzen.Aus"/>). Ersetzt die Instanzen des Rahmens (ohne Umgebung je Graph eine). Danach bindet <see cref="TechnikExtensions.BindeTechnik(IServiceCollection, GraphInstanzen)"/>, was die Knoten im Host brauchen.
    /// </summary>
    public static GraphInstanzen BindeGraphen(this IServiceCollection services, UmgebungKonfig u)
    {
        var instanzen = GraphInstanzen.Aus(u.Graphen, Anwendung.Funktionen.Graphen);
        services.Replace(ServiceDescriptor.Singleton(instanzen));
        Console.WriteLine($"[CQRS] Instanzen: {string.Join(", ", instanzen.Alle.Select(i => i.Name + " (" + i.Graph.Name + ")").DefaultIfEmpty("keine"))}");
        return instanzen;
    }

    /// <summary>
    /// Prüft die App-Instanzen der Umgebung (<c>umgebungen/&lt;name&gt;/apps/*.json</c>) gegen Apps, Quellarten, Akteure, Tokens und die Übersetzungen der
    /// Akteur-Seite (<see cref="Infrastructure.Quellen.Eingaenge.Aus"/>) — ein Boot-Fehler, wenn eine nicht stimmt. Laufen tun ihre Quellen im
    /// Rechnerdienst ihres Rechners (Host.Rechner), nicht hier.
    /// </summary>
    /// <param name="tokens">Token → Akteure (Abschnitt <c>Identitaeten:Tokens</c>); null = ohne Tor.</param>
    public static Infrastructure.Quellen.Eingaenge PruefeApps(this IServiceCollection services, UmgebungKonfig u,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? tokens = null)
    {
        var eingaenge = Infrastructure.Quellen.Eingaenge.Aus(u.Apps, Anwendung.Akteure.AppVertraege, Anwendung.Funktionen.Bausteine,
            new Uebersetzungen(Anwendung.Funktionen.Uebersetzungen), Anwendung.Akteure.Rechte, tokens);
        if (eingaenge.Alle.Count > 0)
            Console.WriteLine($"[CQRS] Quellen (im Rechnerdienst ihres Rechners): {string.Join(", ", eingaenge.Alle.Select(e => e.Ort + " auf " + e.Rechner + " (" + e.Art.Name + " für " + e.Akteur + ")"))}");
        return eingaenge;
    }
}
