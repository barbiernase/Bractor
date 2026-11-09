using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using GraphExtractor;
using Xunit;

namespace Infrastructure.Pruefstand.Extractor;

/// <summary>
/// Die Geste „neuer Katalog“ (docs/07 §7.2) als Text-Patch: Projektdatei, Eintrag in der Solution samt Konfigurationen,
/// Referenz der Schreibseite (ihre Übersetzungen nehmen seine Nachrichten, docs/07 §7.2) — und „nutzen“ setzt bzw. entfernt nur diese
/// Referenz.
/// </summary>
public class KatalogeTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(Path.GetTempPath(), "katalog-" + Guid.NewGuid().ToString("N"));

    private const string Sln = """

        Microsoft Visual Studio Solution File, Format Version 12.00
        Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Domain", "Domain\Domain.csproj", "{34B2F279-904E-4625-A142-F22BA9434738}"
        EndProject
        Global
        	GlobalSection(SolutionConfigurationPlatforms) = preSolution
        		Debug|Any CPU = Debug|Any CPU
        		Release|x64 = Release|x64
        	EndGlobalSection
        	GlobalSection(ProjectConfigurationPlatforms) = postSolution
        		{34B2F279-904E-4625-A142-F22BA9434738}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        	EndGlobalSection
        EndGlobal

        """;

    private const string Schreibseite = """
        <Project Sdk="Microsoft.NET.Sdk">
            <ItemGroup>
              <ProjectReference Include="..\Abstractions\Abstractions.csproj" />
              <ProjectReference Include="..\Core\Core.csproj" />
            </ItemGroup>
        </Project>
        """;

    public KatalogeTests()
    {
        Directory.CreateDirectory(Path.Combine(_wurzel, "Domain"));
        File.WriteAllText(Path.Combine(_wurzel, "Probe.sln"), Sln.Replace("\r\n", "\n"));
        File.WriteAllText(Path.Combine(_wurzel, "Domain", "Domain.csproj"), Schreibseite.Replace("\r\n", "\n"));
    }

    public void Dispose() { try { Directory.Delete(_wurzel, true); } catch (IOException) { } }

    [Fact]
    public void Neuer_Katalog_schreibt_Projekt_Solution_und_Referenz_der_Schreibseite()
    {
        var puffer = new Dateipuffer(_wurzel);

        var projekt = Kataloge.Neu(puffer, "Bild");

        projekt.Should().Be("Bild.Katalog");
        var patch = puffer.Patch();
        patch.Select(p => p.Datei).Should().BeEquivalentTo("Bild.Katalog/Bild.Katalog.csproj", "Probe.sln", "Domain/Domain.csproj");
        puffer.Lies("Bild.Katalog/Bild.Katalog.csproj").Should().Contain(@"<ProjectReference Include=""..\Abstractions\Abstractions.csproj"" />")
            .And.NotContain("Domain\\Domain.csproj", "ein Katalog kennt keine Anwendung");
        var sln = puffer.Lies("Probe.sln")!;
        sln.Should().Contain(@"= ""Bild.Katalog"", ""Bild.Katalog\Bild.Katalog.csproj""");
        var id = System.Text.RegularExpressions.Regex.Match(sln, @"Bild\.Katalog\.csproj"", ""\{(?<id>[^}]+)\}""").Groups["id"].Value;
        sln.Should().Contain($"{{{id}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU").And.Contain($"{{{id}}}.Release|x64.Build.0 = Release|Any CPU");
        sln.IndexOf($"{{{id}}}.Release|x64.Build.0", StringComparison.Ordinal).Should().BeLessThan(sln.LastIndexOf("EndGlobalSection", StringComparison.Ordinal));
        puffer.Lies("Domain/Domain.csproj").Should().Contain(@"      <ProjectReference Include=""..\Bild.Katalog\Bild.Katalog.csproj"" />");
    }

    [Fact]
    public void Ein_Katalog_entsteht_nur_einmal_und_mit_gueltigem_Namen()
    {
        var puffer = new Dateipuffer(_wurzel);
        Kataloge.Neu(puffer, "Bild.Katalog");
        FluentActions.Invoking(() => Kataloge.Neu(puffer, "Bild")).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Kataloge.Projektname("bild")).Should().Throw<ArgumentException>();
        Kataloge.Projektname("Firma.Bild").Should().Be("Firma.Bild.Katalog");
    }

    [Fact]
    public void Nutzen_setzt_und_entfernt_nur_die_Referenz()
    {
        var puffer = new Dateipuffer(_wurzel);
        Kataloge.Nutzen(puffer, "Bild.Katalog", true);
        Kataloge.Nutzen(puffer, "Bild.Katalog", true);
        var mit = puffer.Lies("Domain/Domain.csproj")!;
        Abstractions.Projektstruktur.KatalogeIn(mit).Should().Equal("Bild.Katalog");

        Kataloge.Nutzen(puffer, "Bild.Katalog", false);

        puffer.Lies("Domain/Domain.csproj").Should().Be(Schreibseite.Replace("\r\n", "\n"));
    }
}
