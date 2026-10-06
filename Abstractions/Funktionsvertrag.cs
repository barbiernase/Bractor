namespace Abstractions;

/// <summary>
/// Der NAME, über den der Framework-Generator eine Katalog-Funktion (<c>IFunktion</c>) aufruft: ihre eine Methode
/// <see cref="Methode"/>. Die EINE Quelle dieser Regel — der <c>FunktionsGenerator</c> (per Link eingebunden, netstandard2.0)
/// und der Domänen-Extractor lesen sie von hier, statt sie je für sich zu kennen (wie <see cref="Aggregatvertrag"/>).
/// </summary>
public static class Funktionsvertrag
{
    public const string Methode = "RufeAsync";
}
