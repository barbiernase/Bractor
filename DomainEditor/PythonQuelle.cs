using System.Text.RegularExpressions;

namespace DomainEditor;

/// <summary>
/// Python-Quelltext eines CLIENTS lesen und chirurgisch ändern (docs/konzept-akteure.md §4): die Implementierung eines Client-Vertrags
/// ist eine Klasse, die die generierte Basis <c>&lt;Client&gt;Basis</c> erbt; je Zusage/Kenntnis eine Methode <c>auf_…</c>. Nur Code-Fakten:
/// Klassen und ihre Basisliste, Methoden über ihren Namen aus der generierten Basis — kein Raten über Dateinamen. Reiner Text (kein I/O),
/// eingerückt wie Python es verlangt: der Rumpf einer Methode sind die Zeilen unter dem <c>def</c>, die tiefer eingerückt sind.
/// </summary>
public static class PythonQuelle
{
    public sealed record Klasse(string Name, IReadOnlyList<string> Basen, int Zeile);
    public sealed record Methode(string Name, int DefZeile, int RumpfStart, int RumpfEnde, string Einzug);
    /// <summary>Eine Zusage/Kenntnis der generierten Client-Basis: Event, Methodenname, die <c>def</c>-Zeile (Signatur) und der Akteur.</summary>
    public sealed record BasisAuf(string Event, string Methode, string Def, string? Akteur, bool Strom, IReadOnlyList<string> Ausgaenge);
    public sealed record ClientBasis(string Client, string Basis, IReadOnlyList<BasisAuf> Auf);

    private static readonly Regex KlassenKopf = new(@"^class\s+(\w+)\s*(?:\((.*)\))?\s*:", RegexOptions.Compiled);
    private static readonly Regex DefKopf = new(@"^(\s+)(?:async\s+)?def\s+(\w+)\s*\(", RegexOptions.Compiled);

    private static string[] Zeilen(string text) => text.Replace("\r\n", "\n").Split('\n');
    private static int Einzug(string z) => z.Length - z.TrimStart(' ', '\t').Length;

    /// <summary>Die Klassen auf oberster Ebene mit ihrer Basisliste (Generics/Typ-Argumente abgeschnitten: <c>XBasis[S]</c> → <c>XBasis</c>).</summary>
    public static IReadOnlyList<Klasse> Klassen(string text)
    {
        var erg = new List<Klasse>();
        var z = Zeilen(text);
        for (var i = 0; i < z.Length; i++)
            if (KlassenKopf.Match(z[i]) is { Success: true } m)
                erg.Add(new(m.Groups[1].Value, Basen(m.Groups[2].Value), i));
        return erg;
    }

    private static IReadOnlyList<string> Basen(string liste)
    {
        var teile = new List<string>();
        var tiefe = 0; var start = 0;
        for (var i = 0; i <= liste.Length; i++)
        {
            if (i == liste.Length || (liste[i] == ',' && tiefe == 0))
            {
                var t = liste[start..i].Trim();
                if (t.Length > 0) teile.Add(Regex.Replace(t, @"\[.*$", "").Split('.').Last().Trim());
                start = i + 1;
            }
            else if (liste[i] is '[' or '(') tiefe++;
            else if (liste[i] is ']' or ')') tiefe--;
        }
        return teile;
    }

    /// <summary>Die Methode <paramref name="methode"/> der Klasse <paramref name="klasse"/>: Zeilen des Rumpfs [RumpfStart, RumpfEnde).</summary>
    public static Methode? FindeMethode(string text, string klasse, string methode)
    {
        var z = Zeilen(text);
        var k = Klassen(text).FirstOrDefault(x => x.Name == klasse);
        if (k == null) return null;
        for (var i = k.Zeile + 1; i < z.Length; i++)
        {
            if (z[i].Trim().Length > 0 && Einzug(z[i]) == 0 && !z[i].TrimStart().StartsWith('#')) break;   // Klasse zu Ende
            if (DefKopf.Match(z[i]) is not { Success: true } m || m.Groups[2].Value != methode) continue;
            var defEinzug = m.Groups[1].Value.Length;
            // Mehrzeilige Signatur: bis zur Zeile, die mit „:" endet (Klammern ausgeglichen).
            var j = i; var tiefe = 0;
            for (; j < z.Length; j++)
            {
                foreach (var c in z[j]) { if (c is '(' or '[') tiefe++; else if (c is ')' or ']') tiefe--; }
                if (tiefe <= 0 && Regex.IsMatch(z[j], @":\s*(#.*)?$")) break;
            }
            var start = j + 1; var ende = start;
            for (var r = start; r < z.Length; r++)
            {
                if (z[r].Trim().Length == 0) continue;
                if (Einzug(z[r]) <= defEinzug) break;
                ende = r + 1;
            }
            var einzug = ende > start ? new string(' ', z.Skip(start).Take(ende - start).Where(x => x.Trim().Length > 0).Min(Einzug)) : new string(' ', defEinzug + 4);
            return new(methode, i, start, ende, einzug);
        }
        return null;
    }

    /// <summary>Der Rumpf einer Methode, ohne gemeinsamen Einzug (null = Methode nicht da).</summary>
    public static string? Rumpf(string text, string klasse, string methode)
    {
        if (FindeMethode(text, klasse, methode) is not { } m) return null;
        var z = Zeilen(text);
        return Dedent(string.Join("\n", z.Skip(m.RumpfStart).Take(m.RumpfEnde - m.RumpfStart)));
    }

    /// <summary>Den Rumpf ersetzen (Signatur und alles andere bleibt Wort für Wort). Null = Methode nicht da.</summary>
    public static string? SetzeRumpf(string text, string klasse, string methode, string rumpf)
    {
        if (FindeMethode(text, klasse, methode) is not { } m) return null;
        var z = Zeilen(text).ToList();
        var neu = Eingerueckt(rumpf, m.Einzug);
        z.RemoveRange(m.RumpfStart, m.RumpfEnde - m.RumpfStart);
        z.InsertRange(m.RumpfStart, neu);
        return string.Join("\n", z);
    }

    /// <summary>Eine fehlende Methode ans Ende der Klasse hängen: <paramref name="def"/> ist die Signatur-Zeile (ohne Einzug), wie die Basis sie vorgibt.</summary>
    public static string? FuegeMethodeEin(string text, string klasse, string def, string rumpf)
    {
        var z = Zeilen(text).ToList();
        var k = Klassen(text).FirstOrDefault(x => x.Name == klasse);
        if (k == null) return null;
        var ende = k.Zeile + 1;
        for (var i = k.Zeile + 1; i < z.Count; i++)
        {
            if (z[i].Trim().Length > 0 && Einzug(z[i]) == 0 && !z[i].TrimStart().StartsWith('#')) break;
            if (z[i].Trim().Length > 0) ende = i + 1;
        }
        var block = new List<string> { "", "    " + def.Trim() };
        block.AddRange(Eingerueckt(rumpf, "        "));
        z.InsertRange(ende, block);
        return string.Join("\n", z);
    }

    private static List<string> Eingerueckt(string rumpf, string einzug)
    {
        var zeilen = Dedent(rumpf.Replace("\r\n", "\n")).Split('\n').ToList();
        if (zeilen.All(x => x.Trim().Length == 0)) zeilen = ["pass"];
        return zeilen.Select(x => x.Trim().Length == 0 ? "" : einzug + x).ToList();
    }

    public static string Dedent(string s)
    {
        var zeilen = s.Replace("\r\n", "\n").Split('\n');
        var min = zeilen.Where(z => z.Trim().Length > 0).Select(Einzug).DefaultIfEmpty(0).Min();
        return string.Join("\n", zeilen.Select(z => z.Length >= min ? z[min..] : z.TrimStart())).Trim('\n');
    }

    // ── Die GENERIERTE Basis (vertraege.py, Cqrs.Codegen): je Client-Basis CLIENT, ZUSAGEN und die auf_-Signaturen ──
    private static readonly Regex ClientWert = new(@"^\s+CLIENT\s*:\s*ClassVar\[str\]\s*=\s*""(\w+)""", RegexOptions.Compiled);
    private static readonly Regex ZusageEintrag = new(@"^\s+(\w+)\s*:\s*Zusage\(\s*""(\w+)""\s*,\s*\(([^)]*)\)(.*)\)\s*,?\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Die Client-Basen der generierten <c>vertraege.py</c>. <paramref name="dtoEndung"/> ist die Endung, mit der der Generator die
    /// Nachrichten-Typen benennt (Proto-DTO) — der Event-Name ist der Typ ohne sie.
    /// </summary>
    public static IReadOnlyList<ClientBasis> ClientBasen(string vertraege, string dtoEndung = "Dto")
    {
        var erg = new List<ClientBasis>();
        var z = Zeilen(vertraege);
        string Ohne(string t) => t.EndsWith(dtoEndung, StringComparison.Ordinal) ? t[..^dtoEndung.Length] : t;
        foreach (var k in Klassen(vertraege))
        {
            string? client = null;
            var auf = new List<BasisAuf>();
            for (var i = k.Zeile + 1; i < z.Length; i++)
            {
                if (z[i].Trim().Length > 0 && Einzug(z[i]) == 0) break;
                if (ClientWert.Match(z[i]) is { Success: true } c) client = c.Groups[1].Value;
                else if (ZusageEintrag.Match(z[i]) is { Success: true } e)
                {
                    var methode = e.Groups[2].Value;
                    var rest = e.Groups[4].Value;
                    var akteur = Regex.Match(rest, @"akteur\s*=\s*""(\w+)""") is { Success: true } a ? a.Groups[1].Value : null;
                    var ausg = e.Groups[3].Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Select(Ohne).ToList();
                    var def = FindeMethode(vertraege, k.Name, methode) is { } m ? DefText(z, m) : $"async def {methode}(self, e, ctx, state):";
                    auf.Add(new(Ohne(e.Groups[1].Value), methode, def, akteur, rest.Contains("strom=True"), ausg));
                }
            }
            if (client != null) erg.Add(new(client, k.Name, auf));
        }
        return erg;
    }

    /// <summary>
    /// Eine NEUE Client-Implementierung: Klasse, die die generierte Basis erbt, je Zusage/Kenntnis die Methode mit der Signatur der Basis
    /// und einem Platzhalter-Rumpf (<c>raise NotImplementedError</c> — 📝/🤖 füllt ihn), dazu ein leerer Zustand. <paramref name="paket"/> =
    /// Python-Paketname (Verzeichnisname, z. B. <c>domain_client</c>).
    /// </summary>
    public static string NeueImplementierung(string paket, ClientBasis basis, string klasse, string dtoEndung = "Dto")
    {
        var dtos = basis.Auf.SelectMany(a => a.Ausgaenge.Append(a.Event)).Distinct().OrderBy(x => x, StringComparer.Ordinal).Select(x => x + dtoEndung).ToList();
        var zustand = klasse + "State";
        var b = new System.Text.StringBuilder();
        b.AppendLine($"\"\"\"{basis.Client} — Implementierung des Client-Vertrags (Basis {basis.Basis}, generiert aus dem Vertrag).\"\"\"");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from dataclasses import dataclass");
        b.AppendLine("from typing import AsyncIterator  # noqa: F401");
        b.AppendLine();
        b.AppendLine("from cqrs_client.router import MessageContext  # noqa: F401");
        if (dtos.Count > 0) b.AppendLine($"from {paket}.generated import {string.Join(", ", dtos)}  # noqa: F401");
        b.AppendLine($"from {paket}.generated.vertraege import {basis.Basis}");
        b.AppendLine();
        b.AppendLine();
        b.AppendLine("@dataclass");
        b.AppendLine($"class {zustand}:");
        b.AppendLine("    pass");
        b.AppendLine();
        b.AppendLine();
        b.AppendLine($"class {klasse}({basis.Basis}[{zustand}]):");
        if (basis.Auf.Count == 0) b.AppendLine("    pass");
        foreach (var a in basis.Auf)
        {
            b.AppendLine($"    {Regex.Replace(a.Def.Trim(), @"\bS\b(?=[,)\]])", zustand)}");
            b.AppendLine("        raise NotImplementedError");
            b.AppendLine();
        }
        return b.ToString().TrimEnd() + "\n";
    }

    // ── Die GENERIERTE Basis der Katalog-Funktionen (funktionen.py, Cqrs.Codegen): je Basis FUNKTION und die rufe-Signatur (Plan E13) ──
    private static readonly Regex FunktionWert = new(@"^\s+FUNKTION\s*:\s*ClassVar\[str\]\s*=\s*""(\w+)""", RegexOptions.Compiled);

    /// <summary>Eine Funktions-Basis der generierten <c>funktionen.py</c>: Funktion (Interface-Name), Basis-Klasse und die <c>def rufe</c>-Zeile.</summary>
    public sealed record FunktionsBasis(string Funktion, string Basis, string Def, IReadOnlyList<string> Dtos);

    public static IReadOnlyList<FunktionsBasis> FunktionsBasen(string funktionenPy)
    {
        var erg = new List<FunktionsBasis>();
        var z = Zeilen(funktionenPy);
        foreach (var k in Klassen(funktionenPy))
        {
            string? funktion = null;
            for (var i = k.Zeile + 1; i < z.Length; i++)
            {
                if (z[i].Trim().Length > 0 && Einzug(z[i]) == 0) break;
                if (FunktionWert.Match(z[i]) is { Success: true } f) funktion = f.Groups[1].Value;
            }
            if (funktion == null) continue;
            var def = FindeMethode(funktionenPy, k.Name, Abstractions.Funktionsvertrag.PythonMethode) is { } m ? DefText(z, m)
                : "async def rufe(self, auftrag, x):";
            var dtos = Regex.Matches(def, @"\b(\w+Dto)\b").Select(x => x.Groups[1].Value).Distinct().ToList();
            erg.Add(new(funktion, k.Name, def, dtos));
        }
        return erg;
    }

    /// <summary>
    /// Eine NEUE Python-Implementierung einer Funktion: Klasse, die die generierte Basis erbt, <c>rufe</c> mit der Signatur der Basis und dem
    /// Platzhalter <c>raise NotImplementedError</c> (✎/🤖 füllt ihn). <paramref name="paket"/> = das Python-Paket der Generate (<c>domain_client</c>).
    /// </summary>
    public static string NeueFunktionsImplementierung(string paket, FunktionsBasis basis, string klasse)
    {
        var b = new System.Text.StringBuilder();
        b.AppendLine($"\"\"\"{basis.Funktion} — Python-Implementierung der Katalog-Funktion (Laufort Extern; Basis {basis.Basis}, generiert aus der Signatur).\"\"\"");
        b.AppendLine("from __future__ import annotations");
        b.AppendLine();
        b.AppendLine("from cqrs_client.funktion import Ausfuehrung  # noqa: F401");
        if (basis.Dtos.Count > 0) b.AppendLine($"from {paket}.generated import {string.Join(", ", basis.Dtos.OrderBy(x => x, StringComparer.Ordinal))}  # noqa: F401");
        b.AppendLine($"from {paket}.generated.funktionen import {basis.Basis}");
        b.AppendLine();
        b.AppendLine();
        b.AppendLine($"class {klasse}({basis.Basis}):");
        b.AppendLine($"    {basis.Def.Trim()}");
        b.AppendLine("        raise NotImplementedError");
        return b.ToString();
    }

    private static string DefText(string[] z, Methode m) =>
        string.Join(" ", z.Skip(m.DefZeile).Take(m.RumpfStart - m.DefZeile).Select(x => x.Trim()));
}
