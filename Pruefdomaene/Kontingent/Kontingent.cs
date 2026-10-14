using Abstractions;

namespace Pruefdomaene.Kontingent;

/// <summary>Aggregat <see cref="Kontingent"/> — eine Zahl freier Plätze; Zweig B des Diamanten im <c>AuftragsProzess</c>.</summary>
public partial class Kontingent : IState
{
    public int Frei { get; set; }

    public bool Existiert => Version > 0;
}
