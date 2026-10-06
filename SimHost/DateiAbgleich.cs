using System.Text.RegularExpressions;
using Codeformen;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SimHost;

/// <summary>Die Dateien eines Schreiblaufs im Speicher: gelesen bei Bedarf, geändert im Speicher, gespeichert erst am Ende (Trockenlauf = nie).</summary>
internal sealed class Arbeitsbereich(string root)
{
    private readonly Dictionary<string, string> _text = new(StringComparer.Ordinal);
    private readonly List<string> _geaendert = new();

    private string Voll(string rel) => Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
    public bool Innerhalb(string rel) => Voll(rel).StartsWith(Path.GetFullPath(root), StringComparison.Ordinal);

    public string? Lies(string rel)
    {
        if (_text.TryGetValue(rel, out var t)) return t;
        var p = Voll(rel);
        return File.Exists(p) ? _text[rel] = File.ReadAllText(p) : null;
    }

    public void Setze(string rel, string text)
    {
        if (_text.TryGetValue(rel, out var alt) && alt == text) return;
        _text[rel] = text;
        if (!_geaendert.Contains(rel)) _geaendert.Add(rel);
    }

    public IReadOnlyList<string> Geaendert => _geaendert;

    public void Speichern()
    {
        foreach (var rel in _geaendert)
        {
            var p = Voll(rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, _text[rel]);
        }
    }
}

/// <summary>
/// Der ABGLEICH bestehender Deklarationen mit dem Modell — nur für Elemente, die im Editor geändert wurden (Herkunfts-Stempel
/// weicht ab). Das Modell besitzt die Signatur/Deklaration (Felder, Enum-Werte, OneOf-Rückgaben, Prozess-Regeln, Parameter
/// einer Fähigkeit, Basisliste/Attribut-Argumente); der Code besitzt Rümpfe und Handcode. Vorhandenes, das sich nicht
/// geändert hat, bleibt Wort für Wort; ersetzt wird nur das Geänderte. Bricht dadurch ein Rumpf, meldet es der Bau.
/// </summary>
internal sealed class Abgleich(EditorModell modell, Arbeitsbereich ws, IReadOnlyList<GenerierteDatei> generiert,
    List<string> ergaenzt, List<string> uebersprungen)
{
    private static readonly string OneOf = typeof(Abstractions.OneOf<>).Name.Split('`')[0];
    private static readonly string Regeln = nameof(Abstractions.IProzessDefinition.Regeln);
    private static readonly string SubscriberId = nameof(Abstractions.ISubscriber.SubscriberId);
    private static readonly string PipelineId = nameof(Abstractions.IPipelineHandler.PipelineId);
    private static readonly string ProjectionReader = nameof(Abstractions.ProjectionReaderAttribute)[..^"Attribute".Length];
    private static readonly string TrackDeps = nameof(Abstractions.ProjectionReaderAttribute.TrackDeps);
    private static readonly HashSet<string> StateVertrag = [nameof(Abstractions.IState.Id), nameof(Abstractions.IState.Version)];

    /// <summary>Was abgeglichen wurde (auch ohne Textänderung) — der Rest der geänderten Elemente wird als „nicht schreibbar" gemeldet.</summary>
    private readonly HashSet<string> _behandelt = new(StringComparer.Ordinal);
    private Dictionary<string, string>? _nsVon;

    public void Alles()
    {
        foreach (var r in modell.Records.Where(r => r.Datei != null && Herkunft.Geaendert(r.Herkunft, Herkunft.Von(r))))
            Datei(r.Datei!, $"{r.Kind} {r.Namespace}.{r.Name}", t => RecordFelder(t, r));
        foreach (var e in modell.Enums.Where(e => e.Datei != null && Herkunft.Geaendert(e.Herkunft, Herkunft.Von(e))))
            Datei(e.Datei!, $"enum {e.Namespace}.{e.Name}", t => EnumWerte(t, e));
        foreach (var a in modell.Aggregate.Where(a => a.Datei != null && Herkunft.Geaendert(a.Herkunft, Herkunft.Von(a))))
            Datei(a.Datei!, $"state {a.Namespace}.{a.Name}", t => StateFelder(t, a));
        foreach (var d in modell.Decider.Where(d => d.Datei != null && Herkunft.Geaendert(d.Herkunft, Herkunft.Von(d))))
            Datei(d.Datei!, $"decide {d.Aggregat}.{d.Command}", t => DecideRueckgabe(t, d));
        foreach (var s in modell.Sagas.Where(s => s.Datei != null && Herkunft.Geaendert(s.Herkunft, Herkunft.Von(s))))
            Datei(s.Datei!, $"prozess {s.Namespace}.{s.Name}", t => ProzessRegeln(t, s));
        foreach (var a in modell.Akteure.Where(a => a.Datei != null && Herkunft.Geaendert(a.Herkunft, Herkunft.Von(a))))
        {
            Datei(a.Datei!, $"akteur {a.Namespace}.{a.Name}", t => AkteurBefugnisse(t, a));
            Datei(a.VertragDatei ?? a.Datei!, $"vertrag {a.Namespace}.{a.VertragTyp}", t => AkteurVertrag(t, a));
        }
        foreach (var c in modell.Clients.Where(c => c.Datei != null && Herkunft.Geaendert(c.Herkunft, Herkunft.Von(c))))
            Datei(c.Datei!, $"client {c.Namespace}.{c.Name}", t => ClientVertrag(t, c));

        var l = modell.Lesen;
        if (l == null) return;
        foreach (var st in l.Stores)
            foreach (var f in st.Fns.Where(f => f.Datei != null && Herkunft.Geaendert(f.Herkunft, Herkunft.Von(f))))
            {
                var was = $"fähigkeit {st.Name}.{f.Methode}";
                Datei(f.Datei!, was, t => MethodenSignatur(t, f.Name, null, f, schnittstelle: true));
                if (st.Impl?.Datei is null) { uebersprungen.Add($"{was}: keine eindeutige Impl-Klasse — Methode dort nicht angepasst"); continue; }
                foreach (var datei in new[] { st.Impl.Datei, st.Impl.SchreibDatei, st.Impl.LeseDatei }.Where(x => x != null).Distinct())
                    Datei(datei!, was, t => MethodenSignatur(t, st.Impl.Name, f.Methode, f, schnittstelle: false));
            }
        foreach (var k in l.Konsumenten.Where(k => k.Datei != null && Herkunft.Geaendert(k.Herkunft, Herkunft.Von(k))))
            Datei(k.Datei!, $"konsument {k.Name}", t => Klasse(t, k.Name, k.Basen, (SubscriberId, k.SubscriberId), null));
        foreach (var r in l.Reader.Where(r => r.Datei != null && Herkunft.Geaendert(r.Herkunft, Herkunft.Von(r))))
            Datei(r.Datei!, $"reader {r.Name}", t => Klasse(t, r.Name, r.Basen, null, r.TrackDeps));
        foreach (var p in l.Pipelines.Where(p => p.Datei != null && Herkunft.Geaendert(p.Herkunft, Herkunft.Von(p))))
            Datei(p.Datei!, $"pipeline {p.Name}", t => Klasse(t, p.Name, p.Basen, (PipelineId, p.PipelineId), null));
        foreach (var (besitzer, h) in l.Konsumenten.SelectMany(k => k.Handles.Select(h => (k.Name, h)))
                     .Concat(l.Reader.SelectMany(r => r.Handles.Select(h => (r.Name, h))))
                     .Concat(l.Pipelines.SelectMany(p => p.Handles.Select(h => (p.Name, h))))
                     .Where(x => x.h.Datei != null && Herkunft.Geaendert(x.h.Herkunft, Herkunft.Von(x.h))))
            Datei(h.Datei!, $"handle {besitzer}.Handle({h.Eingang})", t => HandleRueckgabe(t, besitzer, h));
    }

    /// <summary>Eine Änderung auf eine Datei anwenden; <paramref name="aendern"/> liefert (neuer Text, Beschreibung) oder null = nichts zu tun.</summary>
    private void Datei(string rel, string was, Func<string, (string Text, string Notiz)?> aendern)
    {
        _behandelt.Add(was);
        if (!ws.Innerhalb(rel) || ws.Lies(rel) is not { } text) { uebersprungen.Add($"{was}: Datei {rel} fehlt"); return; }
        var r = aendern(text);
        if (r == null) return;
        if (r.Value.Text != text) { ws.Setze(rel, r.Value.Text); ergaenzt.Add($"{rel} ({was}: {r.Value.Notiz})"); }
        else if (r.Value.Notiz.Length > 0) uebersprungen.Add($"{was}: {r.Value.Notiz}");
    }

    /// <summary>Geänderte Elemente, für die es keine Schreib-Regel gibt (z. B. nur Doku) — sagen statt schweigen.</summary>
    public void MeldeUnbehandelte()
    {
        foreach (var g in Herkunft.Geaenderte(modell).Where(g => !_behandelt.Contains(g)))
            uebersprungen.Add($"{g}: im Editor geändert — diese Änderung schreibt „C# schreiben“ nicht");
    }

    // ── Records: Positions-Parameter + Property-Felder (Feldregeln wie der Extractor) ──────────────────────────
    private (string, string)? RecordFelder(string text, Record r)
    {
        var root = Parse(text);
        if (Typ(root, r.Name, r.Namespace) is not TypeDeclarationSyntax decl) return (text, $"Typ {r.Name} nicht gefunden");
        var edits = new List<(int, int, string)>();
        var notiz = new List<string>();
        var pos = r.Felder.Where(f => f.Zugriff is null).ToList();
        if (decl is RecordDeclarationSyntax rd)
        {
            var ist = rd.ParameterList?.Parameters.ToList() ?? [];
            bool Gleich(ParameterSyntax p, Feld f) => p.Identifier.Text == f.Name && N(p.Type?.ToString()) == N(f.Typ) && p.Default?.Value.ToString() == f.Standard;
            if (ist.Count != pos.Count || ist.Zip(pos).Any(z => !Gleich(z.First, z.Second)))
            {
                var liste = Liste(rd.ParameterList, pos.Select(f => ist.FirstOrDefault(p => Gleich(p, f))?.ToString()
                    ?? $"{f.Typ} {f.Name}" + (f.Standard is null ? "" : $" = {f.Standard}")).ToList());
                if (rd.ParameterList != null) edits.Add((rd.ParameterList.SpanStart, rd.ParameterList.Span.End, liste));
                else if (pos.Count > 0) { var at = rd.TypeParameterList?.Span.End ?? rd.Identifier.Span.End; edits.Add((at, at, liste)); }
                notiz.Add("Positions-Felder");
            }
        }
        var soll = r.Felder.Where(f => f.Zugriff is not null).Select(f => (f.Name, $"{N(f.Typ)}|{f.Standard}|{f.Zugriff}|{f.Pflicht}",
            $"public {(f.Pflicht ? "required " : "")}{f.Typ} {f.Name} {f.Zugriff}" + (f.Standard is null ? "" : $" = {f.Standard};"))).ToList();
        if (Eigenschaften(decl, soll, p => Feldregeln.Eigenschaft(p) is { } e ? (e.Name, $"{N(e.Typ)}|{e.Standard}|{e.Zugriff}|{e.Pflicht}") : null, new HashSet<string>(), edits))
            notiz.Add("Property-Felder");
        return (MitNamespaces(Anwenden(text, edits), r.Felder.Select(f => f.Typ)), string.Join(", ", notiz));
    }

    private (string, string)? StateFelder(string text, Aggregat a)
    {
        var root = Parse(text);
        var decl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == a.Name && c.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax
                                 && c.Members.Any(m => m is not BaseTypeDeclarationSyntax));
        decl ??= root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == a.Name);
        if (decl == null) return (text, $"State {a.Name} nicht gefunden");
        var soll = a.State.Select(f => (f.Name, $"{N(f.Typ)}|{f.Standard}|{f.Ausdruck}|{f.NurGet}",
            f.Ausdruck is not null ? $"public {f.Typ} {f.Name} => {f.Ausdruck};"
                : $"public {f.Typ} {f.Name} {{ get;{(f.NurGet ? "" : " set;")} }}" + (f.Standard is null ? "" : $" = {f.Standard};"))).ToList();
        var edits = new List<(int, int, string)>();
        var geaendert = Eigenschaften(decl, soll, p => Feldregeln.State(p) is { } s ? (s.Name, $"{N(s.Typ)}|{s.Standard}|{s.Ausdruck}|{s.NurGet}") : null,
            StateVertrag, edits);
        return (MitNamespaces(Anwenden(text, edits), a.State.Select(f => f.Typ)), geaendert ? "State-Felder" : "");
    }

    /// <summary>
    /// Property-Felder abgleichen: vorhandene Felder, die das Modell nicht mehr hat, entfernen; geänderte ersetzen; neue hinter
    /// dem letzten Feld einfügen (ohne Rumpf: Rumpf anlegen). Handcode-Properties (nicht nach der Feldregel) bleiben unberührt.
    /// </summary>
    private static bool Eigenschaften(TypeDeclarationSyntax decl, List<(string Name, string Schluessel, string Zeile)> soll,
        Func<PropertyDeclarationSyntax, (string Name, string Schluessel)?> ist, ISet<string> geschuetzt, List<(int, int, string)> edits)
    {
        var vorhanden = decl.Members.OfType<PropertyDeclarationSyntax>().Select(p => (P: p, F: ist(p))).Where(x => x.F != null)
            .Select(x => (x.P, Name: x.F!.Value.Name, Schluessel: x.F.Value.Schluessel)).ToList();
        var vorher = edits.Count;
        foreach (var v in vorhanden.Where(v => !geschuetzt.Contains(v.Name) && soll.All(s => s.Name != v.Name)))
            edits.Add((v.P.FullSpan.Start, v.P.FullSpan.End, ""));
        foreach (var s in soll)
            if (vorhanden.FirstOrDefault(v => v.Name == s.Name) is { P: not null } v && v.Schluessel != s.Schluessel)
                edits.Add((v.P.Span.Start, v.P.Span.End, s.Zeile));
        var neu = soll.Where(s => vorhanden.All(v => v.Name != s.Name)).ToList();
        if (neu.Count > 0)
        {
            var spalte = decl.GetLocation().GetLineSpan().StartLinePosition.Character;
            var einzug = vorhanden.Count > 0 ? new string(' ', vorhanden[^1].P.GetLocation().GetLineSpan().StartLinePosition.Character) : new string(' ', spalte + 4);
            var zeilen = string.Concat(neu.Select(s => einzug + s.Zeile + "\n"));
            if (vorhanden.Count > 0) edits.Add((vorhanden[^1].P.FullSpan.End, vorhanden[^1].P.FullSpan.End, zeilen));
            else if (decl.OpenBraceToken.IsKind(SyntaxKind.OpenBraceToken) && !decl.OpenBraceToken.IsMissing)
                edits.Add((decl.OpenBraceToken.FullSpan.End, decl.OpenBraceToken.FullSpan.End, zeilen));
            else if (decl.SemicolonToken.IsKind(SyntaxKind.SemicolonToken))
                edits.Add((decl.SemicolonToken.SpanStart, decl.SemicolonToken.Span.End, "\n" + new string(' ', spalte) + "{\n" + zeilen + new string(' ', spalte) + "}"));
        }
        return edits.Count > vorher;
    }

    // ── Enum-Werte ──
    private (string, string)? EnumWerte(string text, Enumeration e)
    {
        var root = Parse(text);
        if (Typ(root, e.Name, e.Namespace) is not EnumDeclarationSyntax decl) return (text, $"Enum {e.Name} nicht gefunden");
        var ist = decl.Members.Select(Feldregeln.EnumWert).ToList();
        if (ist.SequenceEqual(e.Werte)) return null;
        var teile = e.Werte.Select(w => decl.Members.FirstOrDefault(m => Feldregeln.EnumWert(m) == w)?.ToString() ?? w).ToList();
        var innen = text[decl.OpenBraceToken.Span.End..decl.CloseBraceToken.SpanStart];
        string neu;
        if (!innen.Contains('\n')) neu = " " + string.Join(", ", teile) + " ";
        else
        {
            var einzug = decl.Members.Count > 0 ? new string(' ', decl.Members[0].GetLocation().GetLineSpan().StartLinePosition.Character)
                : new string(' ', decl.GetLocation().GetLineSpan().StartLinePosition.Character + 4);
            neu = "\n" + string.Join(",\n", teile.Select(t => einzug + t)) + ",\n" + new string(' ', decl.GetLocation().GetLineSpan().StartLinePosition.Character);
        }
        return (text[..decl.OpenBraceToken.Span.End] + neu + text[decl.CloseBraceToken.SpanStart..], "Werte");
    }

    // ── Decide: OneOf der Rückgabe (Wrapper bleibt) ──
    private (string, string)? DecideRueckgabe(string text, DecideRegel d)
    {
        var root = Parse(text);
        var m = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(x => ErsterTyp(x) == d.Command);
        if (m == null) return (text, $"Decide({d.Command}) nicht gefunden");
        var alt = m.ReturnType.ToString();
        var soll = $"{Wrapper(alt)}<{OneOf}<{string.Join(", ", d.Ergibt.Select(a => a.Event))}>>";
        if (N(alt) == N(soll)) return null;
        var neu = text[..m.ReturnType.SpanStart] + soll + text[m.ReturnType.Span.End..];
        return (MitNamespaces(neu, d.Ergibt.Select(a => a.Event)), "OneOf-Ausgänge");
    }

    // ── Prozess: nur der Ausdruck der Regeln-Property (Kopf, auch explizite Implementierung, bleibt) ──
    private (string, string)? ProzessRegeln(string text, Saga s)
    {
        var gen = generiert.Where(g => g.Art == DateiArt.Saga).Select(g => Parse(g.Inhalt))
            .Select(r => r.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == s.Name)).FirstOrDefault(c => c != null);
        var genProp = gen?.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p => p.Identifier.Text == Regeln);
        var root = Parse(text);
        var alt = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == s.Name)?
            .Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p => p.Identifier.Text == Regeln);
        if (genProp?.ExpressionBody == null || alt?.ExpressionBody == null) return (text, "Regeln-Property nicht gefunden (nur => …-Form wird geschrieben)");
        if (Ohne(alt.ExpressionBody.ToString()) == Ohne(genProp.ExpressionBody.ToString())) return null;
        // Je Regel eine Anweisung im Definiere-Lambda: eine unveränderte Regel (gleich bis auf Leerraum) bleibt wörtlich samt
        //   ihrer Kommentare; nur neue/geänderte kommen in der Form des Scaffolders. Andere Form ⇒ der Ausdruck als Ganzes.
        static BlockSyntax? Block(ArrowExpressionClauseSyntax a) =>
            a.DescendantNodes().OfType<LambdaExpressionSyntax>().Select(l => l.Body).OfType<BlockSyntax>().FirstOrDefault();
        var (altBlock, genBlock) = (Block(alt.ExpressionBody), Block(genProp.ExpressionBody));
        string neu;
        if (altBlock is { Statements.Count: > 0 } ab && genBlock is { Statements.Count: > 0 } gb)
        {
            var einzug = new string(' ', ab.Statements[0].GetLocation().GetLineSpan().StartLinePosition.Character);
            var teile = gb.Statements.Select(g => ab.Statements.FirstOrDefault(a => Ohne(a.ToString()) == Ohne(g.ToString())) is { } a
                ? a.ToFullString().TrimEnd().TrimStart('\n', '\r') : Umgerueckt(g.ToString(), g.GetLocation().GetLineSpan().StartLinePosition.Character, einzug)).ToList();
            var erster = ab.Statements[0];
            neu = text[..erster.FullSpan.Start] + string.Join("\n\n", teile.Select((t, i) => i == 0 && t.StartsWith(einzug) ? t : (t.StartsWith(' ') ? t : einzug + t)))
                  + text[ab.Statements[^1].Span.End..];
        }
        else if (alt.ExpressionBody.DescendantNodes().OfType<LambdaExpressionSyntax>().FirstOrDefault() is { Body: ExpressionSyntax regel } lambda
                 && genBlock is { Statements.Count: > 0 } gb2)
        {
            // Bisher EINE Regel als Ausdrucks-Lambda: wird ein Block; die alte Regel bleibt wörtlich, wenn sie unverändert ist.
            var ind = new string(' ', alt.GetLocation().GetLineSpan().StartLinePosition.Character);
            var einzug = ind + "    ";
            var teile = gb2.Statements.Select(g => Ohne(regel + ";") == Ohne(g.ToString()) ? einzug + regel + ";"
                : Umgerueckt(g.ToString(), g.GetLocation().GetLineSpan().StartLinePosition.Character, einzug)).ToList();
            neu = text[..lambda.ArrowToken.Span.End] + "\n" + ind + "{\n" + string.Join("\n\n", teile) + "\n" + ind + "}" + text[regel.Span.End..];
        }
        else neu = text[..alt.ExpressionBody.SpanStart] + genProp.ExpressionBody.ToString() + text[alt.ExpressionBody.Span.End..];
        return (MitUsingsAus(neu, gen!.SyntaxTree.GetRoot()), "Regeln");
    }

    // ── Fähigkeit: Rückgabe + Parameter am Interface bzw. an der Impl-Methode ──
    private (string, string)? MethodenSignatur(string text, string klasse, string? methode, Faehigkeit f, bool schnittstelle)
    {
        var root = Parse(text);
        var m = root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => t.Identifier.Text == klasse)
            .SelectMany(t => t.Members.OfType<MethodDeclarationSyntax>()).FirstOrDefault(x => methode == null || x.Identifier.Text == methode);
        if (m == null) return schnittstelle ? (text, $"{klasse} nicht gefunden") : null;   // Impl: die Methode liegt in einer anderen Teil-Datei
        var edits = new List<(int, int, string)>();
        if (N(m.ReturnType.ToString()) != N(f.Rueckgabe)) edits.Add((m.ReturnType.SpanStart, m.ReturnType.Span.End, f.Rueckgabe));
        var ist = m.ParameterList.Parameters.ToList();
        bool Gleich(ParameterSyntax p, Parameter q) => p.Identifier.Text == q.Name && N(p.Type?.ToString()) == N(q.Typ);
        if (ist.Count != f.Parameter.Count || ist.Zip(f.Parameter).Any(z => !Gleich(z.First, z.Second)))
            edits.Add((m.ParameterList.SpanStart, m.ParameterList.Span.End, Liste(m.ParameterList, f.Parameter.Select(q =>
                ist.FirstOrDefault(p => Gleich(p, q))?.ToString() ?? $"{q.Typ} {q.Name}" + (q.Standard is null ? "" : $" = {q.Standard}")).ToList())));
        if (edits.Count == 0) return null;
        return (MitNamespaces(Anwenden(text, edits), f.Parameter.Select(p => p.Typ).Append(f.Rueckgabe)), "Signatur");
    }

    // ── Handle: Rückgabe (Ausgänge); die Fähigkeits-Parameter gleicht ParameterAbgleich ab ──
    private (string, string)? HandleRueckgabe(string text, string klasse, Handle h)
    {
        if (h.Rueckgabe == null) return null;
        var root = Parse(text);
        var m = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Identifier.Text == klasse)
            .SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>()).FirstOrDefault(x => ErsterTyp(x) == h.Eingang);
        if (m == null) return (text, "Handle nicht gefunden");
        if (N(m.ReturnType.ToString()) == N(h.Rueckgabe)) return null;
        var neu = text[..m.ReturnType.SpanStart] + h.Rueckgabe + text[m.ReturnType.Span.End..];
        return (MitNamespaces(neu, h.Ausgaenge), "Ausgänge");
    }

    // ── Klasse: Basisliste (Pull/Append-Marker, IReader<P>), SubscriberId/PipelineId-Literal, TrackDeps ──
    private (string, string)? Klasse(string text, string name, IReadOnlyList<string>? basen, (string Name, string? Wert)? id, bool? trackDeps)
    {
        var root = Parse(text);
        var decls = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Identifier.Text == name).ToList();
        var haupt = decls.FirstOrDefault(d => d.BaseList != null) ?? decls.FirstOrDefault();
        if (haupt == null) return (text, $"Klasse {name} nicht gefunden");
        var edits = new List<(int, int, string)>();
        var notiz = new List<string>();
        if (basen != null && haupt.BaseList is { } bl && !bl.Types.Select(t => N(t.ToString())).SequenceEqual(basen.Select(N)))
        {
            edits.Add((bl.Types[0].SpanStart, bl.Types[^1].Span.End, string.Join(", ", basen)));
            notiz.Add("Basisliste");
        }
        if (id is { Wert: { } wert } kennung)
        {
            var prop = decls.SelectMany(d => d.Members.OfType<PropertyDeclarationSyntax>()).FirstOrDefault(p => p.Identifier.Text == kennung.Name);
            var ausdruck = prop?.ExpressionBody?.Expression;
            // Literal direkt oder über eine Konstante derselben Klasse.
            if (ausdruck is IdentifierNameSyntax idn)
                ausdruck = decls.SelectMany(d => d.Members.OfType<FieldDeclarationSyntax>()).SelectMany(f => f.Declaration.Variables)
                    .FirstOrDefault(v => v.Identifier.Text == idn.Identifier.Text)?.Initializer?.Value;
            if (ausdruck is LiteralExpressionSyntax { Token.Value: string alt } lit)
            {
                if (alt != wert) { edits.Add((lit.SpanStart, lit.Span.End, SymbolDisplay.FormatLiteral(wert, true))); notiz.Add(kennung.Name); }
            }
            else notiz.Add($"{kennung.Name} ist kein Literal im Code — nicht geändert");
        }
        if (trackDeps is { } td)
        {
            var attr = decls.SelectMany(d => d.AttributeLists).SelectMany(a => a.Attributes)
                .FirstOrDefault(a => BoardLeseseite.Basisname(a.Name.ToString()) is var n && (n == ProjectionReader || n == ProjectionReader + "Attribute"));
            var soll = td ? "true" : "false";
            var arg = attr?.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == TrackDeps);
            if (attr == null)
            {
                var spalte = haupt.GetLocation().GetLineSpan().StartLinePosition.Character;
                var at = haupt.AttributeLists.Count > 0 ? haupt.AttributeLists[0].SpanStart : haupt.Modifiers.Count > 0 ? haupt.Modifiers[0].SpanStart : haupt.Keyword.SpanStart;
                edits.Add((at, at, $"[{ProjectionReader}({TrackDeps} = {soll})]\n" + new string(' ', spalte)));
                notiz.Add(TrackDeps);
            }
            else if (arg == null)
            {
                if (attr.ArgumentList == null) edits.Add((attr.Span.End, attr.Span.End, $"({TrackDeps} = {soll})"));
                else edits.Add((attr.ArgumentList.CloseParenToken.SpanStart, attr.ArgumentList.CloseParenToken.SpanStart,
                    (attr.ArgumentList.Arguments.Count > 0 ? ", " : "") + $"{TrackDeps} = {soll}"));
                notiz.Add(TrackDeps);
            }
            else if (arg.Expression.ToString() != soll) { edits.Add((arg.Expression.SpanStart, arg.Expression.Span.End, soll)); notiz.Add(TrackDeps); }
        }
        var neu = Anwenden(text, edits);
        if (basen != null && neu != text) neu = MitNamespaces(neu, basen);
        return (neu, string.Join(", ", notiz));
    }

    // ── Ingress: neue Bindung nach dem Vorbild einer bestehenden desselben Modus ──
    public void Ingress()
    {
        foreach (var b in modell.Ingress?.Where(x => x.Datei == null) ?? [])
        {
            var was = $"ingress {b.Trigger} ({b.Modus})";
            var vorlage = modell.Ingress!.FirstOrDefault(x => x.Datei != null && x.Modus == b.Modus && x.Anweisung != null && x.TypArgument != null);
            var ns = modell.Records.FirstOrDefault(r => r.Kind == RecordArt.Trigger && r.Name == b.Trigger)?.Namespace;
            if (vorlage == null) { uebersprungen.Add($"{was}: keine {b.Modus}-Bindung im Code als Vorbild — Bindung im Host von Hand anlegen"); continue; }
            if (ns == null) { uebersprungen.Add($"{was}: Trigger-Record unbekannt"); continue; }
            var fq = $"{ns}.{b.Trigger}";
            var anw = vorlage.Anweisung!.Replace($"<{vorlage.TypArgument}>", $"<{fq}>");
            if (vorlage.OrtArgument != null && b.Ort != null)
            {
                var ort = vorlage.OrtArgument.StartsWith('"') ? SymbolDisplay.FormatLiteral(b.Ort, true) : b.Ort;
                var i = anw.IndexOf(vorlage.OrtArgument, StringComparison.Ordinal);
                if (i >= 0) anw = anw[..i] + ort + anw[(i + vorlage.OrtArgument.Length)..];
            }
            Datei(vorlage.Datei!, was, text =>
            {
                if (text.Contains($"<{fq}>", StringComparison.Ordinal)) return null;   // schon gebunden
                var i = text.IndexOf(vorlage.Anweisung!, StringComparison.Ordinal);
                if (i < 0) return (text, "Vorbild-Anweisung nicht gefunden");
                var zeile = text.LastIndexOf('\n', i) + 1;
                var einzug = text[zeile..i];
                var ende = i + vorlage.Anweisung!.Length;
                var zeilenEnde = text.IndexOf('\n', ende);
                if (zeilenEnde < 0) zeilenEnde = text.Length;
                return (text[..zeilenEnde] + "\n" + einzug + anw + text[zeilenEnde..], $"Bindung {b.Modus} {b.Ort}");
            });
        }
    }

    // ── Akteur: die Basisliste IST der Akteur — IAkteur + IDarf<…> nach dem Modell (auch Entziehen), Fremdes bleibt ──
    private (string, string)? AkteurBefugnisse(string text, Akteur a)
    {
        if (Typ(Parse(text), a.Name, a.Namespace) is not TypeDeclarationSyntax decl) return (text, $"Akteur {a.Name} nicht gefunden");
        var soll = Scaffolder.AkteurBasen(a);
        var fremd = decl.BaseList?.Types.Select(t => t.ToString().Trim()).Where(t => !Scaffolder.IstAkteurBasis(t)).ToList() ?? [];
        var neu = string.Join(", ", soll.Take(1).Concat(fremd).Concat(soll.Skip(1)));
        if (decl.BaseList is { } bl)
        {
            if (N(string.Join(",", bl.Types.Select(t => t.ToString()))) == N(neu)) return null;
            return (MitNamespaces(Anwenden(text, [(bl.Types[0].SpanStart, bl.Types[^1].Span.End, neu)]), a.Darf), "Befugnisse");
        }
        var at = (decl as RecordDeclarationSyntax)?.ParameterList?.Span.End ?? decl.Identifier.Span.End;
        return (MitNamespaces(Anwenden(text, [(at, at, " : " + neu)]), a.Darf), "Befugnisse");
    }

    // ── Akteur-Vertrag (docs/konzept-akteure.md §3): das Interface hat keine Rümpfe — seine Auf-Signaturen gehören dem Modell.
    //    Fehlende Zusagen anhängen, geänderte Rückgaben ersetzen, entfernte streichen; fehlt das Interface, hinter den Akteur. ──
    private (string, string)? AkteurVertrag(string text, Akteur a)
    {
        var root = Parse(text);
        a = a with { Vertrag = a.Vertrag.Where(r => r.Teil == null).ToList() };   // weitere Vertrags-Teile schreibt der Editor nicht
        var typen = a.Vertrag.SelectMany(r => r.Ausgaenge.Append(r.Eingang)).ToList();
        var decl = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().FirstOrDefault(i => i.Identifier.Text == a.VertragTyp);
        if (decl == null)
        {
            if (a.Vertrag.Count == 0) return null;
            if (Typ(root, a.Name, a.Namespace) is not { } akt) return (text, $"Akteur {a.Name} nicht gefunden — Vertrag nicht geschrieben");
            var neu = "\n\n" + Scaffolder.VertragsInterface(a).TrimEnd('\n', '\r');
            return (MitNamespaces(Anwenden(text, [(akt.Span.End, akt.Span.End, neu)]), typen), "Vertrag angelegt");
        }
        var edits = new List<(int, int, string)>();
        var vorhanden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in decl.Members.OfType<MethodDeclarationSyntax>()
                     .Where(m => m.Identifier.Text == Abstractions.Akteurvertrag.Auf && m.ParameterList.Parameters.Count == 1))
        {
            var ein = ErsterTyp(m) ?? "";
            vorhanden.Add(ein);
            if (a.Vertrag.FirstOrDefault(r => r.Eingang == ein) is not { } soll) edits.Add((m.FullSpan.Start, m.FullSpan.End, ""));
            else if (N(m.ReturnType.ToString()) != N(Scaffolder.VertragsRueckgabe(soll)))
                edits.Add((m.ReturnType.SpanStart, m.ReturnType.Span.End, Scaffolder.VertragsRueckgabe(soll)));
        }
        var fehlend = a.Vertrag.Where(r => !vorhanden.Contains(r.Eingang)).Select(r => $"\n    {Scaffolder.VertragsMethode(r)};\n").ToList();
        if (fehlend.Count > 0) edits.Add((decl.CloseBraceToken.SpanStart, decl.CloseBraceToken.SpanStart, string.Concat(fehlend)));
        return edits.Count == 0 ? null : (MitNamespaces(Anwenden(text, edits), typen), "Zusagen");
    }

    // ── Client-Vertrag (docs/konzept-akteure.md §4): die Basisliste (Teile, ISendet, IFragt) und die Kenntnis-Methoden gehören dem
    //    Modell; fremde Basistypen (z. B. ein geerbter Client-Vertrag) bleiben stehen. Fehlende Kenntnis anhängen, entfernte streichen. ──
    private (string, string)? ClientVertrag(string text, Client c)
    {
        var root = Parse(text);
        if (root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().FirstOrDefault(i => i.Identifier.Text == c.Name) is not { } decl)
            return (text, $"Client {c.Name} nicht gefunden");
        var teile = modell.Akteure.SelectMany(a => a.Vertrag.Select(r => r.Teil ?? a.VertragTyp)).ToHashSet(StringComparer.Ordinal);
        teile.UnionWith(c.Traegt);
        var edits = new List<(int, int, string)>();
        var soll = Scaffolder.ClientBasen(c);
        var fremd = decl.BaseList?.Types.Select(t => t.ToString().Trim()).Where(t => !Scaffolder.IstClientBasis(t, teile)).ToList() ?? [];
        var neu = string.Join(", ", soll.Take(1).Concat(fremd).Concat(soll.Skip(1)));
        if (decl.BaseList is { } bl && N(string.Join(",", bl.Types.Select(t => t.ToString()))) != N(neu))
            edits.Add((bl.Types[0].SpanStart, bl.Types[^1].Span.End, neu));
        var vorhanden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in decl.Members.OfType<MethodDeclarationSyntax>()
                     .Where(m => m.Identifier.Text == Abstractions.Akteurvertrag.Auf && m.ParameterList.Parameters.Count == 1))
        {
            var ein = ErsterTyp(m) ?? "";
            vorhanden.Add(ein);
            if (!c.Kenntnis.Contains(ein)) edits.Add((m.FullSpan.Start, m.FullSpan.End, ""));
        }
        var fehlend = c.Kenntnis.Where(e => !vorhanden.Contains(e)).Select(e => $"\n    {Scaffolder.KenntnisMethode(e)};\n").ToList();
        if (fehlend.Count > 0) edits.Add((decl.CloseBraceToken.SpanStart, decl.CloseBraceToken.SpanStart, string.Concat(fehlend)));
        return edits.Count == 0 ? null : (MitNamespaces(Anwenden(text, edits), c.Traegt.Concat(c.Sendet).Concat(c.Fragt).Concat(c.Kenntnis).ToList()), "Client-Vertrag");
    }

    // ── Bausteine ──────────────────────────────────────────────────────────────────────────────
    private static SyntaxNode Parse(string text) => CSharpSyntaxTree.ParseText(text).GetRoot();

    private static BaseTypeDeclarationSyntax? Typ(SyntaxNode root, string name, string? ns) =>
        root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault(t => t.Identifier.Text == name
            && (t.Parent is BaseNamespaceDeclarationSyntax nsd ? ns == null || nsd.Name.ToString() == ns : t.Parent is CompilationUnitSyntax));

    private static string? ErsterTyp(MethodDeclarationSyntax m) =>
        m.ParameterList.Parameters.Count > 0 ? BoardLeseseite.Basisname(m.ParameterList.Parameters[0].Type?.ToString() ?? "") : null;

    private static string N(string? typ) => BoardLeseseite.Norm(typ ?? "");
    private static string Ohne(string s) => Regex.Replace(s, @"\s+", "");

    /// <summary>Eine Parameterliste in der FORM der alten schreiben: einzeilig bleibt einzeilig, mehrzeilig bleibt je Parameter eine Zeile.</summary>
    private static string Liste(ParameterListSyntax? alt, IReadOnlyList<string> teile)
    {
        if (alt == null || alt.Parameters.Count == 0 || !alt.ToString().Contains('\n')) return "(" + string.Join(", ", teile) + ")";
        var einzug = new string(' ', alt.Parameters[0].GetLocation().GetLineSpan().StartLinePosition.Character);
        var vorErstem = alt.ToString()[..(alt.Parameters[0].SpanStart - alt.SpanStart)];            // "(\n    " bzw. "("
        var nachLetztem = alt.ToString()[(alt.Parameters[^1].Span.End - alt.SpanStart)..];         // "\n)" bzw. ")"
        var zeilenweise = alt.Parameters.SeparatorCount > 0 && alt.Parameters.GetSeparators().Any(s => s.TrailingTrivia.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia)));
        return vorErstem + string.Join(zeilenweise ? ",\n" + einzug : ", ", teile) + nachLetztem;
    }

    /// <summary>Mehrzeiligen Text von seiner Spalte auf einen neuen Einzug bringen (erste Zeile ohne Einzug).</summary>
    private static string Umgerueckt(string text, int spalte, string einzug) =>
        string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select((z, i) =>
            i == 0 ? einzug + z : z.Length >= spalte && z[..spalte].Trim().Length == 0 ? einzug + z[spalte..] : einzug + z.TrimStart()));
    private static string Wrapper(string typ) => typ.Contains('<') ? typ[..typ.IndexOf('<')].Trim() : typ;

    /// <summary>Text-Änderungen (Start, Ende, Neu) von hinten nach vorn anwenden.</summary>
    private static string Anwenden(string text, List<(int Start, int Ende, string Neu)> edits)
    {
        foreach (var (start, ende, neu) in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.Ende))
            text = text[..start] + neu + text[ende..];
        return text;
    }

    /// <summary>Namespaces der genannten Typen (aus dem Modell), die der Datei fehlen, als using ergänzen.</summary>
    private string MitNamespaces(string text, IEnumerable<string> typen)
    {
        _nsVon ??= modell.Records.Select(r => (r.Name, r.Namespace)).Concat(modell.Enums.Select(e => (e.Name, e.Namespace)))
            .Concat((modell.Lesen?.Stores ?? []).SelectMany(s => s.Fns.Select(f => (f.Name, f.Namespace ?? s.Namespace)).Append((s.Name, s.Namespace))))
            // Akteur-Vertrags-Teile liegen beim Akteur — ein Client, der einen neuen Teil trägt, braucht dessen Namespace.
            .Concat(modell.Akteure.SelectMany(a => a.Vertrag.Select(r => (Name: r.Teil ?? a.VertragTyp, a.Namespace))))
            .GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Item2, StringComparer.Ordinal);
        var root = Parse(text);
        var dateiNs = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        var da = root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax).OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString()).ToHashSet(StringComparer.Ordinal);
        var fehlend = typen.SelectMany(t => Regex.Matches(t ?? "", @"[A-Za-z_]\w*").Select(m => m.Value))
            .Select(n => _nsVon.GetValueOrDefault(n)).Where(ns => ns != null && ns != dateiNs && !da.Contains(ns) && dateiNs?.StartsWith(ns + ".") != true)
            .Distinct().ToList();
        return fehlend.Count == 0 ? text : string.Concat(fehlend.Select(ns => $"using {ns};\n")) + text;
    }

    private static string MitUsingsAus(string text, SyntaxNode genRoot)
    {
        var da = Parse(text).DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax).OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString().Trim()).ToHashSet(StringComparer.Ordinal);
        var fehlend = genRoot.DescendantNodes(n => n is CompilationUnitSyntax).OfType<UsingDirectiveSyntax>()
            .Select(u => u.ToString().Trim()).Where(u => !da.Contains(u)).ToList();
        return fehlend.Count == 0 ? text : string.Join("\n", fehlend) + "\n" + text;
    }
}
