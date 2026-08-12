namespace Domain.Verkauf;

/// <summary>
/// Erzeuger und Verhalten von <see cref="Geldwert"/> — auf dem Typ selbst (partial), damit
/// die Fachsprache am Call-Site spricht: <c>Geldwert.Euro(100)</c>, <c>preis.Mal(3)</c>,
/// <c>summe.GroesserAls(limit)</c>. Die Operationen sind abgeschlossen: aus gültigen
/// Werten entstehen nur gültige Werte.
/// </summary>
public partial record Geldwert
{
    // ── Benannte Erzeuger (die einzige sprechende Konstruktion) ──
    public static Geldwert Von(decimal betrag, Waehrung waehrung) => new(betrag, waehrung);
    public static Geldwert Euro(decimal betrag) => new(betrag, Waehrung.EUR);
    public static Geldwert Null(Waehrung waehrung) => new(0m, waehrung);

    // ── Verhalten ──
    public Geldwert Plus(Geldwert anderer)
    {
        MussGleicheWaehrung(anderer);
        return this with { Betrag = Betrag + anderer.Betrag };
    }

    public Geldwert Mal(int menge) => this with { Betrag = Betrag * menge };

    public bool GroesserAls(Geldwert anderer)
    {
        MussGleicheWaehrung(anderer);
        return Betrag > anderer.Betrag;
    }

    public bool GleicheWaehrung(Geldwert anderer) => Waehrung == anderer.Waehrung;

    // Währungsmix ist ein Programmierfehler (der Decider prüft GleicheWaehrung vorher) →
    // defensiver Guard, keine fachliche Ablehnung.
    private void MussGleicheWaehrung(Geldwert anderer)
    {
        if (Waehrung != anderer.Waehrung)
            throw new InvalidOperationException($"Währungen dürfen nicht gemischt werden: {Waehrung} vs. {anderer.Waehrung}");
    }

    public override string ToString() => $"{Betrag:0.00} {Waehrung}";
}
