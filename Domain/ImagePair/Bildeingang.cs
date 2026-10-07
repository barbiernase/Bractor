using Abstractions;
using Domain.Bildaufbereitung;

namespace Domain.ImagePair;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// BILDEINGANG als Pipeline-Fluss (docs/konzept-editor-pipelines.md §14): eine Datei kommt an → der Name wird gedeutet → das Paar
// wird angelegt und, parallel dazu, das Bild verkleinert und im Kontrast ausgeglichen → am Ende meldet ImagePair das Bild verfügbar.
// Kein Umweg mehr über das Aggregat, um die Datei „ins Log zu bringen": die Quelle schreibt sie selbst (genau einmal je Datei),
// und erst die Commands führen in die Aggregat-Welt.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Eine stabile (fertig geschriebene) Datei auf dem Share — die Nachricht der Datei-Quelle. Kein Domänenwissen: kein PairKey, keine
/// Version; die Deutung ist ein eigener Schritt im Fluss. <see cref="Kennung"/> = Name + Größe: dieselbe Datei startet genau einen Vorgang.
/// </summary>
public sealed record DateiErkannt(string Pfad, string Dateiname, long DateigroesseBytes, DateTimeOffset ErkanntAm) : IQuellNachricht
{
    public string Kennung => $"{Dateiname}|{DateigroesseBytes}";
}

/// <summary>Den Dateinamen nach der Aufnahme-Konvention deuten ({Zeitstempel}_{Version}.tiff → Paar + Version).</summary>
public sealed record DeuteDateiname(string Dateiname, string Pfad) : IAuftrag<IDateinameDeutung>;

/// <summary>Der Name passt zur Konvention: Paar-Id (deterministisch aus dem PairKey), Version und Aufnahmezeit.</summary>
public record ImagePairDateiGedeutet(Guid PaarId, string PairKey, BildVersion Version, DateTimeOffset ProduziertAm, string Pfad) : IEvent;

/// <summary>Der Name passt nicht zur Konvention — die Datei gehört zu keinem Paar.</summary>
public record DateinameUnbekannt(string Dateiname) : IEvent;

public interface IDateinameDeutung : IFunktion
{
    Task<OneOf<ImagePairDateiGedeutet, DateinameUnbekannt>> RufeAsync(DeuteDateiname auftrag, IAusfuehrung x);
}

/// <summary>
/// Der Bildeingang des Kamerasystems. Die zweite Datei eines Paars findet das Paar schon vor: <c>ErstelleImagePair</c> wird dann
/// abgelehnt (✕-Port) — auch das ist ein Weg zur Meldung (∨). Liefert eine Bildfunktion <c>BildNichtLesbar</c>, endet der Vorgang
/// ohne Meldung (noch keine fachliche Antwort darauf).
/// </summary>
public sealed class Bildeingang : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var datei = p.Quelle<DateiErkannt>();
        var deuten = datei.Rufe<IDateinameDeutung>(datei => new DeuteDateiname(datei.Dateiname, datei.Pfad));
        var paar = p.Alle(deuten.Bei<ImagePairDateiGedeutet>(), datei).Sende<ErstelleImagePair>((deuten, datei) => new ErstelleImagePair(deuten.PaarId, deuten.PairKey, deuten.ProduziertAm, datei.ErkanntAm, datei.Pfad));
        var vorschau = deuten.Bei<ImagePairDateiGedeutet>().Rufe<IBildVerkleinerung>(deuten => new VerkleinereBild(deuten.Pfad, 512))
            .Zeitlimit(TimeSpan.FromMinutes(5));
        var kontrast = vorschau.Bei<BildVerkleinert>().Rufe<IHistogrammAusgleich>(vorschau => new GleicheHistogrammAus(vorschau.Pfad))
            .Zeitlimit(TimeSpan.FromMinutes(5));
        var melden = p.Alle(kontrast.Bei<HistogrammAusgeglichen>(), paar.Bei<ImagePairErstellt>(), deuten.Bei<ImagePairDateiGedeutet>(), datei).Sende<MeldeBildVerfuegbar>((kontrast, paar, deuten, datei) => new MeldeBildVerfuegbar(deuten.PaarId, deuten.Version, new BildMeta(datei.Dateiname, datei.DateigroesseBytes, kontrast.BreitePixel, kontrast.HoehePixel, kontrast.ErstelltAm), kontrast.Pfad))
            .Oder(p.Alle(kontrast.Bei<HistogrammAusgeglichen>(), paar.BeiAbgelehnt(), deuten.Bei<ImagePairDateiGedeutet>(), datei), (kontrast, paarAbgelehnt, deuten, datei) => new MeldeBildVerfuegbar(deuten.PaarId, deuten.Version, new BildMeta(datei.Dateiname, datei.DateigroesseBytes, kontrast.BreitePixel, kontrast.HoehePixel, kontrast.ErstelltAm), kontrast.Pfad));
    });
}
