using Abstractions;

namespace Infrastructure.Pruefstand.Tests;

/// <summary>
/// Das Ziel-Aggregat der Test-Welten, deren Commands kein eigenes Aggregat haben (sie prüfen Flüsse, Emitter, Dispatcher — nicht
/// Entscheidungen). Ein Command nennt sein Ziel im Typ (<see cref="ICommand{TAggregat}"/>, docs/10 §10.4); hier ist es dieses.
/// </summary>
public sealed class TestZiel : IState
{
    public int Version { get; set; }
    public Guid Id { get; set; }
}
