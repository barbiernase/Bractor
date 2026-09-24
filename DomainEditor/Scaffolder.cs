using System.Text;
using Abstractions;

namespace DomainEditor;

/// <summary>Welche Rolle eine generierte Datei hat — bestimmt, wie sie in eine bestehende Datei gemischt wird.</summary>
public enum DateiArt { Typen, State, Decider, Applier, Saga }

/// <summary>
/// Eine generierte Quelldatei: Pfad (relativ zur Solution bzw. zum Zielordner) + Inhalt + Rolle. <paramref name="Platzierbar"/>
/// = false: der Code kennt für den Namespace kein Verzeichnis — der Pfad ist nur ein Platzhalter, geschrieben wird nicht.
/// </summary>
public sealed record GenerierteDatei(string Pfad, string Inhalt, DateiArt Art = DateiArt.Typen, bool Platzierbar = true);

/// <summary>
/// Der DETERMINISTISCHE Scaffold-Generator (record-zentrisch). Aus den getaggten Records + Enums + der Aggregat-
/// Komposition erzeugt er C#. WO etwas hinkommt, entscheidet der Code, nicht der Scaffolder: jeder Typ geht in seine
/// echte Datei (<c>Datei</c>); ein neuer Typ in die Datei gleichartiger Typen seines Namespace; ein neuer Namespace in
/// das Verzeichnis/Dateimuster, das der übrige Code benutzt (<see cref="Rahmen"/>). Marker- und DSL-Namen kommen über
/// <c>nameof</c>/<c>typeof</c> aus dem Vertrag.
///
/// Erzeugt bewusst NUR die handgeschriebenen Dateien — Id/Version, State-Property, Handler und Factory liefern die
/// bestehenden Roslyn-Generatoren. Decide/Apply-Körper ohne Rumpf werden ein kompilierbarer <c>throw</c>-Platzhalter.
/// </summary>
public static class Scaffolder
{
    // ── Der Vertrag, über nameof/typeof (nie als String) ──
    private static readonly string ICommand = nameof(Abstractions.ICommand);
    private static readonly string ICreationCommand = nameof(Abstractions.ICreationCommand);
    private static readonly string IEvent = nameof(Abstractions.IEvent);
    private static readonly string ITransientEvent = nameof(Abstractions.ITransientEvent);
    private static readonly string IState = nameof(Abstractions.IState);
    private static readonly string IDecider = typeof(IDecider<>).Name.Split('`')[0];
    private static readonly string IApplier = typeof(IApplier<>).Name.Split('`')[0];
    private static readonly string IProzessDefinition = nameof(Abstractions.IProzessDefinition);
    private static readonly string ProzessRegeln = nameof(Abstractions.ProzessRegeln);
    private static readonly string Regeln = nameof(Abstractions.IProzessDefinition.Regeln);
    private static readonly string Prozess = typeof(Prozess<>).Name.Split('`')[0];
    private static readonly string Definiere = nameof(Prozess<IEvent>.Definiere);
    private static readonly string Auf = nameof(RegelBauer.Auf);
    private static readonly string Und = nameof(RegelBauer<IEvent>.Und);
    private static readonly string UndAlle = nameof(RegelBauer<IEvent>.UndAlle);
    private static readonly string Sende = nameof(RegelBauer<IEvent>.Sende);
    private static readonly string SendeJe = nameof(RegelBauer<IEvent>.SendeJe);
    private static readonly string RückgängigDurch = nameof(RegelAbschluss<IEvent>.RückgängigDurch);
    private static readonly string RückgängigDurchJe = nameof(RegelAbschluss<IEvent>.RückgängigDurchJe);
    private static readonly string OneOf = typeof(OneOf<>).Name.Split('`')[0];

    public static IReadOnlyList<GenerierteDatei> Generiere(EditorModell modell)
    {
        var dateien = new List<GenerierteDatei>();
        var rahmen = modell.Rahmen;

        // ── Records + Enums → Zieldatei (echte Datei; sonst Datei gleichartiger Typen im Namespace; sonst Muster) ──
        // Gruppe = (Datei, Namespace): eine Datei darf mehrere Namespaces tragen — jeder Namespace bleibt seiner.
        var gruppen = new Dictionary<(string Pfad, string Ns), (string Ns, List<Record> Records, List<Enumeration> Enums)>();
        (string Ns, List<Record> Records, List<Enumeration> Enums) Gruppe(string pfad, string ns)
        {
            if (!gruppen.TryGetValue((pfad, ns), out var g)) gruppen[(pfad, ns)] = g = (ns, new(), new());
            return g;
        }
        foreach (var rec in modell.Records.Where(x => RecordArt.Alle.Contains(x.Kind)))
            Gruppe(rec.Datei ?? TypZiel(modell, rec.Namespace, Sorte(rec.Kind), rec.Name), rec.Namespace).Records.Add(rec);
        foreach (var e in modell.Enums)
            Gruppe(e.Datei ?? TypZiel(modell, e.Namespace, "enum", e.Name), e.Namespace).Enums.Add(e);
        foreach (var ((pfad, _), g) in gruppen)
            dateien.Add(new(pfad, TypDatei(g.Ns, g.Records, g.Enums, modell), DateiArt.Typen, !pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Aggregate → State; Decider/Applier je Aggregat aus den eigenständigen Regeln ──
        foreach (var agg in modell.Aggregate)
        {
            var decider = modell.Decider.Where(d => d.Aggregat == agg.Name).ToList();
            var applier = modell.Applier.Where(a => a.Aggregat == agg.Name).ToList();
            // Neue Aggregat-Dateien: feste Regel {Aggregat}.cs / {Aggregat}.{Decider}.cs / {Aggregat}.{Applier}.cs — keine Statistik.
            var v = Verzeichnis(modell, agg.Namespace);
            dateien.Add(Platziert(agg.Datei, v, $"{agg.Name}.cs", StateDatei(agg, modell), DateiArt.State));
            dateien.Add(Platziert(agg.DeciderDatei, v, $"{agg.Name}.{rahmen.DeciderKlasse}.cs", DeciderDatei(agg, decider, modell), DateiArt.Decider));
            dateien.Add(Platziert(agg.ApplierDatei, v, $"{agg.Name}.{rahmen.ApplierKlasse}.cs", ApplierDatei(agg, applier, modell), DateiArt.Applier));
        }

        foreach (var saga in modell.Sagas)
            dateien.Add(Platziert(saga.Datei, Verzeichnis(modell, saga.Namespace), $"{saga.Name}.cs", SagaDatei(saga, modell), DateiArt.Saga));

        return dateien.OrderBy(d => d.Pfad, StringComparer.Ordinal).ToList();
    }

    // ── Wohin? — aus dem Code abgeleitet ──────────────────────────────────────────────────────
    private static string Sorte(string kind) => kind == RecordArt.Rejection ? RecordArt.Event : kind;

    /// <summary>Pfad-Präfix für Typen, deren Namespace der Code keinem Verzeichnis zuordnet (nie geschrieben).</summary>
    private const string Unplatziert = "?/";

    private static GenerierteDatei Platziert(string? echt, string? verzeichnis, string name, string inhalt, DateiArt art) =>
        echt != null ? new(echt, inhalt, art)
        : verzeichnis != null ? new($"{verzeichnis}/{name}", inhalt, art)
        : new($"{Unplatziert}{name}", inhalt, art, Platzierbar: false);

    /// <summary>
    /// Zieldatei eines NEUEN Typs (ohne eigene Datei) — feste Regel, keine Mehrheits-Statistik: stehen ALLE gleichartigen
    /// Typen seines Namespace in genau EINER Datei, dorthin (das ist ein Fakt des Codes); sonst die kanonische Datei der
    /// Sorte (<c>Commands.cs</c> …) bzw. bei schon verteilten Typen eine eigene. Kein Verzeichnis bekannt: unplatziert.
    /// </summary>
    private static string TypZiel(EditorModell m, string ns, string sorte, string typName)
    {
        IEnumerable<(string Ns, string? Datei)> gleichartig = sorte == "enum"
            ? m.Enums.Select(e => (e.Namespace, e.Datei))
            : m.Records.Where(r => Sorte(r.Kind) == sorte).Select(r => (r.Namespace, r.Datei));
        var dateienImNs = gleichartig.Where(x => x.Ns == ns && x.Datei != null).Select(x => x.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateienImNs.Count == 1) return dateienImNs[0];
        // Sonst die kanonische Form des Scaffolders (feste Ausgabe-Regel für NEUEN Code, keine Schätzung):
        //   je Sorte eine Datei; verteilt der Code die Sorte schon auf mehrere Dateien, bekommt der neue Typ eine eigene.
        var name = dateienImNs.Count > 1 ? $"{typName}.cs" : sorte switch
        {
            RecordArt.Command => "Commands.cs",
            RecordArt.Event => "Events.cs",
            RecordArt.ValueObject => "ValueObjects.cs",
            _ => "Enums.cs",
        };
        var v = Verzeichnis(m, ns);
        return v != null ? $"{v}/{name}" : $"{Unplatziert}{name}";
    }

    /// <summary>
    /// Verzeichnis eines Namespace (relativ zur Solution): bekannt aus dem Code (alle Typen des Namespace in genau einem
    /// Verzeichnis); sonst über den längsten Namespace, dessen Verzeichnis bekannt ist (auch die Projekt-Wurzel-Namespaces
    /// aus den csproj) plus die Rest-Segmente als Unterordner — die Namespace↔Ordner-Abbildung von MSBuild.
    /// Kein bekannter Präfix: null (nicht platzierbar).
    /// </summary>
    private static string? Verzeichnis(EditorModell m, string ns)
    {
        var v = m.Rahmen.Verzeichnisse;
        if (v.TryGetValue(ns, out var d)) return d;
        var teile = ns.Split('.');
        for (var n = teile.Length - 1; n > 0; n--)
        {
            var präfix = string.Join('.', teile[..n]);
            if (v.TryGetValue(präfix, out var basis)) return basis + "/" + string.Join('/', teile[n..]);
        }
        return null;
    }

    // ── Typ-Dateien aus Records/Enums ─────────────────────────────────────────────────────────
    private static string TypDatei(string ns, List<Record> records, List<Enumeration> enums, EditorModell modell)
    {
        var basis = records.Any(r => r.Kind != RecordArt.ValueObject) ? new[] { modell.Rahmen.VertragsNamespace } : Array.Empty<string>();
        var b = Kopf(ns, Usings(ns, modell, basis, records.SelectMany(RecordTypen), records.SelectMany(r => r.Usings)));
        var abschnitte = new List<Action>();

        if (enums.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < enums.Count; i++)
            {
                Doku(b, enums[i].Doku, "");
                b.AppendLine($"public enum {enums[i].Name} {{ {string.Join(", ", enums[i].Werte)} }}");
                if (i < enums.Count - 1) b.AppendLine();
            }
        });
        var commands = records.Where(r => r.Kind == RecordArt.Command).ToList();
        if (commands.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < commands.Count; i++)
                RecordZeilen(b, commands[i], commands[i].IstErzeugung ? $"{ICommand}, {ICreationCommand}" : ICommand, i < commands.Count - 1);
        });
        var persistent = records.Where(r => r.Kind == RecordArt.Event).ToList();
        var ablehnungen = records.Where(r => r.Kind == RecordArt.Rejection).ToList();
        if (persistent.Count + ablehnungen.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < persistent.Count; i++)
                RecordZeilen(b, persistent[i], IEvent, i < persistent.Count - 1);
            if (ablehnungen.Count > 0)
            {
                if (persistent.Count > 0) b.AppendLine();
                for (var i = 0; i < ablehnungen.Count; i++)
                    RecordZeilen(b, ablehnungen[i], ITransientEvent, i < ablehnungen.Count - 1);
            }
        });
        var vos = records.Where(r => r.Kind == RecordArt.ValueObject).ToList();
        if (vos.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < vos.Count; i++)
                RecordZeilen(b, vos[i], null, i < vos.Count - 1);
        });

        for (var i = 0; i < abschnitte.Count; i++)
        {
            if (i > 0) b.AppendLine();
            abschnitte[i]();
        }
        return b.ToString();
    }

    /// <summary>
    /// Ein Record in SEINER deklarierten Form: Doku, Attribute, <c>{Typart} X(Positions-Felder) : Marker, Basen</c>, im Rumpf
    /// die Property-Felder (mit ihrem Accessor-Satz) und der Handcode (Zusatz). Ohne Rumpf-Inhalt: <c>…;</c>.
    /// </summary>
    private static void RecordZeilen(StringBuilder b, Record r, string? marker, bool leerzeileDanach)
    {
        Doku(b, r.Doku, "");
        if (!string.IsNullOrWhiteSpace(r.Attribute))
            foreach (var zeile in r.Attribute.Replace("\r\n", "\n").Split('\n')) b.AppendLine(zeile);
        var positionell = r.Felder.Where(f => f.Zugriff is null).ToList();
        var eigenschaften = r.Felder.Where(f => f.Zugriff is not null).ToList();
        var basen = new[] { marker }.Concat(r.Basen ?? []).Where(x => !string.IsNullOrEmpty(x)).ToList();
        var kopf = $"{r.Typart ?? "public record"} {r.Name}"
                   + (r.OhneParameterliste && positionell.Count == 0 ? "" : $"({Parameter(positionell)})")
                   + (basen.Count == 0 ? "" : $" : {string.Join(", ", basen)}");
        if (eigenschaften.Count == 0 && string.IsNullOrWhiteSpace(r.Zusatz))
            b.AppendLine(kopf + (r.OhneParameterliste && positionell.Count == 0 ? " { }" : ";"));
        else
        {
            b.AppendLine(kopf);
            b.AppendLine("{");
            foreach (var f in eigenschaften)
                b.AppendLine($"    public {(f.Pflicht ? "required " : "")}{f.Typ} {f.Name} {f.Zugriff}" + (f.Standard is null ? "" : $" = {f.Standard};"));
            if (!string.IsNullOrWhiteSpace(r.Zusatz))
            {
                if (eigenschaften.Count > 0) b.AppendLine();
                Eingerückt(b, r.Zusatz, "    ");
            }
            b.AppendLine("}");
        }
        if (leerzeileDanach && (r.Zusatz is not null || r.Doku is not null || eigenschaften.Count > 0)) b.AppendLine();
    }

    private static IEnumerable<string> RecordTypen(Record r) => r.Felder.Select(f => f.Typ);

    // ── Aggregat-Komposition ──────────────────────────────────────────────────────────────────
    private static IEnumerable<string> AggregatTypen(Aggregat agg, IReadOnlyList<DecideRegel> decider, IReadOnlyList<ApplyRegel> applier) =>
        agg.State.Select(f => f.Typ)
            .Concat(decider.Select(d => d.Command)).Concat(decider.SelectMany(d => d.Ergibt.Select(a => a.Event)))
            .Concat(applier.Select(a => a.Event));

    private static string StateDatei(Aggregat agg, EditorModell modell)
    {
        var b = Kopf(agg.Namespace, Usings(agg.Namespace, modell, [modell.Rahmen.VertragsNamespace], agg.State.Select(f => f.Typ), agg.Usings));
        Doku(b, agg.Doku, "");
        b.AppendLine($"public partial class {agg.Name} : {IState}");
        b.AppendLine("{");

        // In Deklarations-Reihenfolge (gespeichert und abgeleitet gemischt) — so bleibt der Round-trip ein Fixpunkt.
        for (var i = 0; i < agg.State.Count; i++)
        {
            var f = agg.State[i];
            // Kanonische Form: eine Leerzeile, wo gespeicherte Felder in abgeleitete übergehen.
            if (i > 0 && f.Ausdruck is not null && agg.State[i - 1].Ausdruck is null) b.AppendLine();
            b.AppendLine(f.Ausdruck is not null
                ? $"    public {f.Typ} {f.Name} => {f.Ausdruck};"
                : $"    public {f.Typ} {f.Name} {{ get;{(f.NurGet ? "" : " set;")} }}" + (f.Standard is null ? "" : $" = {f.Standard};"));
        }

        if (!string.IsNullOrWhiteSpace(agg.StateZusatz))
        {
            if (agg.State.Count > 0) b.AppendLine();
            Eingerückt(b, agg.StateZusatz, "    ");
        }

        b.AppendLine("}");
        return b.ToString();
    }

    private static string DeciderDatei(Aggregat agg, IReadOnlyList<DecideRegel> decider, EditorModell modell)
    {
        var r = modell.Rahmen;
        var b = Kopf(agg.Namespace, Usings(agg.Namespace, modell, [r.VertragsNamespace], AggregatTypen(agg, decider, []), agg.Usings));
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class {r.DeciderKlasse} : {IDecider}<{agg.Name}>");
        b.AppendLine("    {");

        for (var i = 0; i < decider.Count; i++)
        {
            var d = decider[i];
            var oneOf = string.Join(", ", d.Ergibt.Select(a => a.Event));
            b.AppendLine($"        public IEnumerable<{OneOf}<{oneOf}>> {r.DecideMethode}({d.Command} {d.Parameter})");
            b.AppendLine("        {");
            Rumpf(b, d.Rumpf, "TODO: Entscheidungslogik.");
            b.AppendLine("        }");
            if (i < decider.Count - 1) b.AppendLine();
        }
        Zusatz(b, agg.DeciderZusatz);

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    private static string ApplierDatei(Aggregat agg, IReadOnlyList<ApplyRegel> applier, EditorModell modell)
    {
        var r = modell.Rahmen;
        var b = Kopf(agg.Namespace, Usings(agg.Namespace, modell, [r.VertragsNamespace], AggregatTypen(agg, [], applier), agg.Usings));
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class {r.ApplierKlasse} : {IApplier}<{agg.Name}>");
        b.AppendLine("    {");

        for (var i = 0; i < applier.Count; i++)
        {
            var a = applier[i];
            b.AppendLine($"        public void {r.ApplyMethode}({a.Event} {a.Parameter})");
            b.AppendLine("        {");
            Rumpf(b, a.Rumpf, "TODO: Apply-Logik.");
            b.AppendLine("        }");
            if (i < applier.Count - 1) b.AppendLine();
        }
        Zusatz(b, agg.ApplierZusatz);

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    // ── Saga / Prozess ────────────────────────────────────────────────────────────────────────
    private static string SagaDatei(Saga saga, EditorModell modell)
    {
        var referenzen = new List<string> { saga.TriggerEvent };
        foreach (var st in saga.Schritte)
        {
            referenzen.AddRange(st.Wenn);
            referenzen.Add(st.Sende);
            if (st.SammelEvent is not null) referenzen.Add(st.SammelEvent);
            if (st.Kompensation is not null) referenzen.Add(st.Kompensation);
        }
        var b = Kopf(saga.Namespace, Usings(saga.Namespace, modell, [modell.Rahmen.VertragsNamespace], referenzen, saga.ExtraUsings));
        Doku(b, saga.Doku, "");
        b.AppendLine($"public sealed class {saga.Name} : {IProzessDefinition}");
        b.AppendLine("{");
        b.AppendLine($"    public {ProzessRegeln} {Regeln} => {Prozess}<{saga.TriggerEvent}>.{Definiere}(p =>");
        b.AppendLine("    {");

        for (var i = 0; i < saga.Schritte.Count; i++)
        {
            SchrittZeilen(b, saga.Schritte[i], modell);
            if (i < saga.Schritte.Count - 1) b.AppendLine();
        }

        b.AppendLine("    });");
        b.AppendLine("}");
        return b.ToString();
    }

    private static void SchrittZeilen(StringBuilder b, SagaSchritt s, EditorModell modell)
    {
        var auf = new StringBuilder($"        p.{Auf}<{s.Wenn[0]}>()");
        for (var i = 1; i < s.Wenn.Count; i++) auf.Append($".{Und}<{s.Wenn[i]}>()");
        var hatSammel = !string.IsNullOrWhiteSpace(s.SammelEvent);
        if (hatSammel)
        {
            var anzahl = !string.IsNullOrWhiteSpace(s.SammelAusdruck) ? s.SammelAusdruck
                : $"t => {(string.IsNullOrWhiteSpace(s.SammelAnzahl) ? "0" : s.SammelAnzahl)}";
            auf.Append($".{UndAlle}<{s.SammelEvent}>({anzahl})");
        }
        b.AppendLine(auf.ToString());

        var lambda = LambdaKopf(s.Wenn.Count + (hatSammel ? 1 : 0));
        var hatKomp = s.Kompensation is not null;
        var verb = s.SendeJe ? SendeJe : Sende;
        string sende;
        if (!string.IsNullOrWhiteSpace(s.SendeAusdruck))
            sende = $"            .{verb}<{s.Sende}>({s.SendeAusdruck})";
        else if (s.SendeJe)
        {
            var sendeArgs = ArgListe(s.SendeArgumente, s.Sende, modell);
            var el = string.IsNullOrWhiteSpace(s.SendeJeElement) ? "z" : s.SendeJeElement;
            var coll = string.IsNullOrWhiteSpace(s.SendeJeCollection) ? "/* Collection */" : s.SendeJeCollection;
            sende = $"            .{SendeJe}<{s.Sende}>({lambda} => {coll}.Select({el} => new {s.Sende}({sendeArgs})))";
        }
        else
            sende = $"            .{Sende}<{s.Sende}>({lambda} => new {s.Sende}({ArgListe(s.SendeArgumente, s.Sende, modell)}))";
        b.AppendLine(hatKomp ? sende : sende + ";");

        if (hatKomp)
        {
            var kompVerb = s.KompensationJe ? RückgängigDurchJe : RückgängigDurch;
            var komp = !string.IsNullOrWhiteSpace(s.KompensationAusdruck)
                ? s.KompensationAusdruck
                : $"{lambda} => new {s.Kompensation}({ArgListe(s.KompensationArgumente, s.Kompensation!, modell)})";
            b.AppendLine($"            .{kompVerb}<{s.Kompensation}>({komp});");
        }
    }

    // ── Bausteine ─────────────────────────────────────────────────────────────────────────────
    private static StringBuilder Kopf(string ns, IReadOnlyList<string> usings)
    {
        var b = new StringBuilder();
        foreach (var u in usings) b.AppendLine($"using {u};");
        if (usings.Count > 0) b.AppendLine();
        b.AppendLine($"namespace {ns};");
        b.AppendLine();
        return b;
    }

    private static void Doku(StringBuilder b, string? doku, string einzug)
    {
        if (string.IsNullOrWhiteSpace(doku)) return;
        b.AppendLine($"{einzug}/// <summary>");
        foreach (var zeile in doku.Replace("\r\n", "\n").Split('\n'))
            b.AppendLine($"{einzug}/// {zeile}");
        b.AppendLine($"{einzug}/// </summary>");
    }

    private static void Rumpf(StringBuilder b, string? rumpf, string todo)
    {
        if (rumpf is null)
        {
            b.AppendLine($"            throw new NotImplementedException(\"{todo}\");");
            return;
        }
        // "" = bewusst leerer Rumpf (No-op-Apply) — kein Platzhalter.
        if (rumpf.Trim().Length == 0) return;
        Eingerückt(b, rumpf, "            ");
    }

    /// <summary>Handcode-Member (Hilfsmethoden) in eine innere Klasse hängen.</summary>
    private static void Zusatz(StringBuilder b, string? zusatz)
    {
        if (string.IsNullOrWhiteSpace(zusatz)) return;
        b.AppendLine();
        Eingerückt(b, zusatz, "        ");
    }

    private static void Eingerückt(StringBuilder b, string text, string einzug)
    {
        foreach (var zeile in text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
            b.AppendLine(zeile.Trim().Length == 0 ? "" : einzug + zeile);
    }

    /// <summary>
    /// Die <c>using</c>-Liste einer Datei: feste Basis + explizite (aus dem Code gelesene) + ABGELEITETE —
    /// jeder Typname in <paramref name="typen"/>, der ein Record/Enum eines ANDEREN Namespace im Modell ist,
    /// zieht dessen Namespace nach. So kompiliert Komposition über Aggregat-Grenzen ohne Handarbeit.
    /// </summary>
    private static IReadOnlyList<string> Usings(string ns, EditorModell modell, IEnumerable<string> basis,
        IEnumerable<string> typen, IEnumerable<string> explizit)
    {
        var nsVon = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in modell.Records) nsVon.TryAdd(r.Name, r.Namespace);
        foreach (var e in modell.Enums) nsVon.TryAdd(e.Name, e.Namespace);

        var menge = new SortedSet<string>(basis.Concat(explizit), StringComparer.Ordinal);
        foreach (var typ in typen)
            foreach (System.Text.RegularExpressions.Match m in Bezeichner.Matches(typ ?? ""))
                if (nsVon.TryGetValue(m.Value, out var andere) && andere != ns) menge.Add(andere);
        menge.Remove(ns);
        // Vertrags-Namespace zuerst (Hausstil), dann alphabetisch.
        return menge.OrderBy(u => u == modell.Rahmen.VertragsNamespace ? 0 : 1).ThenBy(u => u, StringComparer.Ordinal).ToList();
    }

    private static readonly System.Text.RegularExpressions.Regex Bezeichner = new(@"[A-Za-z_][A-Za-z0-9_]*");

    private static string Parameter(IReadOnlyList<Feld> felder) =>
        string.Join(", ", felder.Select(f =>
            $"{f.Typ} {f.Name}" + (f.Standard is null ? "" : $" = {f.Standard}")));

    private static string LambdaKopf(int anzahl)
    {
        string[] namen = ["t", "r", "g"];
        var teile = Enumerable.Range(0, Math.Max(1, anzahl))
            .Select(i => i < namen.Length ? namen[i] : $"e{i + 1}")
            .ToArray();
        return anzahl <= 1 ? teile[0] : "(" + string.Join(", ", teile) + ")";
    }

    private static string ArgListe(IReadOnlyList<string>? ausdruecke, string command, EditorModell modell)
    {
        if (ausdruecke is { Count: > 0 }) return string.Join(", ", ausdruecke);
        var record = modell.Records.FirstOrDefault(r => r.Name == command && r.Kind == RecordArt.Command);
        if (record is null) return "/* TODO: Argumente */";
        return string.Join(", ", record.Felder.Select(_ => "default"));
    }
}
