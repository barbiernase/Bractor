using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Client.Modules.Datensaetze;   // DatensatzAusgewaehlt
using Domain.Client.Modules.Kuratieren;    // Markierungs-Intents
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
    IReadOnlySet<Guid> MitgliederIds,
    IReadOnlySet<Guid> AusgeschlossenIds)
{
    public int AnzahlMitglieder => MitgliederIds.Count;
    public int AnzahlAusgeschlossen => AusgeschlossenIds.Count;
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

    /// <summary>Dedizierte Mehrfach-Auswahl in der Galerie (Kachel-Häkchen) — Konzept §5.</summary>
    [ObservableProperty] private IReadOnlySet<Guid> _markierung = new HashSet<Guid>();

    /// <summary>Rückwärts-Tags des betrachteten Paars: „in Datensätzen: …" (Konzept §4.3).</summary>
    [ObservableProperty] private IReadOnlyList<DatensatzTag> _paarTags = System.Array.Empty<DatensatzTag>();

    /// <summary>Galerie-Fokus auf das Sammel-Ziel (Aus/Mitglieder/Ausgeschlossen).</summary>
    public DatensatzGalerieModus DatensatzGalerieModus { get; private set; } = DatensatzGalerieModus.Aus;

    // Das geteilte Bild-Fenster auf den Datensatz scopen (oder wieder freigeben) + neu laden.
    // Der PaarlisteRefreshHandler feuert danach die passende Query (BaueFensterQuery).
    void Handle(DatensatzGalerieModusGesetzt evt, MessageContext ctx)
    {
        var neu = evt.Modus == DatensatzGalerieModus.Aus || SammelZiel is not null
            ? evt.Modus
            : DatensatzGalerieModus.Aus;   // ohne Sammel-Ziel gibt es nichts zu scopen
        if (neu == DatensatzGalerieModus) return;
        DatensatzGalerieModus = neu;
        OnPropertyChanged(nameof(DatensatzGalerieModus));
        NeuLaden();
    }

    /// <summary>O(1): liegt dieses Bildpaar im aktiven Sammel-Ziel?</summary>
    public bool IstMitglied(Guid pairId) => SammelZiel?.MitgliederIds.Contains(pairId) ?? false;

    /// <summary>O(1): ist dieses Bildpaar dediziert markiert?</summary>
    public bool IstMarkiert(Guid pairId) => Markierung.Contains(pairId);

    // ── Markierungs-Reducer (reine Client-Sicht) ──
    void Handle(MarkierungGetoggelt evt, MessageContext ctx)
    {
        var set = new HashSet<Guid>(Markierung);
        if (!set.Add(evt.PairId)) set.Remove(evt.PairId);
        Markierung = set;
    }

    void Handle(MarkierungBereichHinzugefuegt evt, MessageContext ctx)
    {
        if (evt.PairIds.Count == 0) return;
        var set = new HashSet<Guid>(Markierung);
        foreach (var id in evt.PairIds) set.Add(id);
        Markierung = set;
    }

    void Handle(MarkierungGeleert evt, MessageContext ctx)
    {
        if (Markierung.Count == 0) return;
        Markierung = new HashSet<Guid>();
    }

    // ── Rückwärts-Tags des Cursor-Paars ──
    void Handle(DatensaetzeFuerPaar a, MessageContext ctx)
    {
        // Späte Antwort verwerfen, wenn der Cursor inzwischen weitergezogen ist.
        if (Cursor.Id is { } cur && a.ImagePairId != cur) return;
        PaarTags = a.Tags;
    }

    // ── Auswahl = Sammel-Ziel aktivieren ──
    void Handle(DatensatzAusgewaehlt evt, MessageContext ctx)
    {
        if (SammelZiel?.Id == evt.DatensatzId) return;
        // Leeren Kontext scharf stellen; Kopf/Mitglieder kommen per HoleDatensatz nach.
        SammelZiel = new SammelZielKontext(
            evt.DatensatzId, null, DatensatzStatus.Entwurf, 0, new HashSet<Guid>(), new HashSet<Guid>());
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
            AusgeschlossenIds = new HashSet<Guid>(a.Ausgeschlossen),
        };
    }

    // ── Optimistische Tag-Reaktion (sofortiges Badge-Feedback, bevor der Refresh nachzieht) ──
    void Handle(PaarAufgenommen evt, MessageContext ctx) => PatchMitglied(ctx.AggregateId, evt.ImagePairId, drin: true);
    void Handle(PaarEntfernt evt, MessageContext ctx)    => PatchMitglied(ctx.AggregateId, evt.ImagePairId, drin: false);

    void Handle(PaareAufgenommen evt, MessageContext ctx)
    {
        if (SammelZiel is not { } ziel || ctx.AggregateId != ziel.Id) return;
        var mit = new HashSet<Guid>(ziel.MitgliederIds);
        var aus = new HashSet<Guid>(ziel.AusgeschlossenIds);
        foreach (var id in evt.ImagePairIds) { mit.Add(id); aus.Remove(id); }
        SammelZiel = ziel with { MitgliederIds = mit, AusgeschlossenIds = aus };
    }

    private void PatchMitglied(Guid datensatzId, Guid pairId, bool drin)
    {
        if (SammelZiel is not { } ziel || datensatzId != ziel.Id) return;
        var mit = new HashSet<Guid>(ziel.MitgliederIds);
        var aus = new HashSet<Guid>(ziel.AusgeschlossenIds);
        if (drin) { mit.Add(pairId); aus.Remove(pairId); }
        else      { mit.Remove(pairId); aus.Add(pairId); }
        SammelZiel = ziel with { MitgliederIds = mit, AusgeschlossenIds = aus };
    }
}
