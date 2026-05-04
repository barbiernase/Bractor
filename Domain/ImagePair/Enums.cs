namespace Domain.ImagePair;

/// <summary>Aufnahme-Modus der Kamera.</summary>
public enum BildVersion { Dc0, Dc2 }

/// <summary>Klassifikationsergebnis auf jeder Stufe und für jeden Akteur.</summary>
public enum Klassifikation { KeineAnomalie, Questionable, Anomalie }