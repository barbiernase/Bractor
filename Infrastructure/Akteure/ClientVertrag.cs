using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// Der Vertrag EINES Clients (<c>IClientVertrag</c>, <c>docs/konzept-akteure.md</c> §4) — generiert in
/// <see cref="GeneratedClientVertraege"/>. Ein Client ist die Software an der Leitung (eine Verbindung, eine Sitzung); er kann
/// mehrere Akteure verkörpern, und ein Akteur kann über mehrere Clients laufen.
/// </summary>
/// <param name="Name">Handshake-Name (Interface ohne führendes I, <see cref="Akteurvertrag.ClientName"/>).</param>
/// <param name="Typ">Das Client-Vertrags-Interface.</param>
/// <param name="Hash">Client-Hash (<see cref="Akteurvertrag.ClientKanon"/>) — der Client meldet ihn am Handshake mit.</param>
/// <param name="Traegt">Akteur → (Event → erlaubte Ausgaben) der getragenen Vertrags-Teile; im Namen dieses Akteurs wird geantwortet.</param>
/// <param name="Stroeme">Eingänge, auf die mit einem Strom geantwortet wird.</param>
/// <param name="Sendet">was der Client von sich aus hineingibt (<c>ISendet&lt;T&gt;</c>).</param>
/// <param name="Fragt">die Queries des Clients (<c>IFragt&lt;T&gt;</c>).</param>
/// <param name="Kenntnis">eigene <c>void Auf(E)</c> — nur zur Kenntnis, keine Ausgabe, kein Akteur.</param>
/// <param name="Verkoerpert">abgeleitet: Akteure der getragenen Teile, dazu je Sendet/Fragt die IDarf-Halter, falls keiner der Teil-Akteure
/// den Typ schon darf.</param>
public sealed record ClientVertrag(
    string Name,
    Type Typ,
    string Hash,
    IReadOnlyDictionary<string, IReadOnlyDictionary<Type, IReadOnlySet<Type>>> Traegt,
    IReadOnlySet<Type> Stroeme,
    IReadOnlySet<Type> Sendet,
    IReadOnlySet<Type> Fragt,
    IReadOnlySet<Type> Kenntnis,
    IReadOnlyList<string> Verkoerpert)
{
    /// <summary>Alle Eingänge der Leitung: getragene Zusagen (mit und ohne Ausgabe) ∪ eigene Kenntnis — genau das wird abonniert.</summary>
    public IReadOnlySet<Type> Eingaenge => Traegt.Values.SelectMany(r => r.Keys).Concat(Kenntnis).ToHashSet();

    /// <summary>
    /// Die Befugnisse einer Sitzung, die diesen Client spricht — <b>Vertrag ∩ Akteure des Tokens</b> (Akteur-Konzept §4.3). Je verkörpertem
    /// Akteur ein Teil (<see cref="AkteurRechte.Teile"/>): Commands = (Sendet ∩ IDarf) ∪ Ausgaben seiner getragenen Zusagen, Queries =
    /// Fragt ∩ IDarf, Vertrag = seine getragenen Zusagen. Oben stehen die Vereinigung und ALLE Eingänge (Zusage + Kenntnis), damit
    /// <see cref="AkteurVertragsPruefung.Wende"/> genau sie abonniert.
    /// </summary>
    /// <param name="akteure">die generierte Akteur-Tabelle (<see cref="GeneratedAkteurRechte.Alle"/>)</param>
    /// <param name="token">die Befugnis des Tokens (null = Tor aus → alle verkörperten Akteure)</param>
    /// <returns>null, wenn das Token keinen Akteur dieses Clients verkörpert</returns>
    public AkteurRechte? Rechte(IReadOnlyDictionary<string, AkteurRechte> akteure, AkteurRechte? token)
    {
        var erlaubt = token == null ? null
            : (token.Teile.Count > 0 ? token.Teile.Select(t => t.Name) : [token.Name]).ToHashSet(StringComparer.Ordinal);
        var teile = new List<AkteurRechte>();
        foreach (var name in Verkoerpert)
        {
            if (erlaubt != null && !erlaubt.Contains(name)) continue;
            if (!akteure.TryGetValue(name, out var a)) continue;
            var zusagen = Traegt.GetValueOrDefault(name) ?? new Dictionary<Type, IReadOnlySet<Type>>();
            var ausgaben = zusagen.Values.SelectMany(x => x);
            teile.Add(new AkteurRechte(name, a.Typ,
                Sendet.Where(a.Commands.Contains).Concat(ausgaben).ToHashSet(),
                Fragt.Where(a.Queries.Contains).ToHashSet(),
                Sendet.Where(a.Trigger.Contains).ToHashSet(),
                Sendet.Where(a.TransientEvents.Contains).ToHashSet(),
                zusagen.Keys.ToHashSet(),
                a.Art)
            {
                VertragTyp = Typ,
                VertragHash = Hash,
                Vertrag = zusagen,
                Stroeme = Stroeme.Where(zusagen.ContainsKey).ToHashSet(),
            });
        }
        if (teile.Count == 0) return null;

        static HashSet<Type> U(IEnumerable<IReadOnlySet<Type>> s) => s.SelectMany(x => x).ToHashSet();
        var eingaenge = new Dictionary<Type, IReadOnlySet<Type>>();
        foreach (var t in teile) foreach (var (e, aus) in t.Vertrag) eingaenge[e] = aus;
        foreach (var k in Kenntnis) eingaenge.TryAdd(k, new HashSet<Type>());
        return new AkteurRechte(Name, Typ,
            U(teile.Select(t => t.Commands)), U(teile.Select(t => t.Queries)), U(teile.Select(t => t.Trigger)),
            U(teile.Select(t => t.TransientEvents)), eingaenge.Keys.ToHashSet(),
            string.Join("+", teile.Select(t => t.Art).Where(x => x.Length > 0).Distinct()))
        {
            VertragTyp = Typ,
            VertragHash = Hash,
            Vertrag = eingaenge,
            Stroeme = Stroeme,
            Teile = teile,
        };
    }
}
