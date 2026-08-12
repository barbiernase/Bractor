namespace Domain.DddPatterns.Gemeinsam;

/// <summary>
/// VALUE OBJECT mit Format-Invariante — eine syntaktisch gültige, normalisierte
/// E-Mail-Adresse. Zeigt Selbstvalidierung + Normalisierung (Kleinschreibung, getrimmt):
/// zwei Adressen, die sich nur in Groß/Klein unterscheiden, sind gleich per Wert.
/// Ein <c>string</c> könnte das alles nicht garantieren.
/// </summary>
public sealed record Emailadresse
{
    public string Wert { get; }

    private Emailadresse(string wert) => Wert = wert;

    public static Emailadresse Von(string eingabe)
    {
        DomaeneException.Verlange(!string.IsNullOrWhiteSpace(eingabe), "E-Mail darf nicht leer sein.");
        var normalisiert = eingabe.Trim().ToLowerInvariant();

        var at = normalisiert.IndexOf('@');
        DomaeneException.Verlange(at > 0 && at < normalisiert.Length - 1,
            $"E-Mail braucht genau ein '@' mit lokalem und Domänenteil: {eingabe}");
        DomaeneException.Verlange(normalisiert.IndexOf('@', at + 1) < 0,
            $"E-Mail darf nur ein '@' enthalten: {eingabe}");
        var domaene = normalisiert[(at + 1)..];
        DomaeneException.Verlange(domaene.Contains('.'), $"E-Mail-Domäne braucht einen Punkt: {eingabe}");
        DomaeneException.Verlange(!normalisiert.Contains(' '), $"E-Mail darf kein Leerzeichen enthalten: {eingabe}");

        return new Emailadresse(normalisiert);
    }

    public override string ToString() => Wert;
}
