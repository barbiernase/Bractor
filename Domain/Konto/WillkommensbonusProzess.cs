using Abstractions;

namespace Domain.Konto;

/// <summary>
/// Der Willkommensbonus-Prozess als typisierte Event→Command-Regel (Spec §3): wird ein Bonus fällig
/// (<see cref="WillkommensbonusFaellig"/>), schreibt eine Policy ihn automatisch gut — über das
/// bestehende <see cref="SchreibeGut"/>. Ein linearer 1-Hop-Saga; Entscheidung (Command) und Wirkung
/// (Gutschrift) sind bewusst entkoppelt. Kein Manager, keine Korrelation von Hand — die Abhängigkeit
/// IST der Event-Typ.
/// </summary>
public sealed class WillkommensbonusProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<WillkommensbonusFaellig>.Definiere(p =>
    {
        p.Auf<WillkommensbonusFaellig>()
            .Sende<SchreibeGut>(e => new SchreibeGut(e.Konto, e.Betrag));
    });
}
