using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimHost;

/// <summary>
/// Bericht eines Schreiblaufs: was neu, was ergänzt (additiv), was unangetastet blieb. <see cref="Fehler"/>: Compiler-Fehler
/// der betroffenen Projekte nach dem Schreiben (z. B. ein gelöster Fähigkeits-Parameter, den der Rumpf noch benutzt).
/// </summary>
public sealed record SchreibBericht(bool Ok, List<string> Geschrieben, List<string> Ergaenzt, List<string> Uebersprungen, string? Grund = null)
{
    public List<string> Fehler { get; init; } = new();
    /// <summary>Die geschriebenen/ergänzten Dateien (relativ) — für den Bau der betroffenen Projekte.</summary>
    public List<string> Dateien { get; init; } = new();
}

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
///   • <b>Leseseite</b>: Fähigkeiten/Bündel → fehlende Interfaces + fehlende Fähigkeiten in der Basisliste des Bündels;
///     Store-Impl → fehlende Methoden (throw-Platzhalter); Projektion/Reaktion/Reader → fehlende Handles. Danach der
///     <b>Parameter-Abgleich</b>: je bestehendem Handle werden die Fähigkeits-Parameter (alles hinter dem Kontext, CQRS057)
///     auf das Modell gebracht — hinzu/weg, NUR die Parameterliste; der Rumpf bleibt Wort für Wort.
///
/// Welche Rolle eine Datei hat, sagt der Scaffolder (<see cref="DateiArt"/>) — nie ihr Dateiname.
/// </summary>
public static partial class DateiSchreiber
{

    /// <summary>
    /// Schreibt das Modell in die Dateien. <paramref name="trocken"/>: nichts auf Platte — der Bericht sagt, was geschähe.
    /// Reihenfolge: (1) Scaffolder-Dateien anlegen bzw. fehlende Teile additiv einmischen, (2) ABGLEICH der im Editor
    /// geänderten Elemente (Herkunfts-Stempel weicht ab) — nur deren Signatur/Deklaration, Vorhandenes bleibt wörtlich —,
    /// (3) Fähigkeits-Parameter der Handles, (4) neue Ingress-Bindungen. Was geändert, aber nicht schreibbar ist, meldet der Bericht.
    /// </summary>
    public static SchreibBericht Schreibe(EditorModell modell, string slnRoot, bool trocken = false)
    {
        var geschrieben = new List<string>();
        var ergaenzt = new List<string>();
        var uebersprungen = new List<string>();
        var ws = new Arbeitsbereich(slnRoot);
        // Neue Handles je Klasse (im Modell ohne Datei) — nur die werden in eine bestehende Klasse eingefügt.
        var lesen = modell.Lesen ?? new Leseseite();
        var neueHandles = lesen.Konsumenten.Select(k => (k.Name, k.Handles)).Concat(lesen.Reader.Select(r => (r.Name, r.Handles)))
            .Concat(lesen.Pipelines.Select(p => (p.Name, p.Handles)))
            .GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.SelectMany(x => x.Handles).Where(h => h.Datei == null).Select(h => h.Eingang).ToHashSet(StringComparer.Ordinal));

        // Das Modell enthält nur Domänen-Typen (der Extractor filtert Framework-Typen über die Projektlage). Wohin ein Typ
        //   gehört, sagt seine echte Datei bzw. das aus dem Code bekannte Verzeichnis seines Namespace — kennt der Code
        //   keines, markiert der Scaffolder die Datei als nicht platzierbar, und es wird NICHT geraten.
        var generiert = Scaffolder.Generiere(modell);
        foreach (var d in generiert)
        {
            if (!d.Platzierbar)
            {
                if (d.Inhalt.Length > 0) uebersprungen.Add($"{d.Pfad[2..]} (Namespace {NamespaceVon(d.Inhalt)}: kein Verzeichnis aus dem Code bekannt — Datei im Editor zuweisen)");
                continue;
            }
            // Der Pfad kommt aus dem Modell (echte Datei bzw. aus dem Code abgeleitetes Verzeichnis), relativ zur Solution.
            var rel = d.Pfad;
            if (!ws.Innerhalb(rel))
            {
                uebersprungen.Add($"{rel} (außerhalb der Solution)");
                continue;
            }

            var alt = ws.Lies(rel);
            if (alt == null)
            {
                ws.Setze(rel, d.Inhalt);
                geschrieben.Add(rel);
                continue;
            }

            // Die ROLLE der Datei (vom Scaffolder) bestimmt das Mischen — nicht ihr Dateiname.
            (int N, string Neu)? gemischt = d.Art switch
            {
                DateiArt.Typen => TypenMergen(alt, d.Inhalt),
                DateiArt.Decider or DateiArt.Applier => MethodenMergen(alt, d.Inhalt),
                DateiArt.Schnittstellen => SchnittstellenMergen(alt, d.Inhalt),
                DateiArt.StoreImpl => KlassenMethodenMergen(alt, d.Inhalt, _ => true, ImplSchluessel),
                DateiArt.Konsument or DateiArt.Leser or DateiArt.Pipeline => KlassenMethodenMergen(alt, d.Inhalt,
                    m => ErsterTyp(m) is { } e && neueHandles.GetValueOrDefault(KlasseVon(d.Inhalt) ?? "")?.Contains(e) == true, ErsterTyp),
                _ => null,
            };
            if (gemischt is null) { if (d.Art != DateiArt.Saga && d.Art != DateiArt.State && d.Art != DateiArt.Fluss) uebersprungen.Add($"{rel} (unangetastet)"); }
            else if (gemischt.Value.N > 0) { ws.Setze(rel, gemischt.Value.Neu); ergaenzt.Add($"{rel} (+{gemischt.Value.N})"); }
        }

        var abgleich = new Abgleich(modell, ws, generiert, ergaenzt, uebersprungen);
        abgleich.Alles();
        ParameterAbgleich(modell, ws, ergaenzt, uebersprungen);
        abgleich.Ingress();
        abgleich.MeldeUnbehandelte();

        if (!trocken) ws.Speichern();
        return new SchreibBericht(true, geschrieben, ergaenzt, uebersprungen) { Dateien = ws.Geaendert.ToList() };
    }

    // ══ Leseseite ════════════════════════════════════════════════════════════════════════════

    private static string? KlasseVon(string text) =>
        CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;

    private static string Basis(string? typ) => BoardLeseseite.Basisname(typ ?? "");

    /// <summary>Handle-Schlüssel: der Typ des ersten Parameters (Event/Query/Eingang).</summary>
    private static string? ErsterTyp(MethodDeclarationSyntax m) =>
        m.ParameterList.Parameters.Count > 0 ? Basis(m.ParameterList.Parameters[0].Type?.ToString()) : null;

    /// <summary>Store-Methoden-Schlüssel: Name + Parametertypen (eine Fähigkeit trägt genau eine Funktion).</summary>
    private static string ImplSchluessel(MethodDeclarationSyntax m) =>
        m.Identifier.Text + "(" + string.Join(",", m.ParameterList.Parameters.Select(p => Basis(p.Type?.ToString()))) + ")";

    /// <summary>
    /// Fehlende Methoden der generierten Klasse (Schlüssel <paramref name="schluessel"/>, Filter <paramref name="nimm"/>) in
    /// die GLEICHNAMIGE Klasse der bestehenden Datei einfügen — vor ihre schließende Klammer; fehlende usings ergänzen.
    /// </summary>
    private static (int, string) KlassenMethodenMergen(string altText, string genText, Func<MethodDeclarationSyntax, bool> nimm,
        Func<MethodDeclarationSyntax, string?> schluessel)
    {
        var genRoot = CSharpSyntaxTree.ParseText(genText).GetRoot();
        var genKlasse = genRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (genKlasse == null) return (0, altText);
        var altRoot = CSharpSyntaxTree.ParseText(altText).GetRoot();
        var ziel = altRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == genKlasse.Identifier.Text);
        if (ziel == null) return (0, altText);

        var vorhanden = ziel.Members.OfType<MethodDeclarationSyntax>().Select(schluessel).Where(x => x != null).ToHashSet(StringComparer.Ordinal);
        var fehlend = genKlasse.Members.OfType<MethodDeclarationSyntax>().Where(m => nimm(m) && schluessel(m) is { } k && !vorhanden.Contains(k)).ToList();
        if (fehlend.Count == 0) return (0, altText);

        var einzug = new string(' ', ziel.GetLocation().GetLineSpan().StartLinePosition.Character + 4);
        var methoden = string.Join("\n\n", fehlend.Select(m => Umgerueckt(m, einzug)));
        var klammer = ziel.CloseBraceToken;
        var neu = altText[..klammer.SpanStart].TrimEnd() + "\n\n" + methoden + "\n" + einzug[4..] + altText[klammer.SpanStart..];
        return (fehlend.Count, MitUsings(neu, genRoot));
    }

    /// <summary>Eine generierte Methode (Scaffolder-Einzug 4) auf den Einzug der Zielklasse bringen.</summary>
    private static string Umgerueckt(MethodDeclarationSyntax m, string einzug)
    {
        var alt = m.GetLocation().GetLineSpan().StartLinePosition.Character;
        return string.Join("\n", m.ToString().Replace("\r\n", "\n").Split('\n').Select((z, i) =>
            i == 0 ? einzug + z : z.Length == 0 ? z : einzug + (z.Length >= alt && z[..alt].Trim().Length == 0 ? z[alt..] : z.TrimStart())));
    }

    /// <summary>usings der generierten Datei, die der bestehenden fehlen, oben ergänzen (vor dem ersten using bzw. am Anfang).</summary>
    private static string MitUsings(string text, SyntaxNode genRoot)
    {
        var altRoot = CSharpSyntaxTree.ParseText(text).GetRoot();
        var da = altRoot.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax).OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString().Trim()).ToHashSet(StringComparer.Ordinal);
        var fehlend = genRoot.DescendantNodes(n => n is CompilationUnitSyntax).OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString().Trim()).Where(u => !da.Contains(u)).ToList();
        return fehlend.Count == 0 ? text : string.Join("\n", fehlend) + "\n" + text;
    }

    /// <summary>
    /// Fähigkeiten/Bündel: fehlende Interfaces anhängen (wie Typ-Dateien) UND in einem bestehenden Bündel die fehlenden
    /// Fähigkeiten an die Basisliste hängen (additiv; eine im Editor entfernte Fähigkeit bleibt im Bündel stehen).
    /// </summary>
    private static (int, string) SchnittstellenMergen(string altText, string genText)
    {
        var n = 0;
        var genRoot = CSharpSyntaxTree.ParseText(genText).GetRoot();
        foreach (var gi in genRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Where(i => i.BaseList != null))
        {
            var altRoot = CSharpSyntaxTree.ParseText(altText).GetRoot();
            var ai = altRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>().FirstOrDefault(i => i.Identifier.Text == gi.Identifier.Text);
            if (ai?.BaseList == null) continue;
            var da = ai.BaseList.Types.Select(t => Basis(t.ToString())).ToHashSet(StringComparer.Ordinal);
            var neu = gi.BaseList!.Types.Select(t => t.ToString()).Where(t => !da.Contains(Basis(t))).ToList();
            if (neu.Count == 0) continue;
            var ende = ai.BaseList.Types.Last().Span.End;
            altText = altText[..ende] + string.Concat(neu.Select(t => ", " + t)) + altText[ende..];
            n += neu.Count;
        }
        var (m, text) = TypenMergen(altText, genText);
        return (n + m, text);
    }

    private sealed record HandleOrt(string Klasse, Handle Handle, int Kontext);

    /// <summary>
    /// Verbinden/Lösen im Editor = Fähigkeits-Parameter hinzu/weg. Je bestehendem Handle (Datei aus dem Code) werden die
    /// Parameter HINTER dem Kontext (CQRS057: 2 bei Projektion/Reader, 1 bei Pipeline) auf das Modell gebracht; ein
    /// vorhandener Parameter bleibt wörtlich (Name/Format), ein neuer heißt wie im Modell. Nur die Parameterliste ändert
    /// sich; benutzt der Rumpf einen gelösten Parameter noch, meldet das der anschließende Build.
    /// </summary>
    private static void ParameterAbgleich(EditorModell modell, Arbeitsbereich ws, List<string> ergaenzt, List<string> uebersprungen)
    {
        var lesen = modell.Lesen;
        if (lesen == null) return;
        var nsVonFaehigkeit = lesen.Stores.SelectMany(s => s.Fns.Select(f => (f.Name, Ns: f.Namespace ?? s.Namespace)))
            .GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Ns, StringComparer.Ordinal);
        var orte = lesen.Konsumenten.SelectMany(k => k.Handles.Select(h => new HandleOrt(k.Name, h, 2)))
            .Concat(lesen.Reader.SelectMany(r => r.Handles.Select(h => new HandleOrt(r.Name, h, 2))))
            .Concat(lesen.Pipelines.SelectMany(p => p.Handles.Select(h => new HandleOrt(p.Name, h, 1))))
            .Where(o => o.Handle.Datei != null);

        foreach (var gruppe in orte.GroupBy(o => o.Handle.Datei!))
        {
            if (!ws.Innerhalb(gruppe.Key) || ws.Lies(gruppe.Key) is not { } text) continue;
            var aenderungen = new List<string>();
            foreach (var o in gruppe)
            {
                var root = CSharpSyntaxTree.ParseText(text).GetRoot();
                var m = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Identifier.Text == o.Klasse)
                    .SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>())
                    // Eingang: der bearbeitete — oder, falls der Abgleich ihn (noch) nicht umgeschrieben hat, der im Code.
                    .FirstOrDefault(x => (ErsterTyp(x) == o.Handle.Eingang || ErsterTyp(x) == o.Handle.EingangImCode)
                                         && x.ParameterList.Parameters.Count > o.Kontext);
                if (m == null) { uebersprungen.Add($"{gruppe.Key} ({o.Klasse}.Handle({o.Handle.Eingang}) nicht gefunden)"); continue; }
                var ps = m.ParameterList.Parameters;
                var ist = ps.Skip(1 + o.Kontext).ToList();
                var soll = o.Handle.Faehigkeiten;
                if (ist.Select(p => Basis(p.Type?.ToString())).SequenceEqual(soll.Select(f => Basis(f.Typ)))) continue;

                var teile = soll.Select(f => ist.FirstOrDefault(p => Basis(p.Type?.ToString()) == Basis(f.Typ))?.ToString() ?? $"{f.Typ} {f.Name}");
                var fest = ps[o.Kontext];   // letzter Kontext-Parameter
                var neu = text[m.ParameterList.SpanStart..fest.Span.End] + string.Concat(teile.Select(t => ", " + t)) + ")";
                text = text[..m.ParameterList.SpanStart] + neu + text[m.ParameterList.Span.End..];
                var hinzu = soll.Select(f => Basis(f.Typ)).Except(ist.Select(p => Basis(p.Type?.ToString()))).ToList();
                var weg = ist.Select(p => Basis(p.Type?.ToString())).Except(soll.Select(f => Basis(f.Typ))).ToList();
                aenderungen.Add($"{o.Klasse}.Handle({o.Handle.Eingang}): " + string.Join(" ", hinzu.Select(x => "+" + x).Concat(weg.Select(x => "−" + x))));

                // Fähigkeit aus einem anderen Namespace: using ergänzen.
                var dateiNs = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                foreach (var f in hinzu)
                    if (nsVonFaehigkeit.TryGetValue(f, out var fns) && fns != dateiNs
                        && !CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                            .OfType<UsingDirectiveSyntax>().Any(u => u.Name?.ToString() == fns))
                        text = $"using {fns};\n" + text;
            }
            if (aenderungen.Count == 0) continue;
            ws.Setze(gruppe.Key, text);
            ergaenzt.Add($"{gruppe.Key} (Parameter: {string.Join("; ", aenderungen)})");
        }
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
