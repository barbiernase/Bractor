using System.Reflection;
using System.Text;

namespace Cqrs.Codegen;

/// <summary>
/// Katalog-Funktionen → Python (<c>docs/konzept-editor-pipelines.md</c> §14.5): je <c>interface IX : IFunktion</c> eine abstrakte
/// Klasse <c>XBasis(FunktionsBasis)</c> in <c>domain_client/generated/funktionen.py</c>. Ein Worker erbt sie, implementiert
/// <c>rufe(auftrag, x)</c> und bietet die Funktion damit an — Pipeline und Prozess merken nicht, dass sie umgezogen ist.
/// Gelesen wird nur die Signatur (Metadaten der gebauten Domain-DLLs, wie der übrige Prepass):
/// <c>Task&lt;OneOf&lt;E1, E2&gt;&gt; RufeAsync(TAuftrag auftrag, IAusfuehrung x)</c> → AUFTRAG = <c>TAuftragDto</c>,
/// ERGEBNISSE = <c>(E1Dto, E2Dto)</c>.
/// </summary>
public static class PythonFunktionsEmitter
{
    private const string FunktionName = "Abstractions.IFunktion";
    private const string Dto = "Dto";

    private sealed record FunktionInfo(Type Funktion, string Auftrag, List<string> Ergebnisse);

    public static (string Inhalt, int Anzahl) Emit(IEnumerable<Assembly> assemblies)
    {
        var funktionen = Funktionen(assemblies);
        return (Python(funktionen), funktionen.Count);
    }

    /// <summary>
    /// Die Tabelle des generischen Funktions-Workers (konzept-werkbank §12.3 E13): je Funktion mit Python-Basis die Basis, das Modul und die Klasse ihrer
    /// Implementierung (<c>funktionen/&lt;modul&gt;.py</c>, Regel in <c>Abstractions.Funktionsvertrag</c>) — der Runner kennt keine Domäne.
    /// </summary>
    public static string Tabelle(IEnumerable<Assembly> assemblies)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Katalog-Funktionen der Domäne (IFunktion, konzept-werkbank §12.3 E13). Nicht von Hand ändern.");
        b.AppendLine("#");
        b.AppendLine("# Die Tabelle des generischen Funktions-Workers (funktions_worker.py): je Funktion mit Python-Basis die Basis (generated/funktionen.py),");
        b.AppendLine($"# das Modul und die Klasse ihrer Implementierung ({Abstractions.Funktionsvertrag.PythonOrdner}/<modul>.py, die Klasse erbt die Basis und füllt `rufe`).");
        b.AppendLine("# Welche davon ein Worker anbietet, sagt die Umgebung (Funktionen:<F>:Laufort = Extern, Worker-Argumente).");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("TABELLE: dict[str, tuple[str, str, str]] = {");
        foreach (var f in Funktionen(assemblies))
        {
            var n = f.Funktion.Name;
            b.AppendLine($"    \"{n}\": (\"{Abstractions.Funktionsvertrag.PythonBasis(n)}\", \"{Abstractions.Funktionsvertrag.PythonOrdner}.{Abstractions.Funktionsvertrag.PythonModul(n)}\", \"{Abstractions.Funktionsvertrag.OhneI(n)}\"),");
        }
        b.AppendLine("}");
        return b.ToString();
    }

    private static List<FunktionInfo> Funktionen(IEnumerable<Assembly> assemblies) => assemblies.SelectMany(TypenVon)
        .Where(t => t.IsInterface && t.GetInterfaces().Any(i => i.FullName == FunktionName))
        .OrderBy(t => t.FullName, StringComparer.Ordinal)
        .Select(Lies).Where(f => f != null).Select(f => f!).ToList();

    /// <summary>
    /// Die feste Form (CQRS068): genau eine Methode RufeAsync(TAuftrag, IAusfuehrung) → Task&lt;OneOf&lt;…&gt;&gt;. Eine Funktion mit
    /// Lese-Fähigkeiten (weitere Parameter) braucht die Stores des Hosts — sie bekommt keine Python-Basis (läuft nur in C#).
    /// </summary>
    private static FunktionInfo? Lies(Type f)
    {
        var m = f.GetMethods().FirstOrDefault(x => x.Name == "RufeAsync" && x.GetParameters().Length == 2);
        if (m == null) return null;
        var rueck = m.ReturnType;
        if (rueck.IsGenericType && rueck.Name.StartsWith("Task`", StringComparison.Ordinal)) rueck = rueck.GetGenericArguments()[0];
        var ergebnisse = rueck.IsGenericType && rueck.Name.StartsWith("OneOf`", StringComparison.Ordinal)
            ? rueck.GetGenericArguments().Select(a => a.Name).ToList()
            : new List<string> { rueck.Name };
        return new FunktionInfo(f, m.GetParameters()[0].ParameterType.Name, ergebnisse);
    }

    /// <summary><c>IBildVerkleinerung</c> → <c>BildVerkleinerung</c> (Klassenname der Basis ohne Schnittstellen-I).</summary>
    public static string OhneI(string name) => Abstractions.Funktionsvertrag.OhneI(name);

    private static string Python(List<FunktionInfo> funktionen)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Katalog-Funktionen der Domäne (IFunktion, docs/konzept-editor-pipelines.md §14.5).");
        b.AppendLine("# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.");
        b.AppendLine("#");
        b.AppendLine("# Je Funktion eine abstrakte Basis: der Worker erbt sie, implementiert `rufe(auftrag, x)` und übergibt eine Instanz");
        b.AppendLine("# an seinen Client (`funktionen=[…]`). Das SDK meldet sie am Handshake an (Funktion + Slots), nimmt Aufträge an,");
        b.AppendLine("# sendet Lebenszeichen und prüft, dass das Ergebnis einer der ERGEBNISSE ist; der Server schreibt es genau einmal.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from abc import abstractmethod");
        b.AppendLine("from typing import ClassVar");
        b.AppendLine();
        b.AppendLine("from cqrs_client.funktion import Ausfuehrung, FunktionsBasis");
        b.AppendLine();
        var dtos = funktionen.SelectMany(f => f.Ergebnisse.Append(f.Auftrag))
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
            var ergebnisse = "(" + string.Join(", ", f.Ergebnisse.Select(e => e + Dto)) + (f.Ergebnisse.Count == 1 ? ",)" : ")");
            b.AppendLine();
            b.AppendLine($"class {OhneI(f.Funktion.Name)}Basis(FunktionsBasis):");
            b.AppendLine($"    \"\"\"Funktion {f.Funktion.FullName}: {f.Auftrag} → {string.Join(" | ", f.Ergebnisse)}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    FUNKTION: ClassVar[str] = \"{f.Funktion.Name}\"");
            b.AppendLine($"    AUFTRAG: ClassVar[type] = {f.Auftrag}{Dto}");
            b.AppendLine($"    ERGEBNISSE: ClassVar[tuple] = {ergebnisse}");
            b.AppendLine();
            b.AppendLine("    @abstractmethod");
            b.AppendLine($"    async def rufe(self, auftrag: {f.Auftrag}{Dto}, x: Ausfuehrung) -> {string.Join(" | ", f.Ergebnisse.Select(e => e + Dto))}:");
            b.AppendLine($"        \"\"\"Task<OneOf<{string.Join(", ", f.Ergebnisse)}>> RufeAsync({f.Auftrag} auftrag, IAusfuehrung x) — genau ein Ergebnis.\"\"\"");
            b.AppendLine("        ...");
            b.AppendLine();
        }
        b.AppendLine();
        b.AppendLine("__all__ = [");
        foreach (var f in funktionen) b.AppendLine($"    \"{OhneI(f.Funktion.Name)}Basis\",");
        b.AppendLine("]");
        return b.ToString();
    }

    private static IEnumerable<Type> TypenVon(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }
}
