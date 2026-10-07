using Abstractions;
using Domain.Bildaufbereitung;

namespace Domain.ImagePair;

/// <summary>
/// Bildaufbereitung eines eingegangenen Rohbilds — als Kette von Katalog-Funktionen statt als Rumpf einer Pipeline:
/// <code>
///   RohbildEingegangen ─ƒ Verkleinern─▶ BildVerkleinert ─ƒ Histogramm─▶ HistogrammAusgeglichen ─▶ MeldeBildVerfuegbar
/// </code>
/// Je Rohbild eine Prozess-Instanz (Dc0 und Dc2 laufen parallel). Jeder Schritt ist ein Knoten im Graph/Editor; was er
/// rechnet, steckt in der gebundenen Implementierung (Host: AddFunktion). Die Paar-Id und die Datei-Metadaten reisen
/// über den Join mit dem Auslöser, nicht durch die Funktionen — die bleiben domänenfrei und wiederverwendbar.
/// <see cref="BildNichtLesbar"/> hat (noch) keine Folge-Regel: der Prozess endet dann ohne Verfügbar-Meldung, das Ergebnis
/// steht im Ausführungs-Stream. Eine fachliche Antwort darauf (z. B. „Bild unbrauchbar“ am Paar) wäre eine weitere Regel.
/// </summary>
public sealed class BildaufbereitungProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<RohbildEingegangen>.Definiere(p =>
    {
        p.Auf<RohbildEingegangen>()
         .Rufe<IBildVerkleinerung>(r => new VerkleinereBild(r.Pfad, 512))   // Vorschau-Höhe in Pixeln
         .Zeitlimit(TimeSpan.FromMinutes(5));

        p.Auf<BildVerkleinert>()
         .Rufe<IHistogrammAusgleich>(v => new GleicheHistogrammAus(v.Pfad))
         .Zeitlimit(TimeSpan.FromMinutes(5));

        p.Auf<RohbildEingegangen>().Und<HistogrammAusgeglichen>()
         .Sende<MeldeBildVerfuegbar>((r, h) => new MeldeBildVerfuegbar(
             r.AggregateId,
             r.Version,
             new BildMeta(r.Dateiname, r.DateigroesseBytes, h.BreitePixel, h.HoehePixel, h.ErstelltAm),
             h.Pfad));
    });
}
