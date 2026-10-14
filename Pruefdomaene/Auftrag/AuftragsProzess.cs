using Abstractions;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Auftrag;

/// <summary>
/// Ein DIAMANT: aus einem Auslöser laufen zwei unabhängige Zweige (Posten reservieren, Kontingent belegen), die sich am Join wieder
/// vereinen (bestätigen erst, wenn BEIDE gelungen sind). Scheitert ein Zweig, gleicht der Dirigent den gelungenen über sein
/// <c>RückgängigDurch</c> aus und beendet den Vorgang als fehlgeschlagen — der Auftrag wird nie bestätigt.
/// </summary>
public sealed class AuftragsProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<AuftragGestartet>.Definiere(p =>
    {
        p.Auf<AuftragGestartet>()
            .Sende<ReservierePosten>(e => new ReservierePosten(e.PostenId, e.Menge))
            .RückgängigDurch<GibPostenFrei>(e => new GibPostenFrei(e.PostenId, e.Menge));

        p.Auf<AuftragGestartet>()
            .Sende<BelegeKontingent>(e => new BelegeKontingent(e.KontingentId, e.Menge))
            .RückgängigDurch<EntlasteKontingent>(e => new EntlasteKontingent(e.KontingentId, e.Menge));

        p.Auf<AuftragGestartet>().Und<PostenReserviert>().Und<KontingentBelegt>()
            .Sende<BestaetigeAuftrag>((e, r, b) => new BestaetigeAuftrag(e.AuftragId));
    });
}
