using Abstractions;

namespace Bildverarbeitung.Katalog.Resize;

public record ResizeFastAuftrag() : ICommand;

public record ResizeFastErgebnis() : ICommand;
