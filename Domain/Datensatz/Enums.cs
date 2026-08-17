namespace Domain.Datensatz;

/// <summary>Lebenszyklus eines Datensatzes.</summary>
/// <remarks>
/// <see cref="Entwurf"/> = wachsender Korb konkreter Bildpaar-Referenzen (dynamisch,
/// live Label-Stand). <see cref="Eingefroren"/> = Mitgliedschaft + Label-Stand + Split
/// zum Einfrier-Zeitpunkt festgeschrieben, immutabel, reproduzierbar.
/// </remarks>
public enum DatensatzStatus { Entwurf, Eingefroren }

/// <summary>
/// Zuteilung eines Mitglieds beim Einfrieren.
/// Stratifiziert nach Klasse, deterministisch (fester Seed) beim Einfrieren vergeben.
/// </summary>
public enum Split { Train, Val, Test }
