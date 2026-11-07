using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Projections;
using Domain.Trainingslauf;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.GrpcClient;
using Xunit;
using Domain.Projections.Datensatz;
using Domain.Projections.Modell;

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Clients (<c>docs/konzept-akteure.md</c> §4/§5: ein Record mit IVerkoerpert, IDarf und IHoert stehen an den Akteuren): die generierte
/// Tabelle, die Schnittmenge verkörpert ∩ Token für beide Richtungen, der Handshake je Client (Hört statt Selbstauskunft) und die
/// Hash-Gleichheit mit dem Python-Generat — rein, ohne gRPC.
/// </summary>
public class ClientVertragTests
{
    private static ClientVertrag C(string name) => GeneratedClientVertraege.Alle[name];
    private static AkteurRechte A(string name) => GeneratedAkteurRechte.Alle[name];

    [Fact]
    public void Die_Clients_der_Domaene_stehen_in_der_Tabelle()
    {
        GeneratedClientVertraege.Alle.Keys.Should().BeEquivalentTo("KlassifikationsWorker", "TrainingsWorker", "ReaktionsWorker", "Arbeitsplatz");
        C("ReaktionsWorker").Verkoerpert.Should().BeEquivalentTo("Datensatzpfleger", "Teilmelder");
        C("KlassifikationsWorker").Verkoerpert.Should().Equal("Klassifizierer");
        C("KlassifikationsWorker").Typ.Should().Be(typeof(Domain.Akteure.KlassifikationsWorker), "der Client ist ein Record");
        C("TrainingsWorker").Verkoerpert.Should().Equal("TrainingsSystem");
    }

    [Fact]
    public void Ein_Client_verkoerpert_mehrere_Akteure_und_hoert_was_sie_hoeren()
    {
        var ap = C("Arbeitsplatz");
        ap.Verkoerpert.Should().BeEquivalentTo("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer");
        var r = ap.Rechte(GeneratedAkteurRechte.Alle, token: null)!;
        r.Arbeit.Should().BeEmpty("der Arbeitsplatz handelt spontan, seine Akteure haben keine Arbeit");
        r.Hoert.Should().Contain(typeof(ImagePairKomplett)).And.Contain(typeof(PhysischesProduktGelabelt)).And.Contain(typeof(ModellAktiviert));
        r.Hoert.Should().BeEquivalentTo(new[] { "Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer" }.SelectMany(n => A(n).Hoert).Distinct(),
            "ein Client hat keinen eigenen Kenntnis-Satz — er hört, was seine Akteure hören");
    }

    [Fact]
    public void Arbeit_sendet_und_fragt_sind_die_IHoert_und_IDarf_der_verkoerperten_Akteure()
    {
        var ml = C("KlassifikationsWorker").Rechte(GeneratedAkteurRechte.Alle, token: null)!;
        ml.Arbeit.Should().BeEquivalentTo(new[] { typeof(PaarZuBeurteilen) });
        ml.Hoert.Should().BeEquivalentTo(new[] { typeof(BildVerfuegbar), typeof(ModellAktiviert) });
        ml.Commands.Should().BeEquivalentTo(new[] { typeof(KlassifiziereBildPaarDurchKi), typeof(KlassifiziereEinzelBildDurchKi) });
        ml.Queries.Should().BeEquivalentTo(new[] { typeof(HoleAktivesModell) });
        C("KlassifikationsWorker").Verkoerpert.Should().NotContain("Modellfreigeber",
            "HoleAktivesModell darf auch der Modellfreigeber — verkörpert ist nur, wen der Client nennt");
    }

    [Fact]
    public void Der_Rand_ist_die_Befugnis_der_Akteure()
    {
        var r = C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, token: null)!;
        r.Teile.Select(t => t.Name).Should().BeEquivalentTo("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer");
        r.DarfHinein(typeof(LabelBildPaar)).Should().BeTrue();
        r.DarfHinein(typeof(SetzeModellAktiv)).Should().BeTrue();
        r.DarfHinein(typeof(LabelEinzelBild)).Should().BeTrue("der Inspekteur darf es — befugt ist der Akteur, nicht die Software");
        r.DarfHinein(typeof(NimmRangeAuf)).Should().BeFalse("niemand darf es direkt — der Vertrag nennt keine eigene Befugnis");
        r.AkteurFuer(typeof(SetzeModellAktiv)).Should().Be("Modellfreigeber");
        r.AkteurFuer(typeof(LabelPhysischesProdukt)).Should().Be("Produktpruefer");
    }

    [Fact]
    public void Wirksam_ist_Vertrag_geschnitten_mit_dem_Token()
    {
        var anna = C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, A("Inspekteur"))!;
        anna.Teile.Select(t => t.Name).Should().Equal("Inspekteur");
        anna.DarfHinein(typeof(LabelBildPaar)).Should().BeTrue();
        anna.DarfHinein(typeof(FriereEin)).Should().BeFalse("das ist der KIOperator-Anteil — Anna ist nur Inspekteurin");

        var zwei = C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, AkteurRechte.Vereinige([A("Inspekteur"), A("KIOperator")]))!;
        zwei.Teile.Select(t => t.Name).Should().BeEquivalentTo("Inspekteur", "KIOperator");

        C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, A("Klassifizierer")).Should().BeNull("das Token verkörpert keinen Akteur dieses Clients");
    }

    [Fact]
    public void Handshake_nennt_den_Client_und_abonniert_genau_was_er_hoert()
    {
        var e = ClientVertragsPruefung.Pruefe("KlassifikationsWorker", C("KlassifikationsWorker").Hash, null, GeneratedAkteurRechte.Alle,
            streng: true, GeneratedClientVertraege.Alle);
        e.Ablehnung.Should().BeNull();
        e.Client!.Name.Should().Be("KlassifikationsWorker");

        var wunsch = new CapabilitiesResult { SubscribedEvents = { "TrainingAngefordert" } };
        var abweichung = ClientVertragsPruefung.Wende(wunsch, e.Vertrag!, Loese);
        wunsch.SubscribedEvents.Should().BeEquivalentTo("BildVerfuegbar", "ModellAktiviert");
        wunsch.AllowedCommands.Should().BeEquivalentTo("KlassifiziereBildPaarDurchKi", "KlassifiziereEinzelBildDurchKi");
        abweichung.Should().Contain("hört laut Vertrag nicht: TrainingAngefordert");
        e.Vertrag!.Arbeit.Should().BeEquivalentTo(new[] { typeof(PaarZuBeurteilen) }, "die Arbeit des verkörperten Akteurs kommt zu dieser Sitzung");
        e.Vertrag!.Arbeit.Should().NotContain(typeof(TrainingZuRechnen), "die hört ein Akteur, den ein anderer Client verkörpert");
    }

    [Fact]
    public void Ohne_Vertrag_bleibt_alles_wie_bisher_ein_unbekannter_Client_wird_abgelehnt()
    {
        ClientVertragsPruefung.Pruefe("", "", null, GeneratedAkteurRechte.Alle, true, GeneratedClientVertraege.Alle)
            .Should().Be(new ClientVertragsPruefung.Ergebnis(null, null, null));
        ClientVertragsPruefung.Pruefe("Klassifizierer", "x", null, GeneratedAkteurRechte.Alle, false, GeneratedClientVertraege.Alle)
            .Ablehnung.Should().Contain("unbekannt", "ein Akteur ist kein Client — der Akteur-Vertrag mit Auf ist entfallen");
    }

    [Fact]
    public void Handshake_lehnt_Token_ohne_passenden_Akteur_und_streng_einen_falschen_Hash_ab()
    {
        ClientVertragsPruefung.Pruefe("Arbeitsplatz", C("Arbeitsplatz").Hash, A("TrainingsSystem"), GeneratedAkteurRechte.Alle, false,
            GeneratedClientVertraege.Alle).Ablehnung.Should().Contain("keinen der Akteure des Clients Arbeitsplatz");
        ClientVertragsPruefung.Pruefe("Arbeitsplatz", "0000000000000000", null, GeneratedAkteurRechte.Alle, true, GeneratedClientVertraege.Alle)
            .Ablehnung.Should().Contain("anderen Domänen-Stand");
        ClientVertragsPruefung.Pruefe("Arbeitsplatz", "0000000000000000", null, GeneratedAkteurRechte.Alle, false, GeneratedClientVertraege.Alle)
            .Warnung.Should().Contain("anderen Domänen-Stand");
    }

    /// <summary>Ein Client, der beide ML-Akteure verkörpert (wie ein Worker auf einer GPU-Maschine): wirksam ist verkörpert ∩ Token, für beide
    /// Richtungen — die Arbeit und die Kenntnis eines Akteurs, den das Token nicht nennt, kommen nicht zu dieser Sitzung.</summary>
    [Fact]
    public void Ein_Client_mit_zwei_Akteuren_bekommt_ihre_Arbeit_nur_mit_dem_Token()
    {
        var ml = new ClientVertrag("MlWorker", typeof(object), "h", ["Klassifizierer", "TrainingsSystem"]);
        var r = ml.Rechte(GeneratedAkteurRechte.Alle, null)!;

        r.Arbeit.Should().BeEquivalentTo(new[] { typeof(PaarZuBeurteilen), typeof(TrainingZuRechnen) });
        r.DarfHoeren(typeof(TrainingAbgebrochen)).Should().BeTrue();
        r.DarfHoeren(typeof(ModellAktiviert)).Should().BeTrue();
        r.DarfHinein(typeof(HoleDatensatzSamples)).Should().BeTrue();
        r.VertragHash.Should().Be("h");

        var nurK = ml.Rechte(GeneratedAkteurRechte.Alle, A("Klassifizierer"))!;
        nurK.Teile.Select(t => t.Name).Should().Equal("Klassifizierer");
        nurK.Arbeit.Should().BeEquivalentTo(new[] { typeof(PaarZuBeurteilen) }, "verkörpert ∩ Token: das Token nennt nur den Klassifizierer");
        nurK.DarfHoeren(typeof(TrainingAbgebrochen)).Should().BeFalse("auch das Hören ist geschnitten");
        nurK.DarfHinein(typeof(MeldeFortschritt)).Should().BeFalse();
    }

    [Fact]
    public void Der_Client_Hash_kommt_aus_der_kanonischen_Form()
    {
        var kanon = Abstractions.Clientvertragsregeln.ClientKanon("KlassifikationsWorker", ["Klassifizierer"],
            [new("Klassifizierer", [new Abstractions.Clientvertragsregeln.Eingang("PaarZuBeurteilen",
                ["KlassifiziereEinzelBildDurchKi", "KlassifiziereBildPaarDurchKi"], true)])],
            ["KlassifiziereEinzelBildDurchKi", "KlassifiziereBildPaarDurchKi"], ["HoleAktivesModell"], ["ModellAktiviert", "BildVerfuegbar"]);
        kanon.Should().Be("KlassifikationsWorker|A:Klassifizierer"
            + "|W:Klassifizierer:PaarZuBeurteilen>KlassifiziereBildPaarDurchKi,KlassifiziereEinzelBildDurchKi*"
            + "|S:KlassifiziereBildPaarDurchKi,KlassifiziereEinzelBildDurchKi|F:HoleAktivesModell|H:BildVerfuegbar,ModellAktiviert");
        C("KlassifikationsWorker").Hash.Should().Be(Abstractions.Clientvertragsregeln.Hash(kanon)).And.HaveLength(16);
    }

    /// <summary>Beide Generate aus derselben Quelle (Clientvertragsregeln.ClientKanon) — der Python-Client meldet so den Server-Hash.</summary>
    [Fact]
    public void Python_Generat_traegt_dieselben_Client_Hashes_wie_der_Server()
    {
        var text = File.ReadAllText(Path.Combine(Wurzel(), "Domain.Client.Worker.Python.ML", "domain_client", "generated", "vertraege.py"));
        var clients = Regex.Matches(text, "CLIENT: ClassVar\\[str\\] = \"(\\w+)\"[\\s\\S]*?VERTRAG_HASH: ClassVar\\[str\\] = \"(\\w+)\"")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        clients.Should().BeEquivalentTo(GeneratedClientVertraege.Alle.ToDictionary(x => x.Key, x => x.Value.Hash),
            "vertraege.py ist veraltet → ./codegen.sh --force");
    }

    private static string Wurzel()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (d.GetFiles("*.sln").Length > 0) return d.FullName;
        throw new InvalidOperationException("Solution-Wurzel nicht gefunden");
    }

    private static readonly Dictionary<string, Type> Typen = new[]
    {
        typeof(ImagePairKomplett), typeof(BildVerfuegbar), typeof(ModellAktiviert), typeof(TrainingAngefordert), typeof(TrainingAbgebrochen),
        typeof(KlassifiziereBildPaarDurchKi), typeof(KlassifiziereEinzelBildDurchKi),
    }.ToDictionary(t => t.Name);

    private static Type? Loese(string name) => Typen.GetValueOrDefault(name);
}
