using System.Buffers;
using System.Text;
using System.Text.Json;
using Abstractions;
using Infrastructure.Komposition;

namespace Infrastructure.Eingaenge;

/// <summary>
/// Wer eine Folge hört (docs/03 §3.1, docs/04 §4.4): Folge-Typ → die Funktionen, für die ihre Entscheidung je einen offenen Eingang legt. Hört
/// niemand, legt sie keinen.
/// </summary>
public sealed class Folgenplan(IReadOnlyDictionary<Type, string[]> hoerer)
{
    /// <summary>Der Plan ohne Hörer.</summary>
    public static readonly Folgenplan Leer = new(new Dictionary<Type, string[]>());

    public IReadOnlyList<string> Hoerer(Type folge) => hoerer.TryGetValue(folge, out var f) ? f : Array.Empty<string>();
}

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
/// Die Folgen einer Entscheidung als Beilagen (<see cref="EingangOffen"/>): je Folge und hörende Funktion ein offener Eingang mit der Kennung aus
/// Command-Id, Stelle und Funktion (<see cref="Eingangsvertrag"/>) — derselbe Command ergibt dieselben Kennungen.
/// </summary>
public static class Folgenbeilagen
{
    public static IReadOnlyList<Beilage> Von(IReadOnlyList<object> folgen, Folgenplan plan, Guid commandId, int version, string? akteur, string? korrelation)
    {
        if (folgen.Count == 0) return Array.Empty<Beilage>();
        var aus = new List<Beilage>();
        for (var stelle = 0; stelle < folgen.Count; stelle++)
        {
            var hoerer = plan.Hoerer(folgen[stelle].GetType());
            if (hoerer.Count == 0) continue;
            var kennung = Eingangsvertrag.Folge(commandId, stelle);
            var (typ, daten) = Nachrichtenform.Schreibe(folgen[stelle]);
            foreach (var funktion in hoerer)
                aus.Add(new EingangOffen(Eingangsvertrag.Eingang(kennung, funktion), funktion, typ, daten, version,
                    ImAuftrag.IstAkteur(akteur) ? akteur : null, korrelation));
        }
        return aus;
    }
}
