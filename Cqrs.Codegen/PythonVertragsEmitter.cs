using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Abstractions;

namespace Cqrs.Codegen;

/// <summary>
/// Apps → Python (<c>docs/konzept-akteure.md</c> §4, §5): je App (<c>record X : IApp&lt;TClient&gt;</c>) eine abstrakte Klasse <c>XBasis</c> in
/// <c>domain_client/generated/vertraege.py</c>, gegen die die App programmiert — aus den Akteuren ihres Clients, ohne die Nachrichten ihrer Quellen:
/// je Event oder Hinweis, den einer von ihnen hört, eine Methode <c>auf_&lt;event&gt;</c>. Sie reagiert mit Commands des <c>IDarf</c> (Schritt 6: ein Decide gibt nur Fakten aus,
/// die Folgen stößt ein Client an) oder gibt nichts aus (Kenntnis); fehlt eine, lässt sich der Client nicht bauen (ABC). Gelesen wird nur die Signatur (Metadaten der gebauten Domain-DLLs).
/// Der Hash kommt aus derselben Quelle wie auf dem Server (<see cref="Clientvertragsregeln"/>) — so erkennt der Handshake einen Client, der gegen einen
/// anderen Stand gebaut wurde.
/// </summary>
public static class PythonVertragsEmitter
{
    private const string AppName = "Abstractions.IApp`1";
    private const string UebersetztName = "Abstractions.UebersetztAttribute`1";
    private const string TranslateName = "Abstractions.ITranslate`2";
    private const string VerkoerpertName = "Abstractions.IVerkoerpert`1";
    private const string HoertName = "Abstractions.IHoert`1";
    private const string DarfName = "Abstractions.IDarf`1";
    private const string Dto = Core.SourceGeneration.NameSanitizer.DtoSuffix;   // die Namensregel der Leitung

    private sealed record ClientInfo(Type Typ, string Name, string Client, List<string> Akteure, List<string> Hoert,
        List<string> Sendet, List<string> Reagiert, List<string> Fragt, string Hash);

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
        foreach (var app in typen.Where(t => t.IsClass && !t.IsAbstract && Generisch(t, AppName).Count == 1)
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var c = Generisch(app, AppName)[0];
            var name = Clientvertragsregeln.ClientName(app.Name);
            var akteure = Generisch(c, VerkoerpertName).OrderBy(a => a.Name, StringComparer.Ordinal).ToList();
            List<Type> U(Func<Type, IEnumerable<Type>> f) => akteure.SelectMany(f).Distinct().ToList();
            // EINE Regel mit dem Server (Clientvertragsregeln.Art / Hoerbar): was die Akteure hineingeben und was zu ihnen kommt.
            List<Type> Darf(Type a, Clientvertragsregeln.Befugnisart art) =>
                Generisch(a, DarfName).Where(t => Clientvertragsregeln.Art(m => Hat(t, m), t.IsInterface) == art).ToList();
            // Die Ziele ihrer Quellen (je Parameter des primären Konstruktors [Uebersetzt<U>], U : ITranslate<Meldung, Ziel>) setzt nicht die App um.
            var ziele = app.GetConstructors().SelectMany(k => k.GetParameters()).SelectMany(p => p.GetCustomAttributesData())
                .Where(a => a.AttributeType.IsGenericType && a.AttributeType.GetGenericTypeDefinition().FullName == UebersetztName)
                .Select(a => a.AttributeType.GetGenericArguments()[0])
                .SelectMany(u => u.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == TranslateName)
                    .Select(i => i.GetGenericArguments()[1])).ToHashSet();
            var sendet = U(a => Darf(a, Clientvertragsregeln.Befugnisart.Sendet)).Where(t => !ziele.Contains(t)).ToList();
            var fragt = U(a => Darf(a, Clientvertragsregeln.Befugnisart.Fragt));
            var hoert = U(a => Generisch(a, HoertName).Where(t => Clientvertragsregeln.Hoerbar(m => Hat(t, m))));
            var kanon = Clientvertragsregeln.ClientKanon(name, akteure.Select(a => a.Name), sendet.Select(t => t.Name), fragt.Select(t => t.Name),
                hoert.Select(t => t.Name));
            clients.Add(new ClientInfo(app, name, c.Name, akteure.Select(a => a.Name).ToList(),
                hoert.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(), sendet.Select(t => t.Name).ToList(),
                sendet.Where(t => Hat(t, Clientvertragsregeln.ICommand)).Select(t => t.Name).ToList(), fragt.Select(t => t.Name).ToList(),
                Clientvertragsregeln.Hash(kanon)));
        }
        return (Python(clients), clients.Count);
    }

    /// <summary><c>ImagePairKomplett</c> → <c>image_pair_komplett</c> (Methodenname <c>auf_…</c>).</summary>
    public static string Snake(string name) => Regex.Replace(name, "(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", "_$1$2").ToLowerInvariant();

    private static string Python(List<ClientInfo> clients)
    {
        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Apps der Domäne (record X : IApp<Client>, docs/konzept-akteure.md §4).");
        b.AppendLine("# Nicht von Hand ändern — ./codegen.sh --force erzeugt die Datei neu, ./codegen.sh --check prüft auf Drift.");
        b.AppendLine("#");
        b.AppendLine("# Je App eine abstrakte Basis aus den Akteuren ihres Clients (ohne die Nachrichten ihrer Quellen): je gehörtem Event oder Hinweis eine");
        b.AppendLine("# Methode `auf_<event>` — die Reaktion (Commands des IDarf, gern in Stücken: yield) oder nichts (Kenntnis). Die Basis verdrahtet den");
        b.AppendLine("# Dispatch und meldet App + Hash am Handshake.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from abc import abstractmethod");
        b.AppendLine("from typing import AsyncIterator, ClassVar, TypeVar");
        b.AppendLine();
        b.AppendLine("from cqrs_client.router import MessageContext");
        b.AppendLine("from cqrs_client.vertrag import ClientVertragBasis");
        b.AppendLine();
        var dtos = clients.SelectMany(c => c.Hoert.Concat(c.Sendet).Concat(c.Fragt))
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
            b.AppendLine($"    \"\"\"App {c.Typ.FullName} des Clients {c.Client} — verkörpert {string.Join(", ", c.Akteure.DefaultIfEmpty("keinen Akteur"))}.\"\"\"");
            b.AppendLine();
            b.AppendLine($"    APP: ClassVar[str] = \"{c.Name}\"");
            b.AppendLine($"    CLIENT: ClassVar[str] = \"{c.Client}\"");
            b.AppendLine($"    AKTEURE: ClassVar[tuple] = {(c.Akteure.Count == 0 ? "()" : "(" + string.Join(", ", c.Akteure.Select(a => $"\"{a}\"")) + (c.Akteure.Count == 1 ? ",)" : ")"))}");
            b.AppendLine($"    VERTRAG_HASH: ClassVar[str] = \"{c.Hash}\"");
            if (c.Hoert.Count == 0) b.AppendLine("    HOERT: ClassVar[dict] = {}");
            else
            {
                b.AppendLine("    HOERT: ClassVar[dict] = {   # IHoert eines Events oder Hinweises: Reaktion oder Kenntnis");
                foreach (var h in c.Hoert) b.AppendLine($"        {h}{Dto}: \"auf_{Snake(h)}\",");
                b.AppendLine("    }");
            }
            b.AppendLine($"    SPONTAN: ClassVar[tuple] = {Tupel(c.Sendet)}   # IDarf der Akteure: Commands und Hinweise");
            b.AppendLine($"    FRAGT: ClassVar[tuple] = {Tupel(c.Fragt)}   # IDarf der Akteure: ihre Queries");
            foreach (var h in c.Hoert)
            {
                b.AppendLine();
                b.AppendLine("    @abstractmethod");
                var reaktion = string.Join(" | ", c.Reagiert.Select(a => a + Dto));
                b.AppendLine($"    async def auf_{Snake(h)}(self, e: {h}{Dto}, ctx: MessageContext, state: S) -> {(reaktion.Length == 0 ? "None" : $"AsyncIterator[{reaktion}] | None")}:");
                b.AppendLine($"        \"\"\"IHoert<{h}> — die Reaktion: {string.Join(", ", c.Reagiert.DefaultIfEmpty("nichts"))} (ein Wert oder Stücke, yield); oder nichts (Kenntnis)\"\"\"");
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
