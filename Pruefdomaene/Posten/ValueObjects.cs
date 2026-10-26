using Abstractions;

namespace Pruefdomaene.Posten;

/// <summary>
/// Wofür eine Buchung am Posten geschieht (Plan Pub/Sub-Neustrukturierung, Annahme A2): die Zugehörigkeit zu einem Vorgang ist ein FACHLICHES Feld,
/// keine versteckte Korrelation — wie Belegart und Belegnummer einer Lagerbuchung. Die Funktion eines Vorgangs erkennt an ihm, welche Reservierung
/// oder Zubuchung ihr gehört; <see cref="Ohne"/> gehört keinem Vorgang (Nachschub, Hand).
/// </summary>
public sealed record Beleg(Belegart Art, Guid Nummer)
{
    public static readonly Beleg Ohne = new(Belegart.Keiner, Guid.Empty);

    /// <summary>Gehört der Beleg zu einem Vorgang dieser Art? Dann ist <see cref="Nummer"/> dessen Id.</summary>
    public bool Ist(Belegart art) => Art == art && Nummer != Guid.Empty;
}
