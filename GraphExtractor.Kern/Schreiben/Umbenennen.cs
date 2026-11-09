using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace GraphExtractor;

/// <summary>
/// Umbenennen als Geste (Werkbank P7.2): das Symbol am Ort der Karte (Typ, Methode …) wird mit dem Roslyn-<see cref="Renamer"/>
/// über die ganze Solution umbenannt — alle Verweise. Ergebnis ist der Patch (geänderte Dokumente).
/// </summary>
public static class Umbenennen
{
    /// <param name="datei">Datei der Karte (absolut).</param>
    /// <param name="start">Beginn der Deklaration (<c>_ort.start</c>).</param>
    public static async Task<(IReadOnlyList<DateiPatch> Patch, string Symbol)> Async(Solution solution, string datei, int start, string neu, string wurzel,
        CancellationToken ct = default)
    {
        if (!SyntaxFacts.IsValidIdentifier(neu)) throw new ArgumentException($"„{neu}“ ist kein gültiger Name.");
        var docId = solution.GetDocumentIdsWithFilePath(datei).FirstOrDefault()
            ?? throw new InvalidOperationException($"{Path.GetRelativePath(wurzel, datei)} gehört zu keinem Projekt.");
        var doc = solution.GetDocument(docId)!;
        var wurzelKnoten = (await doc.GetSyntaxRootAsync(ct))!;
        var modell = (await doc.GetSemanticModelAsync(ct))!;
        var symbol = Symbol(wurzelKnoten, modell, start, ct)
            ?? throw new InvalidOperationException($"An {Path.GetRelativePath(wurzel, datei)}:{start} steht keine benennbare Deklaration.");
        if (symbol.Name == neu) return ([], symbol.Name);

        var umbenannt = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), neu, ct);
        var patch = new List<DateiPatch>();
        foreach (var projekt in umbenannt.GetChanges(solution).GetProjectChanges())
            foreach (var id in projekt.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
            {
                var alt = solution.GetDocument(id)!;
                var nachher = umbenannt.GetDocument(id)!;
                if (alt.FilePath == null) continue;
                var rel = Path.GetRelativePath(wurzel, alt.FilePath).Replace('\\', '/');
                if (patch.Any(p => p.Datei == rel)) continue;   // dieselbe Datei in mehreren Projekten (verlinkt)
                patch.Add(new DateiPatch(rel, (await alt.GetTextAsync(ct)).ToString(), (await nachher.GetTextAsync(ct)).ToString()));
            }
        // Eine Übersetzung heißt nach ihren Typen ({TSource}_{TTarget}_{n}, docs/04 §4.4): ihr Name zieht mit, wenn einer davon umbenannt wird.
        if (symbol is INamedTypeSymbol)
            for (var i = 0; i < patch.Count; i++)
                if (patch[i].Nachher is { } text && UebersetzungenNachziehen(text, symbol.Name, neu) is { } neuText && neuText != text)
                    patch[i] = patch[i] with { Nachher = neuText };
        // Ein Typ in Dateien seines Namens (<alt>.cs, Teile <alt>.<Teil>.cs wie <A>.Decider.cs/<Store>.Lesen.cs, docs/07 §7.4): die Dateien
        //   ziehen mit (entfernt + neu angelegt) — sonst hieße INeuerDienst.cs bald IGrenzwerte.
        static bool Eigene(string datei, string name) => datei == name + ".cs" || (datei.StartsWith(name + ".", StringComparison.Ordinal) && datei.EndsWith(".cs", StringComparison.Ordinal));
        if (symbol is INamedTypeSymbol)
            foreach (var r in symbol.DeclaringSyntaxReferences.Where(r => Eigene(Path.GetFileName(r.SyntaxTree.FilePath), symbol.Name)))
            {
                var rel = Path.GetRelativePath(wurzel, r.SyntaxTree.FilePath).Replace('\\', '/');
                var neuRel = Path.Combine(Path.GetDirectoryName(rel) ?? "", neu + Path.GetFileName(rel)[symbol.Name.Length..]).Replace('\\', '/');
                if (File.Exists(Path.Combine(wurzel, neuRel)) || patch.Any(p => p.Datei == neuRel)) continue;
                var i = patch.FindIndex(p => p.Datei == rel);
                var text = i >= 0 ? patch[i].Nachher : r.SyntaxTree.GetText(ct).ToString();
                var vorher = i >= 0 ? patch[i].Vorher : text;
                if (i >= 0) patch.RemoveAt(i);
                patch.Add(new DateiPatch(rel, vorher, null));
                patch.Add(new DateiPatch(neuRel, null, text));
            }
        return (patch, symbol.Name);
    }

    /// <summary>
    /// Benennt in <paramref name="text"/> jede Klasse <c>{alt}_{Ziel}_{n}</c> bzw. <c>{Quelle}_{alt}_{n}</c>, die <c>ITranslate&lt;…&gt;</c> mit dem schon
    /// umbenannten Typ <paramref name="neu"/> implementiert, nach ihm um. Niemand verweist auf sie (das Generat folgt dem Build), nur die Deklaration zieht mit.
    /// </summary>
    private static string? UebersetzungenNachziehen(string text, string alt, string neu)
    {
        var baum = CSharpSyntaxTree.ParseText(text);
        var wurzel = baum.GetRoot();
        var klassen = wurzel.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Select(k => (k, Typen: k.BaseList?.Types.Select(b => b.Type).OfType<GenericNameSyntax>()
                .FirstOrDefault(g => g.Identifier.Text == "ITranslate" && g.TypeArgumentList.Arguments.Count == 2)?.TypeArgumentList.Arguments
                .Select(a => a is QualifiedNameSyntax q ? q.Right.Identifier.Text : a is SimpleNameSyntax s ? s.Identifier.Text : a.ToString()).ToArray()))
            .Where(x => x.Typen != null).ToList();
        string? Neu(string name, string[] typen)
        {
            var teile = name.Split('_');
            if (teile.Length != 3 || !int.TryParse(teile[2], out _)) return null;
            var (q, z) = (teile[0] == alt && typen[0] == neu ? neu : teile[0], teile[1] == alt && typen[1] == neu ? neu : teile[1]);
            if ((q, z) == (teile[0], teile[1])) return null;
            var stamm = q + "_" + z + "_";
            var n = int.Parse(teile[2]);
            while (klassen.Any(x => x.k.Identifier.Text == stamm + n)) n++;   // der Zähler bleibt eindeutig
            return stamm + n;
        }
        var ersetzt = klassen.Select(x => (x.k, Neu: Neu(x.k.Identifier.Text, x.Typen!))).Where(x => x.Neu != null).ToList();
        if (ersetzt.Count == 0) return null;
        return wurzel.ReplaceTokens(ersetzt.Select(x => x.k.Identifier), (alt_, _) =>
            SyntaxFactory.Identifier(alt_.LeadingTrivia, ersetzt.First(x => x.k.Identifier == alt_).Neu!, alt_.TrailingTrivia)).ToFullString();
    }

    /// <summary>Das deklarierte Symbol, dessen Deklaration bei <paramref name="start"/> beginnt (für eine lokale Deklaration: ihre Variable).</summary>
    private static ISymbol? Symbol(SyntaxNode wurzel, SemanticModel modell, int start, CancellationToken ct)
    {
        foreach (var n in wurzel.FindToken(start).Parent?.AncestorsAndSelf().Where(n => n.SpanStart == start) ?? [])
        {
            var ziel = n switch
            {
                LocalDeclarationStatementSyntax ld => ld.Declaration.Variables.FirstOrDefault(),
                FieldDeclarationSyntax fd => fd.Declaration.Variables.FirstOrDefault(),
                _ => n,
            };
            if (ziel != null && modell.GetDeclaredSymbol(ziel, ct) is { } s) return s;
        }
        return null;
    }
}
