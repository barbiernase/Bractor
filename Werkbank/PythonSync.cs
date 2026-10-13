using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DomainEditor;

namespace Werkbank;

/// <summary>Anker eines Python-Rumpfs (Zusage/Kenntnis einer Client-Implementierung): Datei relativ zur Solution, Klasse, Methode.</summary>
public sealed record PythonAnker(string Datei, string Klasse, string Methode, string? Signatur);

/// <summary>
/// Die Synchronisations-Naht für PYTHON-Rümpfe (docs/konzept-akteure.md §4) — Spiegel von <see cref="CodeSync"/>: lesen (Rumpf, Prompt,
/// Hash), ersetzen mit optimistischer Sperre, eine fehlende Methode mit der Signatur der Basis anlegen, zurücknehmen. Die Datei ist die
/// Wahrheit; die Prompt-Zeile <c># 🤖 Prompt:</c> steht als erste Rumpfzeile.
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

    public static (bool Ok, string Grund, string? AlterInhalt, string? NeuerHash, int Zeile) SetzeRumpf(
        PythonAnker a, string rumpf, string? prompt, string? baseHash, string sln)
    {
        var p = Voll(a.Datei, sln);
        if (!File.Exists(p)) return (false, $"Datei fehlt: {a.Datei}", null, null, 0);
        var alt = File.ReadAllText(p);
        if (baseHash != null && Hash(PythonQuelle.Rumpf(alt, a.Klasse, a.Methode) ?? "") != baseHash)
            return (false, "Die Datei wurde seit dem Füllen geändert (Hash passt nicht) — neu laden.", null, null, 0);
        var (ok, grund, neu) = MitRumpf(a, rumpf, prompt, sln);
        if (!ok) return (false, grund, null, null, 0);
        File.WriteAllText(p, neu!);
        var m = PythonQuelle.FindeMethode(neu!, a.Klasse, a.Methode);
        return (true, "", alt, Hash(PythonQuelle.Rumpf(neu!, a.Klasse, a.Methode) ?? ""), (m?.DefZeile ?? 0) + 1);
    }

    /// <summary>Rückgängig: die ganze alte Datei zurück — nur, wenn der Rumpf seitdem unverändert ist.</summary>
    public static (bool Ok, string Grund) Zurueck(PythonAnker a, string alteDatei, string erwarteterHash, string sln)
    {
        var p = Voll(a.Datei, sln);
        if (!File.Exists(p)) return (false, $"Datei fehlt: {a.Datei}");
        if (Hash(PythonQuelle.Rumpf(File.ReadAllText(p), a.Klasse, a.Methode) ?? "") != erwarteterHash)
            return (false, "Die Datei wurde nach dem Übernehmen weiter geändert — Rückgängig verweigert.");
        File.WriteAllText(p, alteDatei);
        return (true, "");
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

    /// <summary>
    /// Neue Python-Implementierungen der Katalog-Funktionen anlegen (Plan E13), wenn die Umgebung eine Funktion EXTERN rechnen lässt
    /// (<c>Funktionen:&lt;F&gt;:Laufort = Extern</c>) und es noch keine gibt: <c>funktionen/&lt;modul&gt;.py</c> im Worker-Projekt mit einer Klasse, die die
    /// GENERIERTE Basis erbt (<c>generated/funktionen.py</c>), und <c>rufe</c> als Platzhalter (✎/🤖 füllt ihn). Datei und Klasse nach derselben Regel
    /// wie die generierte Tabelle des Workers (<see cref="Abstractions.Funktionsvertrag"/>). Fehlt die Basis (noch nicht gebaut): melden statt raten.
    /// </summary>
    public static (List<string> Geschrieben, List<string> Uebersprungen) FunktionenAnlegen(EditorModell modell, Abstractions.UmgebungKonfig u, string sln)
    {
        var geschrieben = new List<string>(); var uebersprungen = new List<string>();
        if (modell.Rahmen.PythonPaket is not { Length: > 0 } paketRel) return (geschrieben, uebersprungen);
        var paketVerz = Voll(paketRel, sln);
        var generat = Path.Combine(paketVerz, "generated", "funktionen.py");
        var basen = File.Exists(generat) ? PythonQuelle.FunktionsBasen(File.ReadAllText(generat)) : [];
        foreach (var f in modell.Funktionen.Where(f => f.Python == null && u.Funktionen.GetValueOrDefault(f.Name)?.Laufort == Abstractions.FunktionsLaufort.Extern))
        {
            if (basen.FirstOrDefault(b => b.Funktion == f.Name) is not { } basis)
            {
                uebersprungen.Add($"python {f.Name}: keine Basis in {Path.GetRelativePath(sln, generat)} (eine Funktion mit Lese-Fähigkeit läuft nur im Host)");
                continue;
            }
            var datei = Path.Combine(Path.GetDirectoryName(paketVerz)!, Abstractions.Funktionsvertrag.PythonOrdner, Abstractions.Funktionsvertrag.PythonModul(f.Name) + ".py");
            if (File.Exists(datei)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
            var init = Path.Combine(Path.GetDirectoryName(datei)!, "__init__.py");
            if (!File.Exists(init)) File.WriteAllText(init, "\"\"\"Die Python-Implementierungen der Katalog-Funktionen (Laufort Extern).\"\"\"\n");
            File.WriteAllText(datei, PythonQuelle.NeueFunktionsImplementierung(Path.GetFileName(paketVerz), basis, Abstractions.Funktionsvertrag.OhneI(f.Name)));
            geschrieben.Add($"{Path.GetRelativePath(sln, datei)} (python {Abstractions.Funktionsvertrag.OhneI(f.Name)} : {basis.Basis})");
        }
        return (geschrieben, uebersprungen);
    }

    private static string Hash(string inner) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PythonQuelle.Dedent(inner).Trim())))[..12];
}
