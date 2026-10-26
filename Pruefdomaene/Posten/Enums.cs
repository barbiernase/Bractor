namespace Pruefdomaene.Posten;

/// <summary>Die Art des Vorgangs, zu dem eine Buchung gehört (<see cref="Beleg"/>). 1-basiert: 0 ginge auf dem Proto-Wire verloren (CQRS081).</summary>
public enum Belegart
{
    Keiner = 1,
    Auftrag = 2,
    Verteilung = 3,
}
