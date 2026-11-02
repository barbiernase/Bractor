using System.Buffers;
using System.Text;
using System.Text.Json;
using Abstractions;
using Infrastructure.Komposition;

namespace Infrastructure.Eingaenge;

/// <summary>
/// Der Eingang eines offenen Eingangs als Diskriminator und Wire-JSON (die generierten Tabellen, <c>GeneratedWirePoly</c>) — und zurück: ein Command
/// ohne Ziel (ein Event nur mit Diskriminator-Vorsatz <see cref="EventVorsatz"/>).
/// </summary>
public static class Nachrichtenform
{
    public const string EventVorsatz = "E:";

    public static (string Typ, string Daten) Schreibe(object nachricht)
    {
        var puffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(puffer))
        {
            if (nachricht is IEvent e) Anwendung.WirePoly.WriteEvent(w, e);
            else Anwendung.WirePoly.WriteNachricht(w, nachricht);
        }
        var typ = nachricht is IEvent ev ? EventVorsatz + Anwendung.WirePoly.EventDiskriminator(ev) : Anwendung.WirePoly.NachrichtDiskriminator(nachricht);
        return (typ, Encoding.UTF8.GetString(puffer.WrittenSpan));
    }

    public static object Lies(string typ, string daten)
    {
        var leser = new Utf8JsonReader(Encoding.UTF8.GetBytes(daten));
        leser.Read();
        return typ.StartsWith(EventVorsatz, StringComparison.Ordinal)
            ? Anwendung.WirePoly.ReadEvent(typ[EventVorsatz.Length..], ref leser)
            : Anwendung.WirePoly.ReadNachricht(typ, ref leser);
    }
}

/// <summary>
/// Die Commands ohne Ziel einer Entscheidung als Beilagen (<see cref="EingangOffen"/>): je Command und Instanz, deren Graph ihn am Kopf nimmt (die Köpfe),
/// ein offener Eingang mit der Kennung aus Command-Id, Stelle und Instanz (<see cref="Eingangsvertrag"/>) — derselbe Command ergibt dieselben Kennungen.
/// </summary>
public static class Folgenbeilagen
{
    /// <summary>
    /// Ein Command ohne Ziel von einem AKTEUR (über Leitung oder JSON-Eingang) als offener Eingang am Kopf der Instanz, die ihn nimmt: Kennung aus der
    /// Command-Id und der Instanz — derselbe Command (dieselbe Id) legt keine zweite Zeile, genau einmal am Kopf.
    /// </summary>
    public static IReadOnlyList<(EingangOffen Eingang, object Folge)> VonAkteur(ICommand command, Infrastructure.Funktionen.Verkettung plan, Guid commandId,
        string? akteur, string? korrelation)
    {
        var hoerer = plan.Hoerer(command.GetType());
        if (hoerer.Count == 0) return Array.Empty<(EingangOffen, object)>();
        var (typ, daten) = Nachrichtenform.Schreibe(command);
        return hoerer.Select(f => (new EingangOffen(Eingangsvertrag.Eingang(commandId, f), f, typ, daten, 0, ImAuftrag.IstAkteur(akteur) ? akteur : null,
            korrelation), (object)command)).ToList();
    }

    public static IReadOnlyList<(EingangOffen Eingang, object Folge)> Von(IReadOnlyList<object> folgen, Infrastructure.Funktionen.Verkettung plan, Guid commandId,
        int version, string? akteur, string? korrelation)
    {
        if (folgen.Count == 0) return Array.Empty<(EingangOffen, object)>();
        var aus = new List<(EingangOffen, object)>();
        for (var stelle = 0; stelle < folgen.Count; stelle++)
        {
            var hoerer = plan.Hoerer(folgen[stelle].GetType());
            if (hoerer.Count == 0) continue;
            var kennung = Eingangsvertrag.AusEntscheidung(commandId, stelle);
            var (typ, daten) = Nachrichtenform.Schreibe(folgen[stelle]);
            foreach (var instanz in hoerer)
                aus.Add((new EingangOffen(Eingangsvertrag.Eingang(kennung, instanz), instanz, typ, daten, version,
                    ImAuftrag.IstAkteur(akteur) ? akteur : null, korrelation), folgen[stelle]));
        }
        return aus;
    }

    /// <summary>
    /// Der schnelle Weg nach dem Commit (docs/04 §4.4): jeder offene Eingang geht sofort an den Kopf seiner Instanz — verlierbar, die Zeile in
    /// <c>cqrs_eingaenge</c> bleibt das Netz (der Zusteller bringt sie, wenn sie nach ihrer Frist noch offen ist).
    /// </summary>
    public static void StelleZu(IReadOnlyList<(EingangOffen Eingang, object Folge)> eingaenge, Infrastructure.Funktionen.IFunktionsWege wege, Guid stream)
    {
        foreach (var (e, folge) in eingaenge)
            wege.Melde(new Infrastructure.Funktionen.EingangZustellen(e.Kennung, e.Instanz, folge, Infrastructure.Funktionen.Eingangsart.Durabel, stream,
                e.Version, DateTimeOffset.UtcNow, e.Akteur, e.Korrelation, Spur.Ursache(stream, e.Version)));
    }
}
