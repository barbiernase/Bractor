using Abstractions;

namespace Infrastructure.Akteure;

/// <summary>
/// EIN Vertrag an der Leitung — ein Client (<c>IClient</c>, <c>docs/konzept-akteure.md</c> §4, generiert in <see cref="GeneratedClientVertraege"/>)
/// bzw. eine App, die einen Client umsetzt (<c>IApp&lt;TClient&gt;</c>, generiert in <see cref="GeneratedAppVertraege"/>, gegen die der Handshake
/// prüft). Er nennt nur, wen er verkörpert; was über die Leitung geht, sind die <c>IDarf</c> und <c>IHoert</c> dieser Akteure (bei einer App ohne
/// die Nachrichten ihrer Quellen — das hält ihr Code ein, CQRS095).
/// </summary>
/// <param name="Name">Handshake-Name (der Record-Name).</param>
/// <param name="Typ">Der Record (Client bzw. App).</param>
/// <param name="Hash">Hash des Vertrags (<see cref="Clientvertragsregeln.ClientKanon"/>) — die App meldet ihn am Handshake mit.</param>
/// <param name="Verkoerpert">die Akteure, für die er spricht (<c>IVerkoerpert&lt;A&gt;</c>).</param>
public sealed record ClientVertrag(string Name, Type Typ, string Hash, IReadOnlyList<string> Verkoerpert)
{
    /// <summary>Bei einer App: der Client, den sie umsetzt; bei einem Client er selbst.</summary>
    public string Client { get; init; } = Name;

    /// <summary>Bei einer App: ihre Quellen (je Parameter <c>[Uebersetzt&lt;U&gt;] Quellart Name</c>); sonst keine.</summary>
    public IReadOnlyList<AppQuelle> Quellen { get; init; } = [];

    /// <summary>
    /// Die Befugnisse einer Sitzung, die diesen Client spricht — <b>verkörperte Akteure ∩ Akteure des Tokens</b>, für beide Richtungen: hinein (IDarf)
    /// wie heraus (IHoert). Je solchem Akteur ein Teil (<see cref="AkteurRechte.Teile"/>); oben steht ihre Vereinigung.
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
            U(teile.Select(t => t.Hoert)))
        { VertragTyp = Typ, VertragHash = Hash, Teile = teile };
    }
}

/// <summary>
/// Eine Quelle einer App (generiert aus <c>record X([Uebersetzt&lt;U&gt;] Quellart Name, …) : IApp&lt;TClient&gt;</c>): der Name des Parameters (Schlüssel
/// ihrer Werte in der App-Instanz), die Quellart (Klassenname) und die Übersetzungen der Akteur-Seite (Klassennamen), die aus ihren Meldungen Nachrichten
/// des Clients machen.
/// </summary>
public sealed record AppQuelle(string Name, string Quellart, IReadOnlyList<string> Uebersetzungen);
