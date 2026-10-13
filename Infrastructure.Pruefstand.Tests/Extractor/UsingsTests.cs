using FluentAssertions;
using GraphExtractor;
using Xunit;

namespace Infrastructure.Pruefstand.Extractor;

/// <summary>Werkbank P7.5-Abnahme (3): der Schreiber sortiert neue <c>using</c> ein, statt sie voranzustellen.</summary>
public class UsingsTests
{
    [Fact]
    public void Neue_using_landen_an_ihrer_Stelle_System_zuerst()
    {
        var alt = "using Abstractions;\nusing Domain.Zeta;\n\nnamespace Domain.X;\n\npublic sealed record A;\n";
        Usings.Ergaenze(alt, ["Domain.Alpha", "System.Text", "Domain.Zeta"]).Should().Be(
            "using System.Text;\nusing Abstractions;\nusing Domain.Alpha;\nusing Domain.Zeta;\n\nnamespace Domain.X;\n\npublic sealed record A;\n");
    }

    [Fact]
    public void Hinter_der_letzten_wenn_alle_davor_liegen_und_nichts_wenn_alles_da_ist()
    {
        var alt = "using Abstractions;\n\nnamespace Domain.X;\n";
        Usings.Ergaenze(alt, ["Domain.B", "Domain.A"]).Should().Be("using Abstractions;\nusing Domain.A;\nusing Domain.B;\n\nnamespace Domain.X;\n");
        Usings.Ergaenze(alt, ["Abstractions"]).Should().BeSameAs(alt);
    }

    [Fact]
    public void Ohne_vorhandene_using_sortiert_am_Anfang_mit_Leerzeile_und_Zeilenende_der_Datei()
    {
        Usings.Ergaenze("namespace Domain.X;\n", ["Domain.B", "Domain.A"]).Should().Be("using Domain.A;\nusing Domain.B;\n\nnamespace Domain.X;\n");
        Usings.Ergaenze("using Abstractions;\r\n\r\nnamespace Domain.X;\r\n", ["Domain.A"]).Should().Be("using Abstractions;\r\nusing Domain.A;\r\n\r\nnamespace Domain.X;\r\n");
        Usings.Ergaenze("using Abstractions;", ["Domain.A"]).Should().Be("using Abstractions;\nusing Domain.A;");
    }
}
