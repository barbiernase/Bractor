using System;
using System.IO;
using FluentAssertions;
using Werkbank.Server;
using Xunit;

namespace Infrastructure.Pruefstand.Werkbank;

/// <summary>
/// Der Branch der Werkbank (eine Sim-DB je Branch, Gesten auf <c>main</c> gesperrt) kommt aus <c>.git/HEAD</c>. In einem <c>git worktree</c> ist
/// <c>.git</c> eine Datei mit <c>gitdir:</c> — früher galt die Werkbank dort als „main“ und sperrte jede Geste.
/// </summary>
public sealed class GitKopfTests : IDisposable
{
    private readonly string _verz = Directory.CreateTempSubdirectory("gitkopf-").FullName;

    public void Dispose() { try { Directory.Delete(_verz, true); } catch (IOException) { } }

    [Fact]
    public void Normales_Repository_liest_den_Branch_aus_dem_Verzeichnis()
    {
        Directory.CreateDirectory(Path.Combine(_verz, ".git"));
        File.WriteAllText(Path.Combine(_verz, ".git", "HEAD"), "ref: refs/heads/claude/werkbank-zielbild\n");

        GitKopf.Branch(_verz).Should().Be("claude/werkbank-zielbild");
    }

    [Fact]
    public void Worktree_folgt_dem_gitdir_Zeiger_relativ_und_absolut()
    {
        var gitdir = Path.Combine(_verz, "haupt", ".git", "worktrees", "wt");
        Directory.CreateDirectory(gitdir);
        File.WriteAllText(Path.Combine(gitdir, "HEAD"), "ref: refs/heads/abnahme\n");
        var wt = Path.Combine(_verz, "wt");
        Directory.CreateDirectory(wt);

        File.WriteAllText(Path.Combine(wt, ".git"), $"gitdir: {gitdir}\n");
        GitKopf.Branch(wt).Should().Be("abnahme");

        File.WriteAllText(Path.Combine(wt, ".git"), "gitdir: ../haupt/.git/worktrees/wt\n");
        GitKopf.Branch(wt).Should().Be("abnahme");
    }

    [Fact]
    public void Losgeloest_heisst_detached_ohne_Repository_main()
    {
        GitKopf.Branch(_verz).Should().Be("main", "ohne Repository bleiben Gesten gesperrt");

        Directory.CreateDirectory(Path.Combine(_verz, ".git"));
        File.WriteAllText(Path.Combine(_verz, ".git", "HEAD"), "2876a53f00\n");
        GitKopf.Branch(_verz).Should().Be("detached");
    }
}
