using Abstractions;
using Domain.Datensatz;
using Domain.ImagePair;      // Klassifikation
using Domain.Projections;    // IImagePairReadStore, ImagePairFilter, ImagePairReadModel
using Microsoft.Extensions.Logging;

namespace Domain.Pipeline.Datensatz;

/// <summary>
/// Server-seitiger Resolver des Datensatz-Kontexts (Konzept §3.2/§11, Variante A). Er löst die
/// zwei I/O-behafteten Halbschritte auf, die der reine Decider bewusst nicht selbst tut:
///
///   1. <c>RangeAngefordert</c> → <see cref="IImagePairReadStore.SearchAsync"/> über den 1:1 aus
///      <see cref="RangeKriterien"/> gebauten <see cref="ImagePairFilter"/> (durchpaginiert) →
///      <c>NimmRangeAuf(ids, herkunft)</c>.
///   2. <c>EinfrierenAngefordert</c> → je (autoritativem) Mitglied den Label-Stand + die Bildpfade
///      aus dem ImagePair-Read-Store lesen, den Split deterministisch zuteilen
///      (<see cref="SplitZuteiler"/>) → <c>SchliesseEinfrierenAb(mitglieder)</c>.
///
/// Reines Event→Command-Muster wie <c>ImageProcessingPipeline</c> — kein neuer Transport, kein
/// neuer Konsumenten-Typ. Mitgliedschaft + Split-Konfig kommen autoritativ aus dem Event
/// (Aggregat-State), NICHT aus dem abgeleiteten Read-Model (Invariante 1); nur der Label-Stand
/// ist ein Snapshot des Read-Models — genau das meint „Label-Stand zum Einfrier-Zeitpunkt".
/// </summary>
public partial class DatensatzResolverPipeline : IPipelineHandler
{
    private readonly IImagePairReadStore _imagePairStore;
    private readonly ILogger<DatensatzResolverPipeline> _logger;

    private const int SeitenGroesse = 500;

    public DatensatzResolverPipeline(
        IImagePairReadStore imagePairStore,
        ILogger<DatensatzResolverPipeline> logger)
    {
        _imagePairStore = imagePairStore;
        _logger = logger;
    }

    public string PipelineId => "datensatz-resolver";

    // ═══════════════════════════════════════════════════════════
    // RANGE AUFLÖSEN — Suche → konkrete IDs
    // ═══════════════════════════════════════════════════════════

    public async IAsyncEnumerable<ICommand> Handle(RangeAngefordert evt, PipelineContext ctx)
    {
        var datensatzId = ctx.SourceAggregateId!.Value;

        var ids = new List<Guid>();
        var seite = 1;
        while (true)
        {
            var filter = ToFilter(evt.Kriterien, seite, SeitenGroesse);
            var (items, gesamt) = await _imagePairStore.SearchAsync(filter);
            ids.AddRange(items.Select(m => m.Id));
            if (items.Count == 0 || ids.Count >= gesamt) break;
            seite++;
        }

        if (ids.Count == 0)
        {
            _logger.LogInformation(
                "Datensatz-Resolver: Range für {Id} traf 0 Paare — nichts aufzunehmen", datensatzId);
            yield break;
        }

        _logger.LogInformation(
            "Datensatz-Resolver: Range für {Id} → {Count} Paare aufgelöst", datensatzId, ids.Count);

        yield return new NimmRangeAuf(datensatzId, ids, new RangeHerkunft(evt.Kriterien, ids.Count));
    }

    // ═══════════════════════════════════════════════════════════
    // EINFRIEREN — Label-Stand + Pfade snapshotten, Split zuteilen
    // ═══════════════════════════════════════════════════════════

    public async IAsyncEnumerable<ICommand> Handle(EinfrierenAngefordert evt, PipelineContext ctx)
    {
        var datensatzId = ctx.SourceAggregateId!.Value;

        // 1) Read-Model je Mitglied laden (Label-Stand + Pfade zum Einfrier-Zeitpunkt).
        var modelle = new Dictionary<Guid, ImagePairReadModel>();
        foreach (var pid in evt.Mitglieder)
        {
            var ip = await _imagePairStore.FindByIdAsync(pid);
            if (ip is not null) modelle[pid] = ip;
            else _logger.LogWarning(
                "Datensatz-Resolver: ImagePair {Pid} fehlt im Read-Model — beim Einfrieren übersprungen", pid);
        }

        if (modelle.Count == 0)
        {
            _logger.LogWarning(
                "Datensatz-Resolver: kein Mitglied von {Id} auflösbar — Einfrieren wird nicht abgeschlossen",
                datensatzId);
            yield break;
        }

        // 2) Split deterministisch & stratifiziert (Konfig kam autoritativ mit dem Event).
        var labels = modelle.Select(kv => (kv.Key, BestimmeLabel(kv.Value))).ToList();
        var splits = SplitZuteiler.Zuteilen(labels, evt.Split);

        // 3) Immutablen Mitglieder-Snapshot zusammensetzen (stabile Reihenfolge).
        var mitglieder = modelle
            .OrderBy(kv => kv.Key)
            .Select(kv => new DatensatzMitglied(
                ImagePairId: kv.Key,
                Label: BestimmeLabel(kv.Value),
                Split: splits[kv.Key],
                Dc0Pfad: kv.Value.Dc0Pfad ?? "",
                Dc2Pfad: kv.Value.Dc2Pfad ?? ""))
            .ToList();

        _logger.LogInformation(
            "Datensatz-Resolver: {Id} eingefroren mit {Count} Mitgliedern", datensatzId, mitglieder.Count);

        yield return new SchliesseEinfrierenAb(datensatzId, mitglieder);
    }

    // ═══════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════

    private static ImagePairFilter ToFilter(RangeKriterien k, int seite, int seitenGroesse) => new(
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

    /// <summary>
    /// Das Trainings-Label eines Paares: das physische Produkt-Label ist die stärkste Wahrheit,
    /// danach das menschliche Bildpaar-Label, zuletzt die KI-Klassifikation.
    /// </summary>
    private static Klassifikation BestimmeLabel(ImagePairReadModel m) =>
        m.PhysischesProduktLabel
        ?? m.MenschBildpaarLabel
        ?? m.KiBildpaarKlassifikation
        ?? Klassifikation.KeineAnomalie;
}
