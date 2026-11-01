using System.Buffers;
using System.Text;
using System.Text.Json;
using Abstractions;
using Infrastructure.Komposition;

namespace Infrastructure.Uhr;

/// <summary>Ein Command als Diskriminator und das generierte Wire-JSON (dieselbe Form wie im Command-Envelope) — so steht der Befehl einer Frist im Verzeichnis.</summary>
public static class Befehlsform
{
    public static (string Typ, string Daten) Schreibe(ICommand befehl)
    {
        var puffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(puffer)) Anwendung.WirePoly.WriteCommand(w, befehl);
        return (Anwendung.WirePoly.CommandDiskriminator(befehl), Encoding.UTF8.GetString(puffer.WrittenSpan));
    }

    public static ICommand Lies(string typ, string daten)
    {
        var leser = new Utf8JsonReader(Encoding.UTF8.GetBytes(daten));
        leser.Read();
        return Anwendung.WirePoly.ReadCommand(typ, ref leser);
    }
}
