using Abstractions;
using Domain.Datensatz;
using DatensatzAgg = Domain.Datensatz.Datensatz;

namespace Domain.Projections.Datensatz;

/// <summary>
/// Materialisiert den Datensatz-Zustand: ein <see cref="DatensatzReadModel"/>
/// pro Datensatz (Upsert) und beim Einfrieren je Mitglied eine <see cref="DatensatzSampleReadModel"/>
/// (die queryierbare Trainings-Wahrheit).
///
/// Das Einfrieren schreibt neue Sample-Zeilen (append-artig, eine doppelte Wirkung wäre sichtbar) — genau einmal, weil der
/// <c>DatensatzStore</c> ein Store der Klasse 1 ist (Wirkung und Fortschritt in einer Transaktion).
/// </summary>
public sealed class DatensatzProjektion : IProjektion
{
    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    public Task Auf(DatensatzErstellt e, Herkunft h, IUpsertDatensatz upsertDatensatz)
        => upsertDatensatz.UpsertAsync(new DatensatzReadModel
        {
            Id = h.Id<DatensatzAgg>(),
            Name = e.Name,
            Status = DatensatzStatus.Entwurf,
            AnzahlMitglieder = 0,
            LetzteAktualisierung = h.Zeit
        });

    // ═══════════════════════════════════════════════════════════
    // KOMPOSITION — Ranges & manuelles Delta
    // ═══════════════════════════════════════════════════════════

    public Task Auf(PaareAufgenommen e, Herkunft h, INimmRangeAuf nimmRangeAuf)
        => nimmRangeAuf.NimmRangeAufAsync(
            h.Id<DatensatzAgg>(), e.ImagePairIds, e.Herkunft, h.Zeit);

    public Task Auf(PaarAufgenommen e, Herkunft h, INimmPaarAuf nimmPaarAuf)
        => nimmPaarAuf.NimmPaarAufAsync(h.Id<DatensatzAgg>(), e.ImagePairId, h.Zeit);

    public Task Auf(PaarEntfernt e, Herkunft h, IEntfernePaar entfernePaar)
        => entfernePaar.EntfernePaarAsync(h.Id<DatensatzAgg>(), e.ImagePairId, h.Zeit);

    public Task Auf(SplitGesetzt e, Herkunft h, ISetzeSplit setzeSplit)
        => setzeSplit.SetzeSplitAsync(
            h.Id<DatensatzAgg>(),
            new SplitKonfig(e.TrainProzent, e.ValProzent, e.TestProzent, e.Seed),
            h.Zeit);

    // ═══════════════════════════════════════════════════════════
    // EINFRIEREN — Snapshot festschreiben (append-artig)
    // ═══════════════════════════════════════════════════════════

    public Task Auf(DatensatzEingefroren e, Herkunft h, IFriereEin friereEin)
        => friereEin.FriereEinAsync(
            h.Id<DatensatzAgg>(), e.Version, e.Mitglieder, h.Zeit);
}
