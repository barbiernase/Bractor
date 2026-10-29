using Abstractions;
using Domain.ImagePair;

namespace Domain.Pipeline.Bildaufbereitung;

// ═══════════════════════════════════════════════════
// FUNKTIONEN der Bildaufbereitung (docs/10 §10.3)
//
// Eine Funktion als Schnittstelle läuft draußen ODER im Host mit einer Implementierung — welche, sagt die Umgebung (Funktionen:<F>:Laufort):
// im Host OpenCV (OpenCvBildaufbereitung), draußen ein Python-Worker, der sie trägt. Bilder reisen als Dateipfade: das Ergebnis nennt die
// geschriebene Datei, nicht ihre Bytes. Was herauskommt, hält der Funktions-Actor fest, bevor es wirkt.
// ═══════════════════════════════════════════════════

/// <summary>
/// Eine erkannte Datei aufbereiten: auf die Vorschau-Höhe verkleinern und im Kontrast ausgleichen; das Ergebnis nennt die Datei und ihre Vorschau
/// (die Zugehörigkeit ist ein fachliches Feld, A2) — der <see cref="Domain.Pipeline.ImagePair.Bildeingang"/> macht daraus das Bild des Paars.
/// </summary>
public interface IBildaufbereitung : IFunktion
{
    Task<OneOf<BildAufbereitet, BildNichtLesbar>> Auf(DateiErkannt e);
}
