namespace Pruefwerk.Katalog;

/// <summary>Die eine Umsetzung des Mahnversands: ein Brief, die Nummer aus Vorgang und Stufe.</summary>
public sealed class Briefversand : IMahnversand
{
    public Task<string> VersendeAsync(Guid empfaenger, Guid vorgang, int stufe) => Task.FromResult($"brief-{vorgang:N}-{stufe}");
}
