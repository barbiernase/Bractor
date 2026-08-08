namespace Abstractions;

/// <summary>
/// Die explizite, typisierte Auslieferungs-Art eines <see cref="CommandEnvelope"/> — ersetzt den
/// überladenen <c>ExpectedVersion = -1</c>-Sentinel (EM-2: kein Message-Sentinel kodiert Behandlung).
///
/// Geschlossener Summentyp mit genau zwei Fällen; der Actor dispatcht per <c>switch</c> exhaustiv auf
/// zwei getrennte Eingänge. Die Behandlung ist am <b>Typ</b> ablesbar, nicht an einem magischen Wert.
/// Weil die Version <b>nur</b> im <see cref="Client"/>-Fall lebt, ist „interne Emitter behaupten NIE
/// eine Version" (§4.2) <b>strukturell</b> — ein <see cref="Emittiert"/>-Command hat kein Versionsfeld.
/// </summary>
public abstract record CommandModus
{
    private CommandModus() { }

    /// <summary>
    /// Externer Client-Vertrag: OCC gegen die behauptete <paramref name="ExpectedVersion"/>
    /// (Compare-and-Swap gegen die Stream-Version).
    /// </summary>
    public sealed record Client(int ExpectedVersion) : CommandModus;

    /// <summary>
    /// Interner Emitter (Reaktion / Prozess / Pipeline): behauptet KEINE Empfänger-Version. Idempotenz
    /// sichert die deterministische <see cref="CommandEnvelope.CommandId"/> + die Framework-Inbox
    /// (<c>KommandoVerarbeitet</c>) am Empfänger — nicht OCC.
    /// </summary>
    public sealed record Emittiert : CommandModus;
}
