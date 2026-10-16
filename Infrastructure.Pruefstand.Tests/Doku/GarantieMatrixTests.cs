using System.Text.RegularExpressions;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Doku;

/// <summary>
/// Die Garantie-Matrix bleibt wahr (docs/11 §11.1, §11.14): jeder Nachweis, den die Nachweis-Spalte von docs/11 nennt
/// (<c>P|I|PD `Klasse[.Methode[…]]`</c>), existiert als Test im genannten Projekt, und jeder Test in einem Ordner <c>Garantien/</c> steht in der
/// Spalte. Gelesen wird Text — die Markdown-Tabelle und die Quelltexte der Test-Projekte, keine Reflection.
/// </summary>
public class GarantieMatrixTests
{
    private static readonly Dictionary<string, string> Projekte = new()
    {
        ["P"] = "Infrastructure.Pruefstand.Tests", ["I"] = "Infrastructure.Integration.Tests", ["PD"] = "Pruefdomaene.Integration.Tests",
    };

    private sealed record Nachweis(string Ebene, string Klasse, string? Methode, bool Praefix, string Zeile)
    {
        public bool Deckt(string ebene, string klasse, string methode)
            => Ebene == ebene && Klasse == klasse
               && (Methode is null || (Praefix ? methode.StartsWith(Methode, StringComparison.Ordinal) : methode == Methode));
    }

    private sealed record Test(string Ebene, string Klasse, string Methode, string Datei);

    private static readonly Regex NachweisMuster = new(@"\b(PD|P|I) `([A-Za-z0-9_]+)(?:\.([A-Za-z0-9_]+)(…)?)?`", RegexOptions.Compiled);
    private static readonly Regex KlassenMuster = new(@"^(?:public |internal )?(?:sealed |static |abstract |partial )*class ([A-Za-z0-9_]+Tests)\b", RegexOptions.Compiled);
    private static readonly Regex MethodenMuster = new(@"^\s+public (?:async )?(?:Task|void) ([A-Za-z0-9_]+)\(", RegexOptions.Compiled);

    [Fact]
    public void Jeder_Nachweis_in_docs_11_existiert_als_Test()
    {
        var tests = Tests();
        var fehlend = Nachweise()
            .Where(n => !tests.Any(t => n.Deckt(t.Ebene, t.Klasse, t.Methode)))
            .Select(n => $"{n.Ebene} {n.Klasse}{(n.Methode is null ? "" : "." + n.Methode + (n.Praefix ? "…" : ""))}  ← {n.Zeile[..Math.Min(70, n.Zeile.Length)]}")
            .ToList();
        fehlend.Should().BeEmpty("docs/11 nennt nur Nachweise, die es gibt");
    }

    [Fact]
    public void Jeder_Test_in_einem_Ordner_Garantien_steht_in_docs_11()
    {
        var nachweise = Nachweise();
        var ohne = Tests()
            .Where(t => t.Datei.Contains($"{Path.DirectorySeparatorChar}Garantien{Path.DirectorySeparatorChar}"))
            .Where(t => !nachweise.Any(n => n.Deckt(t.Ebene, t.Klasse, t.Methode)))
            .Select(t => $"{t.Ebene} {t.Klasse}.{t.Methode}")
            .ToList();
        ohne.Should().BeEmpty("ein Garantie-Test ohne Zeile in docs/11 beweist nichts, was dort steht");
    }

    [Fact]
    public void Die_Matrix_ist_nicht_leer()
    {
        // Gegenprobe gegen einen Parser, der still nichts findet. Test-Klassen heißen „…Tests“ (Quelltext-Proben in den Analyzer-Tests
        //   deklarieren selbst Klassen in Spalte 0).
        Nachweise().Select(n => n.Ebene).Distinct().Should().BeEquivalentTo(new[] { "P", "I", "PD" });
        Tests().Count(t => t.Datei.Contains("Garantien")).Should().BeGreaterThan(40);
    }

    private static List<Nachweis> Nachweise()
    {
        // Tabellen und Fließtext: ein Lücken-Test (Skip) steht in §11.13, nicht in einer Tabelle.
        return File.ReadAllLines(Path.Combine(Wurzel(), "docs", "11-garantien.md"))
            .SelectMany(z => NachweisMuster.Matches(z).Select(m => new Nachweis(m.Groups[1].Value, m.Groups[2].Value,
                m.Groups[3].Success ? m.Groups[3].Value : null, m.Groups[4].Success, z)))
            .ToList();
    }

    private static List<Test> Tests()
    {
        var tests = new List<Test>();
        foreach (var (ebene, projekt) in Projekte)
        {
            var verz = Path.Combine(Wurzel(), projekt);
            foreach (var datei in Directory.EnumerateFiles(verz, "*.cs", SearchOption.AllDirectories))
            {
                var teile = Path.GetRelativePath(verz, datei).Split(Path.DirectorySeparatorChar);
                if (teile[0] is "obj" or "bin") continue;
                string? klasse = null;
                var test = false;
                foreach (var z in File.ReadLines(datei))
                {
                    if (KlassenMuster.Match(z) is { Success: true } k) klasse = k.Groups[1].Value;
                    else if (Regex.IsMatch(z, @"^\s+\[(Fact|Theory)\b")) test = true;
                    else if (test && MethodenMuster.Match(z) is { Success: true } m)
                    {
                        if (klasse != null) tests.Add(new Test(ebene, klasse, m.Groups[1].Value, datei));
                        test = false;
                    }
                }
            }
        }
        return tests;
    }

    private static string Wurzel()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "CqrsSolution.sln"))) return d.FullName;
        throw new InvalidOperationException("Repo-Wurzel (CqrsSolution.sln) nicht gefunden");
    }
}
