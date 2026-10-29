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
/// Client-Verträge (<c>docs/konzept-akteure.md</c> §4, docs/konzept-akteure.md §5: ITraegt, ISendet, IFragt, IHoert): die generierte
/// Tabelle, die Schnittmenge Vertrag ∩ Token, der Handshake je Client (Hört statt Selbstauskunft, getragene Funktionen nur mit Befugnis) und die
/// Hash-Gleichheit mit dem Python-Generat — rein, ohne gRPC.
/// </summary>
public class ClientVertragTests
{
    private static ClientVertrag C(string name) => GeneratedClientVertraege.Alle[name];
    private static AkteurRechte A(string name) => GeneratedAkteurRechte.Alle[name];

    [Fact]
    public void Die_Clients_der_Domaene_stehen_in_der_Tabelle()
    {
        GeneratedClientVertraege.Alle.Keys.Should().BeEquivalentTo("KlassifikationsWorker", "TrainingsWorker", "Arbeitsplatz");
        C("KlassifikationsWorker").Verkoerpert.Should().Equal("Klassifizierer");
        C("KlassifikationsWorker").Traegt.Should().BeEquivalentTo(new[] { typeof(Domain.Pipeline.ImagePair.IBildpaarBeurteilung) });
        C("KlassifikationsWorker").Hoert.Should().BeEquivalentTo(new[] { typeof(BildVerfuegbar), typeof(ModellAktiviert) });
        C("TrainingsWorker").Verkoerpert.Should().Equal("TrainingsSystem");
        C("TrainingsWorker").Traegt.Should().BeEquivalentTo(new[] { typeof(Domain.Pipeline.Trainingslauf.ITraining) });
    }

    [Fact]
    public void Ein_Client_verkoerpert_mehrere_Akteure()
    {
        var ap = C("Arbeitsplatz");
        ap.Verkoerpert.Should().BeEquivalentTo("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer");
        ap.Traegt.Should().BeEmpty("der Arbeitsplatz handelt spontan, er trägt keine Funktion");
        ap.Hoert.Should().Contain(typeof(ImagePairKomplett)).And.Contain(typeof(PhysischesProduktGelabelt));
    }

    [Fact]
    public void Verkoerpert_nimmt_nur_Akteure_hinzu_die_der_Client_braucht()
        => C("KlassifikationsWorker").Verkoerpert.Should().NotContain("Modellfreigeber",
            "HoleAktivesModell darf auch der Modellfreigeber — der Klassifizierer (eindeutiger Halter der getragenen Funktion) darf es schon");

    [Fact]
    public void Der_Rand_schneidet_aus_der_Befugnis()
    {
        var r = C("Arbeitsplatz").Rechte(GeneratedAkteurRechte.Alle, token: null)!;
        r.Teile.Select(t => t.Name).Should().BeEquivalentTo("Inspekteur", "KIOperator", "Modellfreigeber", "Produktpruefer");
        r.DarfHinein(typeof(LabelBildPaar)).Should().BeTrue();
        r.DarfHinein(typeof(SetzeModellAktiv)).Should().BeTrue();
        r.DarfHinein(typeof(LabelEinzelBild)).Should().BeFalse("der Inspekteur darf es, aber der Arbeitsplatz sendet es nicht (kein ISendet)");
        r.DarfHinein(typeof(NimmRangeAuf)).Should().BeFalse("niemand darf es direkt — der Vertrag erweitert die Befugnis nie");
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
        wunsch.AllowedCommands.Should().BeEquivalentTo("KlassifiziereEinzelBildDurchKi");
        abweichung.Should().Contain("hört laut Vertrag nicht: TrainingAngefordert");
        e.Vertrag!.DarfRechnen(typeof(Domain.Pipeline.ImagePair.IBildpaarBeurteilung)).Should().BeTrue("die getragene Funktion darf er anbieten");
        e.Vertrag!.DarfRechnen(typeof(Domain.Pipeline.Trainingslauf.ITraining)).Should().BeFalse("die trägt ein anderer Client");
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

    /// <summary>Ein Client, der beide ML-Funktionen trägt (wie ein Worker auf einer GPU-Maschine): er verkörpert beide Akteure und darf beide rechnen.</summary>
    [Fact]
    public void Ein_Client_mit_zwei_getragenen_Funktionen_verkoerpert_beide_Akteure()
    {
        var ml = new ClientVertrag("MlWorker", typeof(object), "h",
            new HashSet<Type> { typeof(Domain.Pipeline.ImagePair.IBildpaarBeurteilung), typeof(Domain.Pipeline.Trainingslauf.ITraining) },
            new HashSet<Type>(), new HashSet<Type> { typeof(HoleDatensatzSamples) }, new HashSet<Type> { typeof(TrainingAbgebrochen) },
            ["Klassifizierer", "TrainingsSystem"]);
        var r = ml.Rechte(GeneratedAkteurRechte.Alle, null)!;

        r.DarfRechnen(typeof(Domain.Pipeline.ImagePair.IBildpaarBeurteilung)).Should().BeTrue();
        r.DarfRechnen(typeof(Domain.Pipeline.Trainingslauf.ITraining)).Should().BeTrue();
        r.Hoert.Should().BeEquivalentTo(new[] { typeof(TrainingAbgebrochen) });
        r.DarfHinein(typeof(HoleDatensatzSamples)).Should().BeTrue();
        ml.Rechte(GeneratedAkteurRechte.Alle, A("Klassifizierer"))!.DarfRechnen(typeof(Domain.Pipeline.Trainingslauf.ITraining))
            .Should().BeFalse("Vertrag ∩ Token: das Token verkörpert nur den Klassifizierer");
    }

    [Fact]
    public void Der_Client_Hash_kommt_aus_der_kanonischen_Form()
    {
        var kanon = Abstractions.Clientvertragsregeln.ClientKanon("KlassifikationsWorker",
            [new("IBildpaarBeurteilung", [new Abstractions.Clientvertragsregeln.Eingang("ImagePairKomplett", ["KlassifiziereBildPaarDurchKi"], false)])],
            ["KlassifiziereEinzelBildDurchKi"], ["HoleAktivesModell"], ["ModellAktiviert", "BildVerfuegbar"]);
        kanon.Should().Be("KlassifikationsWorker|T:IBildpaarBeurteilung:ImagePairKomplett>KlassifiziereBildPaarDurchKi"
            + "|S:KlassifiziereEinzelBildDurchKi|F:HoleAktivesModell|H:BildVerfuegbar,ModellAktiviert");
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
