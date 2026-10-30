using System.Text.RegularExpressions;
using Codeformen;
using DomainEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

/// <summary>Die Änderung einer Geste an EINER Datei: Inhalt vorher (null = neu angelegt) und nachher (null = gelöscht), Pfad relativ zur Solution.</summary>
public sealed record DateiPatch(string Datei, string? Vorher, string? Nachher);

/// <summary>
/// Die Dateien eines Schreiblaufs im Speicher: gelesen bei Bedarf, geändert im Speicher, gespeichert erst am Ende (Trockenlauf = nie).
/// Merkt sich den Ausgangsstand jeder Datei — daraus entsteht der Patch einer Geste (und sein Gegenstück für ↶).
/// <paramref name="quelle"/>: woher eine Datei kommt (voller Pfad → Inhalt, null = gibt es nicht); ohne Angabe die Platte. Die Sonde schreibt
/// so in einen Solution-Fork im Speicher.
/// </summary>
public sealed class Dateipuffer(string root, Func<string, string?>? quelle = null)
{
    private readonly Dictionary<string, string?> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _vorher = new(StringComparer.Ordinal);
    private readonly List<string> _geaendert = new();

    public string Wurzel => root;
    public string Voll(string rel) => Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
    public bool Innerhalb(string rel) => Voll(rel).StartsWith(Path.GetFullPath(root), StringComparison.Ordinal);

    public string? Lies(string rel)
    {
        if (_text.TryGetValue(rel, out var t)) return t;
        var p = Voll(rel);
        var inhalt = quelle != null ? quelle(p) : File.Exists(p) ? File.ReadAllText(p) : null;
        _vorher[rel] = inhalt;
        return _text[rel] = inhalt;
    }

    public void Setze(string rel, string text)
    {
        if (Lies(rel) == text) return;
        _text[rel] = text;
        if (!_geaendert.Contains(rel)) _geaendert.Add(rel);
    }

    /// <summary>Die Datei entfällt (z. B. ihr letzter Typ wurde gelöscht).</summary>
    public void Entferne(string rel)
    {
        if (Lies(rel) == null) return;
        _text[rel] = null;
        if (!_geaendert.Contains(rel)) _geaendert.Add(rel);
    }

    public IReadOnlyList<string> Geaendert => _geaendert;

    /// <summary>Was sich wirklich geändert hat (Datei für Datei, vorher/nachher).</summary>
    public IReadOnlyList<DateiPatch> Patch() => _geaendert.Where(rel => _vorher.GetValueOrDefault(rel) != _text[rel])
        .Select(rel => new DateiPatch(rel, _vorher.GetValueOrDefault(rel), _text[rel])).ToList();

    public void Speichern()
    {
        foreach (var p in Patch())
        {
            var voll = Voll(p.Datei);
            if (p.Nachher == null) { if (File.Exists(voll)) File.Delete(voll); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
            File.WriteAllText(voll, p.Nachher);
        }
    }
}

/// <summary>
/// Der ABGLEICH bestehender Deklarationen mit dem Modell — nur für Elemente, die im Editor geändert wurden (Herkunfts-Stempel
/// weicht ab). Das Modell besitzt die Signatur/Deklaration (Felder, Enum-Werte, OneOf-Rückgaben, Parameter einer Fähigkeit,
/// Basisliste/Attribut-Argumente); der Code besitzt Rümpfe und Handcode. Vorhandenes, das sich nicht
/// geändert hat, bleibt Wort für Wort; ersetzt wird nur das Geänderte. Bricht dadurch ein Rumpf, meldet es der Bau.
/// </summary>
internal sealed class Abgleich(EditorModell modell, Dateipuffer ws, List<string> ergaenzt, List<string> uebersprungen)
{
    private static readonly string OneOf = typeof(Abstractions.OneOf<>).Name.Split('`')[0];
    private static readonly string MindestensEinmal = nameof(Abstractions.MindestensEinmalAttribute)[..^"Attribute".Length];
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
        // Domänen-Dienst (docs/06 §6.5): die Methoden am Interface und an jeder Implementierung (fehlende als throw-Stub); je Implementierung ihr
        //   Konstruktor. Rümpfe gehören dem Code (✎ am Rumpf); der Name ist die Identität (Umbenennen ist ein Refactoring).
        foreach (var d in modell.Dienste.Where(d => d.Datei != null && Herkunft.Geaendert(d.Herkunft, Herkunft.Von(d))))
        {
            var was = $"dienst {d.Namespace}.{d.Name}";
            Datei(d.Datei!, was, t => DienstMethoden(t, d.Name, d, null));
            foreach (var i in d.Implementierungen.Where(i => i.Datei != null))
            {
                Datei(i.Datei!, was, t => DienstMethoden(t, i.Name, d, i));
                Datei(i.Datei!, was, t => Konstruktor(t, i.Name, i.Konstruktor));
            }
        }
        foreach (var a in modell.Akteure.Where(a => a.Datei != null && Herkunft.Geaendert(a.Herkunft, Herkunft.Von(a))))
            Datei(a.Datei!, $"akteur {a.Namespace}.{a.Name}", t => AkteurBefugnisse(t, a));
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
            Datei(k.Datei!, $"konsument {k.Name}", t => Klasse(t, k.Name, k.Basen, k.MindestensEinmal));
        foreach (var r in l.Reader.Where(r => r.Datei != null && Herkunft.Geaendert(r.Herkunft, Herkunft.Von(r))))
            Datei(r.Datei!, $"reader {r.Name}", t => Klasse(t, r.Name, r.Basen, null));
        foreach (var (besitzer, methode, h) in l.Konsumenten.SelectMany(k => k.Handles.Select(h => (k.Name, Abstractions.Projektionsvertrag.Auf, h)))
                     .Concat(l.Reader.SelectMany(r => r.Handles.Select(h => (r.Name, Abstractions.Projektionsvertrag.Frage, h))))
                     .Where(x => x.h.Datei != null && Herkunft.Geaendert(x.h.Herkunft, Herkunft.Von(x.h))))
            Datei(h.Datei!, $"handle {besitzer}.{methode}({h.Eingang})", t => HandleRueckgabe(t, besitzer, h));
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
        var pos = r.Felder.Where(f => !f.Geerbt && f.Zugriff is null).ToList();   // geerbte Treiber-Felder gehören der Basis
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
        var soll = r.Felder.Where(f => !f.Geerbt && f.Zugriff is not null).Select(f => (f.Name, $"{N(f.Typ)}|{f.Standard}|{f.Zugriff}|{f.Pflicht}",
            $"public {(f.Pflicht ? "required " : "")}{f.Typ} {f.Name} {f.Zugriff}" + (f.Standard is null ? "" : $" = {f.Standard};"))).ToList();
        if (Eigenschaften(decl, soll, p => Feldregeln.Eigenschaft(p) is { } e ? (e.Name, $"{N(e.Typ)}|{e.Standard}|{e.Zugriff}|{e.Pflicht}") : null, new HashSet<string>(), edits))
            notiz.Add("Property-Felder");
        if (r.Kind == RecordArt.ReadModel && GeteiltMarker(decl, r.Geteilt, edits)) notiz.Add(r.Geteilt ? "geteilt" : "nicht mehr geteilt");
        return (MitNamespaces(Anwenden(text, edits), r.Felder.Select(f => f.Typ)), string.Join(", ", notiz));
    }

    /// <summary>
    /// „geteilt“ eines ReadModels = der Marker <c>IGeteiltesReadModel</c> in der Basisliste. Nur dieser eine Eintrag wird
    /// hinzugefügt bzw. entfernt (samt Komma); alle übrigen Basen bleiben Wort für Wort.
    /// </summary>
    private static bool GeteiltMarker(TypeDeclarationSyntax decl, bool soll, List<(int, int, string)> edits)
    {
        var marker = nameof(Abstractions.IGeteiltesReadModel);
        var typen = decl.BaseList?.Types;
        var i = typen?.IndexOf(t => N(t.Type.ToString()).Split('.').Last() == marker) ?? -1;
        if ((i >= 0) == soll) return false;
        if (soll)
        {
            if (decl.BaseList is { } bl) edits.Add((bl.Span.End, bl.Span.End, ", " + marker));
            else
            {
                var at = (decl as RecordDeclarationSyntax)?.ParameterList?.Span.End ?? decl.TypeParameterList?.Span.End ?? decl.Identifier.Span.End;
                edits.Add((at, at, " : " + marker));
            }
            return true;
        }
        var liste = typen!.Value;
        if (liste.Count == 1) edits.Add((decl.BaseList!.ColonToken.SpanStart, decl.BaseList.Span.End, ""));
        else if (i > 0) edits.Add((liste.GetSeparator(i - 1).SpanStart, liste[i].Span.End, ""));
        else edits.Add((liste[0].SpanStart, liste[1].SpanStart, ""));
        return true;
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

    // ── Fähigkeit: Rückgabe + Parameter am Interface bzw. an der Impl-Methode ──
    private (string, string)? MethodenSignatur(string text, string klasse, string? methode, Faehigkeit f, bool schnittstelle)
    {
        var m = Methode(Parse(text), klasse, methode);
        if (m == null) return schnittstelle ? (text, $"{klasse} nicht gefunden") : null;   // Impl: die Methode liegt in einer anderen Teil-Datei
        return Signatur(text, m, f.Rueckgabe, f.Parameter);
    }

    // ── Dienst: Methoden-Signaturen (Abgleich je Name), fehlende Methoden anhängen; am Interface fallen entfernte weg ──
    private (string, string)? DienstMethoden(string text, string klasse, Dienst d, DienstImpl? impl)
    {
        var notiz = new List<string>();
        foreach (var m in d.Methoden)
        {
            var root = Parse(text);
            if (root.DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault(t => t.Identifier.Text == klasse) is not { } typ)
                return (text, $"{klasse} nicht gefunden");
            if (typ.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(x => x.Identifier.Text == m.Name) is { } decl)
            {
                if (Signatur(text, decl, m.Rueckgabe, m.Parameter) is { } neu) { text = neu.Item1; notiz.Add(m.Name); }
                continue;
            }
            var einzug = "    ";
            var zeile = impl == null ? $"{einzug}{Scaffolder.DienstMethodeKopf(m)};\n" : Scaffolder.DienstMethodeStub(d, impl, m, einzug);
            var vor = typ.CloseBraceToken.SpanStart;
            var leer = typ.Members.Count > 0 && impl != null ? "\n" : "";
            text = MitNamespaces(text[..vor] + leer + zeile + text[vor..], m.Parameter.Select(p => p.Typ).Append(m.Rueckgabe));
            notiz.Add("+" + m.Name);
        }
        if (impl == null)
        {
            var root = Parse(text);
            var typ = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().FirstOrDefault(t => t.Identifier.Text == klasse);
            var weg = typ?.Members.OfType<MethodDeclarationSyntax>().Where(x => d.Methoden.All(m => m.Name != x.Identifier.Text)).ToList() ?? [];
            foreach (var x in weg.OrderByDescending(x => x.SpanStart))
            {
                var von = x.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)) is { } doku && doku != default
                    ? doku.SpanStart : x.SpanStart;
                while (von > 0 && text[von - 1] is ' ' or '\t') von--;
                var bis = x.Span.End;
                if (bis < text.Length && text[bis] == '\n') bis++;
                text = text[..von] + text[bis..];
                notiz.Add("−" + x.Identifier.Text);
            }
        }
        return notiz.Count == 0 ? null : (text, "Methoden " + string.Join(", ", notiz));
    }

    // ── Konstruktor einer Dienst-Implementierung: nur die Parameterliste — Primär-Konstruktor bzw. der öffentliche Konstruktor ──
    private (string, string)? Konstruktor(string text, string klasse, IReadOnlyList<Parameter> soll)
    {
        var root = Parse(text);
        if (root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == klasse) is not { } cls)
            return (text, $"{klasse} nicht gefunden");
        ParameterListSyntax? liste = cls.ParameterList
            ?? cls.Members.OfType<ConstructorDeclarationSyntax>().Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                .OrderByDescending(c => c.ParameterList.Parameters.Count).FirstOrDefault()?.ParameterList;
        var ist = liste?.Parameters.ToList() ?? [];
        bool Gleich(ParameterSyntax p, Parameter q) => p.Identifier.Text == q.Name && N(p.Type?.ToString()) == N(q.Typ);
        if (ist.Count == soll.Count && ist.Zip(soll).All(z => Gleich(z.First, z.Second))) return null;
        var teile = soll.Select(q => ist.FirstOrDefault(p => Gleich(p, q))?.ToString() ?? $"{q.Typ} {q.Name}").ToList();
        string neu;
        if (liste != null && (liste != cls.ParameterList || soll.Count > 0)) neu = Anwenden(text, [(liste.SpanStart, liste.Span.End, Liste(liste, teile))]);
        else if (liste != null) neu = Anwenden(text, [(liste.SpanStart, liste.Span.End, "")]);   // Primär-Konstruktor ohne Parameter: weg
        else
        {
            var nach = cls.TypeParameterList?.Span.End ?? cls.Identifier.Span.End;
            neu = Anwenden(text, [(nach, nach, "(" + string.Join(", ", teile) + ")")]);
        }
        return (MitNamespaces(neu, soll.Select(p => p.Typ)), "Konstruktor");
    }

    private static MethodDeclarationSyntax? Methode(SyntaxNode root, string klasse, string? methode) =>
        root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => t.Identifier.Text == klasse)
            .SelectMany(t => t.Members.OfType<MethodDeclarationSyntax>()).FirstOrDefault(x => methode == null || x.Identifier.Text == methode);

    /// <summary>
    /// Rückgabe und Parameterliste einer Methode auf die Signatur des Modells bringen — gleiche Parameter bleiben Wort für Wort, die
    /// Form der Liste (ein-/mehrzeilig) bleibt. Der Rumpf wird nie angefasst; bricht er dadurch, meldet es der anschließende Bau.
    /// </summary>
    private (string, string)? Signatur(string text, MethodDeclarationSyntax m, string rueckgabe, IReadOnlyList<Parameter> parameter)
    {
        var edits = new List<(int, int, string)>();
        if (N(m.ReturnType.ToString()) != N(rueckgabe)) edits.Add((m.ReturnType.SpanStart, m.ReturnType.Span.End, rueckgabe));
        var ist = m.ParameterList.Parameters.ToList();
        bool Gleich(ParameterSyntax p, Parameter q) => p.Identifier.Text == q.Name && N(p.Type?.ToString()) == N(q.Typ);
        if (ist.Count != parameter.Count || ist.Zip(parameter).Any(z => !Gleich(z.First, z.Second)))
            edits.Add((m.ParameterList.SpanStart, m.ParameterList.Span.End, Liste(m.ParameterList, parameter.Select(q =>
                ist.FirstOrDefault(p => Gleich(p, q))?.ToString() ?? $"{q.Typ} {q.Name}" + (q.Standard is null ? "" : $" = {q.Standard}")).ToList())));
        if (edits.Count == 0) return null;
        return (MitNamespaces(Anwenden(text, edits), parameter.Select(p => p.Typ).Append(rueckgabe)), "Signatur");
    }

    // ── Auf/Frage: Eingang (umverdrahtet) + Rückgabe (Antworten); die Fähigkeits-Parameter gleicht ParameterAbgleich ab ──
    private (string, string)? HandleRueckgabe(string text, string klasse, Handle h)
    {
        var imCode = h.EingangImCode ?? h.Eingang;
        var root = Parse(text);
        var m = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Identifier.Text == klasse)
            .SelectMany(c => c.Members.OfType<MethodDeclarationSyntax>()).FirstOrDefault(x => ErsterTyp(x) == imCode);
        if (m == null) return (text, "Eingang nicht gefunden");
        var edits = new List<(int, int, string)>();
        var notiz = new List<string>();
        if (imCode != h.Eingang && m.ParameterList.Parameters[0].Type is { } eingangsTyp)
        {
            edits.Add((eingangsTyp.SpanStart, eingangsTyp.Span.End, h.Eingang));
            notiz.Add($"Eingang {imCode} → {h.Eingang}");
        }
        if (h.Rueckgabe != null && N(m.ReturnType.ToString()) != N(h.Rueckgabe))
        {
            edits.Add((m.ReturnType.SpanStart, m.ReturnType.Span.End, h.Rueckgabe));
            notiz.Add("Ausgänge");
        }
        if (edits.Count == 0) return null;
        return (MitNamespaces(Anwenden(text, edits), h.Ausgaenge.Append(h.Eingang)), string.Join(", ", notiz));
    }

    // ── Klasse: Basisliste verbatim; bei einer Projektion das Attribut [MindestensEinmal] (R14) ──
    private (string, string)? Klasse(string text, string name, IReadOnlyList<string>? basen, bool? mindestensEinmal)
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
        if (mindestensEinmal is { } soll)
        {
            var liste = decls.SelectMany(d => d.AttributeLists)
                .FirstOrDefault(al => al.Attributes.Any(a => BoardLeseseite.Basisname(a.Name.ToString()) is var n && (n == MindestensEinmal || n == MindestensEinmal + "Attribute")));
            if (soll && liste == null)
            {
                var spalte = haupt.GetLocation().GetLineSpan().StartLinePosition.Character;
                var at = haupt.AttributeLists.Count > 0 ? haupt.AttributeLists[0].SpanStart : haupt.Modifiers.Count > 0 ? haupt.Modifiers[0].SpanStart : haupt.Keyword.SpanStart;
                edits.Add((at, at, $"[{MindestensEinmal}]\n" + new string(' ', spalte)));
                notiz.Add(MindestensEinmal);
            }
            else if (!soll && liste != null)
            {
                // Steht es allein in seiner Liste, fällt die Liste samt Zeilenende; sonst nur das Attribut.
                var attr = liste.Attributes.First(a => BoardLeseseite.Basisname(a.Name.ToString()) is var n && (n == MindestensEinmal || n == MindestensEinmal + "Attribute"));
                if (liste.Attributes.Count == 1)
                {
                    var ende = liste.Span.End;
                    while (ende < text.Length && text[ende] is ' ' or '\t') ende++;
                    if (ende < text.Length && text[ende] == '\r') ende++;
                    if (ende < text.Length && text[ende] == '\n') ende++;
                    var start = liste.SpanStart;
                    while (start > 0 && text[start - 1] is ' ' or '\t') start--;
                    edits.Add((start, ende, ""));
                }
                else
                {
                    var i = liste.Attributes.IndexOf(attr);
                    var (von, bis) = i == 0 ? (attr.SpanStart, liste.Attributes[1].SpanStart) : (liste.Attributes[i - 1].Span.End, attr.Span.End);
                    edits.Add((von, bis, ""));
                }
                notiz.Add(MindestensEinmal);
            }
        }
        var neu = Anwenden(text, edits);
        if (basen != null && neu != text) neu = MitNamespaces(neu, basen);
        return (neu, string.Join(", ", notiz));
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

    // ── Client-Vertrag (docs/konzept-akteure.md §4): die Basisliste (IVerkoerpert, IHoert) gehört dem Modell; fremde Basistypen
    //    (z. B. ein geerbter Client-Vertrag) bleiben stehen. ──
    private (string, string)? ClientVertrag(string text, Client c)
    {
        var root = Parse(text);
        if (root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().FirstOrDefault(i => i.Identifier.Text == c.Name) is not { } decl)
            return (text, $"Client {c.Name} nicht gefunden");
        var edits = new List<(int, int, string)>();
        var soll = Scaffolder.ClientBasen(c);
        var fremd = decl.BaseList?.Types.Select(t => t.ToString().Trim()).Where(t => !Scaffolder.IstClientBasis(t)).ToList() ?? [];
        var neu = string.Join(", ", soll.Take(1).Concat(fremd).Concat(soll.Skip(1)));
        if (decl.BaseList is { } bl && N(string.Join(",", bl.Types.Select(t => t.ToString()))) != N(neu))
            edits.Add((bl.Types[0].SpanStart, bl.Types[^1].Span.End, neu));
        return edits.Count == 0 ? null : (MitNamespaces(Anwenden(text, edits), c.Verkoerpert.Concat(c.Kenntnis).ToList()), "Client-Vertrag");
    }

    // ── Bausteine ──────────────────────────────────────────────────────────────────────────────
    private static SyntaxNode Parse(string text) => CSharpSyntaxTree.ParseText(text).GetRoot();

    private static BaseTypeDeclarationSyntax? Typ(SyntaxNode root, string name, string? ns) =>
        root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault(t => t.Identifier.Text == name
            && (t.Parent is BaseNamespaceDeclarationSyntax nsd ? ns == null || nsd.Name.ToString() == ns : t.Parent is CompilationUnitSyntax));

    private static string? ErsterTyp(MethodDeclarationSyntax m) =>
        m.ParameterList.Parameters.Count > 0 ? BoardLeseseite.Basisname(m.ParameterList.Parameters[0].Type?.ToString() ?? "") : null;

    private static string N(string? typ) => BoardLeseseite.Norm(typ ?? "");

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
            // Funktionen: ein Akteur, der eine rechnen darf (IDarf<IFunktion>), braucht ihren Namespace.
            .Concat(modell.BausteinFunktionen.Select(f => (f.Name, f.Namespace)))
            // Domänen-Dienste: eine Implementierung, die einen nimmt, braucht seinen Namespace.
            .Concat(modell.Dienste.Select(d => (d.Name, d.Namespace)))
            // Akteure: ein Client, der einen verkörpert (IVerkoerpert<A>), braucht seinen Namespace.
            .Concat(modell.Akteure.Select(a => (a.Name, a.Namespace)))
            .GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Item2, StringComparer.Ordinal);
        var root = Parse(text);
        var dateiNs = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        var da = root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax).OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString()).ToHashSet(StringComparer.Ordinal);
        var fehlend = typen.SelectMany(t => Regex.Matches(t ?? "", @"[A-Za-z_]\w*").Select(m => m.Value))
            .Select(n => _nsVon.GetValueOrDefault(n)).Where(ns => ns != null && ns != dateiNs && !da.Contains(ns) && dateiNs?.StartsWith(ns + ".") != true)
            .Distinct().ToList();
        return Usings.Ergaenze(text, fehlend.OfType<string>());
    }
}
