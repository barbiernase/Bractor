using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// EIN Client (<c>IClient</c>, <c>docs/konzept-akteure.md</c> §4) — generiert in <see cref="GeneratedClientVertraege"/>. Ein Client ist die Software
/// an der Leitung (eine Verbindung, eine Sitzung); er nennt nur, wen er verkörpert. Was über seine Leitung geht, sind die <c>IDarf</c> und
/// <c>IHoert</c> dieser Akteure.
/// </summary>
/// <param name="Name">Handshake-Name (der Record-Name).</param>
/// <param name="Typ">Der Client-Record.</param>
/// <param name="Hash">Client-Hash (<see cref="Clientvertragsregeln.ClientKanon"/>) — der Client meldet ihn am Handshake mit.</param>
/// <param name="Verkoerpert">die Akteure, für die er spricht (<c>IVerkoerpert&lt;A&gt;</c>).</param>
public sealed record ClientVertrag(string Name, Type Typ, string Hash, IReadOnlyList<string> Verkoerpert)
{
    /// <summary>
    /// Die Befugnisse einer Sitzung, die diesen Client spricht — <b>verkörperte Akteure ∩ Akteure des Tokens</b>, für beide Richtungen: hinein (IDarf)
    /// wie heraus (IHoert, Arbeit). Je solchem Akteur ein Teil (<see cref="AkteurRechte.Teile"/>); oben steht ihre Vereinigung.
    /// </summary>
    /// <param name="akteure">die generierte Akteur-Tabelle (<see cref="GeneratedAkteurRechte.Alle"/>)</param>
    /// <param name="token">die Befugnis des Tokens (null = Tor aus → alle verkörperten Akteure)</param>
    /// <returns>null, wenn das Token keinen Akteur dieses Clients verkörpert</returns>
    public AkteurRechte? Rechte(IReadOnlyDictionary<string, AkteurRechte> akteure, AkteurRechte? token)
    {
        var erlaubt = token?.Einzelne.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var teile = Verkoerpert.Where(n => erlaubt == null || erlaubt.Contains(n))
            .Select(n => akteure.TryGetValue(n, out var a) ? a with { VertragTyp = Typ, VertragHash = Hash } : null)
            .OfType<AkteurRechte>().ToList();
        if (teile.Count == 0) return null;
        static HashSet<Type> U(IEnumerable<IReadOnlySet<Type>> s) => s.SelectMany(x => x).ToHashSet();
        return new AkteurRechte(Name, Typ, U(teile.Select(t => t.Commands)), U(teile.Select(t => t.Queries)), U(teile.Select(t => t.TransientEvents)),
            U(teile.Select(t => t.Hoert)), U(teile.Select(t => t.Arbeit)))
        { VertragTyp = Typ, VertragHash = Hash, Teile = teile };
    }
}
