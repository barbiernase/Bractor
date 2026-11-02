using Domain.Bildaufbereitung;
using Abstractions;
using Domain.ImagePair;
using OpenCvSharp;

namespace Domain.Pipeline.Bildaufbereitung;

// ═══════════════════════════════════════════════════
// FUNKTIONEN der Bildaufbereitung (docs/10 §10.3) — ein FORK-JOIN, je Schritt eine Funktion:
//
//   DateiGefunden ─▶ ƒ IBildEinlesen ─▶ BildEingelesen ─┬─▶ ƒ IBildVerkleinerung ─▶ BildVerkleinert ─▶ ƒ IKontrastausgleich ─▶ VorschauErstellt ─┐
//                                                     └──────────────────────────────────────────────────────────────────────────────────────┴─▶ ƒ Bildeingang
//   (ƒ Bildeingang ─▶ NimmBildAuf an das Bildpaar)
//
// Jeder Schritt ist für sich: er nimmt, was er braucht, gibt genau sein eigenes aus, und sein Laufort steht am Knoten der Instanz — im Host OpenCV,
// draußen ein Python-Worker, der ihn trägt. Bilder reisen als Dateipfade: ein Ergebnis nennt die geschriebene Datei, nicht ihre Bytes. Was zusammengehört,
// führt der Lauf zusammen (der Bildeingang nimmt das eingelesene Bild und seine Vorschau desselben Laufs). Die Schritte stehen vor dem Aggregat: die
// Quell-Nachricht ist ein durabler Eingang (genau einmal im Log), die Commands zwischen den Knoten gehen nie ins Log; erst der Bildeingang schickt
// einen Command an das Paar.
// ═══════════════════════════════════════════════════

/// <summary>
/// Schritt 1 — eine erkannte Datei als Bild einlesen: ist sie lesbar, mit ihren Maßen; sonst endet die Kette hier (<see cref="BildNichtLesbar"/>).
/// </summary>
public interface IBildEinlesen : IFunktion
{
    Task<OneOf<BildEingelesen, BildNichtLesbar>> Auf(DateiGefunden e);
}

/// <summary>Schritt 2 — das eingelesene Bild auf die Vorschau-Höhe verkleinern und als Zwischenbild ablegen.</summary>
public interface IBildVerkleinerung : IFunktion
{
    Task<BildVerkleinert> Auf(BildEingelesen e);
}

/// <summary>
/// Schritt 3 — das verkleinerte Bild im Kontrast ausgleichen und als Vorschau ablegen; der <see cref="Domain.Pipeline.ImagePair.Bildeingang"/>
/// macht daraus das Bild des Paars.
/// </summary>
public interface IKontrastausgleich : IFunktion
{
    Task<VorschauErstellt> Auf(BildVerkleinert e);
}
