// ── Client.Infrastructure/Collections/Chunk.cs ──

namespace Client.Infrastructure.Collections;

/// <summary>
/// Client-seitige Adapter-Form eines eintreffenden Listen-Chunks.
///
/// Bewusst HIER (Client-Infra), nicht im geteilten Abstractions-Assembly: die
/// Virtualisierung ist ein Client-Belang. Die Domäne exponiert nur schlichte
/// Felder auf ihrem Query-Response; der Store liest sie an der Grenze in einen
/// Chunk — der Abhängigkeitspfeil zeigt vom Client zur Domäne, nie umgekehrt.
///
/// Ein benanntes record-struct statt vier lose Parameter: verhindert
/// Reihenfolge-Verwechsler (Seite ↔ GesamtAnzahl) beim Aufruf von Absorb.
/// </summary>
public readonly record struct Chunk<TDto>(
    int Seite,
    int SeitenGroesse,
    int GesamtAnzahl,
    IReadOnlyList<TDto> Items);
