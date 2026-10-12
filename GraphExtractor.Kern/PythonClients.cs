using DomainEditor;

/// <summary>
/// Die PYTHON-Implementierungen der Clients (docs/konzept-akteure.md §4) — Code-Fakt statt Konvention: die generierte
/// <c>…/generated/vertraege.py</c> (Cqrs.Codegen) nennt je Client seine Basis und je Zusage/Kenntnis die Methode; jede Klasse im selben
/// Python-Projekt, die diese Basis erbt, ist eine Implementierung, ihre <c>auf_…</c>-Rümpfe sind die Logik des Akteurs draußen.
/// </summary>
public static class PythonClients
{
    private static readonly HashSet<string> Ueberspringen = new(StringComparer.Ordinal)
        { "bin", "obj", "node_modules", ".git", ".venv", "venv", "__pycache__", "site-packages", ".llm-kontext", "generated", "tests" };

    /// <summary>Das (erste) Python-Paket mit einer generierten <c>generated/vertraege.py</c>, relativ zur Wurzel — null = keins.</summary>
    public static string? Paket(string wurzel) =>
        string.IsNullOrEmpty(wurzel) || !Directory.Exists(wurzel) ? null
        : Dateien(wurzel, "vertraege.py", mitGeneriert: true).Where(p => Path.GetFileName(Path.GetDirectoryName(p)) == "generated")
            .Select(p => Path.GetRelativePath(wurzel, Path.GetDirectoryName(Path.GetDirectoryName(p)!)!).Replace('\\', '/')).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Handshake-Name (Interface ohne führendes I) → Implementierungen.</summary>
    public static Dictionary<string, List<ClientImpl>> Lies(string wurzel)
    {
        var erg = new Dictionary<string, List<ClientImpl>>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(wurzel) || !Directory.Exists(wurzel)) return erg;
        foreach (var vertraege in Dateien(wurzel, "vertraege.py", mitGeneriert: true).Where(p => Path.GetFileName(Path.GetDirectoryName(p)) == "generated"))
        {
            var basen = PythonQuelle.ClientBasen(File.ReadAllText(vertraege));
            if (basen.Count == 0) continue;
            // generated/ liegt im Paket (domain_client/), das Paket im Python-Projekt — dort stehen die Implementierungen.
            var projekt = Directory.GetParent(Path.GetDirectoryName(Path.GetDirectoryName(vertraege)!)!)!.FullName;
            foreach (var py in Dateien(projekt, "*.py", mitGeneriert: false).Where(p => !Path.GetFileName(p).StartsWith("test_", StringComparison.Ordinal)))
            {
                var text = File.ReadAllText(py);
                foreach (var k in PythonQuelle.Klassen(text))
                    foreach (var b in basen.Where(b => k.Basen.Contains(b.Basis)))
                    {
                        if (!erg.TryGetValue(b.Client, out var liste)) erg[b.Client] = liste = new();
                        liste.Add(new ClientImpl
                        {
                            Sprache = "python", Klasse = k.Name, Datei = Path.GetRelativePath(wurzel, py).Replace('\\', '/'),
                            Rumpfe = b.Auf.Select(a => new ClientRumpf
                            {
                                Event = a.Event, Methode = a.Methode, Rumpf = PythonQuelle.Rumpf(text, k.Name, a.Methode), Signatur = a.Def,
                                Ausgaenge = a.Ausgaenge, Akteur = a.Akteur,
                            }).ToList(),
                        });
                    }
            }
        }
        foreach (var l in erg.Values) l.Sort((a, b) => string.CompareOrdinal(a.Datei + a.Klasse, b.Datei + b.Klasse));
        return erg;
    }

    private static IEnumerable<string> Dateien(string verz, string muster, bool mitGeneriert)
    {
        var stapel = new Stack<string>([verz]);
        while (stapel.Count > 0)
        {
            var d = stapel.Pop();
            IEnumerable<string> dateien, unter;
            try { dateien = Directory.EnumerateFiles(d, muster); unter = Directory.EnumerateDirectories(d); } catch { continue; }
            foreach (var f in dateien) yield return f;
            foreach (var u in unter)
            {
                var n = Path.GetFileName(u);
                if (n.StartsWith('.') || (Ueberspringen.Contains(n) && !(mitGeneriert && n == "generated"))) continue;
                stapel.Push(u);
            }
        }
    }
}
