using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Abstractions;
using Infrastructure.Umgebung;
using Microsoft.Extensions.Configuration;

namespace Werkbank.Server;

/// <summary>
/// Die Umgebungs-Dateien der Solution (<c>umgebungen/&lt;name&gt;/umgebung.json</c>, docs/06 §6.5): lesen, als Text für eine
/// Geste aufbereiten und auflösen. Aufgelöst wird mit DERSELBEN Regel wie im Host (<see cref="UmgebungLader.LadeDatei"/>: <c>${branch}</c> =
/// Git-Branch dieser Werkbank, <c>${env:NAME}</c> — fehlt die Variable, ist das ein Fehler —, Umgebungsvariablen überschreiben, strenge
/// Bindung); Tokens nach <see cref="AkteurTokens"/>.
/// </summary>
public sealed class Umgebungen(string slnRoot)
{
    public string Verzeichnis => Path.Combine(slnRoot, Abstractions.Projektstruktur.UmgebungsOrdner);

    public IReadOnlyList<string> Namen() => Directory.Exists(Verzeichnis)
        ? Directory.GetDirectories(Verzeichnis).Where(d => File.Exists(Path.Combine(d, Abstractions.Projektstruktur.UmgebungsDatei)))
            .Select(d => Path.GetFileName(d)).OrderBy(n => n, StringComparer.Ordinal).ToList()
        : [];

    public string Datei(string name) => Path.Combine(Verzeichnis, name, Abstractions.Projektstruktur.UmgebungsDatei);

    /// <summary>Die Datei relativ zur Solution (Pfad im Patch einer Geste).</summary>
    public string RelativeDatei(string name) => Path.GetRelativePath(slnRoot, Datei(name)).Replace('\\', '/');

    /// <summary>Die Datei unaufgelöst (Platzhalter bleiben stehen) — das bearbeitet das Formular des Host-Rahmens.</summary>
    public JsonObject Roh(string name) => JsonNode.Parse(File.ReadAllText(Datei(name)),
        documentOptions: new() { CommentHandling = JsonCommentHandling.Skip })?.AsObject() ?? new JsonObject();

    /// <summary>Der Dateitext für einen Inhalt (hübsch eingerückt) — geschrieben wird er als Geste (Verlauf, ↶, Branch-Sperre).</summary>
    public static string Text(JsonObject inhalt)
        => inhalt.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";

    /// <summary>Der Git-Branch der Werkbank (eine Sim-DB je Branch) — auch in einem <c>git worktree</c>.</summary>
    public string Branch() => GitKopf.Branch(slnRoot);

    public static string BranchSicher(string branch) => Regex.Replace(branch.ToLowerInvariant(), "[^a-z0-9_]", "_");

    /// <summary>Die Umgebung aufgelöst, wie der Host sie mit <c>CQRS_BRANCH</c> = Branch sieht.</summary>
    public UmgebungKonfig Aufgeloest(string name) => MitKonfig(name, (u, _) => u);

    /// <summary>Der beobachtete Pfad je Instanz mit einem Quellknoten, der einen <c>Pfad</c> hat (ein Dateiwächter; aufgelöst) — Schlüssel ist die Instanz.</summary>
    public IReadOnlyDictionary<string, string> QuellPfade(string name) => MitKonfig(name, (u, _) => u.Graphen
        .SelectMany(i => i.Value.Knoten.Values.Where(k => k.Werte.TryGetValue("Pfad", out var p) && !string.IsNullOrWhiteSpace(p)).Select(k => (i.Key, k.Werte["Pfad"])))
        .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Item2, StringComparer.Ordinal));

    /// <summary>
    /// <c>Identitaeten:Tokens</c>: Token → Akteur(e) — gelesen wie im Host (<c>AkteurOptionen</c>: Wert oder Liste, je Eintrag
    /// <see cref="AkteurTokens.Akteure"/>).
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Tokens(string name) => MitKonfig(name, (_, k) =>
        k.GetSection(UmgebungsAbschnitte.Identitaeten).GetSection("Tokens").GetChildren().ToDictionary(t => t.Key, t =>
            (IReadOnlyList<string>)(t.Value is { } wert ? AkteurTokens.Akteure(wert) : [])
                .Concat(t.GetChildren().SelectMany(e => e.Value is { } a ? AkteurTokens.Akteure(a) : [])).Distinct(StringComparer.Ordinal).ToList(),
            StringComparer.Ordinal));

    private T MitKonfig<T>(string name, Func<UmgebungKonfig, IConfiguration, T> lies)
    {
        using var k = new ConfigurationManager();
        var u = UmgebungLader.LadeDatei(k, Datei(name), name, [], Branch());
        return lies(u, k);
    }
}
