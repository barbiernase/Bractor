using Abstractions;

namespace Pruefwerk.Katalog.Import;

/// <summary>Ein Import ist angefordert — so viele Zeilen.</summary>
public record Anfordern(int Menge) : ICommand;

/// <summary>Der Import ist angenommen: ein neuer Vorgang (eine Id, aus dem Eingang abgeleitet).</summary>
public record Angenommen(Guid Vorgang, int Menge) : ICommand;

/// <summary>Ein Vorgang ist zu laufen — so viele Zeilen.</summary>
public record Laufen(Guid Vorgang, int Menge) : ICommand;

/// <summary>Eine Zeile ist gelesen: eine neue Id (aus dem Eingang und der Zeile) und ihr Wert.</summary>
public record Gelesen(Guid Id, int Wert) : ICommand;

/// <summary>Der Stand eines Vorgangs: so viele Zeilen gelesen.</summary>
public record Stand(Guid Vorgang, int Gelesen) : ICommand;
