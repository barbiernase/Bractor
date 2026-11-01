using Abstractions;
using Domain.Datensatz;
using Domain.Projections;    // ImagePairFilter
using Domain.Projections.ImagePair;

namespace Domain.Pipeline.Datensatz;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// DATENSATZ-RESOLVER als Funktionen mit LESE-Fähigkeit. Der reine Decider löst die zwei I/O-behafteten
// Halbschritte nicht selbst — eine Funktion hört das Anfordern, liest das Read-Model und schickt dem Datensatz den aufgelösten Command:
//
//   RangeAufzuloesen ──ƒ RangeAufloesung (ISearchImagePairs) ──▶ NimmRangeAuf
//   EinfrierenAbzuschliessen ──ƒ MitgliederEinfrieren (IFindImagePair) ──▶ SchliesseEinfrierenAb
//
// Beide hören eine Folge des Datensatzes (vor dem Aggregat, nie das Log); der Datensatz steht in der Folge. Mitgliedschaft + Split-Konfig kommen
// autoritativ aus dem Aggregat-State (Inv. 1); nur der Label-Stand ist ein Snapshot des Read-Models — genau das meint „Label-Stand zum
// Einfrier-Zeitpunkt". Weil sie lesen, rechnen sie beim Wiederholen nicht unbedingt gleich: es wirkt der erste Command (dieselbe Id).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Eine angeforderte Range gegen den ImagePair-Read-Store auflösen: der 1:1 aus <see cref="RangeKriterien"/> gebaute <see cref="ImagePairFilter"/>,
/// durchpaginiert; der Datensatz nimmt die gefundenen Paare auf. Die Fähigkeit kommt als Parameter (je Aufruf ein Bereich), nie über den Ctor.
/// </summary>
public sealed class RangeAufloesung : IFunktion
{
    private const int SeitenGroesse = 500;

    public async Task<OneOf<NimmRangeAuf, RangeOhneTreffer>> Auf(RangeAufzuloesen e, ISearchImagePairs suche)
    {
        var ids = new List<Id<Domain.ImagePair.ImagePair>>();
        for (var seite = 1; ; seite++)
        {
            var (items, gesamt) = await suche.SearchAsync(ZuFilter(e.Kriterien, seite, SeitenGroesse));
            ids.AddRange(items.Select(m => m.Id));
            if (items.Count == 0 || ids.Count >= gesamt) break;
        }
        if (ids.Count == 0) return new RangeOhneTreffer(e.Kriterien);
        return new NimmRangeAuf(e.Datensatz, ids, new RangeHerkunft(e.Kriterien, ids.Count));
    }

    private static ImagePairFilter ZuFilter(RangeKriterien k, int seite, int seitenGroesse) => new(
        Von: k.Von,
        Bis: k.Bis,
        KiKlassifikation: k.KiKlassifikation,
        MenschLabel: k.MenschLabel,
        ProduktLabel: k.ProduktLabel,
        NurKomplette: k.NurKomplette,
        HatKiKlassifikation: k.HatKiKlassifikation,
        HatMenschLabel: k.HatMenschLabel,
        NurNichtInspizierte: k.NurNichtInspizierte,
        Seite: seite,
        SeitenGroesse: seitenGroesse);
}
