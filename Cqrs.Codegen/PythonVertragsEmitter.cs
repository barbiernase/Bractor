using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Abstractions;

namespace Cqrs.Codegen;

/// <summary>
/// Clients → Python (<c>docs/konzept-akteure.md</c> §4, §5): je <c>record X : IClient</c> eine abstrakte Klasse <c>XBasis</c> in
/// <c>domain_client/generated/vertraege.py</c>, gegen die der Client programmiert — aus den Akteuren, die er verkörpert: je Command, den einer von ihnen
/// hört (Arbeit), eine Methode <c>auf_&lt;command&gt;</c>, die seine Antwort liefert (Commands über das <c>IDarf</c> des Akteurs, gern in Stücken); je
/// Event oder Hinweis, den einer hört (Kenntnis), eine Methode <c>auf_&lt;event&gt;</c> ohne Ausgabe. Wer einen Akteur verkörpert, muss seine Arbeit
/// annehmen können: fehlt eine Methode, lässt sich der Client nicht bauen (ABC). Gelesen wird nur die Signatur (Metadaten der gebauten Domain-DLLs).
/// Der Hash kommt aus derselben Quelle wie auf dem Server (<see cref="Clientvertragsregeln"/>) — so erkennt der Handshake einen Client, der gegen einen
/// anderen Stand gebaut wurde.
/// </summary>
public static class PythonVertragsEmitter
{
    private const string ClientName = "Abstractions.IClient";
    private const string VerkoerpertName = "Abstractions.IVerkoerpert`1";
    private const string HoertName = "Abstractions.IHoert`1";
    private const string DarfName = "Abstractions.IDarf`1";
    private const string Dto = Core.SourceGeneration.NameSanitizer.DtoSuffix;   // die Namensregel der Leitung

    private sealed record Arbeit(string Akteur, List<(string Command, List<string> Antworten)> Eingaenge);
    private sealed record ClientInfo(Type Typ, string Name, List<string> Akteure, List<Arbeit> Arbeit, List<string> Hoert,
        List<string> Sendet, List<string> Fragt, string Hash);

    public static IEnumerable<Type> TypenVon(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; }
    }

    private static bool Hat(Type t, string m) =>
        t.FullName == m || t.GetInterfaces().Any(i => (i.IsGenericType ? i.GetGenericTypeDefinition() : i).FullName == m);

    private static List<Type> Generisch(Type t, string meta) => t.GetInterfaces()
        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == meta).Select(i => i.GetGenericArguments()[0]).Distinct().ToList();

    public static (string Inhalt, int Anzahl) Emit(IEnumerable<Assembly> assemblies)
    {
        var typen = assemblies.SelectMany(TypenVon).ToList();
        var clients = new List<ClientInfo>();
        foreach (var c in typen.Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.FullName == ClientName))
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var name = Clientvertragsregeln.ClientName(c.Name);
            var akteure = Generisch(c, VerkoerpertName).OrderBy(a => a.Name, StringComparer.Ordinal).ToList();
            List<Type> U(Func<Type, IEnumerable<Type>> f) => akteure.SelectMany(f).Distinct().ToList();
            // EINE Regel mit dem Server (Clientvertragsregeln.Art / IstArbeit): was die Akteure hineingeben und was zu ihnen kommt.
            List<Type> Darf(Type a, Clientvertragsregeln.Befugnisart art) =>
                Generisch(a, DarfName).Where(t => Clientvertragsregeln.Art(m => Hat(t, m), t.IsInterface) == art).ToList();
            var sendet = U(a => Darf(a, Clientvertragsregeln.Befugnisart.Sendet));
            var fragt = U(a => Darf(a, Clientvertragsregeln.Befugnisart.Fragt));
            var hoert = U(a => Generisch(a, HoertName).Where(t => !Clientvertragsregeln.IstArbeit(m => Hat(t, m))));
            var arbeit = akteure.Select(a => new Arbeit(a.Name, Generisch(a, HoertName).Where(t => Clientvertragsregeln.IstArbeit(m => Hat(t, m)))
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .Select(t => (t.Name, Darf(a, Clientvertragsregeln.Befugnisart.Sendet).Where(x => Hat(x, Clientvertragsregeln.ICommand))
                        .Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToList())).ToList()))
                .Where(x => x.Eingaenge.Count > 0).ToList();
            var kanon = Clientvertragsregeln.ClientKanon(name, akteure.Select(a => a.Name),
                arbeit.Select(w => new KeyValuePair<string, IEnumerable<Clientvertragsregeln.Eingang>>(w.Akteur,
                    w.Eingaenge.Select(e => new Clientvertragsregeln.Eingang(e.Command, e.Antworten, true)))),
                sendet.Select(t => t.Name), fragt.Select(t => t.Name), hoert.Select(t => t.Name));
            clients.Add(new ClientInfo(c, name, akteure.Select(a => a.Name).ToList(), arbeit,
                hoert.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(), sendet.Select(t => t.Name).ToList(),
                fragt.Select(t => t.Name).ToList(), Clientvertragsregeln.Hash(kanon)));
        }
        return (Python(clients), clients.Count);
    }

    /// <summary><c>ImagePairKomplett</c> → <c>image_pair_komplett</c> (Methodenname <c>auf_…</c>).</summary>
    public static string Snake(string name) => Regex.Replace(name, "(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", "_$1$2").ToLowerInvariant();

    private static string Python(List<ClientInfo> clients)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Clients der Domäne (record X : IClient, docs/konzept-akteure.md §4).");
        b.AppendLine("# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.");
        b.AppendLine("#");
        b.AppendLine("# Je Client eine abstrakte Basis aus den Akteuren, die er verkörpert: je gehörtem Command (Arbeit) eine Methode `auf_<command>`, die die");
        b.AppendLine("# Antwort liefert — Commands über das IDarf des Akteurs, gern in Stücken (yield) —, und je gehörtem Event oder Hinweis (Kenntnis) eine");
        b.AppendLine("# Methode `auf_<event>` ohne Ausgabe. Die Basis nimmt die Arbeit an, verdrahtet den Dispatch und meldet Client + Hash am Handshake.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from abc import abstractmethod");
        b.AppendLine("from typing import AsyncIterator, ClassVar, TypeVar");
        b.AppendLine();
        b.AppendLine("from cqrs_client.arbeit import Arbeit, Herkunft");
        b.AppendLine("from cqrs_client.router import MessageContext");
        b.AppendLine("from cqrs_client.vertrag import ClientVertragBasis");
        b.AppendLine();
        var dtos = clients.SelectMany(c => c.Arbeit.SelectMany(w => w.Eingaenge.SelectMany(e => e.Antworten.Append(e.Command)))
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
            b.AppendLine($"    \"\"\"Client {c.Typ.FullName} — verkörpert {string.Join(", ", c.Akteure.DefaultIfEmpty("keinen Akteur"))}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    CLIENT: ClassVar[str] = \"{c.Name}\"");
            b.AppendLine($"    AKTEURE: ClassVar[tuple] = {(c.Akteure.Count == 0 ? "()" : "(" + string.Join(", ", c.Akteure.Select(a => $"\"{a}\"")) + (c.Akteure.Count == 1 ? ",)" : ")"))}");
            b.AppendLine($"    VERTRAG_HASH: ClassVar[str] = \"{c.Hash}\"");
            if (c.Arbeit.Count == 0) b.AppendLine("    ARBEIT: ClassVar[dict] = {}");
            else
            {
                b.AppendLine("    ARBEIT: ClassVar[dict] = {   # IHoert eines Commands ohne Ziel: Arbeit des Akteurs, die Antwort sind Commands seines IDarf");
                foreach (var w in c.Arbeit)
                {
                    b.AppendLine($"        \"{w.Akteur}\": {{");
                    foreach (var (cmd, antworten) in w.Eingaenge)
                        b.AppendLine($"            {cmd}{Dto}: Arbeit(\"auf_{Snake(cmd)}\", {Tupel(antworten)}),");
                    b.AppendLine("        },");
                }
                b.AppendLine("    }");
            }
            if (c.Hoert.Count == 0) b.AppendLine("    HOERT: ClassVar[dict] = {}");
            else
            {
                b.AppendLine("    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Kenntnis, verlierbar");
                foreach (var h in c.Hoert) b.AppendLine($"        {h}{Dto}: \"auf_{Snake(h)}\",");
                b.AppendLine("    }");
            }
            b.AppendLine($"    SPONTAN: ClassVar[tuple] = {Tupel(c.Sendet)}   # IDarf der Akteure: Commands und Hinweise");
            b.AppendLine($"    FRAGT: ClassVar[tuple] = {Tupel(c.Fragt)}   # IDarf der Akteure: ihre Queries");
            foreach (var w in c.Arbeit)
                foreach (var (cmd, antworten) in w.Eingaenge)
                {
                    b.AppendLine();
                    b.AppendLine("    @abstractmethod");
                    b.AppendLine($"    async def auf_{Snake(cmd)}(self, c: {cmd}{Dto}, h: Herkunft, state: S) -> AsyncIterator[{string.Join(" | ", antworten.Select(a => a + Dto).DefaultIfEmpty("None"))}]:");
                    b.AppendLine($"        \"\"\"Arbeit des {w.Akteur} (IHoert<{cmd}>): die Antwort — {string.Join(", ", antworten.DefaultIfEmpty("nichts"))}; ein Wert, eine Liste oder Stücke (yield)\"\"\"");
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
