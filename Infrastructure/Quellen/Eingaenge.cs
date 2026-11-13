using Abstractions;
using Infrastructure.Akteure;
using Infrastructure.Funktionen;
using Infrastructure.Umgebung;

namespace Infrastructure.Quellen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// EINGÄNGE AUS QUELLEN (docs/04 §4.7): ein Client verwirklicht einen Akteur durch eine Quellart — je Eingang in der Umgebung
// (Clients:<Name>:Eingaenge:<eingang>: Art, Akteur, Rechner, Werte) ein Läufer, der lauscht, die Meldung über eine Übersetzung der Akteur-Seite in eine
// Nachricht des Akteurs macht und sie hineingibt wie eine App. Struktur ist Code (Quellart, Übersetzung, IDarf), Auswahl und Werte sind Konfig.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Eingang, geprüft (<see cref="Eingaenge.Aus"/>): Client und Name, die Quellart (ihr Baustein), der Akteur, die Übersetzungen, die aus ihren Meldungen
/// eine Nachricht machen, die der Akteur darf, die Werte (als Knoten-Konfig für die Aktivierung) und der Rechner; bei einer Annahme ihr Pfad und
/// wahlweise ihr Schlüssel (<see cref="Annahmevertrag"/>).
/// </summary>
public sealed record EingangPlan(string Client, string Name, FunktionsBaustein Art, string Akteur, IReadOnlyDictionary<Type, UebersetzungPlan[]> Uebersetzungen,
    KnotenKonfig Konfig, string? Rechner, int Offen, string? Pfad = null, string? Schluessel = null)
{
    /// <summary>Eine Annahme (<c>IAnnahme&lt;TMeldung&gt;</c>): der Host nimmt ihre Meldungen an, auf jedem Knoten — kein Läufer.</summary>
    public bool Annahme => Art.Annahme;

    /// <summary>Der Name im Cluster, in der Spur und als Schlüssel seiner Ids: <c>Client/Eingang</c>.</summary>
    public string Ort => Client + "/" + Name;

    /// <summary>Der Bereich der Aktivierung (Konfig, Technik-Wahl, gesicherter Zustand): <c>eingang:Client</c> mit dem Eingang als Knoten.</summary>
    public KnotenKontext Kontext => new("eingang:" + Client, Name, Konfig);

    /// <summary>So viele hineingegebene Elemente dürfen offen sein, bevor die Quelle wartet.</summary>
    public int Grenze => Offen > 0 ? Offen : EingangKonfig.StandardOffen;
}

/// <summary>Die Eingänge aus Quellen der Umgebung — beim Start geprüft.</summary>
public sealed class Eingaenge(IReadOnlyList<EingangPlan> alle)
{
    public static readonly Eingaenge Keine = new(Array.Empty<EingangPlan>());

    public IReadOnlyList<EingangPlan> Alle { get; } = alle;

    /// <summary>
    /// Die Eingänge aus den Clients der Umgebung, geprüft (Boot-Fehler mit dem Eingang als Karte): den Client gibt es und er verkörpert den Akteur, die
    /// Art ist eine Quellart, je Meldung, die sie melden kann, gibt es eine Übersetzung auf eine Nachricht, die der Akteur darf, und im Cluster nennt ein
    /// lauschender Eingang seinen Rechner (eine Quelle lauscht genau einmal). Eine ANNAHME nimmt jeder Host an — sie nennt keinen Rechner, dafür ihren
    /// Pfad (Pflicht, je Umgebung eindeutig über alle Annahmen).
    /// </summary>
    public static Eingaenge Aus(IReadOnlyDictionary<string, ClientKonfig> clients, IReadOnlyList<FunktionsBaustein> bausteine, Uebersetzungen uebersetzungen,
        IReadOnlyDictionary<string, AkteurRechte> akteure, IReadOnlyDictionary<string, ClientVertrag> vertraege, bool einzelknoten)
    {
        var alle = new List<EingangPlan>();
        var pfade = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (client, ck) in clients.OrderBy(x => x.Key, StringComparer.Ordinal))
            foreach (var (name, e) in ck.Eingaenge.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var karte = WirkerId.FuerEingang(client, name);
                var wo = $"Clients:{client}:Eingaenge:{name}";
                if (!vertraege.TryGetValue(client, out var vertrag))
                    throw new BootFehler($"{wo}: einen Client {client} gibt es nicht (record {client} : IClient).", karte);
                if (!akteure.TryGetValue(e.Akteur, out var rechte))
                    throw new BootFehler($"{wo}: Akteur „{e.Akteur}“ gibt es nicht.", karte);
                if (!vertrag.Verkoerpert.Contains(e.Akteur))
                    throw new BootFehler($"{wo}: der Client {client} verkörpert {e.Akteur} nicht (IVerkoerpert<{e.Akteur}>).", karte);
                var art = bausteine.FirstOrDefault(b => b.Name == e.Art && b.Quellart)
                    ?? throw new BootFehler($"{wo}: Art „{e.Art}“ ist keine Quellart (bekannt: "
                        + string.Join(", ", bausteine.Where(b => b.Quellart).Select(b => b.Name).DefaultIfEmpty("keine")) + ").", karte);
                var je = new Dictionary<Type, UebersetzungPlan[]>();
                foreach (var meldung in art.Meldet ?? [])
                {
                    var passend = uebersetzungen.Fuer(meldung).Where(u => rechte.DarfHinein(u.Aus)).ToArray();
                    if (passend.Length == 0)
                        throw new BootFehler($"{wo}: keine Übersetzung macht aus {meldung.Name} eine Nachricht, die {e.Akteur} darf — eine Übersetzung "
                            + $"der Akteur-Seite (ITranslate<{meldung.Name}, …> bei den Akteuren) auf ein IDarf von {e.Akteur} fehlt.", karte);
                    je[meldung] = passend;
                }
                if (e.Rechner is { } r && (r.Length == 0 || r.Trim() != r || r.Any(c => c is ',' or '*' or '?' || char.IsWhiteSpace(c))))
                    throw new BootFehler($"{wo}: Rechner „{r}“ ist kein Name — ein Rechner ist genau ein Name.", karte);
                if (art.Annahme)
                {
                    // Eine Annahme nimmt jeder Host an (ein Aufruf kann an jeden gehen) — ein Rechner hätte keine Bedeutung.
                    if (e.Rechner is not null)
                        throw new BootFehler($"{wo}: {e.Art} nimmt an — jeder Host nimmt Aufrufe an, ein Rechner hat hier keine Bedeutung (nimm \"Rechner\" weg).", karte);
                    var pfad = Annahmevertrag.Normal(e.Werte.GetValueOrDefault(Annahmevertrag.Pfad));
                    if (pfad.Length == 0)
                        throw new BootFehler($"{wo}: {e.Art} nimmt unter einem Pfad an — der Wert „{Annahmevertrag.Pfad}“ fehlt "
                            + $"(POST {Annahmevertrag.Route}/<{Annahmevertrag.Pfad}>).", karte);
                    if (pfad.Any(c => char.IsWhiteSpace(c) || c is '?' or '#' or '\\'))
                        throw new BootFehler($"{wo}: Pfad „{pfad}“ ist kein Pfad (ohne Leerzeichen, ?, # und \\).", karte);
                    if (pfade.TryGetValue(pfad, out var schon))
                        throw new BootFehler($"{wo}: Pfad „{pfad}“ nimmt schon der Eingang {schon} an — ein Pfad gehört je Umgebung genau einem Eingang.", karte);
                    pfade[pfad] = client + "/" + name;
                    var schluessel = e.Werte.GetValueOrDefault(Annahmevertrag.Schluessel) is { Length: > 0 } k ? k : null;
                    alle.Add(new EingangPlan(client, name, art, e.Akteur, je, e.AlsKnoten(), null, e.Offen, pfad, schluessel));
                    continue;
                }
                if (!einzelknoten && e.Rechner is null)
                    throw new BootFehler($"{wo}: im Cluster nennt ein Eingang seinen Rechner — eine Quelle lauscht genau einmal: \"Rechner\": \"<Name>\", "
                        + "derselbe Name als Cluster:Rechner des Hosts.", karte);
                alle.Add(new EingangPlan(client, name, art, e.Akteur, je, e.AlsKnoten(), e.Rechner, e.Offen));
            }
        return new Eingaenge(alle);
    }

    /// <summary>Die Eingänge, die lauschen (je einer ein Läufer auf seinem Rechner).</summary>
    public IEnumerable<EingangPlan> Lauschende => Alle.Where(e => !e.Annahme);

    /// <summary>Die Eingänge, die der Host annimmt (auf jedem Knoten, <see cref="Annahmen"/>).</summary>
    public IEnumerable<EingangPlan> Annehmende => Alle.Where(e => e.Annahme);

    /// <summary>Die Aktivierungen der Eingänge für die Technik-Bindung: ihr Kontext (Werte, Technik-Wahl) und die Klasse ihrer Quellart.</summary>
    public IEnumerable<(KnotenKontext Knoten, Type Umsetzung)> Bindungen() => Alle.Select(e => (e.Kontext, e.Art.Typ));
}
