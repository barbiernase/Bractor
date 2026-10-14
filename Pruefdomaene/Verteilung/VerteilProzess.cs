using Abstractions;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Verteilung;

/// <summary>
/// FAN-OUT mit dynamischer Breite und COUNT-JOIN: die Quelle reserviert die Gesamtmenge (Gegenzug: Freigabe), danach bucht
/// <c>SendeJe</c> auf jedes der N Ziele zu (je Ziel ein eigener deterministischer Vorgang), und erst wenn ALLE N Zubuchungen da sind
/// (<c>UndAlle</c>), schließt die Verteilung ab.
/// </summary>
public sealed class VerteilProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<VerteilungGestartet>.Definiere(p =>
    {
        p.Auf<VerteilungGestartet>()
            .Sende<ReservierePosten>(t => new ReservierePosten(t.QuelleId, t.MengeJeZiel * t.Ziele.Count))
            .RückgängigDurch<GibPostenFrei>(t => new GibPostenFrei(t.QuelleId, t.MengeJeZiel * t.Ziele.Count));

        p.Auf<VerteilungGestartet>().Und<PostenReserviert>()
            .SendeJe<BuchePostenZu>((t, r) => t.Ziele.Select(z => new BuchePostenZu(z, t.MengeJeZiel)));

        p.Auf<VerteilungGestartet>().Und<PostenReserviert>().UndAlle<PostenZugebucht>(t => t.Ziele.Count)
            .Sende<SchliesseVerteilungAb>((t, r, zubuchungen) => new SchliesseVerteilungAb(t.VerteilungId, zubuchungen.Sum(z => z.Menge)));
    });
}
