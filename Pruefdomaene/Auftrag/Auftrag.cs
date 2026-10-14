using Abstractions;

namespace Pruefdomaene.Auftrag;

/// <summary>Aggregat <see cref="Auftrag"/> — sein Start löst den <see cref="AuftragsProzess"/> aus, die Bestätigung beendet ihn.</summary>
public partial class Auftrag : IState
{
    public bool Bestaetigt { get; set; }

    public bool Existiert => Version > 0;
}
