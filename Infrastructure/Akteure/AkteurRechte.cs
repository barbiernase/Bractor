using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// Die Befugnisse EINES Akteurs — generiert aus seiner Basisliste (<c>IDarf&lt;T&gt;</c>, <c>IHoert&lt;T&gt;</c>, <see cref="GeneratedAkteurRechte"/>).
/// Keine Reflection, keine Namensregeln: der Generator sortiert die Typen schon nach Art.
/// </summary>
/// <param name="Name">Einfacher Typname des Akteurs (Schlüssel der Composition-Root-Abbildung).</param>
/// <param name="Typ">Der Akteur-Typ selbst.</param>
/// <param name="Commands">darf auslösen (<c>IDarf</c>): Commands an Aggregate und Commands ohne Ziel an den Kopf eines Graphen — von sich aus oder
/// als Reaktion auf ein Fakt, das er hört</param>
/// <param name="Queries">darf fragen</param>
/// <param name="TransientEvents">darf veröffentlichen (verlierbare Hinweise vom Client)</param>
/// <param name="Hoert">was er hört (<c>IHoert</c> eines Events oder Hinweises): gepusht, Events auch im Log lesbar</param>
public sealed record AkteurRechte(
    string Name,
    Type Typ,
    IReadOnlySet<Type> Commands,
    IReadOnlySet<Type> Queries,
    IReadOnlySet<Type> TransientEvents,
    IReadOnlySet<Type> Hoert)
{
    /// <summary>Der Client der Sitzung (<c>IClient</c>) — null bei einem Token ohne Client.</summary>
    public Type? VertragTyp { get; init; }

    /// <summary>Hash der kanonischen Form des Clients (<see cref="Clientvertragsregeln.ClientKanon"/>) — der Client meldet ihn am Handshake mit.</summary>
    public string VertragHash { get; init; } = "";

    /// <summary>
    /// Verkörpert eine Session MEHRERE Akteure (eine Person ist Inspekteur und KIOperator — ein Token, eine Akteur-Menge),
    /// stehen hier die einzelnen; die Mengen oben sind dann ihre Vereinigung (<see cref="Vereinige"/>).
    /// </summary>
    public IReadOnlyList<AkteurRechte> Teile { get; init; } = [];

    /// <summary>Die einzelnen Akteure dieser Session (bei einem einzelnen: er selbst).</summary>
    public IReadOnlyList<AkteurRechte> Einzelne => Teile.Count > 0 ? Teile : [this];

    /// <summary>Wer aus dieser Session gibt <paramref name="typ"/> hinein? Der (erste) Teil-Akteur, der ihn darf — sonst <see cref="Name"/>.
    /// Das ist der Akteur, den der Envelope trägt (und der durch die Kette reist).</summary>
    public string AkteurFuer(Type typ) => Teile.FirstOrDefault(t => t.DarfHinein(typ))?.Name ?? Name;

    /// <summary>Mehrere Akteure als eine Session-Befugnis: Vereinigung aller Mengen, Name „A+B".</summary>
    public static AkteurRechte Vereinige(IReadOnlyList<AkteurRechte> akteure)
    {
        if (akteure.Count == 1) return akteure[0];
        static HashSet<Type> U(IEnumerable<IReadOnlySet<Type>> s) => s.SelectMany(x => x).ToHashSet();
        return new AkteurRechte(string.Join("+", akteure.Select(a => a.Name)), typeof(IAkteur),
            U(akteure.Select(a => a.Commands)), U(akteure.Select(a => a.Queries)),
            U(akteure.Select(a => a.TransientEvents)), U(akteure.Select(a => a.Hoert)))
        { Teile = akteure };
    }

    /// <summary>Darf der Akteur <paramref name="typ"/> hineingeben?</summary>
    public bool DarfHinein(Type typ) =>
        Commands.Contains(typ) || Queries.Contains(typ) || TransientEvents.Contains(typ);

    /// <summary>Darf der Akteur das Event <paramref name="typ"/> hören? <see cref="CommandFailed"/> immer — es ist die gezielte Rückmeldung auf
    /// seine eigenen Commands.</summary>
    public bool DarfHoeren(Type typ) => typ == typeof(CommandFailed) || Hoert.Contains(typ);
}
