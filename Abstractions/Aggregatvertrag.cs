namespace Abstractions;

/// <summary>
/// Die NAMEN, über die der Framework-Generator ein Aggregat verdrahtet: die inneren Klassen
/// <see cref="Decider"/>/<see cref="Applier"/> eines <see cref="IState"/> und ihre Methoden
/// <see cref="Decide"/>/<see cref="Apply"/>. Die EINE Quelle dieser Regel — der Generator (per Link
/// eingebunden, netstandard2.0) und der Domänen-Extractor lesen sie von hier, statt sie je für sich zu kennen.
/// </summary>
public static class Aggregatvertrag
{
    public const string Decider = "Decider";
    public const string Applier = "Applier";
    public const string Decide = "Decide";
    public const string Apply = "Apply";
}
