using System.Text.RegularExpressions;
using P = Abstractions.Projektstruktur;

namespace GraphExtractor;

/// <summary>
/// Kataloge anlegen und nutzen (docs/07 §7.2): ein Funktionskatalog ist ein eigenes Projekt <c>&lt;Name&gt;.Katalog</c>,
/// das nur <c>Abstractions</c> referenziert. Die Geste schreibt seine Projektdatei, trägt ihn in die Solution ein und gibt den Übersetzungen der
/// Anwendung die Referenz (sie nehmen seine Nachrichten, die Domäne bleibt rein, docs/07 §7.2) — als Text-Patch über den <see cref="Dateipuffer"/> (Verlauf und ↶ wie jede Geste). Die Werkbank lädt danach kalt neu.
/// </summary>
public static class Kataloge
{
    /// <summary>Der Projekttyp eines SDK-Projekts in der Solution.</summary>
    private const string CSharpProjekt = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";

    /// <summary>
    /// Legt den Katalog <paramref name="name"/> an (<c>Bild</c> oder <c>Bild.Katalog</c>) und lässt die Anwendung <paramref name="wurzel"/> ihn nutzen.
    /// Liefert den Projektnamen; wirft bei einem ungültigen oder vergebenen Namen.
    /// </summary>
    public static string Neu(Dateipuffer puffer, string name, string wurzel = P.StandardWurzel)
    {
        var projekt = Projektname(name);
        var datei = $"{projekt}/{projekt}.csproj";
        if (puffer.Lies(datei) != null) throw new InvalidOperationException($"Den Katalog {projekt} gibt es schon.");
        var sln = Solution(puffer);
        if (Regex.IsMatch(puffer.Lies(sln)!, $"\"{Regex.Escape(projekt)}\"")) throw new InvalidOperationException($"{projekt} steht schon in der Solution.");

        puffer.Setze(datei, Projektdatei());
        puffer.Setze(sln, InSolution(puffer.Lies(sln)!, projekt));
        Nutzen(puffer, projekt, true, wurzel);
        return projekt;
    }

    /// <summary>Die Anwendung nutzt den Katalog (Referenz der Übersetzungen setzen) bzw. nicht mehr (Referenz entfernen).</summary>
    public static void Nutzen(Dateipuffer puffer, string katalog, bool nutzen, string wurzel = P.StandardWurzel)
    {
        var uebersetzungen = P.Projekt(wurzel, P.Rolle.Uebersetzungen);
        Referenz(puffer, uebersetzungen, katalog, nutzen, $"die Anwendung {wurzel} hat kein Projekt {uebersetzungen}");
    }

    /// <summary>Der Speicher der Anwendung nutzt den Katalog: er setzt eine Lese-Fähigkeit um, die der Katalog deklariert (CQRS124). Idempotent.</summary>
    public static void SpeicherNutzt(Dateipuffer puffer, string katalog, string wurzel = P.StandardWurzel) =>
        Referenz(puffer, P.Projekt(wurzel, P.Rolle.Speicher), katalog, true, $"die Anwendung {wurzel} hat keinen Speicher");

    /// <summary>
    /// Der Katalog <paramref name="nutzer"/> nutzt den vorhandenen Katalog <paramref name="katalog"/> (Referenz in seiner Projektdatei) — etwa, weil er
    /// dessen Commands als Input oder Output nimmt. Kreise sind verboten.
    /// </summary>
    public static void KatalogNutzt(Dateipuffer puffer, string nutzer, string katalog)
    {
        if (!P.IstKatalog(nutzer)) throw new ArgumentException($"{nutzer} ist kein Katalog (<Name>{P.Katalog.Suffix}).");
        if (nutzer == katalog) return;
        if (puffer.Lies($"{katalog}/{katalog}.csproj") == null) throw new InvalidOperationException($"Den Katalog {katalog} gibt es nicht.");
        if (P.KatalogeIn(puffer.Lies($"{katalog}/{katalog}.csproj") ?? "").Contains(nutzer, StringComparer.Ordinal))
            throw new InvalidOperationException($"{katalog} nutzt schon {nutzer} — umgekehrt wäre es ein Kreis.");
        Referenz(puffer, nutzer, katalog, true, $"den Katalog {nutzer} gibt es nicht");
    }

    private static void Referenz(Dateipuffer puffer, string projekt, string katalog, bool nutzen, string fehlt)
    {
        if (!P.IstKatalog(katalog)) throw new ArgumentException($"{katalog} ist kein Katalog (<Name>{P.Katalog.Suffix}).");
        var datei = $"{projekt}/{projekt}.csproj";
        var text = puffer.Lies(datei) ?? throw new InvalidOperationException($"{datei} fehlt — {fehlt}.");
        var hat = P.KatalogeIn(text).Contains(katalog, StringComparer.Ordinal);
        if (nutzen == hat) return;
        var zeile = $"<ProjectReference Include=\"..\\{katalog}\\{katalog}.csproj\" />";
        if (nutzen)
        {
            // Hinter die letzte vorhandene Projekt-Referenz, mit ihrer Einrückung.
            var letzte = Regex.Matches(text, @"(?m)^([ \t]*)<ProjectReference\b[^\n]*\n").LastOrDefault()
                ?? throw new InvalidOperationException($"{datei} hat keine Projekt-Referenz, an die der Katalog anschließen könnte.");
            text = text.Insert(letzte.Index + letzte.Length, letzte.Groups[1].Value + zeile + "\n");
        }
        else
            text = Regex.Replace(text, $@"(?m)^[ \t]*<ProjectReference\s+Include=""\.\.[\\/]{Regex.Escape(katalog)}[\\/]{Regex.Escape(katalog)}\.csproj""\s*/>[ \t]*\r?\n", "");
        puffer.Setze(datei, text);
    }

    /// <summary>Der Projektname zu einem eingegebenen Namen: <c>Bild</c> → <c>Bild.Katalog</c>; geprüft wie ein Namespace.</summary>
    public static string Projektname(string name)
    {
        var n = (name ?? "").Trim();
        if (n.EndsWith(P.Katalog.Suffix, StringComparison.Ordinal)) n = n[..^P.Katalog.Suffix.Length];
        if (!Regex.IsMatch(n, "^" + DomainEditor.Grammatik.Typname + @"(\." + DomainEditor.Grammatik.Typname + ")*$"))
            throw new ArgumentException("Ein Katalogname ist ein Namespace: groß beginnen, nur Buchstaben und Ziffern, Teile durch Punkte getrennt.");
        return n + P.Katalog.Suffix;
    }

    /// <summary>Die Projektdatei eines neuen Katalogs: nur <c>Abstractions</c>, dazu die Domänen-Analyzer (CQRS098, CQRS100 …) wie bei den Abläufen.</summary>
    public static string Projektdatei() =>
        """
        <Project Sdk="Microsoft.NET.Sdk">

            <!-- Ein Funktionskatalog (docs/07 §7.2): Funktionen, ihre Nachrichten, Konfig, Technik und Graphen, domänenneutral —
                 er referenziert nur Abstractions (und andere Kataloge), nie eine Anwendung. -->
            <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>

            <ItemGroup>
                <ProjectReference Include="..\Domain.SourceGeneration\Domain.SourceGeneration.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <ProjectReference Include="..\Abstractions\Abstractions.csproj" />
            </ItemGroup>

        </Project>

        """;

    /// <summary>Die Solution-Datei (die erste <c>.sln</c> im Wurzelverzeichnis, wie der Arbeitsbereich sie öffnet).</summary>
    private static string Solution(Dateipuffer puffer) =>
        Directory.EnumerateFiles(puffer.Wurzel, "*.sln").Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault()
        ?? throw new InvalidOperationException("Keine Solution-Datei gefunden.");

    /// <summary>
    /// Trägt das Projekt in die Solution ein: der Projekt-Eintrag hinter dem letzten, je Lösungs-Konfiguration seine Zuordnung (wie <c>dotnet sln add</c>).
    /// Die Projekt-Id ist aus dem Namen abgeleitet — derselbe Name ergibt dieselbe Id.
    /// </summary>
    public static string InSolution(string sln, string projekt)
    {
        var nl = sln.Contains("\r\n") ? "\r\n" : "\n";
        var id = Abstractions.Herkunft.Ableite("katalog:" + projekt).ToString("D").ToUpperInvariant();
        var eintrag = $"Project(\"{CSharpProjekt}\") = \"{projekt}\", \"{projekt}\\{projekt}.csproj\", \"{{{id}}}\"{nl}EndProject{nl}";
        var letztes = sln.LastIndexOf("EndProject" + nl, StringComparison.Ordinal);
        if (letztes < 0) throw new InvalidOperationException("Die Solution hat keinen Projekt-Eintrag.");
        sln = sln.Insert(letztes + ("EndProject" + nl).Length, eintrag);

        var konfigs = Regex.Match(sln, @"GlobalSection\(SolutionConfigurationPlatforms\) = preSolution\r?\n(?<z>(?:\t\t[^\r\n]*\r?\n)*?)\tEndGlobalSection");
        var zuordnung = Regex.Match(sln, @"GlobalSection\(ProjectConfigurationPlatforms\) = postSolution\r?\n(?:\t\t[^\r\n]*\r?\n)*?(?<ende>\tEndGlobalSection)");
        if (konfigs.Success && zuordnung.Success)
        {
            var zeilen = Regex.Matches(konfigs.Groups["z"].Value, @"\t\t(?<k>[^=\r\n]+?) = ").Select(m => m.Groups["k"].Value.Trim())
                .SelectMany(k => new[] { $"\t\t{{{id}}}.{k}.ActiveCfg = {Basis(k)}|Any CPU{nl}", $"\t\t{{{id}}}.{k}.Build.0 = {Basis(k)}|Any CPU{nl}" });
            sln = sln.Insert(zuordnung.Groups["ende"].Index, string.Concat(zeilen));
        }
        return sln;

        static string Basis(string konfiguration) => konfiguration.Split('|')[0];
    }
}
