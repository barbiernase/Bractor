using Abstractions;

namespace Domain.Vorgang;

/// <summary>
/// Prozess B der Verkettungs-Demo — die eigentliche PROZESS-VERKETTUNG (Zielbild §11). Sein AUSLÖSER ist das
/// terminale Domänen-Event von Prozess A (<see cref="VorgangGenehmigt"/>). Weil das ein persistiertes
/// Domänen-Event ist (kein internes Manager-Log-Event wie <c>ProzessBeendet</c>, dessen Signal inert wäre),
/// trägt es ein echtes Signal — der Korrelations-Router startet daraus B als EIGENE, frisch korrelierte
/// Instanz und aktiviert den Vorgang. Kein Framework-Eingriff nötig: Verkettung fällt aus der bestehenden
/// Auslöser-Erkennung.
/// </summary>
public sealed class AktivierungsProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<VorgangGenehmigt>.Definiere(p =>
    {
        p.Auf<VorgangGenehmigt>()
            .Sende<Aktiviere>(e => new Aktiviere(e.VorgangId));
    });
}
