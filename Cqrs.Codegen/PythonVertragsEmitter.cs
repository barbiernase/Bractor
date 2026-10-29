using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Abstractions;

namespace Cqrs.Codegen;

/// <summary>
/// Client-Verträge → Python (<c>docs/konzept-akteure.md</c> §4, docs/konzept-akteure.md §5): je <c>interface IX : IClientVertrag</c> eine
/// abstrakte Klasse <c>XBasis</c> in <c>domain_client/generated/vertraege.py</c>, gegen die der Client programmiert — je getragener Funktion
/// (<c>ITraegt&lt;F&gt;</c>) eine Methode, die ihre Ausgaben zurückgibt, je Kenntnis (<c>IHoert&lt;T&gt;</c>) eine Methode <c>auf_&lt;event&gt;</c>.
/// Gelesen wird nur die Signatur (Metadaten der gebauten Domain-DLLs). Der Vertrags-Hash kommt aus derselben Quelle wie auf dem Server
/// (<see cref="Clientvertragsregeln"/>) — so erkennt der Handshake einen Client, der gegen einen anderen Stand gebaut wurde.
/// </summary>
public static class PythonVertragsEmitter
{
    private const string ClientVertragName = "Abstractions.IClientVertrag";
    private const string SendetName = "Abstractions.ISendet`1";
    private const string FragtName = "Abstractions.IFragt`1";
    private const string HoertName = "Abstractions.IHoert`1";
    private const string TraegtName = "Abstractions.ITraegt`1";
    private const string DarfName = "Abstractions.IDarf`1";
    private const string Dto = Core.SourceGeneration.NameSanitizer.DtoSuffix;   // die Namensregel der Leitung

    private sealed record Getragen(PythonFunktionsform.Funktion Funktion, List<(PythonFunktionsform.Auf Auf, string Methode)> Aufs);
    private sealed record ClientInfo(Type Vertrag, string Name, List<string> Akteure, List<Getragen> Traegt, List<string> Hoert,
        List<string> Sendet, List<string> Fragt, string Hash);

    private static bool IstClient(Type t) => t.GetInterfaces().Any(i => i.FullName == ClientVertragName);

    /// <summary>
    /// Der Methodenname einer getragenen Funktion: der Funktions-Name ohne I in snake_case (<c>IBildpaarBeurteilung</c> → <c>bildpaar_beurteilung</c>),
    /// bei mehreren <c>Auf</c> dazu der Eingang. Kenntnis heißt <c>auf_&lt;event&gt;</c> — beides kollidiert nie.
    /// </summary>
    public static string Methode(Type funktion, PythonFunktionsform.Auf auf, int aufs) =>
        Snake(Funktionsvertrag.OhneI(funktion.Name)) + (aufs > 1 ? "_" + Snake(auf.Eingang) : "");

    public static (string Inhalt, int Anzahl) Emit(IEnumerable<Assembly> assemblies)
    {
        var typen = assemblies.SelectMany(PythonFunktionsform.TypenVon).ToList();
        var interfaces = typen.Where(t => t.IsInterface && t.Namespace != "Abstractions").OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
        var akteure = typen.Where(t => !t.IsInterface && !t.IsAbstract).ToList();
        var clients = new List<ClientInfo>();
        foreach (var c in interfaces.Where(IstClient))
        {
            var name = Clientvertragsregeln.ClientName(c.Name);
            var basen = new[] { c }.Concat(c.GetInterfaces().Where(IstClient)).Distinct().ToList();
            List<Type> Generisch(string meta) => basen.SelectMany(b => b.GetInterfaces())
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == meta).Select(i => i.GetGenericArguments()[0]).Distinct().ToList();
            var sendet = Generisch(SendetName);
            var fragt = Generisch(FragtName);
            var hoert = Generisch(HoertName);
            var traegt = Generisch(TraegtName).OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(PythonFunktionsform.Lies).Where(f => f != null).Select(f => f!)
                .Select(f => new Getragen(f, f.Aufs.Select(a => (a, Methode(f.Typ, a, f.Aufs.Count))).ToList())).ToList();
            // verkörpert: EINE Regel mit dem Server (Clientvertragsregeln.Verkoerpert).
            var verk = Clientvertragsregeln.Verkoerpert(traegt.Select(g => g.Funktion.Typ).Concat(sendet).Concat(fragt).Select(t =>
                akteure.Where(a => a.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == DarfName
                                                             && i.GetGenericArguments()[0] == t)).Select(a => a.Name)));
            var kanon = Clientvertragsregeln.ClientKanon(name,
                traegt.Select(g => new KeyValuePair<string, IEnumerable<Clientvertragsregeln.Eingang>>(g.Funktion.Typ.Name,
                    g.Funktion.Aufs.Select(a => new Clientvertragsregeln.Eingang(a.Eingang, a.Ausgaben.Select(x => x.Typ).ToList(), a.Strom)))),
                sendet.Select(t => t.Name), fragt.Select(t => t.Name), hoert.Select(t => t.Name));
            clients.Add(new ClientInfo(c, name, verk, traegt, hoert.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(),
                sendet.Select(t => t.Name).ToList(), fragt.Select(t => t.Name).ToList(), Clientvertragsregeln.Hash(kanon)));
        }
        return (Python(clients), clients.Count);
    }

    /// <summary><c>ImagePairKomplett</c> → <c>image_pair_komplett</c> (Methodenname <c>auf_…</c>).</summary>
    public static string Snake(string name) => Regex.Replace(name, "(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", "_$1$2").ToLowerInvariant();

    private static string Python(List<ClientInfo> clients)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Client-Verträgen der Domäne (IClientVertrag, docs/konzept-akteure.md §4).");
        b.AppendLine("# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.");
        b.AppendLine("#");
        b.AppendLine("# Je Client eine abstrakte Basis: der Client erbt sie und füllt je getragener Funktion (ITraegt) eine Methode, die die Ausgaben");
        b.AppendLine("# ihrer Signatur zurückgibt, und je Kenntnis (IHoert) eine Methode `auf_<event>`. Die Basis bietet die Funktionen an, verdrahtet");
        b.AppendLine("# den Dispatch und meldet Client + Hash am Handshake. Ein Client kann mehrere Akteure verkörpern.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from abc import abstractmethod");
        b.AppendLine("from typing import AsyncIterator, ClassVar, TypeVar");
        b.AppendLine();
        b.AppendLine("from cqrs_client.funktion import Ausgabe, Eingang, Herkunft");
        b.AppendLine("from cqrs_client.router import MessageContext");
        b.AppendLine("from cqrs_client.vertrag import ClientVertragBasis");
        b.AppendLine();
        var dtos = clients.SelectMany(c => c.Traegt.SelectMany(g => g.Funktion.Aufs.SelectMany(a => a.Ausgaben.Select(x => x.Typ).Append(a.Eingang)))
                .Concat(c.Hoert).Concat(c.Sendet).Concat(c.Fragt))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (dtos.Count > 0)
        {
            b.AppendLine("from . import (");
            foreach (var d in dtos) b.AppendLine($"    {d}{Dto},");
            b.AppendLine(")");
            b.AppendLine();
        }
        b.AppendLine("S = TypeVar(\"S\")");

        foreach (var c in clients)
        {
            b.AppendLine();
            b.AppendLine();
            b.AppendLine($"class {c.Name}Basis(ClientVertragBasis[S]):");
            b.AppendLine($"    \"\"\"Client-Vertrag {c.Vertrag.FullName} — verkörpert {string.Join(", ", c.Akteure.DefaultIfEmpty("keinen Akteur"))}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    CLIENT: ClassVar[str] = \"{c.Name}\"");
            b.AppendLine($"    AKTEURE: ClassVar[tuple] = {(c.Akteure.Count == 0 ? "()" : "(" + string.Join(", ", c.Akteure.Select(a => $"\"{a}\"")) + (c.Akteure.Count == 1 ? ",)" : ")"))}");
            b.AppendLine($"    VERTRAG: ClassVar[str] = \"{c.Vertrag.Name}\"");
            b.AppendLine($"    VERTRAG_HASH: ClassVar[str] = \"{c.Hash}\"");
            if (c.Traegt.Count == 0) b.AppendLine("    TRAEGT: ClassVar[dict] = {}");
            else
            {
                b.AppendLine("    TRAEGT: ClassVar[dict] = {   # ITraegt: Funktionen, deren Code der Client ausführt");
                foreach (var g in c.Traegt)
                {
                    b.AppendLine($"        \"{g.Funktion.Typ.Name}\": {{");
                    foreach (var (a, m) in g.Aufs)
                        b.AppendLine($"            {a.Eingang}{Dto}: Eingang(\"{m}\", {PythonFunktionsform.Ausgaben(a, Dto)}{(a.Strom ? ", strom=True" : "")}{(a.Hinweis ? ", hinweis=True" : "")}),");
                    b.AppendLine("        },");
                }
                b.AppendLine("    }");
            }
            if (c.Hoert.Count == 0) b.AppendLine("    HOERT: ClassVar[dict] = {}");
            else
            {
                b.AppendLine("    HOERT: ClassVar[dict] = {   # IHoert: Kenntnis, verlierbar");
                foreach (var h in c.Hoert) b.AppendLine($"        {h}{Dto}: \"auf_{Snake(h)}\",");
                b.AppendLine("    }");
            }
            b.AppendLine($"    SPONTAN: ClassVar[tuple] = {Tupel(c.Sendet)}   # ISendet: von sich aus");
            b.AppendLine($"    FRAGT: ClassVar[tuple] = {Tupel(c.Fragt)}   # IFragt: die Queries des Clients");
            foreach (var g in c.Traegt)
                foreach (var (a, m) in g.Aufs)
                {
                    b.AppendLine();
                    b.AppendLine("    @abstractmethod");
                    b.AppendLine($"    async def {m}(self, e: {a.Eingang}{Dto}, h: Herkunft, state: S) -> {PythonFunktionsform.Rueckgabe(a, Dto)}:");
                    b.AppendLine($"        \"\"\"{g.Funktion.Typ.Name}: {PythonFunktionsform.Zeile(a)}{(a.Strom ? " — Strom: beliebig viele yields" : " — höchstens eine Ausgabe")}\"\"\"");
                    b.AppendLine("        ...");
                }
            foreach (var h in c.Hoert)
            {
                b.AppendLine();
                b.AppendLine("    @abstractmethod");
                b.AppendLine($"    async def auf_{Snake(h)}(self, e: {h}{Dto}, ctx: MessageContext, state: S) -> None:");
                b.AppendLine($"        \"\"\"IHoert<{h}> — nur zur Kenntnis\"\"\"");
                b.AppendLine("        ...");
            }
        }
        b.AppendLine();
        b.AppendLine();
        b.AppendLine("__all__ = [");
        foreach (var c in clients) b.AppendLine($"    \"{c.Name}Basis\",");
        b.AppendLine("]");
        return b.ToString();
    }

    private static string Tupel(List<string> typen) => typen.Count == 0 ? "()" : "(" + string.Join(", ", typen.Select(s => s + Dto)) + ",)";
}
