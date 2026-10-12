namespace Abstractions;

// Der Stand der Leseseite (docs/plan-werkbank-umsetzung.md E9) — vom ProjectionServicesGenerator je Store bzw. Read-Model in die DI
// geschrieben. Daraus entscheidet der Host einer Sim-Umgebung beim Start, ob die Projektionen neu aufgebaut werden (Werkbank §4.6).

/// <summary>Code-Hash einer Store-Klasse (normalisierte Syntax aller Teile).</summary>
public sealed record StoreStand(Type Store, string CodeHash);

/// <summary>Ein Read-Model-Dokumenttyp — die Leseseite, die ein Neuaufbau leert.</summary>
public sealed record ReadModelTyp(Type Typ);
