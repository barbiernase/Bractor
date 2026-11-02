using Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// HINWEIS-NETZ (docs/04 §4.4, docs/10 E7, A7): Nachrichten, die nicht zur Fachdomäne gehören (Merkmale, Messwerte, Ablehnungen), gehen von
// Funktion, Aggregat oder Client zu den Funktionen, die sie hören — IM SPEICHER, nie in den Log, kein Broker, keine Abos. Wer einen Hinweis-Typ hört,
// sagen die Köpfe der Instanzen; zugestellt wird über die Wege an den Graph-Actor der Instanz. Höchstens einmal: was im Flug ist, wenn ein Knoten
// stirbt, geht verloren (Invariante 6). Fachlich Wichtiges verlässt das Netz als Command an ein Aggregat — mit einer Command-Id aus der Kennung des
// Hinweises, die Inbox des Ziels verwirft ein Wiederholen.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Hinweis unterwegs (verlierbar): die Kennung, die der Absender vergibt (A7), der Hinweis selbst und seine Herkunft — der Stream, aus dessen
/// Eingang er hervorging, Version, Zeit, Korrelation, Akteur, Ursache.
/// </summary>
public sealed record HinweisZustellen(Guid Kennung, IEvent Hinweis, Guid Stream, int Version, DateTimeOffset Zeit, string? Korrelation, string? Akteur,
    string? Ursache = null) : IWireMessage;

/// <summary>Das Hinweis-Netz eines Knotens: je Instanz, die den Hinweis am Kopf nimmt (die Köpfe), ein Eingang über die Wege, Kennung aus Hinweis und Instanz.</summary>
public sealed class HinweisNetz(Verkettung verkettung, Func<IFunktionsWege> wege, ILogger? logger = null)
{
    /// <summary>Hört irgendeine Funktion diesen Hinweis-Typ?</summary>
    public bool Hoert(Type hinweis) => verkettung.Hoerer(hinweis).Count > 0;

    /// <summary>
    /// Schickt den Hinweis an jede Funktion, die ihn hört — verlierbar, ohne Antwort. Liefert, an wie viele er ging (0 = niemand hört ihn; dann ist er
    /// nur für Clients).
    /// </summary>
    public int Verteile(HinweisZustellen h)
    {
        var hoerer = verkettung.Hoerer(h.Hinweis.GetType());
        foreach (var instanz in hoerer)
            wege().Melde(new EingangZustellen(Eingangsvertrag.Eingang(h.Kennung, instanz), instanz, h.Hinweis, Eingangsart.Hinweis, h.Stream, h.Version, h.Zeit,
                h.Akteur, h.Korrelation, h.Ursache));
        if (hoerer.Count > 0) logger?.LogDebug("[Hinweis] {Typ} {Kennung} an {Anzahl} Funktion(en)", h.Hinweis.GetType().Name, h.Kennung, hoerer.Count);
        return hoerer.Count;
    }
}
