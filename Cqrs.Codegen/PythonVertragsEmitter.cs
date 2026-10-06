using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Abstractions;

namespace Cqrs.Codegen;

/// <summary>
/// Akteur-Verträge → Python (<c>docs/konzept-akteure.md</c> §6): je <c>interface IX : IAkteurVertrag&lt;X&gt;</c> eine abstrakte
/// Klasse <c>XBasis</c> in <c>domain_client/generated/vertraege.py</c>, gegen die der Worker programmiert. Gelesen wird nur die
/// Signatur (Metadaten der gebauten Domain-DLLs, wie der übrige Prepass): je <c>Auf(E)</c> der Eingang und die Ausgänge aus dem
/// Rückgabetyp. Der Vertrags-Hash kommt aus derselben Quelle wie auf dem Server (<see cref="Akteurvertrag"/>) — so erkennt der
/// Handshake einen Worker, der gegen einen anderen Stand gebaut wurde.
/// </summary>
public static class PythonVertragsEmitter
{
    private const string VertragName = "Abstractions.IAkteurVertrag`1";
    private const string ClientVertragName = "Abstractions.IClientVertrag";
    private const string SendetName = "Abstractions.ISendet`1";
    private const string FragtName = "Abstractions.IFragt`1";
    private const string DarfName = "Abstractions.IDarf`1";
    private const string CommandName = "Abstractions.ICommand";
    private const string Dto = "Dto";

    private sealed record ZusageInfo(string Eingang, List<string> Ausgaenge, bool Strom, string Akteur = "");
    private sealed record VertragInfo(Type Vertrag, Type Akteur, string Art, List<ZusageInfo> Zusagen, List<string> Spontan);
    private sealed record ClientInfo(Type Vertrag, string Name, List<string> Akteure, List<ZusageInfo> Zusagen,
        List<string> Sendet, List<string> Fragt, string Hash);

    private static bool IstClient(Type t) => t.GetInterfaces().Any(i => i.FullName == ClientVertragName);

    /// <summary>Der Akteur eines Vertrags-Teils (<c>IAkteurVertrag&lt;A&gt;</c> → A), sonst null.</summary>
    private static Type? AkteurVon(Type teil) => teil.GetInterfaces()
        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == VertragName)?.GetGenericArguments()[0];

    private static List<ZusageInfo> Zusagen(Type teil, string akteur = "") => teil.GetMethods()
        .Where(m => m.Name == Akteurvertrag.Auf && m.GetParameters().Length == 1)
        .OrderBy(m => m.MetadataToken)
        .Select(m => Zusage(m) with { Akteur = akteur }).ToList();

    public static (string Inhalt, int Anzahl) Emit(IEnumerable<Assembly> assemblies)
    {
        var interfaces = assemblies.SelectMany(TypenVon).Where(t => t.IsInterface && t.Namespace != "Abstractions")
            .OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
        // Akteur → seine Vertrags-Teile (ohne Client-Verträge): der Akteur-Vertrag ist die Vereinigung (je Event einmal, CQRS061).
        var teile = interfaces.Where(t => !IstClient(t) && AkteurVon(t) != null).GroupBy(t => AkteurVon(t)!.FullName)
            .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.Ordinal);
        var vertraege = new List<VertragInfo>();
        foreach (var (_, ts) in teile.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var akteur = AkteurVon(ts[0])!;
            var zusagen = ts.SelectMany(t => Zusagen(t)).GroupBy(r => r.Eingang).Select(g => g.First()).ToList();
            // Name der Basis: der Teil, der alle Zusagen sieht (eigene + geerbte), sonst der erste.
            var haupt = ts.FirstOrDefault(t => new[] { t }.Concat(t.GetInterfaces().Where(ts.Contains)).SelectMany(x => Zusagen(x))
                .Select(r => r.Eingang).Distinct().Count() == zusagen.Count) ?? ts[0];
            vertraege.Add(new VertragInfo(haupt, akteur, ArtVon(akteur), zusagen, Darf(akteur, CommandName)));
        }

        var clients = new List<ClientInfo>();
        foreach (var c in interfaces.Where(IstClient))
        {
            var name = Akteurvertrag.ClientName(c.Name);
            var basen = new[] { c }.Concat(c.GetInterfaces().Where(IstClient)).Distinct().ToList();
            var getragen = c.GetInterfaces().Where(i => !IstClient(i) && AkteurVon(i) != null).ToList();
            var jeAkteur = getragen.GroupBy(t => AkteurVon(t)!.Name).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (Akteur: g.Key, Zusagen: g.SelectMany(t => Zusagen(t, g.Key)).GroupBy(r => r.Eingang).Select(x => x.First()).ToList()))
                .ToList();
            var kenntnis = basen.SelectMany(b => Zusagen(b)).GroupBy(r => r.Eingang).Select(g => g.First()).ToList();
            List<Type> Generisch(string meta) => basen.SelectMany(b => b.GetInterfaces())
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == meta).Select(i => i.GetGenericArguments()[0]).Distinct().ToList();
            var sendet = Generisch(SendetName);
            var fragt = Generisch(FragtName);
            // verkörpert: Akteure der Teile ∪ je Sendet/Fragt die IDarf-Halter, wenn keiner der Teil-Akteure den Typ schon darf (wie der Server).
            var basis = jeAkteur.Select(x => x.Akteur).ToHashSet(StringComparer.Ordinal);
            var akteure = new SortedSet<string>(basis, StringComparer.Ordinal);
            var alleAkteure = assemblies.SelectMany(TypenVon).Where(t => !t.IsInterface && !t.IsAbstract).ToList();
            foreach (var t in sendet.Concat(fragt))
            {
                var halter = alleAkteure.Where(a => a.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == DarfName
                                                                             && i.GetGenericArguments()[0] == t)).Select(a => a.Name).ToList();
                if (!halter.Any(basis.Contains)) akteure.UnionWith(halter);
            }
            var kanon = Akteurvertrag.ClientKanon(name,
                jeAkteur.Select(x => new KeyValuePair<string, IEnumerable<Akteurvertrag.Zusage>>(x.Akteur,
                    x.Zusagen.Select(r => new Akteurvertrag.Zusage(r.Eingang, r.Ausgaenge, r.Strom)))),
                sendet.Select(t => t.Name), fragt.Select(t => t.Name), kenntnis.Select(r => r.Eingang));
            clients.Add(new ClientInfo(c, name, akteure.ToList(), jeAkteur.SelectMany(x => x.Zusagen).Concat(kenntnis).ToList(),
                sendet.Select(t => t.Name).ToList(), fragt.Select(t => t.Name).ToList(), Akteurvertrag.Hash(kanon)));
        }
        return (Python(vertraege, clients), vertraege.Count + clients.Count);
    }

    /// <summary>Die <c>IDarf&lt;T&gt;</c> eines Akteurs mit T : <paramref name="marker"/> (einfache Namen).</summary>
    private static List<string> Darf(Type akteur, string marker) => akteur.GetInterfaces()
        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == DarfName)
        .Select(i => i.GetGenericArguments()[0])
        .Where(x => x.GetInterfaces().Any(i => i.FullName == marker))
        .Select(x => x.Name).ToList();

    private static ZusageInfo Zusage(MethodInfo m)
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
        return new ZusageInfo(m.GetParameters()[0].ParameterType.Name, aus, strom);
    }

    private static string ArtVon(Type akteur)
    {
        var basen = akteur.GetInterfaces().Select(i => i.FullName).ToHashSet(StringComparer.Ordinal);
        return basen.Contains("Abstractions.IMensch") ? "Mensch" : basen.Contains("Abstractions.IMaschine") ? "Maschine"
            : basen.Contains("Abstractions.IKi") ? "Ki" : "";
    }

    /// <summary><c>ImagePairKomplett</c> → <c>image_pair_komplett</c> (Methodenname <c>auf_…</c>).</summary>
    public static string Snake(string name) => Regex.Replace(name, "(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", "_$1$2").ToLowerInvariant();

    private static string Python(List<VertragInfo> vertraege, List<ClientInfo> clients)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Akteur-Verträgen der Domäne (IAkteurVertrag<A>, docs/konzept-akteure.md §3).");
        b.AppendLine("# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.");
        b.AppendLine("#");
        b.AppendLine("# Je Vertrag eine abstrakte Basis: der Worker erbt sie und implementiert je Zusage `auf_<event>`. Die Basis");
        b.AppendLine("# verdrahtet den Dispatch, prüft jedes Yield gegen den Ausgabe-Vertrag und meldet Vertrag + Hash am Handshake.");
        b.AppendLine("# Client-Verträge (IClientVertrag, docs/konzept-akteure.md §4): je Client eine Basis `<Client>Basis` — ein Client");
        b.AppendLine("# kann mehrere Akteure verkörpern; jede Zusage nennt den Akteur, in dessen Namen sie antwortet.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from abc import abstractmethod");
        b.AppendLine("from typing import AsyncIterator, ClassVar, TypeVar");
        b.AppendLine();
        b.AppendLine("from cqrs_client.router import MessageContext");
        b.AppendLine("from cqrs_client.vertrag import AkteurVertragBasis, Zusage");
        b.AppendLine();
        var dtos = vertraege.SelectMany(v => v.Zusagen.SelectMany(r => r.Ausgaenge.Append(r.Eingang)).Concat(v.Spontan))
            .Concat(clients.SelectMany(c => c.Zusagen.SelectMany(r => r.Ausgaenge.Append(r.Eingang)).Concat(c.Sendet).Concat(c.Fragt)))
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
            var kanon = Akteurvertrag.Kanon(v.Akteur.Name, v.Zusagen.Select(r => new Akteurvertrag.Zusage(r.Eingang, r.Ausgaenge, r.Strom)));
            b.AppendLine();
            b.AppendLine();
            b.AppendLine($"class {v.Akteur.Name}Basis(AkteurVertragBasis[S]):");
            b.AppendLine($"    \"\"\"Vertrag {v.Vertrag.FullName} — Akteur {v.Akteur.Name}{(v.Art.Length > 0 ? " (" + v.Art + ")" : "")}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    AKTEUR: ClassVar[str] = \"{v.Akteur.Name}\"");
            b.AppendLine($"    VERTRAG: ClassVar[str] = \"{v.Vertrag.Name}\"");
            b.AppendLine($"    VERTRAG_HASH: ClassVar[str] = \"{Akteurvertrag.Hash(kanon)}\"");
            b.AppendLine("    ZUSAGEN: ClassVar[dict] = {");
            foreach (var r in v.Zusagen)
            {
                var aus = r.Ausgaenge.Count == 0 ? "()" : "(" + string.Join(", ", r.Ausgaenge.Select(a => a + Dto)) + ",)";
                b.AppendLine($"        {r.Eingang}{Dto}: Zusage(\"auf_{Snake(r.Eingang)}\", {aus}{(r.Strom ? ", strom=True" : "")}),");
            }
            b.AppendLine("    }");
            b.AppendLine($"    SPONTAN: ClassVar[tuple] = {(v.Spontan.Count == 0 ? "()" : "(" + string.Join(", ", v.Spontan.Select(s => s + Dto)) + ",)")}   # IDarf-Commands: von sich aus");
            foreach (var r in v.Zusagen) Methode(b, r);
        }

        foreach (var c in clients)
        {
            b.AppendLine();
            b.AppendLine();
            b.AppendLine($"class {c.Name}Basis(AkteurVertragBasis[S]):");
            b.AppendLine($"    \"\"\"Client-Vertrag {c.Vertrag.FullName} — verkörpert {string.Join(", ", c.Akteure)}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    CLIENT: ClassVar[str] = \"{c.Name}\"");
            b.AppendLine($"    AKTEURE: ClassVar[tuple] = ({string.Join(", ", c.Akteure.Select(a => $"\"{a}\""))}{(c.Akteure.Count == 1 ? "," : "")})");
            b.AppendLine($"    VERTRAG: ClassVar[str] = \"{c.Vertrag.Name}\"");
            b.AppendLine($"    VERTRAG_HASH: ClassVar[str] = \"{c.Hash}\"");
            b.AppendLine("    ZUSAGEN: ClassVar[dict] = {");
            foreach (var r in c.Zusagen)
            {
                var aus = r.Ausgaenge.Count == 0 ? "()" : "(" + string.Join(", ", r.Ausgaenge.Select(a => a + Dto)) + ",)";
                b.AppendLine($"        {r.Eingang}{Dto}: Zusage(\"auf_{Snake(r.Eingang)}\", {aus}{(r.Strom ? ", strom=True" : "")}{(r.Akteur.Length > 0 ? $", akteur=\"{r.Akteur}\"" : "")}),");
            }
            b.AppendLine("    }");
            b.AppendLine($"    SPONTAN: ClassVar[tuple] = {Tupel(c.Sendet)}   # ISendet: von sich aus");
            b.AppendLine($"    FRAGT: ClassVar[tuple] = {Tupel(c.Fragt)}   # IFragt: die Queries des Clients");
            foreach (var r in c.Zusagen) Methode(b, r);
        }
        b.AppendLine();
        b.AppendLine();
        b.AppendLine("__all__ = [");
        foreach (var v in vertraege) b.AppendLine($"    \"{v.Akteur.Name}Basis\",");
        foreach (var c in clients) b.AppendLine($"    \"{c.Name}Basis\",");
        b.AppendLine("]");
        return b.ToString();
    }

    private static string Tupel(List<string> typen) => typen.Count == 0 ? "()" : "(" + string.Join(", ", typen.Select(s => s + Dto)) + ",)";

    private static void Methode(StringBuilder b, ZusageInfo r)
    {
        var rueck = r.Ausgaenge.Count == 0 ? "None" : $"AsyncIterator[{string.Join(" | ", r.Ausgaenge.Select(a => a + Dto))}]";
        var vertragsZeile = $"{(r.Ausgaenge.Count == 0 ? "void" : (r.Strom ? "IAsyncEnumerable<OneOf<" : "OneOf<") + string.Join(", ", r.Ausgaenge) + (r.Strom ? ">>" : ">"))} Auf({r.Eingang} e)";
        b.AppendLine();
        b.AppendLine("    @abstractmethod");
        b.AppendLine($"    async def auf_{Snake(r.Eingang)}(self, e: {r.Eingang}{Dto}, ctx: MessageContext, state: S) -> {rueck}:");
        b.AppendLine($"        \"\"\"{vertragsZeile}{(r.Akteur.Length > 0 ? $" — als {r.Akteur}" : "")}{(r.Ausgaenge.Count == 0 ? " — nur zur Kenntnis" : r.Strom ? " — Strom: beliebig viele yields" : " — höchstens ein yield")}\"\"\"");
        b.AppendLine("        ...");
    }

    private static IEnumerable<Type> TypenVon(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }
}
