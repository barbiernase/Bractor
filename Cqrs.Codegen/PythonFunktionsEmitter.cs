using System.Reflection;
using System.Text;

namespace Cqrs.Codegen;

/// <summary>
/// Die Lesart einer Funktion draußen für den Python-Prepass (Metadaten der gebauten Domain-DLLs, wie der übrige Prepass): je <c>Auf</c> der Eingang,
/// die Ausgaben mit ihrer Art (Command, Event, Hinweis) und ob die Ausgabe ein Strom ist — dieselbe Signatur, die der
/// <c>FunktionsBausteinGenerator</c> am Server liest (docs/10 §10.3).
/// </summary>
public static class PythonFunktionsform
{
    private const string FunktionName = "Abstractions.IFunktion";

    public sealed record Ausgabe(string Typ, string Art);
    /// <param name="Hinweis">Der Eingang ist ein Hinweis aus dem Hinweis-Netz (docs/04 §4.4): höchstens einmal, die Antwort kommt als Ganzes.</param>
    public sealed record Auf(string Eingang, List<Ausgabe> Ausgaben, bool Strom, bool Hinweis = false);
    public sealed record Funktion(Type Typ, List<Auf> Aufs);

    /// <summary>
    /// Eine Funktion als Schnittstelle (draußen) mit ihren <c>Auf</c>. Eine Funktion mit Lese-Fähigkeit (ein Parameter außer Herkunft und Abbruch)
    /// braucht die Stores des Hosts — sie läuft nie draußen und bekommt keine Python-Basis (null).
    /// </summary>
    public static Funktion? Lies(Type t)
    {
        if (!t.IsInterface || t.FullName == FunktionName || !t.GetInterfaces().Any(i => i.FullName == FunktionName)) return null;
        var aufs = new List<Auf>();
        foreach (var m in t.GetMethods().Where(m => m.Name == Abstractions.Funktionsvertrag.Auf).OrderBy(m => m.MetadataToken))
        {
            var p = m.GetParameters();
            if (p.Length == 0) continue;
            if (p.Skip(1).Any(x => x.ParameterType.FullName is not (Abstractions.Funktionsvertrag.Herkunft or "System.Threading.CancellationToken")))
                return null;
            var r = m.ReturnType;
            if (r.IsGenericType && r.Name is "Task`1" or "ValueTask`1") r = r.GetGenericArguments()[0];
            var strom = false;
            if (r.IsGenericType && r.Name is "IEnumerable`1" or "IAsyncEnumerable`1") { strom = true; r = r.GetGenericArguments()[0]; }
            if (r.IsGenericType && r.Name == "Nullable`1") r = r.GetGenericArguments()[0];
            var faelle = r.IsGenericType && r.Name.StartsWith("OneOf`", StringComparison.Ordinal) ? r.GetGenericArguments().ToList() : new List<Type> { r };
            aufs.Add(new Auf(p[0].ParameterType.Name, faelle.Select(f => new Ausgabe(f.Name, Art(f))).ToList(), strom,
                p[0].ParameterType.GetInterfaces().Any(i => i.FullName == "Abstractions.ITransientEvent")));
        }
        return aufs.Count == 0 ? null : new Funktion(t, aufs);
    }

    /// <summary>Die Art einer Ausgabe auf der Leitung (<c>FunktionsAusgabe</c>): command, hinweis oder event.</summary>
    private static string Art(Type t)
    {
        var basen = t.GetInterfaces().Select(i => i.FullName).ToHashSet(StringComparer.Ordinal);
        return basen.Contains("Abstractions.ICommand") ? "COMMAND" : basen.Contains("Abstractions.ITransientEvent") ? "HINWEIS" : "EVENT";
    }

    /// <summary>Die Funktionen draußen (Schnittstellen mit <c>Auf</c>, ohne Lese-Fähigkeit), nach vollem Namen sortiert.</summary>
    public static List<Funktion> Alle(IEnumerable<Assembly> assemblies) => assemblies.SelectMany(TypenVon)
        .Where(t => t.IsInterface).OrderBy(t => t.FullName, StringComparer.Ordinal)
        .Select(Lies).Where(f => f != null).Select(f => f!).ToList();

    /// <summary>Python-Literal der erlaubten Ausgaben: <c>{XDto: Ausgabe.COMMAND, …}</c>.</summary>
    public static string Ausgaben(Auf a, string dto) =>
        "{" + string.Join(", ", a.Ausgaben.Select(x => $"{x.Typ}{dto}: Ausgabe.{x.Art}")) + "}";

    /// <summary>Die Python-Rückgabe einer Methode (Strom: <c>AsyncIterator[…]</c>).</summary>
    public static string Rueckgabe(Auf a, string dto)
    {
        var faelle = string.Join(" | ", a.Ausgaben.Select(x => x.Typ + dto));
        return a.Strom ? $"AsyncIterator[{faelle}]" : $"{faelle} | None";
    }

    /// <summary>Die Signatur als C#-Zeile für den Docstring.</summary>
    public static string Zeile(Auf a) =>
        $"{(a.Strom ? "IAsyncEnumerable<" : "")}{(a.Ausgaben.Count > 1 ? "OneOf<" + string.Join(", ", a.Ausgaben.Select(x => x.Typ)) + ">" : a.Ausgaben[0].Typ)}{(a.Strom ? ">" : "")} Auf({a.Eingang} e)";

    public static IEnumerable<Type> TypenVon(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }
}

/// <summary>
/// Funktionen draußen → Python (docs/10 §10.1, docs/konzept-akteure.md §5): je <c>interface IX : IFunktion</c> mit <c>Auf</c> eine abstrakte Klasse
/// <c>XBasis(FunktionsBasis)</c> in <c>domain_client/generated/funktionen.py</c> — je <c>Auf</c> eine Methode <c>auf_&lt;eingang&gt;(e, h)</c>. Ein
/// Worker erbt sie, füllt die Methoden und bietet die Funktion an; der Funktions-Actor merkt nicht, dass sie draußen rechnet.
/// </summary>
public static class PythonFunktionsEmitter
{
    private const string Dto = Core.SourceGeneration.NameSanitizer.DtoSuffix;   // die Namensregel der Leitung

    public static (string Inhalt, int Anzahl) Emit(IEnumerable<Assembly> assemblies)
    {
        var funktionen = PythonFunktionsform.Alle(assemblies);
        return (Python(funktionen), funktionen.Count);
    }

    /// <summary>
    /// Die Tabelle des generischen Funktions-Workers (docs/09 §9.5): je Funktion mit Python-Basis die Basis, das Modul und die Klasse ihrer
    /// Implementierung (<c>funktionen/&lt;modul&gt;.py</c>, Regel in <c>Abstractions.Funktionsvertrag</c>) — der Runner kennt keine Domäne.
    /// </summary>
    public static string Tabelle(IEnumerable<Assembly> assemblies)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Funktionen draußen (IFunktion mit Auf, docs/09 §9.5). Nicht von Hand ändern.");
        b.AppendLine("#");
        b.AppendLine("# Die Tabelle des generischen Funktions-Workers (funktions_worker.py): je Funktion mit Python-Basis die Basis (generated/funktionen.py),");
        b.AppendLine($"# das Modul und die Klasse ihrer Implementierung ({Abstractions.Funktionsvertrag.PythonOrdner}/<modul>.py, die Klasse erbt die Basis und füllt `auf_…`).");
        b.AppendLine("# Welche davon ein Worker anbietet, sagt die Umgebung (Funktionen:<F>:Laufort = Extern, Worker-Argumente).");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("TABELLE: dict[str, tuple[str, str, str]] = {");
        foreach (var f in PythonFunktionsform.Alle(assemblies))
        {
            var n = f.Typ.Name;
            b.AppendLine($"    \"{n}\": (\"{Abstractions.Funktionsvertrag.PythonBasis(n)}\", \"{Abstractions.Funktionsvertrag.PythonOrdner}.{Abstractions.Funktionsvertrag.PythonModul(n)}\", \"{Abstractions.Funktionsvertrag.OhneI(n)}\"),");
        }
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary><c>IBildaufbereitung</c> → <c>Bildaufbereitung</c> (Klassenname der Basis ohne Schnittstellen-I).</summary>
    public static string OhneI(string name) => Abstractions.Funktionsvertrag.OhneI(name);

    private static string Python(List<PythonFunktionsform.Funktion> funktionen)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Funktionen draußen der Domäne (IFunktion mit Auf, docs/konzept-akteure.md §5).");
        b.AppendLine("# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.");
        b.AppendLine("#");
        b.AppendLine("# Je Funktion eine abstrakte Basis: der Worker erbt sie, füllt je Eingang `auf_<eingang>(e, h)` und übergibt eine Instanz an seinen");
        b.AppendLine("# Client (`funktionen=[…]`). Das SDK bietet sie am Handshake an (Funktion + Slots), nimmt Eingänge an, sendet Lebenszeichen und prüft");
        b.AppendLine("# jede Ausgabe gegen die Signatur; der Server hält die Antwort fest (die erste gewinnt) und sendet sie.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from abc import abstractmethod");
        b.AppendLine("from typing import AsyncIterator, ClassVar");
        b.AppendLine();
        b.AppendLine("from cqrs_client.funktion import Ausgabe, Eingang, FunktionsBasis, Herkunft");
        b.AppendLine();
        var dtos = funktionen.SelectMany(f => f.Aufs.SelectMany(a => a.Ausgaben.Select(x => x.Typ).Append(a.Eingang)))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (dtos.Count > 0)
        {
            b.AppendLine("from . import (");
            foreach (var d in dtos) b.AppendLine($"    {d}{Dto},");
            b.AppendLine(")");
            b.AppendLine();
        }

        foreach (var f in funktionen)
        {
            b.AppendLine();
            b.AppendLine($"class {OhneI(f.Typ.Name)}Basis(FunktionsBasis):");
            b.AppendLine($"    \"\"\"Funktion {f.Typ.FullName}: {string.Join("; ", f.Aufs.Select(a => $"{a.Eingang} → {string.Join(" | ", a.Ausgaben.Select(x => x.Typ))}{(a.Strom ? " (Strom)" : "")}"))}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    FUNKTION: ClassVar[str] = \"{f.Typ.Name}\"");
            b.AppendLine("    EINGAENGE: ClassVar[dict] = {");
            foreach (var a in f.Aufs)
                b.AppendLine($"        {a.Eingang}{Dto}: Eingang(\"auf_{PythonVertragsEmitter.Snake(a.Eingang)}\", {PythonFunktionsform.Ausgaben(a, Dto)}{(a.Strom ? ", strom=True" : "")}{(a.Hinweis ? ", hinweis=True" : "")}),");
            b.AppendLine("    }");
            foreach (var a in f.Aufs)
            {
                b.AppendLine();
                b.AppendLine("    @abstractmethod");
                b.AppendLine($"    async def auf_{PythonVertragsEmitter.Snake(a.Eingang)}(self, e: {a.Eingang}{Dto}, h: Herkunft) -> {PythonFunktionsform.Rueckgabe(a, Dto)}:");
                b.AppendLine($"        \"\"\"{PythonFunktionsform.Zeile(a)}{(a.Strom ? " — Strom: beliebig viele yields" : " — höchstens eine Ausgabe")}\"\"\"");
                b.AppendLine("        ...");
            }
            b.AppendLine();
        }
        b.AppendLine();
        b.AppendLine("__all__ = [");
        foreach (var f in funktionen) b.AppendLine($"    \"{OhneI(f.Typ.Name)}Basis\",");
        b.AppendLine("]");
        return b.ToString();
    }
}
