using Abstractions;

namespace Pruefdomaene.Import;

public record StarteImport(Id<Importauftrag> AggregateId, int Zeilen) : ICommand<Importauftrag>, ICreationCommand;
public record MeldeImportStand(Id<Importauftrag> AggregateId, int Zeilen) : ICommand<Importauftrag>;

/// <summary>Ein Client fordert einen Import an — übersetzt in den Eingang der Annahme (Katalog Pruefwerk).</summary>
public sealed record ImportAngefordert(int Zeilen) : ICommand;

/// <summary>Ein gestarteter Import ist zu laufen — die Nachricht der Sachbearbeitung, übersetzt in den Eingang des Zeilenlaufs.</summary>
public record ImportZuLaufen(Id<Importauftrag> Import, int Zeilen) : ICommand;
