using System.Text;
using Core.SourceGeneration;
using Abstractions.SourceGeneration;

namespace Cqrs.Codegen;

/// <summary>
/// Die Nutzlast-Tabelle für Python (<c>domain_client/generated/nutzlast_tabelle.py</c>): <c>Nutzlast.typ</c> → DTO-Klasse des generierten
/// Domänen-Moduls, mit festen Importen. Gegenstück zu <c>GeneratedNutzlast</c> auf der C#-Seite (<c>DtoMapperGenerator</c>): dieselben
/// Kategorien (Command, Event, Query, Antwort, Trigger, Auftrag) und dieselbe Namensregel (<see cref="NameSanitizer.NutzlastTyp"/>) — der
/// Python-Client baut die Tabelle nicht mehr zur Laufzeit aus dem Modul.
/// </summary>
public static class PythonNutzlastEmitter
{
    public static (string Inhalt, int Anzahl) Emit(params IEnumerable<(string TypeName, int Depth, TypeNode Node)>[] kategorien)
    {
        var tabelle = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, _, node) in kategorien.SelectMany(k => k))
        {
            if (node.IsEnum) continue;
            var typ = NameSanitizer.NutzlastTyp(node.FullName);
            if (tabelle.TryGetValue(typ, out var vorhanden))
                throw new InvalidOperationException($"Nutzlast-Typ '{typ}' doppelt ({node.FullName}) — Typnamen der Leitung müssen eindeutig sein");
            tabelle[typ] = NameSanitizer.DtoName(node.FullName);
        }

        var b = new StringBuilder();
        b.AppendLine("# GENERIERT von Cqrs.Codegen aus den Nachrichten der Domäne (Command, Event, Query, Antwort, Trigger, Auftrag). Nicht von Hand ändern.");
        b.AppendLine("#");
        b.AppendLine("# Die Nutzlast-Tabelle der Leitung (Cqrs.Leitung/cqrs.proto, Nutzlast.typ): typ → DTO-Klasse des Domänen-Moduls. Gegenstück zu");
        b.AppendLine("# GeneratedNutzlast (C#) mit derselben Namensregel; cqrs_client.NutzlastTabelle packt und entpackt nur über diese Tabelle.");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        if (tabelle.Count > 0)
        {
            b.AppendLine("from . import (");
            foreach (var dto in tabelle.Values.Distinct().OrderBy(d => d, StringComparer.Ordinal)) b.AppendLine($"    {dto},");
            b.AppendLine(")");
            b.AppendLine();
        }
        b.AppendLine("TABELLE: dict[str, type] = {");
        foreach (var (typ, dto) in tabelle) b.AppendLine($"    \"{typ}\": {dto},");
        b.AppendLine("}");
        return (b.ToString(), tabelle.Count);
    }
}
