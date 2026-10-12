using Abstractions;
using Infrastructure.Projections;   // WakeAck
using Infrastructure.Prozess;       // ProzessManagerActor, ProzessWake
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

/// <summary>
/// Bindung der Katalog-Funktionen im Host: WELCHE Implementierung eine Funktion auf diesem Knoten ausführt und wie viele
/// Aufträge gleichzeitig laufen. Der Laufort ist eine Bindung, keine Eigenschaft der Funktion — die Prozess-Regel
/// (<c>Rufe&lt;TFunktion&gt;</c>) bleibt gleich, egal wo und wie schnell gerechnet wird.
/// </summary>
public static class FunktionsExtensions
{
    /// <summary>
    /// Bindet <typeparamref name="TFunktion"/> an <typeparamref name="TImpl"/> (Singleton: der Ausführer darf warme
    /// Ressourcen halten — muss dafür aber bei <paramref name="slots"/> &gt; 1 thread-sicher sein).
    /// </summary>
    /// <param name="slots">Wie viele Aufträge dieser Funktion auf diesem Knoten gleichzeitig laufen.</param>
    /// <param name="wiederholungen">Wie oft eine werfende Ausführung wiederholt wird, bevor der Schritt scheitert.</param>
    public static IServiceCollection AddFunktion<TFunktion, TImpl>(this IServiceCollection services, int slots = 1, int wiederholungen = 0)
        where TFunktion : class, IFunktion
        where TImpl : class, TFunktion
    {
        services.AddSingleton<TFunktion, TImpl>();
        services.AddSingleton(new FunktionsBindung(typeof(TFunktion), slots, wiederholungen));
        return services;
    }

    /// <summary>
    /// Bindet <typeparamref name="TFunktion"/> als EXTERNE Funktion: sie rechnet außerhalb des Hosts (z. B. ein Python-Worker,
    /// der sich am gRPC-Handshake als Anbieter meldet und Aufträge beim Vermittler abholt). Der Host prüft nur, dass sie
    /// gebunden ist; Laufort und Slots bestimmt der Worker.
    /// </summary>
    public static IServiceCollection AddExterneFunktion<TFunktion>(this IServiceCollection services)
        where TFunktion : class, IFunktion
    {
        services.AddSingleton(new FunktionsBindung(typeof(TFunktion), Slots: 0, Extern: true));
        return services;
    }

    /// <summary>Der Ausführer des Knotens — live an den generierten Dispatch und den Prozess-Manager im Cluster gebunden.</summary>
    internal static FunktionsAusfuehrer BaueAusfuehrer(IServiceProvider sp)
    {
        var bindungen = sp.GetServices<FunktionsBindung>().GroupBy(b => b.Funktion).ToDictionary(g => g.Key, g => g.Last());
        return new FunktionsAusfuehrer(
            sp.GetRequiredService<IEventStoreRepository>(),
            (auftrag, x) => GeneratedFunktionen.RufeAsync(sp, auftrag, x),
            // Der Cluster wird erst beim Wecken aufgelöst (er ist bei der Registrierung noch nicht fertig — die (A)-Lektion).
            (korrelation, ct) => WeckeProzessAsync(sp.GetRequiredService<ActorSystem>(), korrelation, ct),
            auftrag => GeneratedFunktionen.AuftragZuFunktion.TryGetValue(auftrag.GetType(), out var f) &&
                       bindungen.TryGetValue(f, out var b) ? b : null);
    }

    /// <summary>Weckt den Prozess-Manager der Korrelation (bounded; verloren → der §3-Backstop weckt ihn).</summary>
    private static async Task WeckeProzessAsync(ActorSystem system, Guid korrelation, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), ProzessManagerActor.KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await system.Cluster().RequestAsync<WakeAck>(identity, new ProzessWake(Guid.Empty, 0, null), cts.Token); }
        catch (OperationCanceledException) { /* Backstop heilt */ }
    }

    /// <summary>
    /// Boot-Guard: jede Funktion, die ein Prozess ruft, muss auf diesem Knoten gebunden sein — sonst bliebe ihr Auftrag
    /// still unbeantwortet (bzw. scheiterte erst zur Laufzeit). Lieber laut am Start.
    /// </summary>
    public static void PrüfeBindungen(IReadOnlyDictionary<string, ProzessRegeln> registry, IEnumerable<FunktionsBindung> bindungen)
    {
        var liste = bindungen.ToList();
        var gebunden = new HashSet<Type>(liste.Select(b => b.Funktion));
        // Eine Funktion mit Lese-Fähigkeiten braucht die Stores des Hosts — draußen (Python, GPU-Rechner) gibt es sie nicht.
        foreach (var b in liste.Where(b => b.Extern && GeneratedFunktionen.Faehigkeiten.ContainsKey(b.Funktion)))
            throw new Infrastructure.Umgebung.BootFehler(
                $"Die Funktion '{b.Funktion.Name}' liest Fähigkeiten ({string.Join(", ", GeneratedFunktionen.Faehigkeiten[b.Funktion].Select(t => t.Name))}) " +
                "und läuft deshalb nur im Host (Funktionen:" + b.Funktion.Name + ":Laufort = Host).", "funktion:" + b.Funktion.Name);
        foreach (var (name, regeln) in registry)
            foreach (var f in regeln.Regeln.SelectMany(r => r.GerufeneFunktionen).Distinct())
                if (!gebunden.Contains(f))
                    throw new Infrastructure.Umgebung.BootFehler(
                        $"Der Ablauf '{name}' ruft die Funktion '{f.Name}', aber sie ist nicht gebunden — in der Umgebung fehlt Funktionen:{f.Name} (Laufort).",
                        "funktion:" + f.Name);
    }

    /// <summary>
    /// Was ein Aufruf-Ziel erzeugt — für den Azyklizitäts-Guard: eine Funktion ihre Ergebnis-Events (Signatur), ein
    /// Command die Events seines Decide (OneOf).
    /// </summary>
    public static IEnumerable<Type> Produziert(Type ziel)
        => GeneratedFunktionen.Ergebnisse.TryGetValue(ziel, out var ergebnisse)
            ? ergebnisse
            : Infrastructure.Mapping.GeneratedCommandRouting.Produziert(ziel);
}

/// <summary>Eine Implementierung einer Katalog-Funktion (aus <c>GeneratedFunktionen.Implementierungen</c>): Name + generische Bindung.</summary>
public sealed record FunktionsImplementierung(Type Funktion, string Name, Action<IServiceCollection, int, int> Binde);

public static class FunktionsLaufortExtensions
{
    /// <summary>
    /// Bindet die Funktionen nach dem Abschnitt <c>Funktionen</c> der Umgebung (docs/konzept-werkbank.md §4.2): je Funktion der Laufort
    /// (<c>Host</c> mit Implementierung, <c>Extern</c>, <c>Hand</c>). Geprüft gegen die generierte Tabelle: Funktion bekannt, Implementierung eindeutig,
    /// eine Funktion mit Lese-Fähigkeit läuft nie extern. Eine gerufene, aber nicht gebundene Funktion meldet der Boot-Guard.
    /// </summary>
    public static IServiceCollection BindeFunktionen(this IServiceCollection services, IReadOnlyDictionary<string, Abstractions.FunktionsKonfig> konfig)
        => services.BindeFunktionen(konfig, GeneratedFunktionen.Namen, GeneratedFunktionen.Implementierungen, GeneratedFunktionen.Extern,
            GeneratedFunktionen.Faehigkeiten);

    public static IServiceCollection BindeFunktionen(this IServiceCollection services, IReadOnlyDictionary<string, Abstractions.FunktionsKonfig> konfig,
        IReadOnlyDictionary<string, Type> namen, IReadOnlyList<FunktionsImplementierung> implementierungen,
        IReadOnlyDictionary<Type, Action<IServiceCollection>> externBinden, IReadOnlyDictionary<Type, Type[]> faehigkeiten)
    {
        foreach (var (name, k) in konfig)
        {
            if (!namen.TryGetValue(name, out var funktion))
                throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{name} — keine Katalog-Funktion dieses Namens (bekannt: {string.Join(", ", namen.Keys.OrderBy(n => n))}).", "host:Funktionen");
            switch (k.Laufort)
            {
                case Abstractions.FunktionsLaufort.Host:
                    var passend = implementierungen.Where(i => i.Funktion == funktion && (k.Implementierung == null || i.Name == k.Implementierung)).ToList();
                    if (passend.Count != 1)
                        throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{name}: Laufort Host braucht genau eine Implementierung"
                            + (k.Implementierung == null ? "" : $" namens '{k.Implementierung}'") + " — gefunden: "
                            + string.Join(", ", implementierungen.Where(i => i.Funktion == funktion).Select(i => i.Name).DefaultIfEmpty("keine")) + ".", "funktion:" + name);
                    passend[0].Binde(services, Math.Max(1, k.Slots), Math.Max(0, k.Wiederholungen));
                    break;
                case Abstractions.FunktionsLaufort.Extern:
                    if (faehigkeiten.ContainsKey(funktion))
                        throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{name}: eine Funktion mit Lese-Fähigkeit läuft nur im Host (Laufort Host).", "funktion:" + name);
                    externBinden[funktion](services);
                    break;
                case Abstractions.FunktionsLaufort.Hand:
                    // Ein Mensch antwortet (Werkbank): die Aufträge holt der Hand-Ausführer und hält sie, bis die Antwort kommt.
                    services.AddSingleton(new FunktionsBindung(funktion, Slots: 0, Extern: true, Hand: true));
                    services.AddHandAusfuehrer();
                    break;
                default:
                    throw new NotSupportedException($"Funktionen:{name}: Laufort {k.Laufort} gibt es nicht.");
            }
        }
        return services;
    }
}
