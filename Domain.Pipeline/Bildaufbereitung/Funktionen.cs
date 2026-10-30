using Abstractions;
using Domain.ImagePair;

namespace Domain.Pipeline.Bildaufbereitung;

// ═══════════════════════════════════════════════════
// FUNKTIONEN der Bildaufbereitung (docs/10 §10.3) — eine KETTE, je Schritt eine Funktion:
//
//   DateiErkannt ─▶ ƒ IBildEinlesen ─▶ BildEingelesen ─▶ ƒ IBildVerkleinerung ─▶ BildVerkleinert ─▶ ƒ IKontrastausgleich ─▶ VorschauErstellt
//   (─▶ ƒ Bildeingang ─▶ NimmBildAuf an das Bildpaar)
//
// Jeder Schritt ist für sich: er hört genau ein Ergebnis des vorigen, gibt genau sein eigenes aus, und sein Laufort steht für sich in der Umgebung
// (Funktionen:<F>:Laufort) — im Host OpenCV, draußen ein Python-Worker, der ihn trägt. Bilder reisen als Dateipfade: ein Ergebnis nennt die
// geschriebene Datei, nicht ihre Bytes. Jedes Ergebnis trägt die Datei der Quelle weiter (Name, Pfad, Größe, wann erkannt) — die Zugehörigkeit
// ist ein fachliches Feld (A2). Was herauskommt, hält der Funktions-Actor fest, bevor es wirkt.
// ═══════════════════════════════════════════════════

/// <summary>
/// Schritt 1 — eine erkannte Datei als Bild einlesen: ist sie lesbar, mit ihren Maßen; sonst endet die Kette hier (<see cref="BildNichtLesbar"/>).
/// </summary>
public interface IBildEinlesen : IFunktion
{
    Task<OneOf<BildEingelesen, BildNichtLesbar>> Auf(DateiErkannt e);
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
