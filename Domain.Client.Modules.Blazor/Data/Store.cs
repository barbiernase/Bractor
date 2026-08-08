using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Collections;
using Domain.ImagePair;
using Domain.Projections;
namespace Domain.Client.Modules;

public partial class Store : StoreBase, IHydrationStore
{
    /// <summary>Virtualisiertes Fenster über die gesamte (virtuelle) Paarliste.</summary>
    public VirtualCollection<ImagePairRecord, Guid> VirtualImagePairs { get; }
    public Cursor<Guid> Cursor { get; }

    public ArbeitsModus Modus { get; set; } = ArbeitsModus.Live;

    /// <summary>Label-/Inspektions-Filter (Von/Bis bleiben hier null, sobald Bereiche aktiv sind).</summary>
    public SuchKriterien Suche { get; set; } = SuchKriterien.Leer;

    /// <summary>
    /// Die aktuell im Baum gewählten, zu minimalen zusammenhängenden Bereichen
    /// gefalteten Tage. Leer = keine Baum-Auswahl → unbegrenzte, lazy-paginierte
    /// Einzel-Query. Nicht leer → je Bereich EINE volle Query, danach Merge.
    /// </summary>
    public IReadOnlyList<Bereich> AktiveBereiche { get; private set; } = System.Array.Empty<Bereich>();

    public bool IstLive => Modus == ArbeitsModus.Live;

    // ── Sammel-Zustand für den Multi-Query-Merge ──
    private const int VollLadeSeitenGroesse = 100_000;   // ein Bereich in einer Antwort
    private readonly List<ImagePairAntwort> _puffer = new();
    private int _offeneAntworten;
    private bool _sammelModus;

    public Store()
    {
        VirtualImagePairs = Track(new VirtualCollection<ImagePairRecord, Guid>(
            keyOf:     r => r.Id,
            baueQuery: (seite, groesse) => new SucheImagePairs(BaueFilter(seite, groesse))));
        Cursor = Track(new Cursor<Guid>());
    }

    /// <summary>
    /// Unbegrenzter/lazy Filter (nur wenn KEINE Bereiche aktiv sind). Von/Bis aus
    /// Suche (i.d.R. null), plus Label-/Inspektionsfilter. Wird von der baueQuery-
    /// Closure für die Seiten-Nachforderung benutzt.
    /// </summary>
    public ImagePairFilter BaueFilter(int seite, int groesse)
        => new(
            Von:                 Suche.Von,
            Bis:                 Suche.Bis,
            ProduktLabel:        Suche.ProduktLabel,
            HatMenschLabel:      Suche.HatMenschLabel,
            NurNichtInspizierte: Suche.NurNichtInspizierte ? true : (bool?)null,
            Seite:               seite,
            SeitenGroesse:       groesse);

    /// <summary>
    /// Voll-Lade-Filter für EINEN Bereich: dessen Von/Bis + die aktiven Label-/
    /// Inspektionsfilter, in einer einzigen großen Seite. Der RefreshHandler baut
    /// daraus je Bereich eine Query.
    /// </summary>
    public ImagePairFilter BaueBereichsFilter(Bereich b)
        => new(
            Von:                 b.Von,
            Bis:                 b.Bis,
            ProduktLabel:        Suche.ProduktLabel,
            HatMenschLabel:      Suche.HatMenschLabel,
            NurNichtInspizierte: Suche.NurNichtInspizierte ? true : (bool?)null,
            Seite:               1,
            SeitenGroesse:       VollLadeSeitenGroesse);

    /// <summary>Fenster leeren und Sammel-Zähler für die kommenden N Antworten scharf stellen.</summary>
    private void NeuLaden()
    {
        VirtualImagePairs.Reset();
        _puffer.Clear();
        _offeneAntworten = AktiveBereiche.Count;
        _sammelModus = AktiveBereiche.Count > 0;   // 0 Bereiche → Normalpfad (unbegrenzt/lazy)
    }
}
