using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// Der Vertrag EINES Clients (<c>IClientVertrag</c>, <c>docs/konzept-akteure.md</c> §4, Plan Pub/Sub-Neustrukturierung §2.7) — generiert in
/// <see cref="GeneratedClientVertraege"/>. Ein Client ist die Software an der Leitung (eine Verbindung, eine Sitzung); er kann mehrere Akteure
/// verkörpern, und ein Akteur kann über mehrere Clients laufen.
/// </summary>
/// <param name="Name">Handshake-Name (Interface ohne führendes I, <see cref="Clientvertragsregeln.ClientName"/>).</param>
/// <param name="Typ">Das Client-Vertrags-Interface.</param>
/// <param name="Hash">Client-Hash (<see cref="Clientvertragsregeln.ClientKanon"/>) — der Client meldet ihn am Handshake mit.</param>
/// <param name="Traegt">die Funktionen, deren Code er ausführt (<c>ITraegt&lt;F&gt;</c>).</param>
/// <param name="Sendet">was der Client von sich aus hineingibt (<c>ISendet&lt;T&gt;</c>).</param>
/// <param name="Fragt">die Queries des Clients (<c>IFragt&lt;T&gt;</c>).</param>
/// <param name="Hoert">Kenntnis (<c>IHoert&lt;T&gt;</c>) — genau das wird abonniert.</param>
/// <param name="Verkoerpert">abgeleitet: je getragener Funktion, gesendetem und gefragtem Typ die IDarf-Halter (<see cref="Clientvertragsregeln.Verkoerpert"/>).</param>
public sealed record ClientVertrag(
    string Name,
    Type Typ,
    string Hash,
    IReadOnlySet<Type> Traegt,
    IReadOnlySet<Type> Sendet,
    IReadOnlySet<Type> Fragt,
    IReadOnlySet<Type> Hoert,
    IReadOnlyList<string> Verkoerpert)
{
    /// <summary>
    /// Die Befugnisse einer Sitzung, die diesen Client spricht — <b>Vertrag ∩ Akteure des Tokens</b> (Akteur-Konzept §4.3). Je verkörpertem
    /// Akteur ein Teil (<see cref="AkteurRechte.Teile"/>): Commands = Sendet ∩ IDarf, Queries = Fragt ∩ IDarf, Funktionen = Trägt ∩ IDarf. Oben
    /// stehen die Vereinigung und das Gehörte des Vertrags (Kenntnis braucht keinen Akteur).
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
            teile.Add(new AkteurRechte(name, a.Typ,
                Sendet.Where(a.Commands.Contains).ToHashSet(),
                Fragt.Where(a.Queries.Contains).ToHashSet(),
                Sendet.Where(a.Quellen.Contains).ToHashSet(),
                Sendet.Where(a.TransientEvents.Contains).ToHashSet(),
                new HashSet<Type>(),
                a.Art)
            {
                VertragTyp = Typ,
                VertragHash = Hash,
                Funktionen = Traegt.Where(a.Funktionen.Contains).ToHashSet(),
            });
        }
        if (teile.Count == 0) return null;

        static HashSet<Type> U(IEnumerable<IReadOnlySet<Type>> s) => s.SelectMany(x => x).ToHashSet();
        return new AkteurRechte(Name, Typ,
            U(teile.Select(t => t.Commands)), U(teile.Select(t => t.Queries)), U(teile.Select(t => t.Quellen)),
            U(teile.Select(t => t.TransientEvents)), Hoert,
            string.Join("+", teile.Select(t => t.Art).Where(x => x.Length > 0).Distinct()))
        {
            VertragTyp = Typ,
            VertragHash = Hash,
            Funktionen = U(teile.Select(t => t.Funktionen)),
            Teile = teile,
        };
    }
}
