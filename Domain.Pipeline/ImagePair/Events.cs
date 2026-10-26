using Abstractions;

namespace Domain.Pipeline.ImagePair;

/// <summary>Hinweis des Bildeingangs: der Name passt nicht zur Aufnahme-Konvention — die Datei gehört zu keinem Paar.</summary>
public record DateinameUnbekannt(string Dateiname) : ITransientEvent;
