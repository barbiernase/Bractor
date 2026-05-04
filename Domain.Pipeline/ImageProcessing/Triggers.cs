using Abstractions;

namespace Domain.Pipeline.ImageProcessing;

/// <summary>
/// FileWatcher hat eine stabile Datei erkannt.
/// 
/// KEIN Domain-Wissen: kein PairId, keine BildVersion, kein Parsing.
/// Der FileWatcher weiß nichts über die Dateistruktur —
/// er erkennt nur, dass eine Datei stabil (fertig geschrieben) ist.
///
/// Die gesamte Interpretation passiert in der Pipeline.
/// </summary>
public record DateiErkannt(
    string Pfad,
    string Dateiname,
    long DateigroesseBytes
) : IPipelineTrigger;