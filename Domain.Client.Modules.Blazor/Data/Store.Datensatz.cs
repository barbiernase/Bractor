using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Client.Modules.Datensaetze;   // DatensatzAusgewaehlt
using Domain.Datensatz;                     // DatensatzStatus, Paar-Events
using Domain.Projections;                   // DatensatzAntwort

namespace Domain.Client.Modules;

/// <summary>
/// Das aktive Sammel-Ziel — ein Datensatz als „Aufnahme-Knopf" (Konzept
/// datensatz-kuratierung §4.1). Rein clientseitige Sicht: die Mitgliedschaft ist
/// ein <em>Tag</em> an den Bildern, kein Domänenzustand (Invariante 5). Die
/// Mitglieder-Menge speist O(1)-Mitgliedschaft für Galerie-Badges und Einbild-Toggle.
/// </summary>
public sealed record SammelZielKontext(
    Guid Id,
    string? Name,
    DatensatzStatus Status,
    int EingefroreneVersion,
    IReadOnlySet<Guid> MitgliederIds)
{
    public int AnzahlMitglieder => MitgliederIds.Count;
    public bool IstEingefroren => Status == DatensatzStatus.Eingefroren;
}

/// <summary>
/// Sammel-Ziel-Zustand am geteilten Store — dieselbe Notion wie <c>AktiveBereiche</c>:
/// clientseitig, cursor-nah, von allen Flächen (Galerie/Einbild/Leiste) geteilt.
///
/// Der Datensatz wird als „Sammel-Ziel" aktiviert, indem er ausgewählt wird
/// (<see cref="DatensatzAusgewaehlt"/> — dasselbe Ereignis, das die Verwalten-Sicht und
/// die Sidebar nutzen: es gibt genau EIN aktives Ziel). Die konkreten Mitglieder kommen
/// per <see cref="DatensatzAntwort"/> nach (der Komposition-RefreshHandler holt sie schon);
/// die Paar-Events patchen die Menge optimistisch für sofortiges Badge-Feedback.
/// </summary>
public partial class Store
{
    [ObservableProperty] private SammelZielKontext? _sammelZiel;

    /// <summary>O(1): liegt dieses Bildpaar im aktiven Sammel-Ziel?</summary>
    public bool IstMitglied(Guid pairId) => SammelZiel?.MitgliederIds.Contains(pairId) ?? false;

    // ── Auswahl = Sammel-Ziel aktivieren ──
    void Handle(DatensatzAusgewaehlt evt, MessageContext ctx)
    {
        if (SammelZiel?.Id == evt.DatensatzId) return;
        // Leeren Kontext scharf stellen; Kopf/Mitglieder kommen per HoleDatensatz nach.
        SammelZiel = new SammelZielKontext(
            evt.DatensatzId, null, DatensatzStatus.Entwurf, 0, new HashSet<Guid>());
    }

    // ── Kopf-Antwort des aktiven Ziels: Mitglieder + Status übernehmen ──
    void Handle(DatensatzAntwort a, MessageContext ctx)
    {
        if (SammelZiel is not { } ziel || a.Id != ziel.Id) return;
        SammelZiel = ziel with
        {
            Name = a.Name,
            Status = a.Status,
            EingefroreneVersion = a.EingefroreneVersion,
            MitgliederIds = new HashSet<Guid>(a.Mitglieder),
        };
    }

    // ── Optimistische Tag-Reaktion (sofortiges Badge-Feedback, bevor der Refresh nachzieht) ──
    void Handle(PaarAufgenommen evt, MessageContext ctx) => PatchMitglied(ctx.AggregateId, evt.ImagePairId, drin: true);
    void Handle(PaarEntfernt evt, MessageContext ctx)    => PatchMitglied(ctx.AggregateId, evt.ImagePairId, drin: false);

    void Handle(PaareAufgenommen evt, MessageContext ctx)
    {
        if (SammelZiel is not { } ziel || ctx.AggregateId != ziel.Id) return;
        var set = new HashSet<Guid>(ziel.MitgliederIds);
        foreach (var id in evt.ImagePairIds) set.Add(id);
        SammelZiel = ziel with { MitgliederIds = set };
    }

    private void PatchMitglied(Guid datensatzId, Guid pairId, bool drin)
    {
        if (SammelZiel is not { } ziel || datensatzId != ziel.Id) return;
        var set = new HashSet<Guid>(ziel.MitgliederIds);
        if (drin) set.Add(pairId); else set.Remove(pairId);
        SammelZiel = ziel with { MitgliederIds = set };
    }
}
