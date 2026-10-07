using Abstractions;

namespace Domain.Bildaufbereitung;

// ═══════════════════════════════════════════════════
// KATALOG-FUNKTIONEN der Bildaufbereitung
//
// Jede Funktion: EIN Auftrag hinein, OneOf-Ergebnis-Events heraus (CQRS068). Sie kennt weder Aggregat noch Prozess —
// ein Prozess ruft sie mit Rufe<F>, die Implementierung (OpenCV, Python, extern) bindet der Host mit AddFunktion<F, Impl>.
// Bilder reisen als Dateipfade: das Ergebnis nennt die geschriebene Datei, nicht ihre Bytes.
// ═══════════════════════════════════════════════════

/// <summary>Bild auf eine Zielhöhe verkleinern (Seitenverhältnis bleibt).</summary>
public sealed record VerkleinereBild(string QuellPfad, int Hoehe) : IAuftrag<IBildVerkleinerung>;

public record BildVerkleinert(string Pfad, int BreitePixel, int HoehePixel) : IEvent;

/// <summary>Die Datei ließ sich nicht lesen/schreiben — Ergebnis beider Funktionen.</summary>
public record BildNichtLesbar(string Pfad, string Grund) : IEvent;

public interface IBildVerkleinerung : IFunktion
{
    Task<OneOf<BildVerkleinert, BildNichtLesbar>> RufeAsync(VerkleinereBild auftrag, IAusfuehrung x);
}

/// <summary>Histogramm-Ausgleich (Kontrast) und als Vorschau-PNG ablegen.</summary>
public sealed record GleicheHistogrammAus(string QuellPfad) : IAuftrag<IHistogrammAusgleich>;

public record HistogrammAusgeglichen(string Pfad, int BreitePixel, int HoehePixel, DateTimeOffset ErstelltAm) : IEvent;

public interface IHistogrammAusgleich : IFunktion
{
    Task<OneOf<HistogrammAusgeglichen, BildNichtLesbar>> RufeAsync(GleicheHistogrammAus auftrag, IAusfuehrung x);
}
