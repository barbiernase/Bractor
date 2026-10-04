using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// Die Befugnisse EINES Akteurs — generiert aus seiner Basisliste (<c>IDarf&lt;T&gt;</c>) plus den aus dem Graphen
/// abgeleiteten Hör-Rechten (<see cref="GeneratedAkteurRechte"/>). Keine Reflection, keine Namensregeln: der
/// Generator sortiert die <c>IDarf</c>-Typen schon nach Art.
/// </summary>
/// <param name="Name">Einfacher Typname des Akteurs (Schlüssel der Composition-Root-Abbildung).</param>
/// <param name="Typ">Der Akteur-Typ selbst.</param>
/// <param name="Commands">darf auslösen</param>
/// <param name="Queries">darf fragen — bzw. als Zuständiger beantworten, wenn das System sie nicht selbst bedient</param>
/// <param name="Trigger">darf starten — bzw. als Zuständiger verarbeiten, wenn keine Pipeline ihn bedient</param>
/// <param name="TransientEvents">darf veröffentlichen (verlierbare Hinweise vom Client)</param>
/// <param name="Hoert">abgeleitet: Events der Aggregate seiner Commands + Events der Projektionen hinter seinen Queries</param>
public sealed record AkteurRechte(
    string Name,
    Type Typ,
    IReadOnlySet<Type> Commands,
    IReadOnlySet<Type> Queries,
    IReadOnlySet<Type> Trigger,
    IReadOnlySet<Type> TransientEvents,
    IReadOnlySet<Type> Hoert)
{
    /// <summary>Darf der Akteur <paramref name="typ"/> hineingeben?</summary>
    public bool DarfHinein(Type typ) =>
        Commands.Contains(typ) || Queries.Contains(typ) || Trigger.Contains(typ) || TransientEvents.Contains(typ);

    /// <summary>Darf der Akteur das Event <paramref name="typ"/> abonnieren? <see cref="CommandFailed"/> immer —
    /// es ist die gezielte Rückmeldung auf seine eigenen Commands.</summary>
    public bool DarfHoeren(Type typ) => typ == typeof(CommandFailed) || Hoert.Contains(typ);
}
