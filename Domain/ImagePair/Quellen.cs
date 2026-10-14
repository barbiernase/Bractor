using Abstractions;

namespace Domain.ImagePair;

/// <summary>
/// Eine fertig geschriebene Datei auf dem Share — die Quelle des Bildeingangs. Der Datei-Treiber befüllt alle Felder (Name, Pfad,
/// Größe, Kennung …); welches Verzeichnis, welche Muster und wann eine Datei fertig ist, sagt die Konfig (<c>Quellen:DateiErkannt</c>).
/// Kein Domänenwissen: die Deutung des Namens ist ein eigener Schritt im Fluss (<c>Domain.Pipeline.ImagePair.Bildeingang</c>).
/// </summary>
public sealed record DateiErkannt : DateiGemeldet;
