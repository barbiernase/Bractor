using Domain.DddPatterns.Gemeinsam;

namespace Domain.DddPatterns.Prozess;

/// <summary>
/// Kleines Ziel-Aggregat für die Saga-Demo: reserviert und gibt Bestand frei. REINE
/// Domäne mit eigener Invariante (nie mehr reservieren als verfügbar). Der Freigabe-Weg
/// ist die fachliche Umkehrung der Reservierung — die Grundlage der Kompensation.
/// </summary>
public sealed class Lagerbestand
{
    public int Verfuegbar { get; private set; }
    public int Reserviert { get; private set; }

    public Lagerbestand(int anfangsbestand)
    {
        DomaeneException.Verlange(anfangsbestand >= 0, "Anfangsbestand darf nicht negativ sein.");
        Verfuegbar = anfangsbestand;
    }

    public void Reserviere(int menge)
    {
        DomaeneException.Verlange(menge > 0, "Reservierungsmenge muss positiv sein.");
        DomaeneException.Verlange(Verfuegbar >= menge, $"Bestand reicht nicht: {Verfuegbar} < {menge}.");
        Verfuegbar -= menge;
        Reserviert += menge;
    }

    /// <summary>Kompensationsschritt: macht eine Reservierung rückgängig.</summary>
    public void GibFrei(int menge)
    {
        DomaeneException.Verlange(Reserviert >= menge, $"Nicht so viel reserviert: {Reserviert} < {menge}.");
        Reserviert -= menge;
        Verfuegbar += menge;
    }
}

/// <summary>
/// Kleines Ziel-Aggregat für die Saga-Demo: belastet ein Guthaben (in <see cref="Geld"/>)
/// und verweigert Überziehung. Zweiter Zweig, der scheitern kann → Auslöser der Kompensation.
/// </summary>
public sealed class Zahlungsvorgang
{
    public Geld Guthaben { get; private set; }

    public Zahlungsvorgang(Geld start) => Guthaben = start;

    public void Belaste(Geld betrag)
    {
        DomaeneException.Verlange(!betrag.GroesserAls(Guthaben), $"Guthaben ungedeckt: {Guthaben} < {betrag}.");
        Guthaben = Guthaben.Minus(betrag);
    }
}
