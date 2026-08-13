using System.Text;

namespace DomainEditor;

/// <summary>Eine generierte Quelldatei: relativer Pfad (unter dem Zielordner) + Inhalt.</summary>
public sealed record GenerierteDatei(string Pfad, string Inhalt);

/// <summary>
/// Der DETERMINISTISCHE Scaffold-Generator (Kern-Idee 1: der Generator macht die FORM). Modell →
/// C# in der kanonischen Gestalt der Domäne (Konto/Prozess). Erzeugt bewusst NUR die vom
/// Fachcode-Autor handgeschriebenen Dateien — Id/Version, State-Property, Handler und Factory
/// liefern die bestehenden Roslyn-Generatoren; würde der Scaffolder sie mit-emittieren, gäbe es
/// doppelte Definitionen.
///
/// Reine Textgenerierung, keine Roslyn-Abhängigkeit: der Prüfstand kann sie store-frei dogfooden.
/// Decide/Apply-Körper bleiben leer (kompilierbarer <c>throw</c>-Platzhalter), bis Stufe 3 sie
/// füllt — oder ein bereits vorhandener Rumpf im Modell steht.
/// </summary>
public static class Scaffolder
{
    /// <summary>Erzeugt alle Quelldateien des Modells, deterministisch nach Pfad sortiert.</summary>
    public static IReadOnlyList<GenerierteDatei> Generiere(EditorModell modell)
    {
        var dateien = new List<GenerierteDatei>();

        foreach (var agg in modell.Aggregate)
        {
            var ordner = OrdnerVon(agg.Namespace);
            dateien.Add(new($"{ordner}/{agg.Name}.cs", StateDatei(agg)));
            dateien.Add(new($"{ordner}/Commands.cs", CommandDatei(agg)));
            dateien.Add(new($"{ordner}/Events.cs", EventDatei(agg)));
            dateien.Add(new($"{ordner}/Decider.cs", DeciderDatei(agg)));
            dateien.Add(new($"{ordner}/Applier.cs", ApplierDatei(agg)));
        }

        foreach (var saga in modell.Sagas)
        {
            var ordner = OrdnerVon(saga.Namespace);
            dateien.Add(new($"{ordner}/{saga.Name}.cs", SagaDatei(saga, modell)));
        }

        return dateien.OrderBy(d => d.Pfad, StringComparer.Ordinal).ToList();
    }

    // ── Aggregat: die State-Klasse ────────────────────────────────────────────────────────────
    private static string StateDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        Doku(b, agg.Doku, "");
        b.AppendLine($"public partial class {agg.Name} : IState");
        b.AppendLine("{");

        var gespeichert = agg.Felder.Where(f => f.Ausdruck is null).ToList();
        var abgeleitet = agg.Felder.Where(f => f.Ausdruck is not null).ToList();

        foreach (var f in gespeichert)
            b.AppendLine($"    public {f.Typ} {f.Name} {{ get; set; }}");

        if (abgeleitet.Count > 0)
        {
            if (gespeichert.Count > 0) b.AppendLine();
            foreach (var f in abgeleitet)
                b.AppendLine($"    public {f.Typ} {f.Name} => {f.Ausdruck};");
        }

        b.AppendLine("}");
        return b.ToString();
    }

    // ── Commands ──────────────────────────────────────────────────────────────────────────────
    private static string CommandDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        foreach (var c in agg.Commands)
            b.AppendLine($"public record {c.Name}({Parameter(c.Felder)}) : ICommand;");
        return b.ToString();
    }

    // ── Events (persistent, dann transient) ─────────────────────────────────────────────────────
    private static string EventDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        var persistent = agg.Events.Where(e => !e.Transient).ToList();
        var transient = agg.Events.Where(e => e.Transient).ToList();

        foreach (var e in persistent)
            b.AppendLine($"public record {e.Name}({Parameter(e.Felder)}) : IEvent;");

        if (transient.Count > 0)
        {
            if (persistent.Count > 0) b.AppendLine();
            foreach (var e in transient)
                b.AppendLine($"public record {e.Name}({Parameter(e.Felder)}) : ITransientEvent;");
        }

        return b.ToString();
    }

    // ── Decider ─────────────────────────────────────────────────────────────────────────────────
    private static string DeciderDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class Decider : IDecider<{agg.Name}>");
        b.AppendLine("    {");

        for (var i = 0; i < agg.Commands.Count; i++)
        {
            var c = agg.Commands[i];
            var oneOf = string.Join(", ", c.Ergibt.Select(a => a.Event));
            b.AppendLine($"        public IEnumerable<OneOf<{oneOf}>> Decide({c.Name} cmd)");
            b.AppendLine("        {");
            Rumpf(b, c.Rumpf, "TODO: Entscheidungslogik (Stufe 3).");
            b.AppendLine("        }");
            if (i < agg.Commands.Count - 1) b.AppendLine();
        }

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    // ── Applier (nur persistente Events) ─────────────────────────────────────────────────────────
    private static string ApplierDatei(Aggregat agg)
    {
        var b = Kopf(agg.Namespace, "using Abstractions;");
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class Applier : IApplier<{agg.Name}>");
        b.AppendLine("    {");

        var persistent = agg.Events.Where(e => !e.Transient).ToList();
        for (var i = 0; i < persistent.Count; i++)
        {
            var e = persistent[i];
            b.AppendLine($"        public void Apply({e.Name} evt)");
            b.AppendLine("        {");
            Rumpf(b, e.ApplyRumpf, "TODO: Apply-Logik (Stufe 3).");
            b.AppendLine("        }");
            if (i < persistent.Count - 1) b.AppendLine();
        }

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    // ── Saga / Prozess ───────────────────────────────────────────────────────────────────────────
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

    // ── Bausteine ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Dateikopf: usings, Leerzeile, file-scoped namespace, Leerzeile.</summary>
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

    /// <summary>Kompilierbarer Platzhalter-Rumpf, oder der vorhandene Rumpf (auf 12 Spalten eingerückt).</summary>
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

    /// <summary>Record-Parameterliste: <c>Typ Name</c> je Feld, optional <c>= Standard</c>.</summary>
    private static string Parameter(IReadOnlyList<Feld> felder) =>
        string.Join(", ", felder.Select(f =>
            $"{f.Typ} {f.Name}" + (f.Standard is null ? "" : $" = {f.Standard}")));

    /// <summary>Lambda-Kopf: <c>t</c> bei einer Bedingung, sonst <c>(t, r, g, …)</c>.</summary>
    private static string LambdaKopf(int anzahl)
    {
        string[] namen = ["t", "r", "g"];
        var teile = Enumerable.Range(0, Math.Max(1, anzahl))
            .Select(i => i < namen.Length ? namen[i] : $"e{i + 1}")
            .ToArray();
        return anzahl <= 1 ? teile[0] : "(" + string.Join(", ", teile) + ")";
    }

    /// <summary>
    /// Argumentliste für den Command-Ctor. Sind Ausdrücke bekannt, werden sie roh übernommen;
    /// sonst <c>default</c> je Command-Feld (kompilierbarer Platzhalter — die Argument-Ausdrücke
    /// sind Fachlogik und werden im Editor/Stufe 3 gefüllt). Ist der Command unbekannt (aggregat-
    /// fremd, nicht im Modell), bleibt ein sichtbarer TODO-Kommentar.
    /// </summary>
    private static string ArgListe(IReadOnlyList<string>? ausdruecke, string command, EditorModell modell)
    {
        if (ausdruecke is { Count: > 0 }) return string.Join(", ", ausdruecke);

        var befehl = modell.Aggregate.SelectMany(a => a.Commands).FirstOrDefault(c => c.Name == command);
        if (befehl is null) return "/* TODO: Argumente */";
        return string.Join(", ", befehl.Felder.Select(_ => "default"));
    }

    /// <summary>Ordnername = letztes Namespace-Segment (z. B. <c>Domain.Konto</c> → <c>Konto</c>).</summary>
    private static string OrdnerVon(string ns)
    {
        var idx = ns.LastIndexOf('.');
        return idx < 0 ? ns : ns[(idx + 1)..];
    }
}
