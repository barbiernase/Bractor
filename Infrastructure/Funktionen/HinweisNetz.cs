using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // Konsum
using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// HINWEIS-NETZ (docs/04 §4.4, docs/10 E7, E8, A6, A7): Nachrichten, die nicht zur Fachdomäne gehören (Merkmale, Messwerte,
// Ablehnungen), gehen von Baustein zu Funktions-Actor IM SPEICHER — nie in den Log, kein Broker, keine Abos. Hart verdrahtet: aus den
// Signaturen kennt der Generator je Hinweis-Typ die Funktionen, die ihn hören (Hinweisplan); der Rahmen schlägt dort nach und schickt den
// Hinweis per Bote.Melde DIREKT an jede Adresse (die Partition des Herkunfts-Streams bzw. die eine Adresse). Höchstens einmal: was im Flug
// ist, wenn eine Aktivierung umzieht oder abstürzt, geht verloren (Invariante 6). Fachlich Wichtiges verlässt das Netz als Command an ein
// Aggregat — mit einer Command-Id aus der Kennung des Hinweises, die Inbox des Ziels verwirft ein Wiederholen.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Hinweis unterwegs zu einem Funktions-Actor (verlierbar, <see cref="Bote.Melde"/>): die Kennung, die der Absender vergibt (A7), der Hinweis
/// selbst und seine Herkunft — der Stream, aus dessen Eingang er hervorging (er bestimmt die Adresse, A6), Version, Zeit, Korrelation, Akteur, Ursache.
/// </summary>
public sealed record HinweisZustellen(Guid Kennung, IEvent Hinweis, Guid Stream, int Version, DateTimeOffset Zeit, string? Korrelation, string? Akteur,
    string? Ursache = null) : IWireMessage;

/// <summary>
/// Die Routing-Tabelle des Hinweis-Netzes (zur Build-Zeit generiert: <c>GeneratedFunktionsBausteine.Hinweisplan</c>) und der direkte Versand. Eine
/// Instanz je Knoten; der Bote wird erst beim ersten Hinweis gebaut (der Cluster steht dann).
/// </summary>
public sealed class HinweisNetz
{
    private readonly IReadOnlyDictionary<Type, FunktionsBaustein[]> _hoerer;
    private readonly Konsum _konsum;
    private readonly Lazy<Bote> _bote;
    private readonly ILogger? _logger;

    /// <param name="hinweisplan">Hinweis-Typ → Cluster-Kinds der Funktions-Actors, die ihn hören (generiert).</param>
    /// <exception cref="Infrastructure.Umgebung.BootFehler">Eine Adresse der Tabelle gehört zu keiner Funktion (R20: jede Adresse existiert).</exception>
    public HinweisNetz(IReadOnlyList<FunktionsBaustein> bausteine, IReadOnlyDictionary<Type, string[]> hinweisplan, Konsum konsum, Func<Bote> bote,
        ILogger? logger = null)
    {
        var jeKind = bausteine.ToDictionary(b => b.KindName, StringComparer.Ordinal);
        _hoerer = hinweisplan.ToDictionary(e => e.Key, e => e.Value.Select(kind => jeKind.TryGetValue(kind, out var b) ? b
            : throw new Infrastructure.Umgebung.BootFehler($"Hinweisplan: {e.Key.Name} → '{kind}' — keine Funktion dieses Actors (R20).", "hinweis:" + e.Key.Name)).ToArray());
        _konsum = konsum;
        _bote = new Lazy<Bote>(bote);
        _logger = logger;
    }

    /// <summary>Hört irgendeine Funktion diesen Hinweis-Typ?</summary>
    public bool Hoert(Type hinweis) => _hoerer.ContainsKey(hinweis);

    /// <summary>Die Funktionen, die den Hinweis-Typ hören (leer = keine).</summary>
    public IReadOnlyList<FunktionsBaustein> Hoerer(Type hinweis) => _hoerer.TryGetValue(hinweis, out var b) ? b : Array.Empty<FunktionsBaustein>();

    /// <summary>Die Adresse, an die der Hinweis für <paramref name="funktion"/> geht (A6: Partition des Herkunfts-Streams bzw. die eine Adresse).</summary>
    public ClusterIdentity Adresse(FunktionsBaustein funktion, HinweisZustellen h) => FunktionsActor.IdentitaetFuer(funktion, h.Stream, _konsum);

    /// <summary>
    /// Schickt den Hinweis direkt an jede Funktion, die ihn hört — verlierbar, ohne Antwort. Liefert, an wie viele Adressen er ging (0 = niemand
    /// hört ihn; dann ist er nur für Clients).
    /// </summary>
    public int Verteile(HinweisZustellen h)
    {
        if (!_hoerer.TryGetValue(h.Hinweis.GetType(), out var hoerer)) return 0;
        foreach (var f in hoerer) _bote.Value.Melde(Adresse(f, h), h);
        _logger?.LogDebug("[Hinweis] {Typ} {Kennung} an {Anzahl} Funktion(en)", h.Hinweis.GetType().Name, h.Kennung, hoerer.Length);
        return hoerer.Length;
    }
}
