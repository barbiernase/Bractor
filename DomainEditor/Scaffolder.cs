using System.Text;

namespace DomainEditor;

/// <summary>Eine generierte Quelldatei: relativer Pfad (unter dem Zielordner) + Inhalt.</summary>
public sealed record GenerierteDatei(string Pfad, string Inhalt);

/// <summary>
/// Der DETERMINISTISCHE Scaffold-Generator (record-zentrisch). Aus den getaggten Records +
/// Enums + der Aggregat-Komposition erzeugt er C# in kanonischer Gestalt:
///   Records nach Namespace → <c>Commands.cs</c> / <c>Events.cs</c> / <c>ValueObjects.cs</c>,
///   Enums → <c>Enums.cs</c>, Aggregat → <c>{Name}.cs</c> (State) / <c>Decider.cs</c> / <c>Applier.cs</c>.
///
/// Erzeugt bewusst NUR die handgeschriebenen Dateien — Id/Version, State-Property, Handler und
/// Factory liefern die bestehenden Roslyn-Generatoren. Decide/Apply-Körper bleiben leer
/// (kompilierbarer <c>throw</c>-Platzhalter), bis der Rumpf im Modell steht.
/// </summary>
public static class Scaffolder
{
    public static IReadOnlyList<GenerierteDatei> Generiere(EditorModell modell)
    {
        var dateien = new List<GenerierteDatei>();

        // ── Records + Enums nach Namespace gruppieren → Typ-Dateien ──
        var namespaces = modell.Records.Select(r => r.Namespace)
            .Concat(modell.Enums.Select(e => e.Namespace))
            .Distinct(StringComparer.Ordinal);

        foreach (var ns in namespaces)
        {
            var ordner = OrdnerVon(ns);
            var records = modell.Records.Where(r => r.Namespace == ns).ToList();
            var enums = modell.Enums.Where(e => e.Namespace == ns).ToList();

            if (enums.Count > 0)
                dateien.Add(new($"{ordner}/Enums.cs", EnumDatei(ns, enums)));

            var commands = records.Where(r => r.Kind == RecordArt.Command).ToList();
            if (commands.Count > 0)
                dateien.Add(new($"{ordner}/Commands.cs", CommandDatei(ns, commands)));

            var events = records.Where(r => r.Kind is RecordArt.Event or RecordArt.Rejection).ToList();
            if (events.Count > 0)
                dateien.Add(new($"{ordner}/Events.cs", EventDatei(ns, events)));

            var vos = records.Where(r => r.Kind == RecordArt.ValueObject).ToList();
            if (vos.Count > 0)
                dateien.Add(new($"{ordner}/ValueObjects.cs", ValueObjectDatei(ns, vos)));
        }

        // ── Aggregate (Komposition) → State + Decider + Applier ──
        foreach (var agg in modell.Aggregate)
        {
            var ordner = OrdnerVon(agg.Namespace);
            dateien.Add(new($"{ordner}/{agg.Name}.cs", StateDatei(agg)));
            dateien.Add(new($"{ordner}/Decider.cs", DeciderDatei(agg)));
            dateien.Add(new($"{ordner}/Applier.cs", ApplierDatei(agg)));
        }

        foreach (var saga in modell.Sagas)
            dateien.Add(new($"{OrdnerVon(saga.Namespace)}/{saga.Name}.cs", SagaDatei(saga, modell)));

        return dateien.OrderBy(d => d.Pfad, StringComparer.Ordinal).ToList();
    }

    // ── Typ-Dateien aus Records/Enums ─────────────────────────────────────────────────────────
    private static string CommandDatei(string ns, List<Record> commands)
    {
        var b = Kopf(ns, "using Abstractions;");
        foreach (var c in commands)
        {
            Doku(b, c.Doku, "");
            var marker = c.IstErzeugung ? "ICommand, ICreationCommand" : "ICommand";
            b.AppendLine($"public record {c.Name}({Parameter(c.Felder)}) : {marker};");
        }
        return b.ToString();
    }

    private static string EventDatei(string ns, List<Record> events)
    {
        var b = Kopf(ns, "using Abstractions;");
        var persistent = events.Where(e => e.Kind == RecordArt.Event).ToList();
        var rejections = events.Where(e => e.Kind == RecordArt.Rejection).ToList();

        foreach (var e in persistent)
        {
            Doku(b, e.Doku, "");
            b.AppendLine($"public record {e.Name}({Parameter(e.Felder)}) : IEvent;");
        }
        if (rejections.Count > 0)
        {
            if (persistent.Count > 0) b.AppendLine();
            foreach (var e in rejections)
            {
                Doku(b, e.Doku, "");
                b.AppendLine($"public record {e.Name}({Parameter(e.Felder)}) : ITransientEvent;");
            }
        }
        return b.ToString();
    }

    private static string ValueObjectDatei(string ns, List<Record> vos)
    {
        // Value Objects: reine Records, keine Interfaces, keine usings (ImplicitUsings deckt System/Collections).
        var b = new StringBuilder();
        b.AppendLine($"namespace {ns};");
        b.AppendLine();
        for (var i = 0; i < vos.Count; i++)
        {
            Doku(b, vos[i].Doku, "");
            b.AppendLine($"public record {vos[i].Name}({Parameter(vos[i].Felder)});");
            if (i < vos.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    private static string EnumDatei(string ns, List<Enumeration> enums)
    {
        var b = new StringBuilder();
        b.AppendLine($"namespace {ns};");
        b.AppendLine();
        for (var i = 0; i < enums.Count; i++)
        {
            Doku(b, enums[i].Doku, "");
            b.AppendLine($"public enum {enums[i].Name} {{ {string.Join(", ", enums[i].Werte)} }}");
            if (i < enums.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    // ── Aggregat-Komposition ──────────────────────────────────────────────────────────────────
    private static string StateDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        Doku(b, agg.Doku, "");
        b.AppendLine($"public partial class {agg.Name} : IState");
        b.AppendLine("{");

        var gespeichert = agg.State.Where(f => f.Ausdruck is null).ToList();
        var abgeleitet = agg.State.Where(f => f.Ausdruck is not null).ToList();

        foreach (var f in gespeichert)
            b.AppendLine($"    public {f.Typ} {f.Name} {{ get; set; }}");

        if (abgeleitet.Count > 0)
        {
            if (gespeichert.Count > 0) b.AppendLine();
            foreach (var f in abgeleitet)
                b.AppendLine($"    public {f.Typ} {f.Name} => {f.Ausdruck};");
        }

        if (!string.IsNullOrWhiteSpace(agg.StateZusatz))
        {
            if (agg.State.Count > 0) b.AppendLine();
            foreach (var zeile in agg.StateZusatz.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
                b.AppendLine(zeile.Length == 0 ? "" : $"    {zeile}");
        }

        b.AppendLine("}");
        return b.ToString();
    }

    private static string DeciderDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class Decider : IDecider<{agg.Name}>");
        b.AppendLine("    {");

        for (var i = 0; i < agg.Decider.Count; i++)
        {
            var d = agg.Decider[i];
            var oneOf = string.Join(", ", d.Ergibt.Select(a => a.Event));
            b.AppendLine($"        public IEnumerable<OneOf<{oneOf}>> Decide({d.Command} cmd)");
            b.AppendLine("        {");
            Rumpf(b, d.Rumpf, "TODO: Entscheidungslogik.");
            b.AppendLine("        }");
            if (i < agg.Decider.Count - 1) b.AppendLine();
        }

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    private static string ApplierDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class Applier : IApplier<{agg.Name}>");
        b.AppendLine("    {");

        for (var i = 0; i < agg.Applier.Count; i++)
        {
            var a = agg.Applier[i];
            b.AppendLine($"        public void Apply({a.Event} evt)");
            b.AppendLine("        {");
            Rumpf(b, a.Rumpf, "TODO: Apply-Logik.");
            b.AppendLine("        }");
            if (i < agg.Applier.Count - 1) b.AppendLine();
        }

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    // ── Saga / Prozess ────────────────────────────────────────────────────────────────────────
    private static string SagaDatei(Saga saga, EditorModell modell)
    {
        var usings = new List<string> { "using Abstractions;" };
        usings.AddRange(saga.ExtraUsings.Select(u => $"using {u};"));
        var b = Kopf(saga.Namespace, usings.ToArray());
        Doku(b, saga.Doku, "");
        b.AppendLine($"public sealed class {saga.Name} : IProzessDefinition");
        b.AppendLine("{");
        b.AppendLine($"    public ProzessRegeln Regeln => Prozess<{saga.TriggerEvent}>.Definiere(p =>");
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
        var auf = new StringBuilder($"        p.Auf<{s.Wenn[0]}>()");
        for (var i = 1; i < s.Wenn.Count; i++) auf.Append($".Und<{s.Wenn[i]}>()");
        b.AppendLine(auf.ToString());

        var lambda = LambdaKopf(s.Wenn.Count);
        var sendeArgs = ArgListe(s.SendeArgumente, s.Sende, modell);
        var hatKomp = s.Kompensation is not null;
        var sende = $"            .Sende<{s.Sende}>({lambda} => new {s.Sende}({sendeArgs}))";
        b.AppendLine(hatKomp ? sende : sende + ";");

        if (hatKomp)
        {
            var kompArgs = ArgListe(s.KompensationArgumente, s.Kompensation!, modell);
            b.AppendLine($"            .RückgängigDurch<{s.Kompensation}>({lambda} => new {s.Kompensation}({kompArgs}));");
        }
    }

    // ── Bausteine ─────────────────────────────────────────────────────────────────────────────
    private static StringBuilder Kopf(string ns, params string[] usings)
    {
        var b = new StringBuilder();
        foreach (var u in usings) b.AppendLine(u);
        b.AppendLine();
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
        if (string.IsNullOrWhiteSpace(rumpf))
        {
            b.AppendLine($"            throw new NotImplementedException(\"{todo}\");");
            return;
        }
        foreach (var zeile in rumpf.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
            b.AppendLine(zeile.Length == 0 ? "" : $"            {zeile}");
    }

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

    private static string OrdnerVon(string ns)
    {
        var idx = ns.LastIndexOf('.');
        return idx < 0 ? ns : ns[(idx + 1)..];
    }
}
