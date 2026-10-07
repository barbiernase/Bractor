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

        public IEnumerable<OneOf<RohbildEingegangen, ImagePairNichtGefunden, RohbildBereitsEingegangen>> Decide(
            NimmRohbildAuf cmd)
        {
            if (this.State.Version == 0)
            {
                yield return new ImagePairNichtGefunden(cmd.AggregateId);
                yield break;
            }

            var schonDa = cmd.Version == BildVersion.Dc0 ? this.State.Dc0Eingegangen : this.State.Dc2Eingegangen;
            if (schonDa || this.State.GetBild(cmd.Version) != null)
            {
                yield return new RohbildBereitsEingegangen(cmd.Version);
                yield break;
            }

            yield return new RohbildEingegangen(cmd.AggregateId, cmd.Version, cmd.Pfad, cmd.Dateiname, cmd.DateigroesseBytes);
        }

        public IEnumerable<OneOf<BildVerfuegbar, ImagePairKomplett, ImagePairNichtGefunden, BildVersionBereitsVerfuegbar>> Decide(
            MeldeBildVerfuegbar cmd)
        {
            // 🤖 Prompt: wir brauchen eine initiationslogik der vorab schaut, ob wirklich alles leer ist.
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

            const int anzahlRegionen = 8;
            const double regionBreite = 100.0 / anzahlRegionen;
            var regionen = new List<RegionBewertung>(anzahlRegionen);
            for (var i = 0; i < anzahlRegionen; i++)
            {
                regionen.Add(new RegionBewertung(
                    RegionIndex: i,
                    Position: new RegionPosition(i * regionBreite, 0.0, regionBreite, 100.0),
                    KiKlassifikation: null,
                    MenschLabel: null));
            }

            yield return new BildVerfuegbar(cmd.Version, cmd.Meta, cmd.Pfad, regionen);

            var andereVersion = cmd.Version == BildVersion.Dc0 ? BildVersion.Dc2 : BildVersion.Dc0;
            if (this.State.GetBild(andereVersion) != null)
                yield return new ImagePairKomplett();
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