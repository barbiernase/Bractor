using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Abstractions;

namespace Cqrs.Codegen;

/// <summary>
/// Akteur-Verträge → Python (<c>docs/konzept-akteure.md</c> §9.5b): je <c>interface IX : IAkteurVertrag&lt;X&gt;</c> eine abstrakte
/// Klasse <c>XBasis</c> in <c>domain_client/generated/vertraege.py</c>, gegen die der Worker programmiert. Gelesen wird nur die
/// Signatur (Metadaten der gebauten Domain-DLLs, wie der übrige Prepass): je <c>Auf(E)</c> der Eingang und die Ausgänge aus dem
/// Rückgabetyp. Der Vertrags-Hash kommt aus derselben Quelle wie auf dem Server (<see cref="Akteurvertrag"/>) — so erkennt der
/// Handshake einen Worker, der gegen einen anderen Stand gebaut wurde.
/// </summary>
public static class PythonVertragsEmitter
{
    private const string VertragName = "Abstractions.IAkteurVertrag`1";
    private const string DarfName = "Abstractions.IDarf`1";
    private const string CommandName = "Abstractions.ICommand";
    private const string Dto = "Dto";

    private sealed record ReaktionInfo(string Eingang, List<string> Ausgaenge, bool Strom);
    private sealed record VertragInfo(Type Vertrag, Type Akteur, string Art, List<ReaktionInfo> Reaktionen, List<string> Spontan);

    public static (string Inhalt, int Anzahl) Emit(IEnumerable<Assembly> assemblies)
    {
        var vertraege = new List<VertragInfo>();
        foreach (var t in assemblies.SelectMany(TypenVon).Where(t => t.IsInterface).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var vi = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == VertragName);
            if (vi == null) continue;
            var akteur = vi.GetGenericArguments()[0];
            if (vertraege.Any(v => v.Akteur.FullName == akteur.FullName)) continue;   // höchstens einer je Akteur (CQRS061)
            var reaktionen = t.GetMethods()
                .Where(m => m.Name == Akteurvertrag.Auf && m.GetParameters().Length == 1)
                .OrderBy(m => m.MetadataToken)
                .Select(m => Reaktion(m)).ToList();
            var spontan = akteur.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == DarfName)
                .Select(i => i.GetGenericArguments()[0])
                .Where(x => x.GetInterfaces().Any(i => i.FullName == CommandName))
                .Select(x => x.Name).ToList();
            vertraege.Add(new VertragInfo(t, akteur, ArtVon(akteur), reaktionen, spontan));
        }
        return (Python(vertraege), vertraege.Count);
    }

    private static ReaktionInfo Reaktion(MethodInfo m)
    {
        var el = m.ReturnType;
        var strom = false;
        var aus = new List<string>();
        if (el.FullName != "System.Void")
        {
            if (el.IsGenericType && el.Name is "IEnumerable`1" or "IAsyncEnumerable`1")
            {
                strom = true;
                el = el.GetGenericArguments()[0];
            }
            if (el.IsGenericType && el.Name.StartsWith("OneOf`", StringComparison.Ordinal)) aus.AddRange(el.GetGenericArguments().Select(a => a.Name));
            else aus.Add(el.Name);
        }
        return new ReaktionInfo(m.GetParameters()[0].ParameterType.Name, aus, strom);
    }

    private static string ArtVon(Type akteur)
    {
        var basen = akteur.GetInterfaces().Select(i => i.FullName).ToHashSet(StringComparer.Ordinal);
        return basen.Contains("Abstractions.IMensch") ? "Mensch" : basen.Contains("Abstractions.IMaschine") ? "Maschine"
            : basen.Contains("Abstractions.IKi") ? "Ki" : "";
    }

    /// <summary><c>ImagePairKomplett</c> → <c>image_pair_komplett</c> (Methodenname <c>auf_…</c>).</summary>
    public static string Snake(string name) => Regex.Replace(name, "(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", "_$1$2").ToLowerInvariant();

    private static string Python(List<VertragInfo> vertraege)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Akteur-Verträgen der Domäne (IAkteurVertrag<A>, docs/konzept-akteure.md §9).");
        b.AppendLine("# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.");
        b.AppendLine("#");
        b.AppendLine("# Je Vertrag eine abstrakte Basis: der Worker erbt sie und implementiert je Reaktion `auf_<event>`. Die Basis");
        b.AppendLine("# verdrahtet den Dispatch, prüft jedes Yield gegen den Ausgabe-Vertrag und meldet Vertrag + Hash am Handshake.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from abc import abstractmethod");
        b.AppendLine("from typing import AsyncIterator, ClassVar, TypeVar");
        b.AppendLine();
        b.AppendLine("from cqrs_client.router import MessageContext");
        b.AppendLine("from cqrs_client.vertrag import AkteurVertragBasis, Reaktion");
        b.AppendLine();
        var dtos = vertraege.SelectMany(v => v.Reaktionen.SelectMany(r => r.Ausgaenge.Append(r.Eingang)).Concat(v.Spontan))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (dtos.Count > 0)
        {
            b.AppendLine("from . import (");
            foreach (var d in dtos) b.AppendLine($"    {d}{Dto},");
            b.AppendLine(")");
            b.AppendLine();
        }
        b.AppendLine("S = TypeVar(\"S\")");

        foreach (var v in vertraege)
        {
            var kanon = Akteurvertrag.Kanon(v.Akteur.Name, v.Reaktionen.Select(r => new Akteurvertrag.Reaktion(r.Eingang, r.Ausgaenge, r.Strom)));
            b.AppendLine();
            b.AppendLine();
            b.AppendLine($"class {v.Akteur.Name}Basis(AkteurVertragBasis[S]):");
            b.AppendLine($"    \"\"\"Vertrag {v.Vertrag.FullName} — Akteur {v.Akteur.Name}{(v.Art.Length > 0 ? " (" + v.Art + ")" : "")}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    AKTEUR: ClassVar[str] = \"{v.Akteur.Name}\"");
            b.AppendLine($"    VERTRAG: ClassVar[str] = \"{v.Vertrag.Name}\"");
            b.AppendLine($"    VERTRAG_HASH: ClassVar[str] = \"{Akteurvertrag.Hash(kanon)}\"");
            b.AppendLine("    REAKTIONEN: ClassVar[dict] = {");
            foreach (var r in v.Reaktionen)
            {
                var aus = r.Ausgaenge.Count == 0 ? "()" : "(" + string.Join(", ", r.Ausgaenge.Select(a => a + Dto)) + ",)";
                b.AppendLine($"        {r.Eingang}{Dto}: Reaktion(\"auf_{Snake(r.Eingang)}\", {aus}{(r.Strom ? ", strom=True" : "")}),");
            }
            b.AppendLine("    }");
            b.AppendLine($"    SPONTAN: ClassVar[tuple] = {(v.Spontan.Count == 0 ? "()" : "(" + string.Join(", ", v.Spontan.Select(s => s + Dto)) + ",)")}   # IDarf-Commands: von sich aus");
            foreach (var r in v.Reaktionen)
            {
                var rueck = r.Ausgaenge.Count == 0 ? "None" : $"AsyncIterator[{string.Join(" | ", r.Ausgaenge.Select(a => a + Dto))}]";
                var vertragsZeile = $"{(r.Ausgaenge.Count == 0 ? "void" : (r.Strom ? "IAsyncEnumerable<OneOf<" : "OneOf<") + string.Join(", ", r.Ausgaenge) + (r.Strom ? ">>" : ">"))} Auf({r.Eingang} e)";
                b.AppendLine();
                b.AppendLine("    @abstractmethod");
                b.AppendLine($"    async def auf_{Snake(r.Eingang)}(self, e: {r.Eingang}{Dto}, ctx: MessageContext, state: S) -> {rueck}:");
                b.AppendLine($"        \"\"\"{vertragsZeile}{(r.Ausgaenge.Count == 0 ? " — nur zur Kenntnis" : r.Strom ? " — Strom: beliebig viele yields" : " — höchstens ein yield")}\"\"\"");
                b.AppendLine("        ...");
            }
        }
        b.AppendLine();
        b.AppendLine();
        b.AppendLine("__all__ = [");
        foreach (var v in vertraege) b.AppendLine($"    \"{v.Akteur.Name}Basis\",");
        b.AppendLine("]");
        return b.ToString();
    }

    private static IEnumerable<Type> TypenVon(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }
}
