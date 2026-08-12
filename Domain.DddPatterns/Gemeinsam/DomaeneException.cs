namespace Domain.DddPatterns.Gemeinsam;

/// <summary>
/// Verletzung einer fachlichen Invariante. Bewusst ein eigener Typ (kein
/// <see cref="ArgumentException"/>): der Aufrufer kann „ungültige Eingabe des
/// Programmierers" von „ungültige fachliche Operation" unterscheiden. Value Objects
/// und Aggregate werfen ihn bei Konstruktion/Übergang, wenn eine Invariante bräche.
/// </summary>
public sealed class DomaeneException : Exception
{
    public DomaeneException(string nachricht) : base(nachricht) { }

    /// <summary>Wirft, wenn <paramref name="bedingung"/> falsch ist — eine deklarative Guard-Klausel.</summary>
    public static void Verlange(bool bedingung, string nachricht)
    {
        if (!bedingung) throw new DomaeneException(nachricht);
    }
}
