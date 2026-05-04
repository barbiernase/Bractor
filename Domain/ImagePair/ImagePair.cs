using Abstractions;

namespace Domain.ImagePair;

public partial class ImagePair : IState
{
    public string? PairKey { get; set; }
    public BildInfo? Dc0 { get; set; }
    public BildInfo? Dc2 { get; set; }

    // ═══════════════════════════════════════════════════
    // PAAR-EBENE
    // ═══════════════════════════════════════════════════

    public Klassifikation? KiBildpaarKlassifikation { get; set; }
    public Klassifikation? MenschBildpaarLabel { get; set; }
    public Klassifikation? PhysischesProduktLabel { get; set; }

    // ═══════════════════════════════════════════════════
    // INSPEKTION
    // ═══════════════════════════════════════════════════

    /// <summary>Wurde dieses Bildpaar von einem Menschen betrachtet?</summary>
    public bool IstInspiziert { get; set; }

    // ═══════════════════════════════════════════════════
    // ZEITSTEMPEL
    // ═══════════════════════════════════════════════════

    public DateTimeOffset ProduziertAm { get; set; }
    public DateTimeOffset AufgenommenAm { get; set; }

    // ═══════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════

    public bool IstKomplett => Dc0 != null && Dc2 != null;
    public string? UrsprungsPfad { get; set; }

    public BildInfo? GetBild(BildVersion version) => version switch
    {
        BildVersion.Dc0 => Dc0,
        BildVersion.Dc2 => Dc2,
        _ => null
    };
}