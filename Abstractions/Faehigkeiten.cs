namespace Abstractions;

/// <summary>
/// Die Quelle, aus der ein GENERIERTER Dispatch die Fähigkeits-Parameter eines Handles auflöst
/// (<see cref="IWriteStore"/>/<see cref="IReadStore"/>-Interfaces). Der Entwickler-Code sieht sie nie:
/// ein Handle bekommt nur die Fähigkeiten, die in seiner Signatur stehen — die Obergrenze ist ein Typ,
/// kein Rumpf-Fakt. Innerhalb EINES Bereichs liefert jede Fähigkeit eines Stores dieselbe Instanz
/// (Co-Commit: Effekte + Marke in einer Transaktion).
/// </summary>
public interface IFaehigkeiten
{
    T Hole<T>() where T : class;
}

/// <summary>Ein Auflöse-Bereich (je Pull-Actor, je Pipeline-Actor, je Query) — beim Ende freigeben.</summary>
public interface IFaehigkeitsBereich : IFaehigkeiten, IDisposable { }

/// <summary>Öffnet Bereiche. Die Infrastruktur implementiert sie über den DI-Container.</summary>
public interface IFaehigkeitsFabrik
{
    IFaehigkeitsBereich Oeffne();
}

/// <summary>
/// Fähigkeiten aus fertigen Instanzen (Tests, Simulation): <c>Hole&lt;T&gt;</c> nimmt die erste Instanz,
/// die <typeparamref name="T"/> implementiert — ein Typtest, keine Reflection.
/// </summary>
public sealed class FaehigkeitenAus : IFaehigkeitsBereich
{
    private readonly object[] _instanzen;

    public FaehigkeitenAus(params object[] instanzen) => _instanzen = instanzen;

    public T Hole<T>() where T : class
    {
        foreach (var i in _instanzen)
            if (i is T t) return t;
        throw new InvalidOperationException($"Keine Instanz für die Fähigkeit {typeof(T).Name}.");
    }

    public void Dispose() { }
}
