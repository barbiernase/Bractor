using Abstractions;

namespace Bildvorschau.Katalog;

// COMMANDS, die nur Knoten einander geben (docs/04 §4.4): Commands ohne Ziel, nie im Log.

/// <summary>
/// Der Kopf der Bildaufbereitung: eine fertig geschriebene Bilddatei ist zu lesen — Name, Pfad, Größe und wann sie erkannt wurde. Wer sie hineingibt,
/// sagt sein Akteur (etwa ein Kamerasystem, dessen Dateiwächter sie fand, über eine Übersetzung der Akteur-Seite).
/// </summary>
public record LiesBild(string Name, string Pfad, long Groesse, DateTimeOffset ErkanntAm) : ICommand;

/// <summary>Schritt 1: die erkannte Datei ist als Bild lesbar — die Datei (Name, Pfad, Größe, wann erkannt) und ihre Maße.</summary>
public record BildEingelesen(string Dateiname, string Pfad, long Groesse, DateTimeOffset ErkanntAm, int BreitePixel, int HoehePixel) : ICommand;

/// <summary>Schritt 2: das Bild ist auf die Vorschau-Höhe verkleinert — die Datei der Quelle und das Zwischenbild mit seinen Maßen.</summary>
public record BildVerkleinert(string Dateiname, string Pfad, long Groesse, DateTimeOffset ErkanntAm,
    string Verkleinert, int BreitePixel, int HoehePixel) : ICommand;

/// <summary>
/// Schritt 3: die Vorschau ist erstellt (verkleinert, im Kontrast ausgeglichen) — die Datei der Quelle und die Vorschau.
/// </summary>
public record VorschauErstellt(string Dateiname, string Pfad, long Groesse, DateTimeOffset ErkanntAm,
    string Vorschau, int BreitePixel, int HoehePixel, DateTimeOffset ErstelltAm) : ICommand;

/// <summary>
/// Schritt 4: der Name der Datei ist nach der Aufnahme-Konvention gedeutet (<see cref="Aufnahmename"/>) — das Paar (Guid aus dem Schlüssel), seine
/// Version und Aufnahmezeit, dazu die Datei der Quelle, ihre Metadaten und die Vorschau. Die Felder heißen, wie eine Domäne sie nimmt; was daraus wird,
/// entscheidet die Übersetzung des Ablaufs.
/// </summary>
public record BildGedeutet(Guid Paar, string PairKey, DateTimeOffset ProduziertAm, DateTimeOffset AufgenommenAm, string UrsprungsPfad, string Version,
    string OriginalDateiname, long DateigroesseBytes, int BreitePixel, int HoehePixel, DateTimeOffset ErstelltAm, string Pfad) : ICommand;

