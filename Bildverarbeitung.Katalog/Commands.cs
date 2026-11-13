using Abstractions;

namespace Bildverarbeitung.Katalog;

public record ResizeFastAuftrag() : ICommand;

public record ResizeFastErgebnis() : ICommand;
