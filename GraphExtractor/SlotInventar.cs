using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphExtractor;

// ════════════════════════════════════════════════════════════════════════════
//  Slot-Inventar (docs/konzept-llm-minimalkontext.md §13)
//
//  Alle Stellen, an denen ein Code-Block hängt (H-Slots), über ihre Marker gefunden — Decide, Apply, Projektion,
//  Reaktion, Reader, Pipeline, Store-Implementierung, Saga-Lambda — und je Rumpf jedes gebundene Symbol klassifiziert:
//
//    P  Parameter des Slots            S  State-Member (Decide/Apply)       F  Feld/Property der Klasse (injiziert)
//    H  Helfer-Methode der Klasse      X  Aufruf eines anderen Slots        K  statisch/const der Klasse
//    D  Domänen-Symbol                 R  Framework-Symbol                  B  BCL / Fremdbibliothek
//
//  Dann für D/R: liegt das Symbol im SPIELRAUM (transitive Hülle der Typen, die aus Signatur, State, Feldern und Helfern
//  erreichbar sind)? Wenn nicht: benutzt es ein NACHBAR (anderer Slot derselben Klasse) oder ein ARTGENOSSE (Slot derselben
//  Art irgendwo)? Was übrig bleibt, ist Wissen, das von außen kommen muss. Reine Messung — keine Heuristik, kein LLM.
// ════════════════════════════════════════════════════════════════════════════

public sealed class SlotInventar
{
    public sealed record SlotRef(string Art, INamedTypeSymbol Klasse, IMethodSymbol? Methode, SyntaxNode Rumpf, string Disc,
        INamedTypeSymbol? State, List<IParameterSymbol> Parameter, List<ITypeSymbol> SignaturTypen)
    {
        public string Id => $"{Art} {Klasse.Name}.{Disc}";
    }

    public sealed record Nutzung(char Kat, string Id, string Anzeige);

    private readonly List<Compilation> _comps;
    private readonly HashSet<string> _domänen, _framework;
    private readonly INamedTypeSymbol? _iCommand, _iEvent, _iQuery, _iDecider, _iApplier, _iSubscriber, _iReader,
        _iPipeline, _pipelineContext, _iAggEnvelope, _iWriteStore, _iReadStore, _iProzessDef;
    public List<SlotRef> Slots { get; } = new();
    public Dictionary<SlotRef, List<Nutzung>> Nutzungen { get; } = new();
    public Dictionary<SlotRef, HashSet<string>> Spielraum { get; } = new();
    /// <summary>Zusätzlich über Graph-Relationen erreichbar (Editor-Verdrahtung): Pipeline → gesendete Commands, Store → ReadModels.</summary>
    public Dictionary<SlotRef, HashSet<string>> GraphRaum { get; } = new();
    private readonly DomainModel _dom;

    public SlotInventar(Projektlage lage, DomainModel dom)
    {
        _dom = dom;
        _comps = lage.Compilations;
        _domänen = new HashSet<string>(lage.DomänenAssemblies, StringComparer.Ordinal);
        // Framework = analysierte Projekte, die keine Domäne sind (Vertrag, Kern, Laufzeit) — abgeleitet, nicht benannt.
        _framework = lage.Analyse.Select(a => a.Compilation.AssemblyName ?? "").Where(n => !_domänen.Contains(n)).ToHashSet(StringComparer.Ordinal);
        INamedTypeSymbol? Get(string n) => _comps.Select(c => c.GetTypeByMetadataName(n)).FirstOrDefault(x => x != null);
        _iCommand = Get(Vertrag.ICommand); _iEvent = Get(Vertrag.IEvent); _iQuery = Get(Vertrag.IQuery);
        _iDecider = Get(Vertrag.IDecider); _iApplier = Get(Vertrag.IApplier); _iSubscriber = Get(Vertrag.ISubscriber);
        _iReader = Get(Vertrag.IReader); _iPipeline = Get(Vertrag.IPipelineHandler); _pipelineContext = Get(Vertrag.PipelineContext);
        _iAggEnvelope = Get(Vertrag.IAggregateEnvelope); _iWriteStore = Get(Vertrag.IWriteStore); _iReadStore = Get(Vertrag.IReadStore);
        _iProzessDef = Get(Vertrag.IProzessDefinition);
        Sammle();
        foreach (var s in Slots)
        {
            Nutzungen[s] = Klassifiziere(s).ToList();
            Spielraum[s] = Hülle(s, Array.Empty<ITypeSymbol>());
            var graph = GraphWurzeln(s).ToList();
            GraphRaum[s] = graph.Count == 0 ? new() : Hülle(s, graph).Except(Spielraum[s]).ToHashSet(StringComparer.Ordinal);
        }
    }

    // ── Slots finden (Marker + Signatur, nie Namen) ──

    private void Sammle()
    {
        var typen = _comps.Where(c => _domänen.Contains(c.AssemblyName ?? ""))
            .SelectMany(c => AlleTypen(c.Assembly.GlobalNamespace))
            .Where(t => t.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree)))
            .GroupBy(t => t.Fq()).Select(g => g.First()).ToList();

        foreach (var t in typen)
        {
            var decider = t.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.Fq() == _iDecider?.Fq());
            var applier = t.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.Fq() == _iApplier?.Fq());
            foreach (var m in Methoden(t))
            {
                var p0 = m.Parameters.FirstOrDefault()?.Type;
                if (p0 == null) continue;
                if (decider != null && Sym.Implements(p0, _iCommand))
                    Add("decide", t, m, decider.TypeArguments[0] as INamedTypeSymbol);
                else if (applier != null && Sym.Implements(p0, _iEvent))
                    Add("apply", t, m, applier.TypeArguments[0] as INamedTypeSymbol);
                else if (Sym.Implements(t, _iSubscriber) && Sym.Implements(p0, _iEvent)
                         && m.Parameters.Skip(1).Any(p => p.Type.Fq() == _iAggEnvelope?.Fq()))
                    Add(Vertrag.Ist(m.ReturnType, typeof(IAsyncEnumerable<>)) ? "reaktion" : "projektion", t, m, null);
                else if (t.AllInterfaces.Any(i => i.OriginalDefinition.Fq() == _iReader?.Fq()) && Sym.Implements(p0, _iQuery))
                    Add("reader", t, m, null);
                else if (Sym.Implements(t, _iPipeline) && m.Parameters.Skip(1).Any(p => p.Type.Fq() == _pipelineContext?.Fq()))
                    Add("pipeline", t, m, null);
            }

            // Store-Implementierung: Methoden, die ein Member eines Domänen-Store-Interfaces implementieren.
            if (t.TypeKind == TypeKind.Class && !t.IsAbstract)
                foreach (var iface in t.AllInterfaces.Where(i => _domänen.Contains(i.ContainingAssembly?.Name ?? "")
                             && (Sym.Implements(i, _iWriteStore) || Sym.Implements(i, _iReadStore))))
                    foreach (var im in iface.GetMembers().OfType<IMethodSymbol>())
                        if (t.FindImplementationForInterfaceMember(im) is IMethodSymbol impl && Rumpf(impl) != null
                            && !Slots.Any(s => SymbolEqualityComparer.Default.Equals(s.Methode, impl)))
                            Add("store", t, impl, null, impl.Name);

            // Saga-Lambdas: Lambda-Argumente der Regel-Verben in Prozess-Definitionen.
            if (Sym.Implements(t, _iProzessDef))
                foreach (var syn in t.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).Where(n => !Projektlage.IstGeneriert(n.SyntaxTree)))
                {
                    var model = Model(syn.SyntaxTree);
                    foreach (var inv in syn.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    {
                        if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol verb || !Vertrag.IstRegelVerb(verb)) continue;
                        foreach (var lam in inv.ArgumentList.Arguments.Select(a => a.Expression).OfType<LambdaExpressionSyntax>())
                        {
                            var ps = (model.GetSymbolInfo(lam).Symbol as IMethodSymbol)?.Parameters.ToList() ?? new();
                            var sig = ps.Select(p => p.Type).ToList();
                            if (model.GetTypeInfo(lam).ConvertedType is INamedTypeSymbol dt) sig.Add(dt);
                            Slots.Add(new SlotRef("saga", t, null, lam.Body, $"{verb.Name}<{string.Join(",", verb.TypeArguments.Select(a => a.Name))}>",
                                null, ps, sig));
                        }
                    }
                }
        }
    }

    private void Add(string art, INamedTypeSymbol t, IMethodSymbol m, INamedTypeSymbol? state, string? disc = null)
    {
        if (Rumpf(m) is not { } r) return;
        var sig = m.Parameters.Select(p => p.Type).Append(m.ReturnType).ToList();
        Slots.Add(new SlotRef(art, t, m, r, disc ?? m.Parameters.FirstOrDefault()?.Type.Name ?? m.Name, state, m.Parameters.ToList(), sig));
    }

    /// <summary>
    /// Typen, die der GRAPH dem Slot zuordnet, obwohl die Signatur sie nicht nennt — im Editor als Kante verdrahtet:
    /// Pipeline-Handle → gesendete Commands/Trigger; Store-Implementierung → die ReadModels ihres Stores.
    /// </summary>
    private IEnumerable<ITypeSymbol> GraphWurzeln(SlotRef s)
    {
        INamedTypeSymbol? Typ(string full) => _comps.Select(c => c.GetTypeByMetadataName(full)).FirstOrDefault(x => x != null);
        if (s.Art == "pipeline" && s.Parameter.FirstOrDefault()?.Type is { } input)
            foreach (var p in _dom.Pipelines.Where(p => p.Full == s.Klasse.Fq()))
                foreach (var h in p.Handles.Where(h => h.InputFull == input.Fq()))
                    foreach (var e in h.EmitsFull.Concat(p.HandleEmitsTriggers.GetValueOrDefault(h.InputFull) ?? new()))
                        if (Typ(e) is { } t) yield return t;
        // Projektion/Reaktion: das Aggregat, das das behandelte Event erzeugt (Graph: Decide-Ausgang) — für Track<Agg>.
        if (s.Art is "projektion" or "reaktion" && s.Parameter.FirstOrDefault()?.Type is { } evt)
            foreach (var a in _dom.Aggregates.Where(a => a.DecideOutcomes.Values.Any(o => o.Contains(evt.Fq()))))
                if (Typ(a.Full) is { } t) yield return t;
        if (s.Art == "store")
            foreach (var st in _dom.Stores.Where(st => st.ImplsFull.Contains(s.Klasse.Fq())))
                foreach (var rm in _dom.ReadModels.Where(rm => rm.Store == st.Name || rm.StoreKandidaten?.Contains(st.Name) == true))
                    if (Typ(rm.Full) is { } t) yield return t;
    }

    private static IEnumerable<IMethodSymbol> Methoden(INamedTypeSymbol t) => t.GetMembers().OfType<IMethodSymbol>()
        .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public
                    && m.DeclaringSyntaxReferences.Any(r => !Projektlage.IstGeneriert(r.SyntaxTree)));

    private static SyntaxNode? Rumpf(IMethodSymbol m) =>
        m.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<MethodDeclarationSyntax>()
            .Where(s => !Projektlage.IstGeneriert(s.SyntaxTree))
            .Select(s => (SyntaxNode?)s.Body ?? s.ExpressionBody).FirstOrDefault(b => b != null);

    // ── Symbole eines Rumpfs klassifizieren ──

    private IEnumerable<Nutzung> Klassifiziere(SlotRef s)
    {
        var model = Model(s.Rumpf.SyntaxTree);
        var slotMethoden = Slots.Where(x => x.Klasse.Fq() == s.Klasse.Fq() && x.Methode != null).Select(x => x.Methode!).ToList();
        foreach (var n in s.Rumpf.DescendantNodesAndSelf().OfType<SimpleNameSyntax>())
        {
            var sym = model.GetSymbolInfo(n).Symbol ?? model.GetSymbolInfo(n).CandidateSymbols.FirstOrDefault();
            if (sym == null || sym is INamespaceSymbol or ILocalSymbol or IRangeVariableSymbol or ITypeParameterSymbol) continue;
            if (sym is IMethodSymbol { MethodKind: MethodKind.Constructor } c) sym = c.ContainingType;
            if (sym is IMethodSymbol { ReducedFrom: { } red }) sym = red;   // Extension-Methoden auf ihre Definition

            if (sym is IParameterSymbol ps)
            {
                if (s.Parameter.Any(p => SymbolEqualityComparer.Default.Equals(p, ps))) yield return new('P', "P:" + ps.Name, ps.Name);
                continue;   // Lambda-Parameter im Rumpf = lokal
            }
            var owner = sym.ContainingType;
            if (s.State != null && owner?.Fq() == s.State.Fq()) { yield return new('S', "S:" + sym.Name, "State." + sym.Name); continue; }
            if (owner != null && owner.Fq() == s.Klasse.Fq())
            {
                if (sym is IPropertySymbol sp && s.State != null && SymbolEqualityComparer.Default.Equals(sp.Type, s.State)) continue; // generierter State-Zugang
                if (sym is IMethodSymbol hm)
                {
                    var istSlot = slotMethoden.Any(x => SymbolEqualityComparer.Default.Equals(x, hm));
                    yield return new(istSlot ? 'X' : sym.IsStatic ? 'K' : 'H', (istSlot ? "X:" : "H:") + sym.Name, sym.Name + "()");
                }
                else yield return new(sym.IsStatic ? 'K' : 'F', "F:" + sym.Name, $"{sym.Name} : {TypVon(sym)?.ToDisplayString() ?? "?"}");
                continue;
            }
            var asm = (sym as ITypeSymbol ?? owner)?.ContainingAssembly?.Name ?? "";
            var id = Id(sym);
            var anzeige = sym is ITypeSymbol ? sym.Name : $"{owner?.Name}.{sym.Name}";
            yield return new(_domänen.Contains(asm) ? 'D' : _framework.Contains(asm) ? 'R' : 'B', id, anzeige);
        }
    }

    // ── Spielraum: transitive Hülle der aus der Signatur erreichbaren Domänen-/Framework-Typen ──

    private HashSet<string> Hülle(SlotRef s, IEnumerable<ITypeSymbol> zusatz)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var besucht = new HashSet<string>(StringComparer.Ordinal);
        var offen = new Queue<ITypeSymbol>(s.SignaturTypen.Concat(zusatz));
        if (s.State != null) offen.Enqueue(s.State);
        // Geerbte, nicht-private Member der eigenen Klasse (z. B. protected Primitive einer Store-Basisklasse).
        for (var bt = s.Klasse.BaseType; bt != null && bt.SpecialType != SpecialType.System_Object; bt = bt.BaseType)
            foreach (var m in bt.GetMembers().Where(m => m.DeclaredAccessibility is not Accessibility.Private))
            {
                ids.Add(Id(m));
                if (TypVon(m) is { } mt) offen.Enqueue(mt);
                if (m is IMethodSymbol mm) foreach (var p in mm.Parameters) offen.Enqueue(p.Type);
            }
        foreach (var m in s.Klasse.GetMembers())
        {
            if (m is IFieldSymbol or IPropertySymbol && !m.IsImplicitlyDeclared && TypVon(m) is { } ft) offen.Enqueue(ft);
            if (m is IMethodSymbol { MethodKind: MethodKind.Ordinary } hm && !Slots.Any(x => SymbolEqualityComparer.Default.Equals(x.Methode, hm)))
                foreach (var t in hm.Parameters.Select(p => p.Type).Append(hm.ReturnType)) offen.Enqueue(t);
        }
        while (offen.Count > 0)
        {
            var t = offen.Dequeue();
            if (t is IArrayTypeSymbol a) { offen.Enqueue(a.ElementType); continue; }
            if (t is not INamedTypeSymbol nt) continue;
            foreach (var arg in nt.TypeArguments) offen.Enqueue(arg);
            if (nt.DelegateInvokeMethod is { } inv) foreach (var x in inv.Parameters.Select(p => p.Type).Append(inv.ReturnType)) offen.Enqueue(x);
            var asm = nt.ContainingAssembly?.Name ?? "";
            if (!_domänen.Contains(asm) && !_framework.Contains(asm)) continue;
            if (!besucht.Add(nt.OriginalDefinition.Fq())) continue;
            ids.Add(Id(nt));
            foreach (var b in nt.AllInterfaces.Cast<ITypeSymbol>().Append(nt.BaseType).OfType<ITypeSymbol>()) offen.Enqueue(b);
            foreach (var m in nt.GetMembers().Concat(nt.AllInterfaces.SelectMany(i => i.GetMembers())))
            {
                if (m.DeclaredAccessibility != Accessibility.Public) continue;
                ids.Add(Id(m));
                if (TypVon(m) is { } mt) offen.Enqueue(mt);
                if (m is IMethodSymbol mm) foreach (var p in mm.Parameters) offen.Enqueue(p.Type);
            }
        }
        return ids;
    }

    private static ITypeSymbol? TypVon(ISymbol m) => m switch
    {
        IFieldSymbol f => f.Type, IPropertySymbol p => p.Type, IMethodSymbol mm => mm.ReturnType, _ => null,
    };

    private static string Id(ISymbol s) => s is ITypeSymbol t ? t.OriginalDefinition.Fq() : s.OriginalDefinition.ToDisplayString();

    private SemanticModel Model(SyntaxTree tree) => _comps.First(c => c.ContainsSyntaxTree(tree)).GetSemanticModel(tree);

    private static IEnumerable<INamedTypeSymbol> AlleTypen(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
            foreach (var x in MitGeschachtelten(t)) yield return x;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in AlleTypen(sub)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> MitGeschachtelten(INamedTypeSymbol t)
    {
        yield return t;
        foreach (var n in t.GetTypeMembers())
            foreach (var x in MitGeschachtelten(n)) yield return x;
    }

    // ── Bericht ──

    public string Bericht()
    {
        var b = new StringBuilder();
        var arten = new[] { "decide", "apply", "projektion", "reaktion", "reader", "pipeline", "store", "saga" };
        b.AppendLine("── Slot-Inventar: Isolation der Code-Block-Stellen ──\n");
        b.AppendLine($"   {"Art",-11}{"Slots",6}{"Symb/Slot",10}{"Spielraum",11}{"+Graph",8}{"+Nachbar",10}{"+Artgen.",10}{"Rest",7}   {"nutzt F",8}{"nutzt H",8}{"nutzt X",8}");
        foreach (var art in arten)
        {
            var ss = Slots.Where(s => s.Art == art).ToList();
            if (ss.Count == 0) continue;
            int gesamt = 0, imRaum = 0, graph = 0, nachbar = 0, artgen = 0, rest = 0;
            foreach (var s in ss)
            {
                var (r, g, n, a, x) = Abdeckung(s);
                gesamt += r + g + n + a + x; imRaum += r; graph += g; nachbar += n; artgen += a; rest += x;
            }
            string P(int v) => gesamt == 0 ? "—" : $"{100.0 * v / gesamt:0}%";
            string Q(char k) => $"{ss.Count(s => Nutzungen[s].Any(u => u.Kat == k))}/{ss.Count}";
            b.AppendLine($"   {art,-11}{ss.Count,6}{(double)gesamt / ss.Count,10:0.0}{P(imRaum),11}{P(graph),8}{P(nachbar),10}{P(artgen),10}{P(rest),7}   {Q('F'),8}{Q('H'),8}{Q('X'),8}");
        }
        b.AppendLine("\n   Symb/Slot = verschiedene Domänen-/Framework-Symbole je Rumpf. Spielraum = aus Signatur/State/Feldern/Helfern erreichbar.");
        b.AppendLine("   +Graph = zusätzlich über eine Graph-Kante erreichbar (Pipeline → gesendete Commands, Store → seine ReadModels).");
        b.AppendLine("   +Nachbar = sonst von einem anderen Slot derselben Klasse benutzt; +Artgen. = von einem Slot derselben Art.");
        b.AppendLine("   Rest = weder noch (Wissen von außen). F/H/X = Rümpfe, die injizierte Felder / Helfer / andere Slots benutzen.\n");

        foreach (var art in arten)
        {
            var ss = Slots.Where(s => s.Art == art).ToList();
            if (ss.Count == 0) continue;
            b.AppendLine($"── {art} ({ss.Count}) ──");
            // Injizierte Abhängigkeiten und Helfer
            var felder = ss.SelectMany(s => Nutzungen[s].Where(u => u.Kat == 'F').Select(u => u.Anzeige).Distinct()).GroupBy(x => x).OrderByDescending(g => g.Count());
            foreach (var g in felder) b.AppendLine($"   F  {g.Key}   in {g.Count()} Rümpfen");
            var helfer = ss.SelectMany(s => Nutzungen[s].Where(u => u.Kat is 'H' or 'X').Select(u => $"{s.Klasse.Name}.{u.Anzeige}").Distinct()).GroupBy(x => x).OrderByDescending(g => g.Count());
            foreach (var g in helfer) b.AppendLine($"   H  {g.Key}   geteilt von {g.Count()} Rümpfen");
            // Framework-Idiome: R-Symbole, die in ≥ der Hälfte der Rümpfe dieser Art vorkommen
            var idiome = ss.SelectMany(s => Nutzungen[s].Where(u => u.Kat == 'R').Select(u => u.Anzeige).Distinct()).GroupBy(x => x)
                .Where(g => g.Count() * 2 >= ss.Count).OrderByDescending(g => g.Count());
            foreach (var g in idiome) b.AppendLine($"   R  {g.Key}   in {g.Count()}/{ss.Count} Rümpfen");
            // Nur über Nachbarn/Artgenossen bekannt (nicht aus Signatur oder Graph)
            var nurNachbar = ss.SelectMany(s => NachbarSymbole(s).Select(x => (s, x))).ToList();
            foreach (var g in nurNachbar.GroupBy(x => x.x).OrderByDescending(g => g.Count()).Take(8))
                b.AppendLine($"   N  {g.Key}   (nur über Nachbarn) in {g.Count()} Rümpfen");
            // Bibliotheks-API (BCL/NuGet), die ≥ 3 Rümpfe dieser Art benutzen
            var bib = ss.SelectMany(s => Nutzungen[s].Where(u => u.Kat == 'B').Select(u => u.Anzeige).Distinct()).GroupBy(x => x)
                .Where(g => g.Count() >= 3).OrderByDescending(g => g.Count()).Take(8);
            foreach (var g in bib) b.AppendLine($"   B  {g.Key}   in {g.Count()}/{ss.Count} Rümpfen");
            // Rest: Symbole, die weder Spielraum noch Nachbarn noch Artgenossen liefern
            var restListe = ss.SelectMany(s => RestSymbole(s).Select(x => (s, x))).ToList();
            foreach (var g in restListe.GroupBy(x => x.x).OrderByDescending(g => g.Count()).Take(12))
                b.AppendLine($"   ?  {g.Key}   (von außen) in {string.Join(", ", g.Select(x => x.s.Klasse.Name + "." + x.s.Disc).Distinct().Take(3))}");
            b.AppendLine();
        }
        return b.ToString();
    }

    private IEnumerable<SlotRef> NachbarnVon(SlotRef s) => Slots.Where(x => x != s && x.Klasse.Fq() == s.Klasse.Fq() && x.Art == s.Art);
    private IEnumerable<SlotRef> ArtgenossenVon(SlotRef s) => Slots.Where(x => x != s && x.Klasse.Fq() != s.Klasse.Fq() && x.Art == s.Art);

    private List<Nutzung> DR(SlotRef s) => Nutzungen[s].Where(u => u.Kat is 'D' or 'R').GroupBy(u => u.Id).Select(g => g.First()).ToList();

    public (int Raum, int Graph, int Nachbar, int Artgen, int Außen) Abdeckung(SlotRef s)
    {
        var nb = NachbarnVon(s).SelectMany(DR).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        var ag = ArtgenossenVon(s).SelectMany(DR).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        int r = 0, g = 0, n = 0, a = 0, x = 0;
        foreach (var u in DR(s))
            if (Spielraum[s].Contains(u.Id)) r++; else if (GraphRaum[s].Contains(u.Id)) g++;
            else if (nb.Contains(u.Id)) n++; else if (ag.Contains(u.Id)) a++; else x++;
        return (r, g, n, a, x);
    }

    private IEnumerable<string> NachbarSymbole(SlotRef s)
    {
        var nb = NachbarnVon(s).Concat(ArtgenossenVon(s)).SelectMany(DR).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        return DR(s).Where(u => !Spielraum[s].Contains(u.Id) && !GraphRaum[s].Contains(u.Id) && nb.Contains(u.Id)).Select(u => $"[{u.Kat}] {u.Anzeige}");
    }

    private IEnumerable<string> RestSymbole(SlotRef s)
    {
        var nb = NachbarnVon(s).Concat(ArtgenossenVon(s)).SelectMany(DR).Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        return DR(s).Where(u => !Spielraum[s].Contains(u.Id) && !GraphRaum[s].Contains(u.Id) && !nb.Contains(u.Id)).Select(u => $"[{u.Kat}] {u.Anzeige}");
    }
}
