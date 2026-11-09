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
/// Ein Command ohne Ziel eines Clients als offene Eingänge (<see cref="EingangOffen"/>): je Instanz, deren Graph ihn (oder seine Übersetzung) am Kopf
/// nimmt, einer mit der Kennung aus Command-Id und Instanz (<see cref="Eingangsvertrag"/>) — derselbe Command ergibt dieselben Kennungen.
/// </summary>
public static class Folgenbeilagen
{
    /// <summary>
    /// Ein Command ohne Ziel von einem AKTEUR (über Leitung oder JSON-Eingang) als offener Eingang am Kopf der Instanz, die ihn nimmt: Kennung aus der
    /// Command-Id und der Instanz — derselbe Command (dieselbe Id) legt keine zweite Zeile, genau einmal am Kopf.
    /// </summary>
    public static IReadOnlyList<(EingangOffen Eingang, object Folge)> VonAkteur(ICommand command, Infrastructure.Funktionen.Verkettung plan, Guid commandId,
        string? akteur, string? korrelation, int version = 0)
    {
        // Nimmt kein Graph ihn am Kopf, übersetzt eine Kopf-Übersetzung der Domäne ihn in den Eingang eines Graphen (Verkettung.Ziele).
        return plan.Ziele(command).Select(z =>
        {
            var (typ, daten) = Nachrichtenform.Schreibe(z.Nachricht);
            return (new EingangOffen(Eingangsvertrag.Eingang(commandId, z.Instanz), z.Instanz, typ, daten, version, ImAuftrag.IstAkteur(akteur) ? akteur : null,
                korrelation), z.Nachricht);
        }).ToList();
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
