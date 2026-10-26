using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DomainEditor;

namespace Werkbank;

/// <summary>Anker eines Python-Rumpfs (getragene Funktion bzw. Kenntnis einer Client-Implementierung): Datei relativ zur Solution, Klasse, Methode.</summary>
public sealed record PythonAnker(string Datei, string Klasse, string Methode, string? Signatur);

/// <summary>
/// Die Synchronisations-Naht für PYTHON-Rümpfe (docs/konzept-akteure.md §4) — Spiegel von <see cref="CodeSync"/>: lesen (Rumpf, Prompt,
/// Hash) und den Dateitext mit einem Kandidaten bilden (eine fehlende Methode mit der Signatur der Basis angelegt). Geschrieben wird nur als
/// Rumpf-Geste (✎ wie 🤖, Verlauf und ↶ der Werkbank). Die Datei ist die Wahrheit; die Prompt-Zeile <c># 🤖 Prompt:</c> steht als erste
/// Rumpfzeile.
/// </summary>
public static class PythonSync
{
    public const string PromptMarke = "# 🤖 Prompt:";

    private static string Voll(string rel, string sln) => Path.GetFullPath(Path.Combine(sln, rel));

    public static object Lese(PythonAnker a, string sln)
    {
        var p = Voll(a.Datei, sln);
        if (!File.Exists(p)) return new { ok = false, grund = $"Datei fehlt: {a.Datei}" };
        var text = File.ReadAllText(p);
        var r = PythonQuelle.Rumpf(text, a.Klasse, a.Methode);
        if (r == null) return new { ok = true, pfad = a.Datei, zeile = 0, body = "", prompt = (string?)null, hash = Hash(""), fehlt = true };
        var m = PythonQuelle.FindeMethode(text, a.Klasse, a.Methode)!;
        var zeilen = r.Split('\n');
        var prompt = zeilen.FirstOrDefault(z => z.TrimStart().StartsWith(PromptMarke, StringComparison.Ordinal))?.Trim()[PromptMarke.Length..].Trim();
        var body = string.Join("\n", zeilen.Where(z => !z.TrimStart().StartsWith(PromptMarke, StringComparison.Ordinal)));
        return new { ok = true, pfad = a.Datei, zeile = m.DefZeile + 1, body, prompt, hash = Hash(r) };
    }

    public static string? RumpfHash(PythonAnker a, string sln)
    {
        var p = Voll(a.Datei, sln);
        if (!File.Exists(p)) return null;
        return Hash(PythonQuelle.Rumpf(File.ReadAllText(p), a.Klasse, a.Methode) ?? "");
    }

    /// <summary>Der Dateitext mit dem Kandidaten (fehlt die Methode: angehängt mit der Signatur der Basis) — ohne zu schreiben.</summary>
    public static (bool Ok, string Grund, string? Text) MitRumpf(PythonAnker a, string rumpf, string? prompt, string sln)
    {
        var p = Voll(a.Datei, sln);
        if (!File.Exists(p)) return (false, $"Datei fehlt: {a.Datei}", null);
        var text = File.ReadAllText(p);
        var zeilen = rumpf.Replace("\r\n", "\n").Split('\n').Where(z => !z.TrimStart().StartsWith(PromptMarke, StringComparison.Ordinal)).ToList();
        if (!string.IsNullOrWhiteSpace(prompt)) zeilen.Insert(0, PromptMarke + " " + prompt.Replace("\n", " ").Trim());
        var inhalt = string.Join("\n", zeilen);
        var neu = PythonQuelle.SetzeRumpf(text, a.Klasse, a.Methode, inhalt);
        if (neu == null)
        {
            if (string.IsNullOrWhiteSpace(a.Signatur)) return (false, $"{a.Klasse}.{a.Methode} fehlt und die Basis nennt keine Signatur — erst „C# schreiben“ + bauen (erzeugt die Python-Basis).", null);
            neu = PythonQuelle.FuegeMethodeEin(text, a.Klasse, a.Signatur, inhalt);
            if (neu == null) return (false, $"Klasse {a.Klasse} nicht gefunden in {a.Datei}.", null);
        }
        return (true, "", neu);
    }


    /// <summary>Die Zeile der <c>def</c> der Methode (1-basiert, 0 = fehlt).</summary>
    public static int Zeile(PythonAnker a, string sln)
    {
        var p = Voll(a.Datei, sln);
        return File.Exists(p) && PythonQuelle.FindeMethode(File.ReadAllText(p), a.Klasse, a.Methode) is { } m ? m.DefZeile + 1 : 0;
    }

    /// <summary>Syntax-Prüfung mit Pythons eigenem Parser (<c>ast.parse</c>) über den ganzen Dateitext.</summary>
    public static List<string> Pruefe(string text)
    {
        try
        {
            var psi = new ProcessStartInfo("python3", "-c \"import ast,sys\ntry:\n ast.parse(sys.stdin.read())\nexcept SyntaxError as e:\n print(f'Zeile {e.lineno}: {e.msg}')\"")
            { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using var pr = Process.Start(psi)!;
            pr.StandardInput.Write(text);
            pr.StandardInput.Close();
            var aus = pr.StandardOutput.ReadToEnd().Trim();
            pr.WaitForExit(15000);
            return aus.Length == 0 ? [] : [$"Python-Syntax {aus}"];
        }
        catch (Exception ex) { return [$"python3 nicht ausführbar: {ex.Message}"]; }
    }

    /// <summary>
    /// Neue Client-Implementierungen anlegen (im Editor „+ Python-Implementierung"): je Implementierung, deren Datei es noch nicht
    /// gibt, eine Klasse mit den Methoden der GENERIERTEN Basis (<c>generated/vertraege.py</c> im Paket der Datei) und Platzhalter-
    /// Rümpfen. Fehlt die Basis (Vertrag noch nicht gebaut), wird gemeldet statt geraten. Bestehende Dateien bleiben unberührt.
    /// </summary>
    public static (List<string> Geschrieben, List<string> Uebersprungen) ImplementierungenAnlegen(EditorModell modell, string sln, bool trocken)
    {
        var geschrieben = new List<string>(); var uebersprungen = new List<string>();
        foreach (var c in modell.Clients)
            foreach (var im in c.Implementierungen.Where(i => i.Sprache == "python" && !string.IsNullOrWhiteSpace(i.Datei)))
            {
                var voll = Voll(im.Datei, sln);
                if (!voll.StartsWith(Path.GetFullPath(sln), StringComparison.Ordinal) || File.Exists(voll)) continue;
                var paketVerz = Path.GetDirectoryName(voll)!;
                var vertraege = Path.Combine(paketVerz, "generated", "vertraege.py");
                var client = c.Name.Length > 1 && c.Name[0] == 'I' && char.IsUpper(c.Name[1]) ? c.Name[1..] : c.Name;
                var basis = File.Exists(vertraege) ? PythonQuelle.ClientBasen(File.ReadAllText(vertraege)).FirstOrDefault(b => b.Client == client) : null;
                if (basis == null)
                {
                    uebersprungen.Add($"python {im.Klasse}: die Basis {client}Basis fehlt noch in {Path.GetRelativePath(sln, vertraege)} — erst den Vertrag schreiben und bauen, dann erneut");
                    continue;
                }
                if (!trocken) { Directory.CreateDirectory(paketVerz); File.WriteAllText(voll, PythonQuelle.NeueImplementierung(Path.GetFileName(paketVerz), basis, im.Klasse)); }
                geschrieben.Add($"{im.Datei} (python {im.Klasse} : {basis.Basis}, {basis.Auf.Count} Methode(n))");
            }
        return (geschrieben, uebersprungen);
    }

    private static string Hash(string inner) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PythonQuelle.Dedent(inner).Trim())))[..12];
}
