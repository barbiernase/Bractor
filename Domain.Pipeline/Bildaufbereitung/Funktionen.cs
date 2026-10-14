using Abstractions;

namespace Domain.Pipeline.Bildaufbereitung;

// ═══════════════════════════════════════════════════
// KATALOG-FUNKTIONEN der Bildaufbereitung
//
// Jede Funktion: EIN Auftrag hinein, OneOf-Ergebnis-Events heraus (CQRS068). Sie kennt weder Aggregat noch Prozess —
// ein Fluss ruft sie mit Rufe<F>, die Implementierung (OpenCV, Python, extern) bindet die Umgebung (Funktionen:<F>:Laufort).
// Bilder reisen als Dateipfade: das Ergebnis nennt die geschriebene Datei, nicht ihre Bytes.
// ═══════════════════════════════════════════════════

/// <summary>Bild auf eine Zielhöhe verkleinern (Seitenverhältnis bleibt).</summary>
public interface IBildVerkleinerung : IFunktion
{
    Task<OneOf<BildVerkleinert, BildNichtLesbar>> RufeAsync(VerkleinereBild auftrag, IAusfuehrung x);
}

/// <summary>Histogramm-Ausgleich (Kontrast) und als Vorschau-PNG ablegen.</summary>
public interface IHistogrammAusgleich : IFunktion
{
    Task<OneOf<HistogrammAusgeglichen, BildNichtLesbar>> RufeAsync(GleicheHistogrammAus auftrag, IAusfuehrung x);
}
