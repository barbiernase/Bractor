using System.Text.Json;
using GraphExtractor;

namespace Werkbank.Server;

/// <summary>Ein Eintrag im Verlauf: eine Geste (bzw. eine LLM-Übernahme) mit ihrem Patch — Datei für Datei vorher/nachher.</summary>
public sealed record VerlaufsEintrag(DateTimeOffset Zeit, string Art, string Titel, IReadOnlyList<DateiPatch> Patch);

/// <summary>
/// ↶ = den letzten Patch zurücknehmen (Werkbank P7.2) — EIN Mechanismus für Gesten und LLM-Übernahmen. Je Branch eine Datei
/// (<c>.werkbank/verlauf.&lt;branch&gt;.json</c>); zurückgenommen wird nur, solange die Dateien seither unverändert sind.
/// </summary>
public sealed class Verlauf(string slnRoot, Func<string> branch)
{
    private const int Hoechstens = 100;
    private readonly object _sperre = new();
    private static readonly JsonSerializerOptions Optionen = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private string Datei => Path.Combine(slnRoot, ".werkbank", $"verlauf.{Branchname()}.json");
    private string Branchname() => string.Concat(branch().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

    private List<VerlaufsEintrag> Lies()
    {
        try { return File.Exists(Datei) ? JsonSerializer.Deserialize<List<VerlaufsEintrag>>(File.ReadAllText(Datei), Optionen) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    private void Schreibe(List<VerlaufsEintrag> eintraege)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Datei)!);
        File.WriteAllText(Datei, JsonSerializer.Serialize(eintraege.TakeLast(Hoechstens).ToList(), Optionen));
    }

    public void Merke(string art, string titel, IReadOnlyList<DateiPatch> patch)
    {
        if (patch.Count == 0) return;
        lock (_sperre)
        {
            var l = Lies();
            l.Add(new VerlaufsEintrag(DateTimeOffset.Now, art, titel, patch));
            Schreibe(l);
        }
    }

    /// <summary>Die letzten Einträge (neueste zuerst), ohne Dateiinhalte.</summary>
    public IReadOnlyList<object> Liste(int max = 30)
    {
        lock (_sperre)
            return Lies().AsEnumerable().Reverse().Take(max)
                .Select(e => (object)new { e.Zeit, e.Art, e.Titel, dateien = e.Patch.Select(p => p.Datei).ToList() }).ToList();
    }

    /// <summary>
    /// Den letzten Eintrag zurücknehmen: jede Datei muss noch genau den Stand „nachher“ haben (sonst wurde seither geändert — dann nichts),
    /// dann wird „vorher“ wiederhergestellt (eine angelegte Datei entfällt, eine gelöschte kommt zurück). Liefert den zurückgenommenen Eintrag.
    /// </summary>
    public (VerlaufsEintrag? Eintrag, string? Fehler) NimmZurueck()
    {
        lock (_sperre)
        {
            var l = Lies();
            if (l.Count == 0) return (null, "Nichts zurückzunehmen.");
            var e = l[^1];
            foreach (var p in e.Patch)
            {
                var voll = Path.Combine(slnRoot, p.Datei);
                var jetzt = File.Exists(voll) ? File.ReadAllText(voll) : null;
                if (jetzt != p.Nachher) return (null, $"„{e.Titel}“ lässt sich nicht zurücknehmen: {p.Datei} wurde seither geändert.");
            }
            foreach (var p in e.Patch)
            {
                var voll = Path.Combine(slnRoot, p.Datei);
                if (p.Vorher == null) File.Delete(voll);
                else { Directory.CreateDirectory(Path.GetDirectoryName(voll)!); File.WriteAllText(voll, p.Vorher); }
            }
            l.RemoveAt(l.Count - 1);
            Schreibe(l);
            return (e, null);
        }
    }
}
