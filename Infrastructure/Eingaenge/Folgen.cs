using System.Buffers;
using System.Text;
using System.Text.Json;
using Abstractions;
using Infrastructure.Komposition;

namespace Infrastructure.Eingaenge;

/// <summary>Eine Nachricht ohne Marker als Diskriminator und Wire-JSON (die generierten Tabellen, <c>GeneratedWirePoly</c>) — und zurück.</summary>
public static class Nachrichtenform
{
    public static (string Typ, string Daten) Schreibe(object nachricht)
    {
        var puffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(puffer)) Anwendung.WirePoly.WriteNachricht(w, nachricht);
        return (Anwendung.WirePoly.NachrichtDiskriminator(nachricht), Encoding.UTF8.GetString(puffer.WrittenSpan));
    }

    public static object Lies(string typ, string daten)
    {
        var leser = new Utf8JsonReader(Encoding.UTF8.GetBytes(daten));
        leser.Read();
        return Anwendung.WirePoly.ReadNachricht(typ, ref leser);
    }
}

/// <summary>
/// Die Folgen einer Entscheidung als Beilagen (<see cref="EingangOffen"/>): je Folge und hörende Funktion (die Verkettung) ein offener Eingang mit der Kennung aus
/// Command-Id, Stelle und Funktion (<see cref="Eingangsvertrag"/>) — derselbe Command ergibt dieselben Kennungen.
/// </summary>
public static class Folgenbeilagen
{
    public static IReadOnlyList<(EingangOffen Eingang, object Folge)> Von(IReadOnlyList<object> folgen, Infrastructure.Funktionen.Verkettung plan, Guid commandId,
        int version, string? akteur, string? korrelation)
    {
        if (folgen.Count == 0) return Array.Empty<(EingangOffen, object)>();
        var aus = new List<(EingangOffen, object)>();
        for (var stelle = 0; stelle < folgen.Count; stelle++)
        {
            var hoerer = plan.Hoerer(folgen[stelle].GetType());
            if (hoerer.Count == 0) continue;
            var kennung = Eingangsvertrag.Folge(commandId, stelle);
            var (typ, daten) = Nachrichtenform.Schreibe(folgen[stelle]);
            foreach (var funktion in hoerer)
                aus.Add((new EingangOffen(Eingangsvertrag.Eingang(kennung, funktion), funktion, typ, daten, version,
                    ImAuftrag.IstAkteur(akteur) ? akteur : null, korrelation), folgen[stelle]));
        }
        return aus;
    }

    /// <summary>
    /// Der schnelle Weg nach dem Commit (docs/04 §4.4): jeder offene Eingang geht sofort an den Parent seiner Funktion — verlierbar, die Zeile in
    /// <c>cqrs_eingaenge</c> bleibt das Netz (der Zusteller bringt sie, wenn sie nach ihrer Frist noch offen ist).
    /// </summary>
    public static void StelleZu(IReadOnlyList<(EingangOffen Eingang, object Folge)> eingaenge, Infrastructure.Funktionen.IFunktionsWege wege, Guid stream)
    {
        foreach (var (e, folge) in eingaenge)
            wege.Melde(new Infrastructure.Funktionen.EingangZustellen(e.Kennung, e.Funktion, folge, Infrastructure.Funktionen.Eingangsart.Durabel, stream,
                e.Version, DateTimeOffset.UtcNow, e.Akteur, e.Korrelation, Spur.Ursache(stream, e.Version)));
    }
}
