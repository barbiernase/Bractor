namespace Pruefwerk.Katalog.Abrechnung;

/// <summary>Technik hinter dem Mahnlauf (ohne Marker, im Konstruktor): verschickt eine Mahnung und nennt ihre Versandnummer.</summary>
public interface IMahnversand
{
    Task<string> VersendeAsync(Guid empfaenger, Guid vorgang, int stufe);
}
