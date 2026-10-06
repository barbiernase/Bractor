namespace DomainEditor;

/// <summary>Ein Modell-Befund: Schweregrad, Code, lesbare Meldung. Spiegelt die Graph-Diagnostik.</summary>
public sealed record Befund(string Schweregrad, string Code, string Meldung)
{
    public bool IstFehler => Schweregrad == "error";
    public override string ToString() => $"[{Schweregrad}] {Code}: {Meldung}";
}

/// <summary>
/// Strukturelle Guardrails auf dem record-zentrischen Modell — die FORM-Regeln, die auch
/// Compiler/Analyzer erzwingen, nur schon beim Modellieren. Leichtgewichtig; die volle Wahrheit
/// bleibt Compiler + Analyzer + Debugger.
/// </summary>
public static class Validator
{
    public static IReadOnlyList<Befund> Prüfe(EditorModell modell)
    {
        var befunde = new List<Befund>();

        var commandNamen = new HashSet<string>(modell.Records.Where(r => r.Kind == RecordArt.Command).Select(r => r.Name), StringComparer.Ordinal);
        var eventNamen = new HashSet<string>(modell.Records.Where(r => r.Kind is RecordArt.Event or RecordArt.Rejection).Select(r => r.Name), StringComparer.Ordinal);
        var persistentEvents = new HashSet<string>(modell.Records.Where(r => r.Kind == RecordArt.Event).Select(r => r.Name), StringComparer.Ordinal);

        // Bekannte Typen: die Wire-Skalare (aus dem Codegen, via Rahmen) + alle Records + alle Enums.
        var bekannt = new HashSet<string>(modell.Rahmen.Skalare, StringComparer.Ordinal);
        foreach (var r in modell.Records) bekannt.Add(r.Name);
        foreach (var e in modell.Enums) bekannt.Add(e.Name);

        // Doppelte Record-Namen.
        foreach (var g in modell.Records.GroupBy(r => r.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            befunde.Add(new("error", "EDIT-DUP-RECORD", $"Record '{g.Key}' ist {g.Count()}× definiert — Namen müssen eindeutig sein."));

        // Feldtypen prüfen (nur Hinweis bei unbekannt — reiche Typen sind erlaubt).
        foreach (var r in modell.Records)
        {
            // ICommand verlangt die Property (Position egal — Positions-Parameter ODER Property-Feld).
            var id = modell.Rahmen.AggregatIdFeld;
            if (r.Kind == RecordArt.Command && !r.Felder.Any(f => f.Name == id && f.Typ == "Guid"))
                befunde.Add(new("error", "EDIT-CMD-ID", $"Command '{r.Name}': Feld 'Guid {id}' fehlt (ICommand verlangt es)."));

            foreach (var f in r.Felder)
                if (!TypBekannt(f.Typ, bekannt))
                    befunde.Add(new("info", "EDIT-TYP-UNBEKANNT", $"{r.Name}.{f.Name}: Typ '{f.Typ}' ist kein Skalar/Record/Enum der Domäne — kompiliert nur, wenn der Typ existiert."));
        }

        // Leseseite (nur wenn das Modell sie trägt): was der Scaffolder nicht schreiben kann, sagen — nicht still übergehen.
        if (modell.Lesen is { } lesen)
        {
            foreach (var g in lesen.Stores.SelectMany(s => s.Fns.Select(f => (f.Name, Store: s.Name))).GroupBy(x => x.Name).Where(g => g.Count() > 1))
                befunde.Add(new("error", "EDIT-FAEHIGKEIT-DUP", $"Fähigkeit '{g.Key}' trägt {g.Count()} Funktionen ({string.Join(", ", g.Select(x => x.Store))}) — eine Fn je Fähigkeit (CQRS051)."));
            foreach (var r in lesen.Reader)
                foreach (var h in r.Handles.Where(h => h.Rueckgabe == null && h.Ausgaenge.Count == 0))
                    befunde.Add(new("warning", "EDIT-READER-OHNE-ANTWORT", $"{r.Name}.Handle({h.Eingang}) hat keine Response — ohne Ausgabe-Vertrag (CQRS050) wird er nicht geschrieben."));
            if (modell.Rahmen.ProjektionsSchreiber is null)
                foreach (var k in lesen.Konsumenten.Where(k => k.Datei == null))
                    befunde.Add(new("warning", "EDIT-KONSUMENT-SCHREIBER", $"{k.Name}: der Schreiber-Typ der Projektions-Handles ist aus dem Code nicht bekannt (keine Projektion im Code) — nicht geschrieben."));
        }

        // Eigenständige Decider/Applier (verweisen auf ihr Aggregat).
        var aggNamen = new HashSet<string>(modell.Aggregate.Select(a => a.Name), StringComparer.Ordinal);
        foreach (var d in modell.Decider)
        {
            if (!aggNamen.Contains(d.Aggregat))
                befunde.Add(new("error", "EDIT-DECIDE-AGG", $"Decider({d.Command}): '{d.Aggregat}' ist kein Aggregat."));
            if (!commandNamen.Contains(d.Command))
                befunde.Add(new("error", "EDIT-DECIDE-CMD", $"Decider: '{d.Command}' ist kein Command-Record."));
            if (d.Ergibt.Count is 0)
                befunde.Add(new("error", "EDIT-ONEOF-LEER", $"Decider({d.Command}) hat keinen Ausgang — mindestens ein Event nötig."));
            else if (modell.Rahmen.OneOfMax > 0 && d.Ergibt.Count > modell.Rahmen.OneOfMax)
                befunde.Add(new("error", "EDIT-ONEOF-MAX", $"Decider({d.Command}) hat {d.Ergibt.Count} Ausgänge — OneOf gibt es im Vertrag nur bis {modell.Rahmen.OneOfMax}."));
            foreach (var a in d.Ergibt)
                if (!eventNamen.Contains(a.Event))
                    befunde.Add(new("error", "EDIT-EVENT-FEHLT", $"Decider({d.Command}) → '{a.Event}' ist kein Event/Ablehnungs-Record."));
        }
        foreach (var a in modell.Applier)
        {
            if (!aggNamen.Contains(a.Aggregat))
                befunde.Add(new("error", "EDIT-APPLY-AGG", $"Applier({a.Event}): '{a.Aggregat}' ist kein Aggregat."));
            if (!persistentEvents.Contains(a.Event))
                befunde.Add(new("error", "EDIT-APPLY-EVENT", $"Applier: '{a.Event}' ist kein persistenter Event-Record."));
        }

        // Sagas.
        foreach (var saga in modell.Sagas)
        {
            if (!eventNamen.Contains(saga.TriggerEvent))
                befunde.Add(new("warning", "EDIT-SAGA-TRIGGER", $"Saga {saga.Name}: Trigger '{saga.TriggerEvent}' ist kein bekanntes Event."));
            foreach (var s in saga.Schritte)
            {
                if (s.Wenn.Count is 0)
                    befunde.Add(new("error", "EDIT-SAGA-WENN", $"Saga {saga.Name}: eine Transition ohne Auf/Wenn-Event."));
                else if (modell.Rahmen.UndMax > 0 && s.Wenn.Count > modell.Rahmen.UndMax)
                    befunde.Add(new("error", "EDIT-SAGA-JOIN-MAX", $"Saga {saga.Name}: Join über {s.Wenn.Count} Events — die DSL trägt höchstens {modell.Rahmen.UndMax}."));
                if (!commandNamen.Contains(s.Sende))
                    befunde.Add(new("error", "EDIT-UNROUTED-SAGA-CMD", $"Saga {saga.Name} sendet '{s.Sende}', der kein Command-Record ist (Runtime-Hang)."));
                if (s.Kompensation is not null && !commandNamen.Contains(s.Kompensation))
                    befunde.Add(new("error", "EDIT-UNROUTED-SAGA-CMD", $"Saga {saga.Name} kompensiert mit '{s.Kompensation}', der kein Command-Record ist."));
            }
        }

        befunde.AddRange(PruefeGrammatik(modell));
        return befunde;
    }

    /// <summary>
    /// Die GRAMMATIK (<see cref="Grammatik"/>) auf dem Nachrichtenfluss: Sorte × Eingang, Kardinalität, Erzeugung und Zusatzregeln.
    /// Jeder Befund trägt die Regel-Id als Code und nennt die Regel beim Namen (samt Build-Gegenstück).
    /// </summary>
    public static IReadOnlyList<Befund> PruefeGrammatik(EditorModell modell) => PruefeGrammatik(modell, Fluss.Aus(modell));

    public static IReadOnlyList<Befund> PruefeGrammatik(EditorModell modell, Fluss fluss)
    {
        var befunde = new List<Befund>();
        void Melde(string regel, string meldung, string? schwere = null) =>
            befunde.Add(new(schwere ?? Grammatik.RegelVon(regel).Schwere, regel, $"{meldung} — {Grammatik.Beschreibe(regel)}"));
        string Art(string id) => fluss.KnotenVon(id)?.Art ?? "?";
        string Name(string id) => fluss.KnotenVon(id)?.Name ?? id;
        string Wer(string id) => $"{Grammatik.BausteinName(Art(id))} {Name(id)}";
        var recs = modell.Records.GroupBy(r => r.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // (1) Sorte × Eingang + Kardinalität je Nachricht.
        foreach (var g in fluss.Kanten.Where(k => Art(k.Nach) != "nachricht").GroupBy(k => (k.Nachricht, k.Sorte)))
        {
            var konsumenten = g.Select(k => k.Nach).Distinct().ToList();
            foreach (var kid in konsumenten)
                if (Grammatik.KonsumVon(g.Key.Sorte, Art(kid)) == null)
                    Melde(RegelFuerSorte(g.Key.Sorte), $"{Grammatik.SorteName(g.Key.Sorte)} '{g.Key.Nachricht}' läuft in {Wer(kid)} — dieser Eingang nimmt die Sorte nicht an", "error");
            foreach (var kg in konsumenten.GroupBy(Art))
            {
                var konsum = Grammatik.KonsumVon(g.Key.Sorte, kg.Key);
                if (konsum?.Kardinalitaet == Grammatik.GenauEins && kg.Count() > 1)
                    Melde(konsum.Regel, $"{Grammatik.SorteName(g.Key.Sorte)} '{g.Key.Nachricht}' hat {kg.Count()} Konsumenten: {string.Join(", ", kg.Select(Wer))}");
            }
            // Selbst: nur in der Pipeline, die sie plant.
            if (g.Key.Sorte == Grammatik.Selbst)
            {
                var planer = fluss.Kanten.Where(k => k.Nach == "msg:" + g.Key.Nachricht).Select(k => k.Von).ToHashSet();
                foreach (var kid in konsumenten.Where(k => planer.Count > 0 && !planer.Contains(k)))
                    Melde("GR-SELBST", $"Selbst '{g.Key.Nachricht}' kommt in {Wer(kid)} an, geplant wird sie von {string.Join(", ", planer.Select(Wer))}");
            }
        }

        // (2) Erzeugung: Baustein-Ausgang → Sorte.
        foreach (var k in fluss.Kanten.Where(k => Art(k.Von) != "nachricht" && Art(k.Nach) == "nachricht" && k.Sorte != Grammatik.Faehigkeit))
            if (Grammatik.ErzeugungVon(Art(k.Von), k.Sorte) == null)
            {
                var regel = Art(k.Von) == Grammatik.Pipeline && k.Sorte == Grammatik.Event ? "GR-KEIN-EVENT-AUS-PIPELINE" : RegelFuerBaustein(Art(k.Von));
                Melde(regel, $"{Wer(k.Von)} erzeugt {Grammatik.SorteName(k.Sorte)} '{k.Nachricht}'" + (k.Handle != null ? $" (Handle {k.Handle})" : ""), "error");
            }

        // (3) Zusatzregeln an den Pipeline-Handles: Selbst ohne Event-Eingang, Start höchstens einmal, Garantie; Frist-Command-Ctor.
        foreach (var p in modell.Lesen?.Pipelines ?? [])
        {
            var starts = p.Handles.Count(h => h.Eingang == nameof(Abstractions.PipelineGestartet));
            if (starts > 1) Melde("GR-START-EINMAL", $"Pipeline {p.Name} hat {starts} Start-Handles");
            foreach (var h in p.Handles)
            {
                var eingang = recs.TryGetValue(h.Eingang, out var er) ? Grammatik.SorteVonRecordArt(er.Kind) : null;
                var ausgaenge = h.Ausgaenge.Select(Fluss.Generisch).ToList();
                if (eingang is Grammatik.Event or Grammatik.Transient && ausgaenge.Any(a => a.Huelle == typeof(Abstractions.Selbst<>).Name.Split('`')[0]))
                    Melde("GR-SELBST-OHNE-EVENT", $"{p.Name}.Handle({h.Eingang}) plant Selbst, hat aber einen Event-Eingang (keine Mailbox)");
                var verlierbar = eingang is null or Grammatik.Trigger or Grammatik.Selbst or Grammatik.Transient;   // null: Start/Selbst ohne Record
                if (verlierbar)
                    foreach (var c in h.Ausgaenge.Where(a => recs.TryGetValue(a, out var r) && r.Kind == RecordArt.Command))
                        Melde("GR-GARANTIE", $"{p.Name}.Handle({h.Eingang}) sendet {c} ab einem verlierbaren Eingang — nicht idempotent, ein Re-Trigger kann doppelt wirken");
                foreach (var (huelle, arg) in ausgaenge.Where(a => a.Huelle == typeof(Abstractions.Frist<>).Name.Split('`')[0]))
                    if (arg != null && recs.TryGetValue(arg, out var fc) && !HatGuidKtor(fc))
                        Melde("GR-FRIST-CTOR", $"{p.Name}.Handle({h.Eingang}) plant Frist<{arg}>, aber {arg} hat keinen Konstruktor (Guid)");
            }
        }
        foreach (var k in modell.Lesen?.Konsumenten ?? [])
            foreach (var h in k.Handles.Where(h => recs.TryGetValue(h.Eingang, out var r) && r.Kind == RecordArt.Rejection))
                foreach (var c in h.Ausgaenge.Where(a => recs.TryGetValue(a, out var r) && r.Kind == RecordArt.Command))
                    Melde("GR-GARANTIE", $"{k.Name}.Handle({h.Eingang}) sendet {c} ab einem transienten Event (verlierbar)");

        // (4) Zyklus ohne Zustandsschritt: Kreise im Fluss ohne Aggregat (Selbst-Schleifen sind der Schleifen-Operator).
        foreach (var zyklus in ZyklenOhneZustand(fluss))
            Melde("GR-ZYKLUS", $"Kreis ohne Aggregat: {string.Join(" → ", zyklus.Select(Name))}");

        // (5) Regel Z: Zustand in zustandslosen Übersetzern (Hinweis).
        foreach (var k in modell.Lesen?.Konsumenten ?? [])
            if (k.Zustand is { Count: > 0 } z) Melde("GR-ZUSTAND", $"{k.Name} hält Zustand in {string.Join(", ", z)}");
        foreach (var p in modell.Lesen?.Pipelines ?? [])
            if (p.Zustand is { Count: > 0 } z) Melde("GR-ZUSTAND", $"Pipeline {p.Name} hält Zustand in {string.Join(", ", z)} — im Entwurf wird das Gedächtnis ein Aggregat");

        // (6) Offene Modul-Ports (Entwurf von oben nach unten): je Nachricht einmal, am innersten Modul benannt.
        foreach (var n in fluss.Knoten.Where(n => n.Art == "nachricht"))
        {
            if (Module.BrauchtKonsument.Contains(n.Sorte!) && !fluss.Aus(n.Id).Any())
                Melde("GR-MODUL-EINGANG-OFFEN", $"Modul {n.Namespace}: Eingang {Grammatik.SorteName(n.Sorte!)} '{n.Name}' ohne Konsument");
            else if (Module.BrauchtErzeuger.Contains(n.Sorte!) && !fluss.Ein(n.Id).Any())
                Melde("GR-MODUL-AUSGANG-OFFEN", $"Modul {n.Namespace}: Ausgang {Grammatik.SorteName(n.Sorte!)} '{n.Name}' ohne Erzeuger");
        }

        // (6b) Im Auftrag eines Akteurs über seinen Dienst (CQRS060): höchstens einer je Pipeline-Handle, nur Commands, die er darf.
        var dienstVon = modell.Akteure.SelectMany(a => a.Dienste.Select(d => (d, a))).GroupBy(x => x.d, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().a, StringComparer.Ordinal);
        foreach (var p in modell.Lesen?.Pipelines ?? [])
            foreach (var h in p.Handles)
            {
                var auftrag = h.Faehigkeiten.Select(f => Fluss.Basisname(f.Typ)).Where(dienstVon.ContainsKey).Select(d => dienstVon[d]).Distinct().ToList();
                if (auftrag.Count > 1) Melde("GR-AUFTRAG", $"{p.Name}.Handle({h.Eingang}) entscheidet im Auftrag mehrerer Akteure: {string.Join(", ", auftrag.Select(a => a.Name))}");
                else if (auftrag.Count == 1)
                    foreach (var c in h.Ausgaenge.Where(a => recs.TryGetValue(a, out var r) && r.Kind == RecordArt.Command && !auftrag[0].Darf.Contains(a)))
                        Melde("GR-AUFTRAG", $"{p.Name}.Handle({h.Eingang}) sendet {c} im Auftrag von {auftrag[0].Name}, aber {auftrag[0].Name} darf das nicht");
            }

        // (6c) Akteur-Vertrag (CQRS061/062): je Event höchstens eine Reaktion, Eingang ist ein Event, Ausgänge sind Commands; und keine
        //      zweite Verkörperung derselben Entscheidung über einen Dienst desselben Akteurs (CQRS060).
        foreach (var a in modell.Akteure.Where(a => a.Vertrag.Count > 0))
        {
            foreach (var g in a.Vertrag.GroupBy(r => r.Eingang, StringComparer.Ordinal).Where(g => g.Count() > 1))
                Melde("GR-VERTRAG", $"{a.VertragTyp}: zwei Reaktionen auf {g.Key} — je Event höchstens ein Auf");
            foreach (var r in a.Vertrag)
            {
                if (!recs.TryGetValue(r.Eingang, out var ein) || ein.Kind is not (RecordArt.Event or RecordArt.Rejection))
                    Melde("GR-VERTRAG", $"{a.VertragTyp}.Auf({r.Eingang}) — reagieren kann man nur auf ein Event");
                foreach (var aus in r.Ausgaenge.Where(x => !recs.TryGetValue(x, out var c) || c.Kind != RecordArt.Command))
                    Melde("GR-VERTRAG", $"{a.VertragTyp}.Auf({r.Eingang}) gibt {aus} aus — eine Reaktion gibt nur Commands hinein");
            }
            foreach (var p in modell.Lesen?.Pipelines ?? [])
                foreach (var h in p.Handles.Where(h => h.Faehigkeiten.Any(f => a.Dienste.Contains(Fluss.Basisname(f.Typ)))))
                    foreach (var c in h.Ausgaenge.Where(c => a.Vertrag.Any(r => r.Eingang == h.Eingang && r.Ausgaenge.Contains(c))))
                        Melde("GR-VERKOERPERUNG", $"{a.Name} entscheidet {c} auf {h.Eingang} zweimal: in {p.Name} (Dienst) und im Vertrag {a.VertragTyp}");
        }

        // (7) Herkunft (docs/konzept-akteure.md §8.3) — erst, wenn das Modell Akteure hat (wie am Tor: ohne Akteure ist der Pfad offen):
        //     jeder Command, jede Query und jeder Trigger kommt von einem Akteur — direkt (IDarf) oder über die Kette.
        if (modell.Akteure.Count > 0)
        {
            var anteile = AkteurAnteile.Aus(modell, fluss);
            foreach (var (nachricht, sorte) in anteile.Eingaenge.Where(e => anteile.AkteureVon(e.Nachricht).Count == 0))
                Melde("GR-HERKUNFT", $"{Grammatik.SorteName(sorte)} '{nachricht}' kommt von keinem Akteur — weder IDarf noch über eine Kette");
            foreach (var (nachricht, kandidaten) in anteile.MehrdeutigeIngresse)
                Melde("GR-INGRESS-EINDEUTIG", $"Trigger '{nachricht}' entsteht ohne Kette, aber mehrere Akteure dürfen ihn ({string.Join(", ", kandidaten)}) — wer liefert ihn?");
        }
        return befunde;
    }

    private static string RegelFuerSorte(string sorte) =>
        Grammatik.Konsume.FirstOrDefault(k => k.Sorte == sorte)?.Regel ?? "GR-AUSGANG-GESCHLOSSEN";
    private static string RegelFuerBaustein(string baustein) =>
        Grammatik.Erzeugungen.FirstOrDefault(e => e.Baustein == baustein)?.Regel ?? "GR-AUSGANG-GESCHLOSSEN";

    /// <summary>Konstruktor (Guid): genau ein Positions-Parameter vom Typ Guid (Property-Felder zählen nicht).</summary>
    private static bool HatGuidKtor(Record r) =>
        !r.OhneParameterliste && r.Felder.Where(f => f.Zugriff == null).Select(f => f.Typ).SequenceEqual(["Guid"]);

    /// <summary>
    /// Kreise im Fluss, die durch KEIN Aggregat laufen (Tarjan über den Fluss ohne Aggregat-Knoten und ohne Selbst-Kanten).
    /// Rückgabe: je starker Zusammenhangskomponente mit Kreis die Bausteine darin.
    /// </summary>
    internal static List<List<string>> ZyklenOhneZustand(Fluss fluss)
    {
        var kanten = fluss.Kanten.Where(k => k.Sorte != Grammatik.Selbst
                && fluss.KnotenVon(k.Von)?.Art != Grammatik.Aggregat && fluss.KnotenVon(k.Nach)?.Art != Grammatik.Aggregat)
            .GroupBy(k => k.Von).ToDictionary(g => g.Key, g => g.Select(k => k.Nach).Distinct().ToList(), StringComparer.Ordinal);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var stapel = new Stack<string>();
        var aufStapel = new HashSet<string>(StringComparer.Ordinal);
        var ergebnis = new List<List<string>>();
        var i = 0;
        void Besuche(string v)
        {
            index[v] = low[v] = i++;
            stapel.Push(v);
            aufStapel.Add(v);
            foreach (var w in kanten.GetValueOrDefault(v) ?? [])
            {
                if (!index.ContainsKey(w)) { Besuche(w); low[v] = Math.Min(low[v], low[w]); }
                else if (aufStapel.Contains(w)) low[v] = Math.Min(low[v], index[w]);
            }
            if (low[v] != index[v]) return;
            var komp = new List<string>();
            string x;
            do { x = stapel.Pop(); aufStapel.Remove(x); komp.Add(x); } while (x != v);
            var selbstschleife = komp.Count == 1 && (kanten.GetValueOrDefault(v)?.Contains(v) ?? false);
            if (komp.Count > 1 || selbstschleife)
                ergebnis.Add(komp.Where(id => fluss.KnotenVon(id)?.Art != "nachricht").OrderBy(id => id, StringComparer.Ordinal).ToList());
        }
        foreach (var v in fluss.Knoten.Select(k => k.Id).OrderBy(x => x, StringComparer.Ordinal))
            if (!index.ContainsKey(v)) Besuche(v);
        return ergebnis;
    }

    /// <summary>Basistyp bekannt? Streift <c>?</c> und Collection-Wrapper (List&lt;X&gt; …) ab.</summary>
    private static bool TypBekannt(string typ, HashSet<string> bekannt)
    {
        var t = typ.Trim().TrimEnd('?').Trim();
        var lt = t.IndexOf('<');
        if (lt >= 0 && t.EndsWith(">"))
        {
            var inner = t[(lt + 1)..^1];
            // Nur den (letzten) Typ-Parameter prüfen — reicht für List<X>/IReadOnlyList<X>.
            var arg = inner.Split(',').Last().Trim();
            return TypBekannt(arg, bekannt);
        }
        return bekannt.Contains(t);
    }
}
