namespace Domain.Lagerartikel;

public record Lagerplatz(string Gang, string Regal, int Ebene);
public record Geldbetrag(decimal Wert, string Waehrung);
