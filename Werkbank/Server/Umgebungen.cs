using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Abstractions;

namespace Werkbank.Server;

/// <summary>
/// Die Umgebungs-Dateien der Solution (<c>umgebungen/umgebung.&lt;name&gt;.json</c>, docs/konzept-werkbank.md §4): lesen, schreiben,
/// Platzhalter auflösen (<c>${branch}</c> = Git-Branch dieser Werkbank, <c>${env:NAME}</c>) und als <see cref="UmgebungKonfig"/> deuten.
/// Der Host bindet dieselbe Datei streng (UmgebungLader); hier liest nur die Werkbank, um Ports, DB und Pfade zu kennen.
/// </summary>
public sealed partial class Umgebungen(string slnRoot)
{
    public string Verzeichnis => Path.Combine(slnRoot, Abstractions.Projektstruktur.UmgebungsOrdner);

    public IReadOnlyList<string> Namen() => Directory.Exists(Verzeichnis)
        ? Directory.GetFiles(Verzeichnis, "umgebung.*.json").Select(d => Path.GetFileNameWithoutExtension(d)["umgebung.".Length..])
            .Where(n => n != "schema").OrderBy(n => n, StringComparer.Ordinal).ToList()
        : [];

    public string Datei(string name) => Path.Combine(Verzeichnis, $"umgebung.{name}.json");

    public JsonObject Roh(string name) => JsonNode.Parse(File.ReadAllText(Datei(name)),
        documentOptions: new() { CommentHandling = JsonCommentHandling.Skip })?.AsObject() ?? new JsonObject();

    /// <summary>Schreibt die Umgebung (hübsch eingerückt) — ein Wechsel im Host-Rahmen ist eine Änderung dieser Datei.</summary>
    public void Schreibe(string name, JsonObject inhalt)
        => File.WriteAllText(Datei(name), inhalt.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");

    /// <summary>Der Git-Branch der Werkbank (eine Sim-DB je Branch).</summary>
    public string Branch()
    {
        try
        {
            var head = File.ReadAllText(Path.Combine(slnRoot, ".git", "HEAD")).Trim();
            return head.StartsWith("ref: refs/heads/", StringComparison.Ordinal) ? head["ref: refs/heads/".Length..] : "detached";
        }
        catch { return "main"; }
    }

    public static string BranchSicher(string branch) => Regex.Replace(branch.ToLowerInvariant(), "[^a-z0-9_]", "_");

    [GeneratedRegex(@"\$\{env:([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Env();

    /// <summary>Die Umgebung mit aufgelösten Platzhaltern (wie der Host sie mit <c>CQRS_BRANCH</c> = Branch sieht).</summary>
    public UmgebungKonfig Aufgeloest(string name)
    {
        var text = File.ReadAllText(Datei(name)).Replace("${branch}", BranchSicher(Branch()), StringComparison.Ordinal);
        text = Env().Replace(text, m => JsonEncodedText.Encode(Environment.GetEnvironmentVariable(m.Groups[1].Value) ?? "").ToString());
        return JsonSerializer.Deserialize<UmgebungKonfig>(text, Optionen) ?? new UmgebungKonfig();
    }

    /// <summary>Ein Abschnitt der aufgelösten Umgebung roh (z. B. <c>Quellen</c>).</summary>
    public JsonObject AufgeloestRoh(string name)
    {
        var text = File.ReadAllText(Datei(name)).Replace("${branch}", BranchSicher(Branch()), StringComparison.Ordinal);
        text = Env().Replace(text, m => JsonEncodedText.Encode(Environment.GetEnvironmentVariable(m.Groups[1].Value) ?? "").ToString());
        return JsonNode.Parse(text, documentOptions: new() { CommentHandling = JsonCommentHandling.Skip })?.AsObject() ?? new JsonObject();
    }

    private static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };
}
