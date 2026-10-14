namespace Werkbank.Server;

/// <summary>
/// Der Git-Branch eines Arbeitsverzeichnisses, ohne <c>git</c> aufzurufen: <c>.git/HEAD</c> lesen. In einem <c>git worktree</c> (und in
/// Submodulen) ist <c>.git</c> eine Datei <c>gitdir: &lt;pfad&gt;</c> — dann liegt <c>HEAD</c> dort. Ohne Repository gilt „main“ (gesperrt für
/// Gesten), ein losgelöster HEAD heißt „detached“. Per Link auch im Prüfstand übersetzt (rein, ohne Werkbank-Abhängigkeiten).
/// </summary>
public static class GitKopf
{
    private const string Ref = "ref: refs/heads/";

    public static string Branch(string wurzel)
    {
        try
        {
            var git = Path.Combine(wurzel, ".git");
            string? head = null;
            if (Directory.Exists(git)) head = Path.Combine(git, "HEAD");
            else if (File.Exists(git) && File.ReadAllText(git).Trim() is var zeiger && zeiger.StartsWith("gitdir:", StringComparison.Ordinal))
            {
                var verz = zeiger["gitdir:".Length..].Trim();
                head = Path.Combine(Path.IsPathRooted(verz) ? verz : Path.GetFullPath(Path.Combine(wurzel, verz)), "HEAD");
            }
            if (head is null || !File.Exists(head)) return "main";
            var inhalt = File.ReadAllText(head).Trim();
            return inhalt.StartsWith(Ref, StringComparison.Ordinal) ? inhalt[Ref.Length..] : "detached";
        }
        catch (IOException) { return "main"; }
        catch (UnauthorizedAccessException) { return "main"; }
    }
}
