using Abstractions;

namespace Domain.Datensatz;

public partial class Datensatz
{
    public partial class Decider : IDecider<Datensatz>
    {
        // ═══════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<DatensatzErstellt, DatensatzExistiertBereits>> Decide(
            ErstelleDatensatz cmd)
        {
            if (this.State.Existiert)
            {
                yield return new DatensatzExistiertBereits(cmd.AggregateId);
                yield break;
            }

            yield return new DatensatzErstellt(cmd.Name);
        }

        // ═══════════════════════════════════════════════════
        // RANGE — Variante A (zweiphasig)
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<RangeAngefordert, DatensatzBereitsEingefroren>> Decide(
            FuegeRangeHinzu cmd)
        {
            if (this.State.IstEingefroren)
            {
                yield return new DatensatzBereitsEingefroren(cmd.AggregateId);
                yield break;
            }

            // Rein: keine Suche im Decider. Nur die Kriterien als Provenienz festhalten;
            // der server-seitige Resolver löst sie auf.
            yield return new RangeAngefordert(cmd.Kriterien);
        }

        public IEnumerable<OneOf<PaareAufgenommen, RangeLeer, DatensatzBereitsEingefroren>> Decide(
            NimmRangeAuf cmd)
        {
            if (this.State.IstEingefroren)
            {
                yield return new DatensatzBereitsEingefroren(cmd.AggregateId);
                yield break;
            }

            if (cmd.ImagePairIds.Count == 0)
            {
                yield return new RangeLeer(cmd.AggregateId);
                yield break;
            }

            yield return new PaareAufgenommen(cmd.ImagePairIds, cmd.Herkunft);
        }

        // ═══════════════════════════════════════════════════
        // MANUELLES DELTA
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<PaarAufgenommen, DatensatzBereitsEingefroren>> Decide(
            NimmPaarAuf cmd)
        {
            if (this.State.IstEingefroren)
            {
                yield return new DatensatzBereitsEingefroren(cmd.AggregateId);
                yield break;
            }

            // Idempotent — schon im Korb, kein Event.
            if (this.State.IstDraftMitglied(cmd.ImagePairId))
                yield break;

            yield return new PaarAufgenommen(cmd.ImagePairId);
        }

        public IEnumerable<OneOf<PaarEntfernt, DatensatzBereitsEingefroren>> Decide(
            EntfernePaar cmd)
        {
            if (this.State.IstEingefroren)
            {
                yield return new DatensatzBereitsEingefroren(cmd.AggregateId);
                yield break;
            }

            // Idempotent — nicht im Korb, kein Event.
            if (!this.State.IstDraftMitglied(cmd.ImagePairId))
                yield break;

            yield return new PaarEntfernt(cmd.ImagePairId);
        }

        // ═══════════════════════════════════════════════════
        // SPLIT — optionaler Override
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<SplitGesetzt, DatensatzBereitsEingefroren, SplitUngueltig>> Decide(
            SetzeSplit cmd)
        {
            if (this.State.IstEingefroren)
            {
                yield return new DatensatzBereitsEingefroren(cmd.AggregateId);
                yield break;
            }

            var konfig = new SplitKonfig(cmd.TrainProzent, cmd.ValProzent, cmd.TestProzent, cmd.Seed);
            if (!konfig.IstGueltig)
            {
                yield return new SplitUngueltig(cmd.TrainProzent, cmd.ValProzent, cmd.TestProzent);
                yield break;
            }

            yield return new SplitGesetzt(cmd.TrainProzent, cmd.ValProzent, cmd.TestProzent, cmd.Seed);
        }

        // ═══════════════════════════════════════════════════
        // EINFRIEREN — zweiphasig
        // ═══════════════════════════════════════════════════

        public IEnumerable<OneOf<EinfrierenAngefordert, DatensatzBereitsEingefroren, DatensatzLeer>> Decide(
            FriereEin cmd)
        {
            if (this.State.IstEingefroren)
            {
                yield return new DatensatzBereitsEingefroren(cmd.AggregateId);
                yield break;
            }

            if (this.State.AnzahlDraftMitglieder == 0)
            {
                yield return new DatensatzLeer(cmd.AggregateId);
                yield break;
            }

            yield return new EinfrierenAngefordert();
        }

        public IEnumerable<OneOf<DatensatzEingefroren, DatensatzBereitsEingefroren, DatensatzLeer>> Decide(
            SchliesseEinfrierenAb cmd)
        {
            if (this.State.IstEingefroren)
            {
                yield return new DatensatzBereitsEingefroren(cmd.AggregateId);
                yield break;
            }

            if (cmd.Mitglieder.Count == 0)
            {
                yield return new DatensatzLeer(cmd.AggregateId);
                yield break;
            }

            // Version vergeben (Phase 1: immer 1 — Einfrieren ist terminal).
            var version = this.State.EingefroreneVersion + 1;
            yield return new DatensatzEingefroren(version, cmd.Mitglieder);
        }
    }
}
