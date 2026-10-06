using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Trainingslauf;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.GrpcClient;
using Xunit;

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Akteur-Verträge am Handshake und die Kausalität einer Reaktion von außen (<c>docs/konzept-akteure.md</c> §9.6/9.7) — rein, ohne
/// gRPC/Cluster. Dazu der Abgleich der zwei Generate: der Vertrags-Hash im Python-Generat (Cqrs.Codegen) = der in der Server-Tabelle.
/// </summary>
public class AkteurVertragTests
{
    private static AkteurRechte R(string name) => GeneratedAkteurRechte.Alle[name];
    private static string Hash(string name) => R(name).VertragHash;

    [Fact]
    public void Ohne_Vertrag_bleibt_alles_wie_bisher()
        => AkteurVertragsPruefung.Pruefe("", "", null, GeneratedAkteurRechte.Alle, streng: true).Should().Be(new AkteurVertragsPruefung.Ergebnis(null, null, null));

    [Fact]
    public void Bekannter_Vertrag_mit_passendem_Hash_wird_angenommen()
    {
        var e = AkteurVertragsPruefung.Pruefe("Klassifizierer", Hash("Klassifizierer"), null, GeneratedAkteurRechte.Alle, streng: true);
        e.Vertrag!.Name.Should().Be("Klassifizierer");
        e.Ablehnung.Should().BeNull();
        e.Warnung.Should().BeNull();
    }

    [Fact]
    public void Unbekannter_Vertrag_oder_Akteur_ohne_Vertrag_wird_abgelehnt()
    {
        AkteurVertragsPruefung.Pruefe("Gibtsnicht", "x", null, GeneratedAkteurRechte.Alle, false).Ablehnung.Should().Contain("unbekannt");
        AkteurVertragsPruefung.Pruefe("Inspekteur", "x", null, GeneratedAkteurRechte.Alle, false).Ablehnung.Should().Contain("unbekannt",
            "der Inspekteur handelt nur spontan — er hat keinen Vertrag");
    }

    [Fact]
    public void Mit_Tor_muss_das_Token_den_Vertrags_Akteur_verkoerpern()
    {
        AkteurVertragsPruefung.Pruefe("Klassifizierer", Hash("Klassifizierer"), R("Inspekteur"), GeneratedAkteurRechte.Alle, false)
            .Ablehnung.Should().Contain("verkörpert Inspekteur");
        var mehrere = AkteurRechte.Vereinige([R("Inspekteur"), R("Klassifizierer")]);
        AkteurVertragsPruefung.Pruefe("Klassifizierer", Hash("Klassifizierer"), mehrere, GeneratedAkteurRechte.Alle, false)
            .Vertrag.Should().NotBeNull("eine Session, die mehrere Akteure verkörpert, darf einen davon als Vertrag nennen");
    }

    [Fact]
    public void Abweichender_Hash_warnt_und_wird_streng_abgelehnt()
    {
        var lax = AkteurVertragsPruefung.Pruefe("TrainingsSystem", "0000000000000000", null, GeneratedAkteurRechte.Alle, streng: false);
        lax.Vertrag.Should().NotBeNull();
        lax.Warnung.Should().Contain("anderen Domänen-Stand");
        var streng = AkteurVertragsPruefung.Pruefe("TrainingsSystem", "0000000000000000", null, GeneratedAkteurRechte.Alle, streng: true);
        streng.Vertrag.Should().BeNull();
        streng.Ablehnung.Should().Contain("anderen Domänen-Stand");
    }

    [Fact]
    public void Der_Vertrag_ersetzt_die_Selbstauskunft()
    {
        var wunsch = new CapabilitiesResult
        {
            AllowedCommands = { "StarteTraining" },
            SubscribedEvents = { "ImagePairKomplett", "TrainingAngefordert", "CommandFailed" },   // Selbstauskunft
        };
        var abweichung = AkteurVertragsPruefung.Wende(wunsch, R("Klassifizierer"), Loese, _ => false, _ => false);

        wunsch.SubscribedEvents.Should().BeEquivalentTo("BildVerfuegbar", "ImagePairKomplett", "ModellAktiviert");
        wunsch.AllowedCommands.Should().BeEquivalentTo("KlassifiziereBildPaarDurchKi", "KlassifiziereEinzelBildDurchKi");
        abweichung.Should().Contain("hört laut Vertrag nicht: TrainingAngefordert").And.Contain("Selbstauskunft ohne Vertrags-Eingang: ModellAktiviert");
    }

    [Fact]
    public void Reaktion_von_aussen_bekommt_eine_deterministische_CommandId()
    {
        var stream = Guid.NewGuid();
        var ziel = Guid.NewGuid();
        var korr = Guid.NewGuid().ToString();
        Guid Id(int version, int index, Type cmd) =>
            AkteurVertragsPruefung.CommandId(korr, stream, version, "TrainingAngefordert", index, cmd, ziel);

        Id(1, 0, typeof(MeldeTrainingBegonnen)).Should().Be(Id(1, 0, typeof(MeldeTrainingBegonnen)), "doppelt zugestellt = dieselbe Id → Inbox dedupliziert");
        Id(1, 1, typeof(MeldeFortschritt)).Should().NotBe(Id(1, 2, typeof(MeldeFortschritt)), "jede Ausgabe eines Stroms ist eine eigene");
        Id(1, 0, typeof(MeldeTrainingBegonnen)).Should().NotBe(Id(2, 0, typeof(MeldeTrainingBegonnen)), "ein anderes Event ist eine andere Ursache");
        AkteurVertragsPruefung.CommandId(korr, stream, 1, "TrainingAngefordert", 0, typeof(MeldeTrainingBegonnen), Guid.NewGuid())
            .Should().NotBe(Id(1, 0, typeof(MeldeTrainingBegonnen)), "das Ziel gehört zum Hash");
    }

    [Fact]
    public void AntwortetMit_kennt_auch_die_Teile_einer_Session()
    {
        var s = AkteurRechte.Vereinige([R("Inspekteur"), R("TrainingsSystem")]);
        s.AntwortetMit(typeof(TrainingAngefordert), typeof(MeldeFortschritt)).Should().BeTrue();
        s.AntwortetMit(typeof(TrainingAbgebrochen), typeof(MeldeFortschritt)).Should().BeFalse("darauf antwortet er nur zur Kenntnis");
    }

    /// <summary>Beide Generate kommen aus derselben Quelle (Akteurvertrag.Kanon) — der Python-Worker meldet so den Server-Hash.</summary>
    [Fact]
    public void Python_Generat_traegt_denselben_Vertrags_Hash_wie_der_Server()
    {
        var datei = Path.Combine(Wurzel(), "Domain.Client.Worker.Python.ML", "domain_client", "generated", "vertraege.py");
        var text = File.ReadAllText(datei);
        var klassen = Regex.Matches(text, "class (\\w+)Basis\\(AkteurVertragBasis\\[S\\]\\):[\\s\\S]*?VERTRAG_HASH: ClassVar\\[str\\] = \"(\\w+)\"")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var server = GeneratedAkteurRechte.Alle.Values.Where(a => a.VertragTyp != null).ToDictionary(a => a.Name, a => a.VertragHash);
        klassen.Should().BeEquivalentTo(server, "vertraege.py ist veraltet → ./codegen.sh --force");
    }

    private static string Wurzel()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (d.GetFiles("*.sln").Length > 0) return d.FullName;
        throw new InvalidOperationException("Solution-Wurzel nicht gefunden");
    }

    private static readonly Dictionary<string, Type> Typen = new[]
    {
        typeof(ImagePairKomplett), typeof(BildVerfuegbar), typeof(ModellAktiviert), typeof(TrainingAngefordert),
        typeof(KlassifiziereBildPaarDurchKi), typeof(StarteTraining),
    }.ToDictionary(t => t.Name);

    private static Type? Loese(string name) => Typen.GetValueOrDefault(name);
}
