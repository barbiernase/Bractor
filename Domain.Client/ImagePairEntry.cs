using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

public partial class ImagePairEntry : ObservableObject
{
    [ObservableProperty] private Guid _id;

    /// <summary>Produktionszeitpunkt — aus dem Dateinamen geparst.</summary>
    [ObservableProperty] private DateTimeOffset _produziertAm;

    /// <summary>Aufnahmezeitpunkt im System — wann die Pipeline verarbeitet hat.</summary>
    [ObservableProperty] private DateTimeOffset _aufgenommenAm;

    [ObservableProperty] private bool _istKomplett;
    [ObservableProperty] private string? _pairKey;
    [ObservableProperty] private string? _ursprungsPfad;

    // ── DC0 ──

    [ObservableProperty] private string? _dc0Pfad;
    [ObservableProperty] private Klassifikation? _dc0KiBildKlassifikation;
    [ObservableProperty] private Klassifikation? _dc0MenschBildLabel;
    [ObservableProperty] private string? _dc0BildSrc;

    public Klassifikation?[] Dc0KiRegionen { get; set; } = new Klassifikation?[8];
    public Klassifikation?[] Dc0MenschRegionen { get; set; } = new Klassifikation?[8];

    // ── DC2 ──

    [ObservableProperty] private string? _dc2Pfad;
    [ObservableProperty] private Klassifikation? _dc2KiBildKlassifikation;
    [ObservableProperty] private Klassifikation? _dc2MenschBildLabel;
    [ObservableProperty] private string? _dc2BildSrc;

    public Klassifikation?[] Dc2KiRegionen { get; set; } = new Klassifikation?[8];
    public Klassifikation?[] Dc2MenschRegionen { get; set; } = new Klassifikation?[8];

    // ── Paar-Ebene ──

    [ObservableProperty] private Klassifikation? _kiBildpaarKlassifikation;
    [ObservableProperty] private Klassifikation? _menschBildpaarLabel;
    [ObservableProperty] private Klassifikation? _physischesProduktLabel;

    // ── Inspektion ──

    [ObservableProperty] private bool _istInspiziert;

    // ── Historie ──

    /// <summary>
    /// Timeline dieses ImagePairs — wird aus der Server-Query befüllt
    /// und durch Live-Events ergänzt.
    ///
    /// ObservableCollection damit die UI automatisch aktualisiert.
    /// Wird bei Paar-Wechsel geleert und neu geladen.
    /// </summary>
    public ObservableCollection<HistorieEintrag> Historie { get; } = new();

    /// <summary>True wenn die Historie vom Server geladen wurde.</summary>
    [ObservableProperty] private bool _historieGeladen;

    // ── Berechnete Properties ──

    public bool HatKiKlassifikation =>
        Dc0KiBildKlassifikation != null || Dc2KiBildKlassifikation != null;

    public bool HatMenschLabel =>
        Dc0MenschBildLabel != null || Dc2MenschBildLabel != null;

    public bool IstVollstaendigKlassifiziert =>
        HatKiKlassifikation && HatMenschLabel && PhysischesProduktLabel != null;

    // ── Hilfsmethoden ──

    public Klassifikation?[] GetKiRegionen(BildVersion version) => version switch
    {
        BildVersion.Dc0 => Dc0KiRegionen,
        BildVersion.Dc2 => Dc2KiRegionen,
        _ => Dc0KiRegionen
    };

    public Klassifikation?[] GetMenschRegionen(BildVersion version) => version switch
    {
        BildVersion.Dc0 => Dc0MenschRegionen,
        BildVersion.Dc2 => Dc2MenschRegionen,
        _ => Dc0MenschRegionen
    };
}