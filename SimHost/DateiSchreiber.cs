using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimHost;

/// <summary>Bericht eines Schreiblaufs: was neu, was ergänzt (additiv), was unangetastet blieb.</summary>
public sealed record SchreibBericht(bool Ok, List<string> Geschrieben, List<string> Ergaenzt, List<string> Uebersprungen, string? Grund = null);

/// <summary>
/// Schreibt die Scaffolder-Ausgabe in die ECHTEN <c>.cs</c>-Dateien — an die Pfade, die das Modell aus dem Code kennt
/// (echte Datei eines Typs bzw. das aus dem Code abgeleitete Verzeichnis/Dateimuster), <b>chirurgisch und additiv</b>:
///
///   • <b>Typ-Dateien</b> (<see cref="DateiArt.Typen"/>): fehlt die Datei → voll schreiben; existiert sie → nur die noch
///     NICHT deklarierten Typen in DEN Namespace-Block ihres Namespace einfügen (Match über den Typnamen). Bestehendes
///     bleibt Wort für Wort erhalten. Kennt der Code für einen Namespace kein Verzeichnis, wird nicht geschrieben.
///   • <b>Decider/Applier</b>: fehlt die Datei → voll schreiben (throw-Platzhalter); existiert sie → nur fehlende
///     Methoden (Match über den ersten Parametertyp) in die Klasse mit derselben Rolle für dasselbe Aggregat
///     (gleicher Basistyp, z. B. <c>IDecider&lt;Konto&gt;</c>) einfügen.
///   • <b>State/Saga</b>: fehlt → voll schreiben; existiert → unangetastet (dort lebt Handcode).
///
/// Welche Rolle eine Datei hat, sagt der Scaffolder (<see cref="DateiArt"/>) — nie ihr Dateiname.
/// </summary>
public static class DateiSchreiber
{

    public static SchreibBericht Schreibe(EditorModell modell, string slnRoot)
    {
        var geschrieben = new List<string>();
        var ergaenzt = new List<string>();
        var uebersprungen = new List<string>();

        // Das Modell enthält nur Domänen-Typen (der Extractor filtert Framework-Typen über die Projektlage). Wohin ein Typ
        //   gehört, sagt seine echte Datei bzw. das aus dem Code bekannte Verzeichnis seines Namespace — kennt der Code
        //   keines, markiert der Scaffolder die Datei als nicht platzierbar, und es wird NICHT geraten.
        foreach (var d in Scaffolder.Generiere(modell))
        {
            if (!d.Platzierbar)
            {
                uebersprungen.Add($"{d.Pfad[2..]} (Namespace {NamespaceVon(d.Inhalt)}: kein Verzeichnis aus dem Code bekannt — Datei im Editor zuweisen)");
                continue;
            }
            // Der Pfad kommt aus dem Modell (echte Datei bzw. aus dem Code abgeleitetes Verzeichnis), relativ zur Solution.
            var pfad = Path.GetFullPath(Path.Combine(slnRoot, d.Pfad.Replace('/', Path.DirectorySeparatorChar)));
            var rel = d.Pfad;
            if (!pfad.StartsWith(Path.GetFullPath(slnRoot), StringComparison.Ordinal))
            {
                uebersprungen.Add($"{rel} (außerhalb der Solution)");
                continue;
            }

            if (!File.Exists(pfad))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
                File.WriteAllText(pfad, d.Inhalt);
                geschrieben.Add(rel);
                continue;
            }

            var alt = File.ReadAllText(pfad);
            // Die ROLLE der Datei (vom Scaffolder) bestimmt das Mischen — nicht ihr Dateiname.
            (int N, string Neu)? gemischt = d.Art switch
            {
                DateiArt.Typen => TypenMergen(alt, d.Inhalt),
                DateiArt.Decider or DateiArt.Applier => MethodenMergen(alt, d.Inhalt),
                _ => null,
            };
            if (gemischt is null) uebersprungen.Add($"{rel} (Rumpf-Datei, unangetastet)");
            else if (gemischt.Value.N > 0) { File.WriteAllText(pfad, gemischt.Value.Neu); ergaenzt.Add($"{rel} (+{gemischt.Value.N})"); }
            else uebersprungen.Add(rel);
        }

        return new SchreibBericht(true, geschrieben, ergaenzt, uebersprungen);
    }

    // Der file-scoped Namespace einer generierten Datei (jede Scaffolder-Datei hat `namespace X;`).
    private static string NamespaceVon(string inhalt)
    {
        var root = CSharpSyntaxTree.ParseText(inhalt).GetRoot();
        return root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString()).FirstOrDefault() ?? "";
    }

    // ── Typ-Dateien: fehlende Record-/Enum-Deklarationen (nach Name, JE NAMESPACE) in DEN Namespace-Block der Datei, zu
    //    dem sie gehören (eine Datei darf mehrere tragen); nötige usings ergänzen. ──
    private static (int, string) TypenMergen(string altText, string genText)
    {
        var altRoot = CSharpSyntaxTree.ParseText(altText).GetRoot();
        var genRoot = CSharpSyntaxTree.ParseText(genText).GetRoot();
        var ns = NamespaceVon(genText);
        var ziel = altRoot.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault(n => n.Name.ToString() == ns);

        var vorhanden = (ziel ?? altRoot).DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
            .Select(t => t.Identifier.Text).ToHashSet(StringComparer.Ordinal);
        var fehlend = genRoot.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
            .Where(t => ziel == null || !vorhanden.Contains(t.Identifier.Text)).ToList();
        if (ziel != null && fehlend.Count == 0) return (0, altText);
        // Fremder Namespace in einer Datei mit Datei-Namespace: kein zweiter Namespace möglich (CS8955) → nicht schreiben.
        if (ziel == null && altRoot.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>().Any()) return (0, altText);

        var fehlendeUsings = genRoot.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString().Trim())
            .Where(u => !altText.Contains(u)).ToList();
        var anhang = string.Join("\n\n", fehlend.Select(t => t.ToFullString().Trim()));

        string body;
        if (ziel is NamespaceDeclarationSyntax block)
        {
            // Block-Namespace: VOR seine schließende Klammer, eingerückt wie seine Member.
            var einr = "    ";
            var eingerueckt = string.Join("\n", anhang.Split('\n').Select(z => z.Length == 0 ? z : einr + z));
            body = altText[..block.CloseBraceToken.SpanStart].TrimEnd() + "\n\n" + eingerueckt + "\n" + altText[block.CloseBraceToken.SpanStart..];
        }
        else if (ziel is FileScopedNamespaceDeclarationSyntax)
            body = altText.TrimEnd() + "\n\n" + anhang + "\n";
        else
            // Der Namespace kommt in der Datei noch nicht vor: als eigener Block anhängen (Datei-Namespace bleibt unberührt).
            body = altText.TrimEnd() + $"\n\nnamespace {ns}\n{{\n" + string.Join("\n", anhang.Split('\n').Select(z => z.Length == 0 ? z : "    " + z)) + "\n}\n";
        if (fehlendeUsings.Count > 0) body = string.Join("\n", fehlendeUsings) + "\n" + body;
        return (fehlend.Count, body);
    }

    // ── Klassen-Dateien: fehlende Entscheidungs-/Faltungs-Methoden (nach erstem Parametertyp) in DIE Klasse, die dieselbe
    //    Rolle für DASSELBE Aggregat trägt wie die generierte (gleicher Basistyp, z. B. IDecider<Konto>) — nicht die erste
    //    gleichnamige Klasse der Datei (eine Datei darf mehrere Aggregate tragen). ──
    private static (int, string) MethodenMergen(string altText, string genText)
    {
        static string? RollenBasis(ClassDeclarationSyntax c) =>
            c.BaseList?.Types.Select(t => t.Type).OfType<GenericNameSyntax>().Select(g => g.ToString().Replace(" ", "")).FirstOrDefault();
        var genKlasse = CSharpSyntaxTree.ParseText(genText).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => RollenBasis(c) != null);
        var rolle = genKlasse == null ? null : RollenBasis(genKlasse);
        var altRoot = CSharpSyntaxTree.ParseText(altText).GetRoot();
        var innere = rolle == null ? null : altRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.BaseList?.Types.Any(t => t.Type is GenericNameSyntax g && g.ToString().Replace(" ", "") == rolle) == true);
        if (innere is null) return (0, altText);

        static string? Disc(MethodDeclarationSyntax m) =>
            m.Modifiers.Any(x => x.Text == "public") && m.ParameterList.Parameters.Count > 0
                ? m.ParameterList.Parameters[0].Type?.ToString().Split('.').Last() : null;

        var vorhanden = innere.Members.OfType<MethodDeclarationSyntax>()
            .Select(Disc).Where(x => x is not null).ToHashSet(StringComparer.Ordinal);

        var genRoot = CSharpSyntaxTree.ParseText(genText).GetRoot();
        var fehlend = genRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => Disc(m) is { } disc && !vorhanden.Contains(disc)).ToList();
        if (fehlend.Count == 0) return (0, altText);

        // Die schließende-Klammer-Region deterministisch neu setzen (unabhängig davon, wie Roslyn die
        //   Trivia aufteilt): alles vor der Klammer trimmen, Methoden anhängen, dann die innere Klammer
        //   wieder 4-fach eingerückt. Signatur/Body der Methoden sind schon 8/12-eingerückt.
        var klammer = innere.CloseBraceToken;
        var vor = altText[..klammer.SpanStart].TrimEnd();
        var nach = altText[klammer.Span.End..];
        var methoden = string.Join("\n\n", fehlend.Select(m => "        " + m.ToString()));
        return (fehlend.Count, vor + "\n\n" + methoden + "\n    }" + nach);
    }
}
