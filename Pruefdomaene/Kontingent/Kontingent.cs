using Abstractions;

namespace Pruefdomaene.Kontingent;

/// <summary>Aggregat <see cref="Kontingent"/> — eine Zahl freier Plätze; belegt vom Auftrag (Zweig B neben der Reservierung).</summary>
public partial class Kontingent : IState
{
    public int Frei { get; set; }

    public bool Existiert => Version > 0;
}
