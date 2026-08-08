// ── Client.Infrastructure/Collections/IVirtualWindow.cs ──

namespace Client.Infrastructure.Collections;

/// <summary>
/// Read-Vertrag, den die generische VirtualList-Komponente (der "Treiber")
/// von der Datenstruktur braucht. Bewusst schmal und domänenfrei: die
/// Komponente kennt weder den Key-Typ noch die konkreten Query-/DTO-Typen.
///
/// Umgekehrt kennt die Datenstruktur weder Bus noch Pixel noch scrollTop. Sie
/// liefert nur Wissen ("welche Seiten fehlen im Bereich X–Y") und eine fertige
/// Query-Nachricht — dispatcht wird ausschließlich vom Treiber.
///
/// Adressierung erfolgt über den ANZEIGE-Index (View-Koordinate), also inkl.
/// InsertOffset. Die Übersetzung in stabile Daten-Indizes ist Sache der
/// Implementierung.
///
/// Spacer-Pixel liegen bewusst NICHT hier: sie hängen am sichtbaren Bereich
/// (scrollTop/Höhe), den nur der Treiber kennt. Sie aus GesamtAnzahl + Fenster
/// zu rechnen ist reine View-Arithmetik und hält diese Struktur pixel-frei.
/// </summary>
public interface IVirtualWindow<out TItem> : IVersioned where TItem : class
{
    /// <summary>Anzahl Zeilen der virtuellen Gesamtliste (inkl. Live-Einfügungen).</summary>
    int GesamtAnzahl { get; }

    /// <summary>Chunk-/Seitengröße — für die Bereich→Seiten-Umrechnung im Treiber.</summary>
    int ChunkGroesse { get; }

    /// <summary>
    /// Zahl der seit dem Snapshot oben live eingefügten Zeilen.
    /// anzeigeIndex = stabilerIndex + InsertOffset. Der Treiber liest den
    /// Zuwachs, um scrollTop einmalig zu kompensieren (außer am Live-Rand).
    /// </summary>
    int InsertOffset { get; }

    /// <summary>
    /// Item am Anzeige-Index, oder null (= Skeleton: Chunk noch nicht geladen).
    /// Blockiert nie — fehlende Daten sind ein null, kein await.
    /// </summary>
    TItem? this[int anzeigeIndex] { get; }

    /// <summary>
    /// Fehlende (weder geladene noch angeforderte) Seiten im Anzeige-Bereich
    /// [vonIndex, bisIndex]. Der Treiber fordert genau diese nach.
    /// </summary>
    IEnumerable<int> FehlendeSeiten(int vonIndex, int bisIndex);

    /// <summary>Markiert eine Seite als angefordert (in-flight) — verhindert Doppel-Requests.</summary>
    void MarkiereAngefordert(int seite);

    /// <summary>Baut die domänenspezifische Query-Nachricht für eine Seite. Dispatch macht der Treiber.</summary>
    object BaueQuery(int seite);
}
