using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// Die Befugnisse EINES Akteurs — generiert aus seiner Basisliste (<c>IDarf&lt;T&gt;</c>) plus den aus dem Graphen
/// abgeleiteten Hör-Rechten (<see cref="GeneratedAkteurRechte"/>). Keine Reflection, keine Namensregeln: der
/// Generator sortiert die <c>IDarf</c>-Typen schon nach Art.
/// </summary>
/// <param name="Name">Einfacher Typname des Akteurs (Schlüssel der Composition-Root-Abbildung).</param>
/// <param name="Typ">Der Akteur-Typ selbst.</param>
/// <param name="Queries">darf fragen — bzw. als Zuständiger beantworten, wenn das System sie nicht selbst bedient</param>
/// <param name="Trigger">darf starten — bzw. als Zuständiger verarbeiten, wenn keine Pipeline ihn bedient</param>
/// <param name="TransientEvents">darf veröffentlichen (verlierbare Hinweise vom Client)</param>
/// <param name="Commands">darf auslösen — Befugt = <c>IDarf</c> ∪ Ausgaben seines Vertrags</param>
/// <param name="Hoert">abgeleitet: mit Vertrag exakt dessen Eingänge, sonst Events der Aggregate seiner Commands; dazu die Events
/// der Projektionen hinter seinen Queries</param>
/// <param name="Art">„Mensch", „Maschine", „Ki" (aus <c>IMensch</c>/<c>IMaschine</c>/<c>IKi</c>) oder leer</param>
public sealed record AkteurRechte(
    string Name,
    Type Typ,
    IReadOnlySet<Type> Commands,
    IReadOnlySet<Type> Queries,
    IReadOnlySet<Type> Trigger,
    IReadOnlySet<Type> TransientEvents,
    IReadOnlySet<Type> Hoert,
    string Art = "")
{
    /// <summary>Der Vertrag des Akteurs (<c>IAkteurVertrag&lt;A&gt;</c>, docs/konzept-akteure.md §9) — null, wenn er nur spontan handelt.</summary>
    public Type? VertragTyp { get; init; }

    /// <summary>Hash der kanonischen Vertragsform (<see cref="Akteurvertrag.Hash"/>) — der Client meldet ihn am Handshake mit.</summary>
    public string VertragHash { get; init; } = "";

    /// <summary>Je Reaktion <c>Auf(Event)</c>: die Commands, die der Akteur darauf hineingeben darf (leer = nur zur Kenntnis).</summary>
    public IReadOnlyDictionary<Type, IReadOnlySet<Type>> Vertrag { get; init; } = new Dictionary<Type, IReadOnlySet<Type>>();

    /// <summary>Die Eingänge, auf die der Akteur mit einem Strom antwortet (mehrere Commands je Reaktion).</summary>
    public IReadOnlySet<Type> Stroeme { get; init; } = new HashSet<Type>();

    /// <summary>Darf der Akteur auf das Event <paramref name="ausloeser"/> mit <paramref name="command"/> antworten (laut Vertrag)?</summary>
    public bool AntwortetMit(Type ausloeser, Type command) =>
        (Teile.Count > 0 ? Teile : [this]).Any(t => t.Vertrag.TryGetValue(ausloeser, out var aus) && aus.Contains(command));

    /// <summary>
    /// Verkörpert eine Session MEHRERE Akteure (eine Person ist Inspekteur und KIOperator — ein Token, eine Akteur-Menge),
    /// stehen hier die einzelnen; die Mengen oben sind dann ihre Vereinigung (<see cref="Vereinige"/>).
    /// </summary>
    public IReadOnlyList<AkteurRechte> Teile { get; init; } = [];

    /// <summary>Wer aus dieser Session gibt <paramref name="typ"/> hinein? Der (erste) Teil-Akteur, der ihn darf — sonst <see cref="Name"/>.
    /// Das ist der Akteur, den der Envelope trägt (und der durch die Kette reist).</summary>
    public string AkteurFuer(Type typ) => Teile.FirstOrDefault(t => t.DarfHinein(typ))?.Name ?? Name;

    /// <summary>Mehrere Akteure als eine Session-Befugnis: Vereinigung aller Mengen, Name „A+B".</summary>
    public static AkteurRechte Vereinige(IReadOnlyList<AkteurRechte> akteure)
    {
        if (akteure.Count == 1) return akteure[0];
        static HashSet<Type> U(IEnumerable<IReadOnlySet<Type>> s) => s.SelectMany(x => x).ToHashSet();
        return new AkteurRechte(string.Join("+", akteure.Select(a => a.Name)), typeof(IAkteur),
            U(akteure.Select(a => a.Commands)), U(akteure.Select(a => a.Queries)), U(akteure.Select(a => a.Trigger)),
            U(akteure.Select(a => a.TransientEvents)), U(akteure.Select(a => a.Hoert)),
            string.Join("+", akteure.Select(a => a.Art).Distinct()))
        { Teile = akteure };
    }

    /// <summary>Darf der Akteur <paramref name="typ"/> hineingeben?</summary>
    public bool DarfHinein(Type typ) =>
        Commands.Contains(typ) || Queries.Contains(typ) || Trigger.Contains(typ) || TransientEvents.Contains(typ);

    /// <summary>Darf der Akteur das Event <paramref name="typ"/> abonnieren? <see cref="CommandFailed"/> immer —
    /// es ist die gezielte Rückmeldung auf seine eigenen Commands.</summary>
    public bool DarfHoeren(Type typ) => typ == typeof(CommandFailed) || Hoert.Contains(typ);
}
