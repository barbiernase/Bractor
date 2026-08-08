using Client.Infrastructure.Abstractions;
namespace Domain.Client.Modules.Suche;

/// <summary>
/// Anzeige-Grenzen für den Produktionstage-Baum — bewusst NICHT der Bildfilter.
/// Schneidet nur ein Fenster aus der bereits geladenen Tagesliste heraus und
/// löst niemals eine Query aus. Bis ist exklusiv (konsistent mit SuchKriterien).
/// </summary>
public record BaumGrenzen(DateTimeOffset? Ab = null, DateTimeOffset? Bis = null)
{
    /// <summary>Keine Einschränkung — der gesamte Baum.</summary>
    public static readonly BaumGrenzen Alles = new();

    public bool Enthaelt(DateTimeOffset datum)
        => (Ab is null || datum >= Ab) && (Bis is null || datum < Bis);

    /// <summary>Nur das laufende Kalenderjahr.</summary>
    public static BaumGrenzen DiesesJahr(DateTimeOffset jetzt)
    {
        var ab = new DateTimeOffset(new DateTime(jetzt.Year, 1, 1), TimeSpan.Zero);
        return new(ab, ab.AddYears(1));
    }

    /// <summary>Die letzten N Monate bis einschließlich heute.</summary>
    public static BaumGrenzen LetzteMonate(int monate, DateTimeOffset jetzt)
    {
        var bis = new DateTimeOffset(jetzt.Date, TimeSpan.Zero).AddDays(1);
        return new(bis.AddMonths(-monate), bis);
    }
}

/// <summary>
/// Rein clientseitig: setzt die Anzeige-Grenzen des Baums. KEIN Reload, KEINE
/// Query — deshalb reagiert auch kein RefreshHandler darauf, nur der SuchStore.
/// </summary>
public record BaumGrenzenGeaendert(BaumGrenzen Grenzen) : IClientEvent;
