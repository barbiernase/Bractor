using System.Collections.Immutable;
using System.Linq;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Collections;
using Domain.ImagePair;
using Domain.Projections;
namespace Domain.Client.Modules;

public partial class Store
{
    // ─── Live-Insert (nur ohne Baum-Auswahl und im Live-Modus) ───

    void Handle(ImagePairErstellt evt, MessageContext ctx)
    {
        // In einer fixen Bereichs-Ansicht wandern keine "Jetzt"-Neuzugänge rein.
        if (AktiveBereiche.Count > 0) return;
        if (!IstLive) return;
        if (Suche.Bis is not null) return;
        if (Suche.ProduktLabel is not null || Suche.HatMenschLabel == true) return;

        var folgtNeuesten = !Cursor.Id.HasValue
            || Cursor.Index + VirtualImagePairs.InsertOffset == 0;

        var rec = ImagePairRecord.Neu(
            ctx.AggregateId, evt.PairKey, evt.ProduziertAm, evt.AufgenommenAm, evt.UrsprungsPfad);
        VirtualImagePairs.FuegeVorneEin(rec);

        if (folgtNeuesten)
            Cursor.Select(-VirtualImagePairs.InsertOffset, rec.Id);
    }

    void Handle(ImagePairKomplett evt, MessageContext ctx)
        => VirtualImagePairs.Patch(ctx.AggregateId, e => e with { IstKomplett = true });

    void Handle(EinzelBildDurchKiKlassifiziert evt, MessageContext ctx)
        => VirtualImagePairs.Patch(ctx.AggregateId, e => evt.Version switch
        {
            BildVersion.Dc0 => e with { Dc0KiBildKlassifikation = evt.BildLabel, Dc0KiRegionen = MapRegionen(evt.RegionLabels) },
            BildVersion.Dc2 => e with { Dc2KiBildKlassifikation = evt.BildLabel, Dc2KiRegionen = MapRegionen(evt.RegionLabels) },
            _ => e
        });

    void Handle(BildPaarDurchKiKlassifiziert evt, MessageContext ctx)
        => VirtualImagePairs.Patch(ctx.AggregateId, e => e with { KiBildpaarKlassifikation = evt.Label });

    // ─── Suchergebnis: Sammel-Merge (N Bereiche) oder Normalpfad (unbegrenzt/lazy) ───

    void Handle(ImagePairSuchergebnis r, MessageContext ctx)
    {
        if (_sammelModus)
        {
            // Antwort eines Bereichs einsammeln; erst wenn ALLE da sind, mergen.
            _puffer.AddRange(r.Items);
            if (--_offeneAntworten > 0) return;
            _sammelModus = false;

            var zusammen = _puffer
                .GroupBy(x => x.Id).Select(g => g.First())          // dedup über Bereichsgrenzen
                .OrderByDescending(x => x.ProduziertAm)             // neueste zuerst, wie der Server
                .ToList();
            _puffer.Clear();

            VirtualImagePairs.Absorb(
                new Chunk<ImagePairAntwort>(1, System.Math.Max(1, zusammen.Count), zusammen.Count, zusammen),
                ImagePairRecord.FromAntwort);

            MarkHydrated();
            PositioniereCursor();
            return;
        }

        // Normalpfad: einzelner paginierter Chunk (unbegrenzte Suche / Tag-Labeling).
        VirtualImagePairs.Absorb(
            new Chunk<ImagePairAntwort>(r.Seite, r.SeitenGroesse, r.GesamtAnzahl, r.Items),
            ImagePairRecord.FromAntwort);

        MarkHydrated();
        PositioniereCursor();
    }

    private void PositioniereCursor()
    {
        var pending = Cursor.ConsumePending();

        if (VirtualImagePairs.GesamtAnzahl == 0) { Cursor.Clear(); return; }

        if (pending is { } p)
        {
            var ziel = p < 0
                ? VirtualImagePairs.GesamtAnzahl - 1
                : System.Math.Min(p, VirtualImagePairs.GesamtAnzahl - 1);

            var item = VirtualImagePairs[ziel + VirtualImagePairs.InsertOffset];
            Cursor.Select(ziel, item?.Id ?? default);
        }
    }

    private static ImmutableArray<Klassifikation?> MapRegionen(IReadOnlyList<Klassifikation> labels)
    {
        var b = ImmutableArray.CreateBuilder<Klassifikation?>(8);
        for (int i = 0; i < 8; i++) b.Add(i < labels.Count ? labels[i] : null);
        return b.MoveToImmutable();
    }
}
