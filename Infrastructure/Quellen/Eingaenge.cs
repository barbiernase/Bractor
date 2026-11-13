using Abstractions;
using Infrastructure.Akteure;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;

namespace Infrastructure.Quellen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// EINGÄNGE AUS QUELLEN (docs/04 §4.7, docs/10 E18): eine App setzt einen Client um; je Parameter ihres Records
// (`[Uebersetzt<U>] Quellart Name`) eine Quelle, deren Meldung die Übersetzung der Akteur-Seite zu einer Nachricht des Clients macht. Je Instanz der App in
// der Umgebung (apps/<instanz>.json: Rechner, Token, Werte je Quelle) und Quelle ein Eingang — er läuft im RECHNERDIENST des Rechners der Instanz
// (Host.Rechner, Client.Infrastructure/Quellen): ein Läufer, der lauscht und über die Leitung hineingibt wie eine App, bzw. eine Annahme auf dem Port der
// Instanz. Hier steht nur der Plan, geprüft — derselbe für den Rechnerdienst und für den Start des Hosts, der die App-Instanzen der Umgebung prüft.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Eingang, geprüft (<see cref="Eingaenge.Aus"/>): App-Instanz und Name der Quelle, die Quellart (ihr Baustein), der Akteur, für den sie hineingibt,
/// die Übersetzungen je Meldung, die Werte (als Knoten-Konfig für die Aktivierung), der Rechner und das Token der Instanz; bei einer Annahme ihr Pfad,
/// wahlweise ihr Schlüssel (<see cref="Annahmevertrag"/>) und der Port der Instanz, auf dem der Rechnerdienst annimmt.
/// </summary>
public sealed record EingangPlan(string Instanz, string Name, FunktionsBaustein Art, string Akteur, IReadOnlyDictionary<Type, UebersetzungPlan[]> Uebersetzungen,
    KnotenKonfig Konfig, string Rechner, string Token = "", string? Pfad = null, string? Schluessel = null, int Port = 0)
{
    /// <summary>Eine Annahme (<c>IAnnahme&lt;TMeldung&gt;</c>): der Rechnerdienst nimmt ihre Meldungen auf dem Port der Instanz an — kein Läufer.</summary>
    public bool Annahme => Art.Annahme;

    /// <summary>Der Name im Cluster, in der Spur und als Schlüssel seiner Ids: <c>Instanz/Quelle</c>.</summary>
    public string Ort => Instanz + "/" + Name;

    /// <summary>Der Bereich der Aktivierung (Konfig, Technik-Wahl): <c>eingang:Instanz</c> mit der Quelle als Knoten.</summary>
    public KnotenKontext Kontext => new("eingang:" + Instanz, Name, Konfig);
}

/// <summary>Die Eingänge aus Quellen der Umgebung — beim Start geprüft.</summary>
public sealed class Eingaenge(IReadOnlyList<EingangPlan> alle)
{
    public static readonly Eingaenge Keine = new(Array.Empty<EingangPlan>());

    public IReadOnlyList<EingangPlan> Alle { get; } = alle;

    /// <summary>
    /// Prüft die App-Instanzen der Umgebung (Boot-Fehler mit der Instanz bzw. der Quelle als Karte) und macht aus ihren Quellen Eingänge: die App gibt es,
    /// die Werte nennen nur Quellen der App, das Token gibt es und es nennt einen Akteur ihres Clients; je Quelle ist der Typ eine Quellart, jede ihrer
    /// Meldungen nimmt eine ihrer Übersetzungen, und ein Akteur des Clients (unter denen des Tokens) darf, was sie geben — er ist der Akteur des Eingangs.
    /// Eine Annahme braucht einen Pfad (je Umgebung eindeutig über alle übergebenen Instanzen) und einen Port der Instanz.
    /// </summary>
    /// <param name="apps">Die App-Instanzen, deren Eingänge geplant werden (der Start des Hosts: alle; ein Rechnerdienst: die seines Rechners).</param>
    /// <param name="tokens">Token → seine Akteure (Abschnitt <c>Identitaeten:Tokens</c>); null = ohne Tor.</param>
    public static Eingaenge Aus(IReadOnlyDictionary<string, AppInstanzKonfig> apps, IReadOnlyDictionary<string, ClientVertrag> appVertraege,
        IReadOnlyList<FunktionsBaustein> bausteine, Uebersetzungen uebersetzungen, IReadOnlyDictionary<string, AkteurRechte> akteure,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? tokens)
    {
        var alle = new List<EingangPlan>();
        var pfade = new Dictionary<string, string>(StringComparer.Ordinal);
        var nachName = uebersetzungen.Alle.GroupBy(u => u.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var (instanz, ak) in apps.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var karte = Rechnerregeln.AppKarte(instanz);
            var wo = $"App-Instanz {instanz}";
            if (!appVertraege.TryGetValue(ak.App, out var app))
                throw new BootFehler($"{wo}: eine App „{ak.App}“ gibt es nicht (record {ak.App} : IApp<Client>; bekannt: "
                    + string.Join(", ", appVertraege.Keys.Order(StringComparer.Ordinal).DefaultIfEmpty("keine")) + ").", karte);
            if (ak.Quellen.Keys.FirstOrDefault(q => app.Quellen.All(x => x.Name != q)) is { } fremd)
                throw new BootFehler($"{wo}: „{fremd}“ ist keine Quelle von {ak.App} (Quellen: "
                    + string.Join(", ", app.Quellen.Select(q => q.Name).DefaultIfEmpty("keine")) + ").", karte);
            // Das Token nennt Akteure; wirksam sind die, die der Client verkörpert (wie am Handshake).
            IReadOnlyList<string> wirksam = app.Verkoerpert;
            if (ak.Token.Length > 0 && tokens != null)
            {
                if (!tokens.TryGetValue(ak.Token, out var ta))
                    throw new BootFehler($"{wo}: das Token „{ak.Token}“ gibt es nicht (Identitaeten:Tokens).", karte);
                wirksam = app.Verkoerpert.Where(ta.Contains).ToList();
                if (wirksam.Count == 0)
                    throw new BootFehler($"{wo}: das Token „{ak.Token}“ nennt keinen Akteur von {app.Client} ({string.Join(", ", app.Verkoerpert)}).", karte);
            }
            foreach (var q in app.Quellen)
            {
                var qkarte = WirkerId.FuerEingang(instanz, q.Name);
                var qwo = $"{wo}, Quelle {q.Name}";
                var art = bausteine.FirstOrDefault(b => b.Name == q.Quellart && b.Quellart)
                    ?? throw new BootFehler($"{qwo}: {q.Quellart} ist keine Quellart (bekannt: "
                        + string.Join(", ", bausteine.Where(b => b.Quellart).Select(b => b.Name).DefaultIfEmpty("keine")) + ").", qkarte);
                var plaene = q.Uebersetzungen.Select(n => nachName.TryGetValue(n, out var u) ? u
                    : throw new BootFehler($"{qwo}: die Übersetzung {n} gibt es nicht.", qkarte)).ToList();
                var je = new Dictionary<Type, UebersetzungPlan[]>();
                foreach (var meldung in art.Meldet ?? [])
                {
                    var passend = plaene.Where(u => u.Ein == meldung).ToArray();
                    if (passend.Length == 0)
                        throw new BootFehler($"{qwo}: keine ihrer Übersetzungen nimmt {meldung.Name} — [Uebersetzt<…>] am Parameter nennt eine "
                            + $"ITranslate<{meldung.Name}, …>.", qkarte);
                    je[meldung] = passend;
                }
                var akteur = wirksam.FirstOrDefault(a => akteure.TryGetValue(a, out var r) && plaene.All(u => r.DarfHinein(u.Aus)))
                    ?? throw new BootFehler($"{qwo}: kein Akteur von {app.Client}{(ak.Token.Length > 0 ? " unter dem Token " + ak.Token : "")} darf, was "
                        + $"sie gibt ({string.Join(", ", plaene.Select(u => u.Aus.Name).Distinct())}).", qkarte);
                var werte = ak.Quellen.GetValueOrDefault(q.Name) ?? new QuelleKonfig();
                if (art.Annahme)
                {
                    var pfad = Annahmevertrag.Normal(werte.Werte.GetValueOrDefault(Annahmevertrag.Pfad));
                    if (pfad.Length == 0)
                        throw new BootFehler($"{qwo}: {q.Quellart} nimmt unter einem Pfad an — der Wert „{Annahmevertrag.Pfad}“ fehlt "
                            + $"(POST {Annahmevertrag.Route}/<{Annahmevertrag.Pfad}>).", qkarte);
                    if (pfad.Any(c => char.IsWhiteSpace(c) || c is '?' or '#' or '\\'))
                        throw new BootFehler($"{qwo}: Pfad „{pfad}“ ist kein Pfad (ohne Leerzeichen, ?, # und \\).", qkarte);
                    if (ak.Port <= 0)
                        throw new BootFehler($"{qwo}: {q.Quellart} nimmt an — die Instanz braucht einen Port, auf dem ihr Rechnerdienst annimmt.", karte);
                    if (pfade.TryGetValue(pfad, out var schon))
                        throw new BootFehler($"{qwo}: Pfad „{pfad}“ nimmt schon {schon} an — ein Pfad gehört je Umgebung genau einer Quelle.", qkarte);
                    pfade[pfad] = instanz + "/" + q.Name;
                    var schluessel = werte.Werte.GetValueOrDefault(Annahmevertrag.Schluessel) is { Length: > 0 } k ? k : null;
                    alle.Add(new EingangPlan(instanz, q.Name, art, akteur, je, werte.AlsKnoten(), ak.Rechner, ak.Token, pfad, schluessel, ak.Port));
                    continue;
                }
                alle.Add(new EingangPlan(instanz, q.Name, art, akteur, je, werte.AlsKnoten(), ak.Rechner, ak.Token));
            }
        }
        return new Eingaenge(alle);
    }

    /// <summary>Die Eingänge, die lauschen (je einer ein Läufer im Rechnerdienst seines Rechners).</summary>
    public IEnumerable<EingangPlan> Lauschende => Alle.Where(e => !e.Annahme);

    /// <summary>Die Eingänge, die angenommen werden (der Rechnerdienst ihres Rechners, auf dem Port der Instanz).</summary>
    public IEnumerable<EingangPlan> Annehmende => Alle.Where(e => e.Annahme);

    /// <summary>Die Aktivierungen der Eingänge für die Technik-Bindung: ihr Kontext (Werte, Technik-Wahl) und die Klasse ihrer Quellart.</summary>
    public IEnumerable<(KnotenKontext Knoten, Type Umsetzung)> Bindungen() => Alle.Select(e => (e.Kontext, e.Art.Typ));
}
