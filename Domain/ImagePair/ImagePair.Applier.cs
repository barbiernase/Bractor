using Abstractions;

namespace Domain.ImagePair;

public partial class ImagePair
{
    public partial class Applier : IApplier<ImagePair>
    {
        // ═══════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════

        public void Apply(ImagePairErstellt evt)
        {
            this.State.PairKey = evt.PairKey;
            this.State.ProduziertAm = evt.ProduziertAm;
            this.State.AufgenommenAm = evt.AufgenommenAm;
            this.State.UrsprungsPfad = evt.UrsprungsPfad;
        }

        public void Apply(BildVerfuegbar evt)
        {
            var bildInfo = new BildInfo(
                Version: evt.Version,
                Meta: evt.Meta,
                Pfad: evt.Pfad,
                Bewertung: new BildBewertung(KiKlassifikation: null, MenschLabel: null),
                Regionen: evt.Regionen);

            SetBild(evt.Version, bildInfo);
        }

        public void Apply(ImagePairKomplett evt) { }

        // ═══════════════════════════════════════════════════
        // STRANG 1 — KI klassifiziert Kamerabilder
        // ═══════════════════════════════════════════════════

        public void Apply(EinzelBildDurchKiKlassifiziert evt)
        {
            var bild = this.State.GetBild(evt.Version)!;

            var neueBewertung = bild.Bewertung with { KiKlassifikation = evt.BildLabel };

            var neueRegionen = bild.Regionen
                .Select((r, i) => r with { KiKlassifikation = evt.RegionLabels[i] })
                .ToList();

            SetBild(evt.Version, bild with
            {
                Bewertung = neueBewertung,
                Regionen = neueRegionen
            });
        }

        public void Apply(BildPaarDurchKiKlassifiziert evt)
        {
            this.State.KiBildpaarKlassifikation = evt.Label;
        }

        // ═══════════════════════════════════════════════════
        // STRANG 2 — Mensch labelt Kamerabilder
        // ═══════════════════════════════════════════════════

        public void Apply(BildRegionGelabelt evt)
        {
            var bild = this.State.GetBild(evt.Version)!;

            var neueRegionen = bild.Regionen
                .Select(r => r.RegionIndex == evt.RegionIndex
                    ? r with { MenschLabel = evt.Label }
                    : r)
                .ToList();

            SetBild(evt.Version, bild with { Regionen = neueRegionen });
        }

        public void Apply(EinzelBildGelabelt evt)
        {
            var bild = this.State.GetBild(evt.Version)!;
            SetBild(evt.Version, bild with
            {
                Bewertung = bild.Bewertung with { MenschLabel = evt.Label }
            });
        }

        public void Apply(BildPaarGelabelt evt)
        {
            this.State.MenschBildpaarLabel = evt.Label;
        }

        // ═══════════════════════════════════════════════════
        // STRANG 3 — Mensch labelt physisches Produkt
        // ═══════════════════════════════════════════════════

        public void Apply(PhysischesProduktGelabelt evt)
        {
            this.State.PhysischesProduktLabel = evt.Label;
        }

        // ═══════════════════════════════════════════════════
        // INSPEKTION
        // ═══════════════════════════════════════════════════

        public void Apply(ImagePairInspiziert evt)
        {
            this.State.IstInspiziert = true;
        }

        // ─── Hilfsmethode ───

        private void SetBild(BildVersion version, BildInfo bild)
        {
            switch (version)
            {
                case BildVersion.Dc0: this.State.Dc0 = bild; break;
                case BildVersion.Dc2: this.State.Dc2 = bild; break;
            }
        }
    }
}