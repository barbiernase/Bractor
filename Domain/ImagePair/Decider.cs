using Abstractions;

namespace Domain.ImagePair;

public partial class ImagePair
{
    public partial class Decider : IDecider<ImagePair>
    {
        // ═══════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<ImagePairErstellt, ImagePairExistiertBereits>> Decide(
            ErstelleImagePair cmd)
        {
            if (this.State.Version > 0)
            {
                yield return new ImagePairExistiertBereits(cmd.AggregateId);
                yield break;
            }

            yield return new ImagePairErstellt(
                cmd.AggregateId, cmd.PairKey,
                cmd.ProduziertAm, cmd.AufgenommenAm,
                cmd.UrsprungsPfad);
        }

        public IEnumerable<OneOf<BildVerfuegbar, ImagePairKomplett, ImagePairNichtGefunden, BildVersionBereitsVerfuegbar>> Decide(
            MeldeBildVerfuegbar cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new ImagePairNichtGefunden(cmd.AggregateId);
                yield break;
            }

            if (this.State.GetBild(cmd.Version) != null)
            {
                yield return new BildVersionBereitsVerfuegbar(cmd.Version);
                yield break;
            }

            var breiteProRegion = 100.0 / 8;
            var regionen = Enumerable.Range(0, 8)
                .Select(i => new RegionBewertung(
                    RegionIndex: i,
                    Position: new RegionPosition(
                        XProzent: i * breiteProRegion,
                        YProzent: 0,
                        BreiteProzent: breiteProRegion,
                        HoeheProzent: 100),
                    KiKlassifikation: null,
                    MenschLabel: null))
                .ToList();

            yield return new BildVerfuegbar(cmd.Version, cmd.Meta, cmd.Pfad, regionen);

            var andereVersion = cmd.Version == BildVersion.Dc0
                ? BildVersion.Dc2
                : BildVersion.Dc0;

            if (this.State.GetBild(andereVersion) != null)
            {
                yield return new ImagePairKomplett();
            }
        }

        // ═══════════════════════════════════════════════════
        // STRANG 1 — KI klassifiziert Kamerabilder
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<EinzelBildDurchKiKlassifiziert, BildNichtVerfuegbar, RegionLabelsUngueltig>> Decide(
            KlassifiziereEinzelBildDurchKi cmd)
        {
            if (this.State.GetBild(cmd.Version) == null)
            {
                yield return new BildNichtVerfuegbar(cmd.Version);
                yield break;
            }

            if (cmd.RegionLabels.Count != 8)
            {
                yield return new RegionLabelsUngueltig(cmd.RegionLabels.Count);
                yield break;
            }

            yield return new EinzelBildDurchKiKlassifiziert(
                cmd.Version, cmd.BildLabel, cmd.RegionLabels);
        }

        public IEnumerable<OneOf<BildPaarDurchKiKlassifiziert, PaarNichtKomplett>> Decide(
            KlassifiziereBildPaarDurchKi cmd)
        {
            if (!this.State.IstKomplett)
            {
                yield return new PaarNichtKomplett();
                yield break;
            }

            yield return new BildPaarDurchKiKlassifiziert(cmd.Label);
        }

        // ═══════════════════════════════════════════════════
        // STRANG 2 — Mensch labelt Kamerabilder
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<BildRegionGelabelt, BildNichtVerfuegbar, RegionIndexUngueltig>> Decide(
            LabelBildRegion cmd)
        {
            if (this.State.GetBild(cmd.Version) == null)
            {
                yield return new BildNichtVerfuegbar(cmd.Version);
                yield break;
            }

            if (cmd.RegionIndex < 0 || cmd.RegionIndex > 7)
            {
                yield return new RegionIndexUngueltig(cmd.RegionIndex);
                yield break;
            }

            yield return new BildRegionGelabelt(cmd.Version, cmd.RegionIndex, cmd.Label);
        }

        public IEnumerable<OneOf<EinzelBildGelabelt, BildNichtVerfuegbar>> Decide(
            LabelEinzelBild cmd)
        {
            if (this.State.GetBild(cmd.Version) == null)
            {
                yield return new BildNichtVerfuegbar(cmd.Version);
                yield break;
            }

            yield return new EinzelBildGelabelt(cmd.Version, cmd.Label);
        }

        public IEnumerable<OneOf<BildPaarGelabelt, PaarNichtKomplett>> Decide(
            LabelBildPaar cmd)
        {
            if (!this.State.IstKomplett)
            {
                yield return new PaarNichtKomplett();
                yield break;
            }

            yield return new BildPaarGelabelt(cmd.Label);
        }

        // ═══════════════════════════════════════════════════
        // STRANG 3 — Mensch labelt physisches Produkt
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<PhysischesProduktGelabelt, ImagePairNichtGefunden>> Decide(
            LabelPhysischesProdukt cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new ImagePairNichtGefunden(cmd.AggregateId);
                yield break;
            }

            yield return new PhysischesProduktGelabelt(cmd.Label);
        }

        // ═══════════════════════════════════════════════════
        // INSPEKTION — Mensch hat Bildpaar betrachtet
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<ImagePairInspiziert, ImagePairNichtGefunden>> Decide(
            MarkiereAlsInspiziert cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new ImagePairNichtGefunden(cmd.AggregateId);
                yield break;
            }

            // Idempotent — wenn bereits inspiziert, kein Event
            if (this.State.IstInspiziert)
                yield break;

            yield return new ImagePairInspiziert();
        }
    }
}