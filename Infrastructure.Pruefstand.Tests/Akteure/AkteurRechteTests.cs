using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Domain.Akteure;
using Domain.ImagePair;
using Domain.Modell;
using Domain.Projections;
using Domain.Trainingslauf;
using FluentAssertions;
using Infrastructure.Akteure;
using Xunit;
using Domain.Projections.ImagePair;
using Domain.Projections.Modell;

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Akteure (docs/konzept-akteure.md): die generierte Befugnis-Tabelle (deklariert aus <c>IDarf&lt;T&gt;</c>, Hören
/// aus dem Graphen abgeleitet) und das Tor am Handshake — rein, ohne gRPC/Cluster.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AkteurRechteTests
{
    private static AkteurRechte R(string name) => GeneratedAkteurRechte.Alle[name];

    [Fact]
    public void Tabelle_kennt_alle_IAkteur_Typen()
    {
        GeneratedAkteurRechte.Alle.Keys.Should().Contain(new[]
            { "KameraSystem", "TrainingsSystem", "Klassifizierer", "Inspekteur", "Produktpruefer", "KIOperator", "Modellfreigeber" });
        R("Inspekteur").Typ.Should().Be(typeof(Inspekteur));
    }

    [Fact]
    public void Art_kommt_aus_der_Basisliste()
    {
        R("KameraSystem").Art.Should().Be("Maschine");
        R("Klassifizierer").Art.Should().Be("Ki");
        R("Inspekteur").Art.Should().Be("Mensch");
    }

    [Fact]
    public void Ein_Dienst_ist_kein_Akteur()
        => GeneratedAkteurRechte.Alle.Keys.Should().NotContain(k => k.StartsWith("I") && char.IsUpper(k[1]), "Akteure sind Records, keine Dienst-Verträge");

    [Fact]
    public void Ein_Command_kann_mehrere_Akteure_haben()
    {
        R("Inspekteur").Queries.Should().Contain(typeof(GetImagePair));
        R("Produktpruefer").Queries.Should().Contain(typeof(GetImagePair));
    }

    [Fact]
    public void Ketten_Commands_darf_niemand_direkt()
        => GeneratedAkteurRechte.Alle.Values.Should().NotContain(a => a.DarfHinein(typeof(Domain.Datensatz.NimmRangeAuf)),
            "„nie von der GUI“: der Resolver erzeugt ihn in der Kette");

    [Fact]
    public void IDarf_wird_nach_Art_sortiert()
    {
        var p = R("Produktpruefer");
        p.Commands.Should().BeEquivalentTo(new[] { typeof(LabelPhysischesProdukt) });
        p.Queries.Should().BeEquivalentTo(new[] { typeof(SucheImagePairs), typeof(GetImagePair) });
        p.Quellen.Should().BeEmpty();
        p.TransientEvents.Should().BeEmpty();
        R("KameraSystem").Quellen.Should().BeEquivalentTo(new[] { typeof(Domain.ImagePair.DateiErkannt) });
    }

    [Fact]
    public void Hoeren_abgeleitet_aus_dem_Aggregat_der_eigenen_Commands()
    {
        // Ohne Vertrag (rein spontan): wer auf ein Aggregat einwirkt, hört dessen Events.
        var p = R("Produktpruefer");
        p.VertragTyp.Should().BeNull();
        p.DarfHoeren(typeof(ImagePairKomplett)).Should().BeTrue("LabelPhysischesProdukt wirkt auf ImagePair ein");
        p.DarfHoeren(typeof(TrainingAngefordert)).Should().BeFalse();
    }

    // ── Funktionen draußen (Plan §2.7): ein Akteur darf eine Funktion rechnen (IDarf<IFunktion-Typ>) — ein Client, der ihn verkörpert, darf sie tragen ──

    [Fact]
    public void Ein_Akteur_darf_Funktionen_rechnen_ihre_Ausgaben_gibt_der_Funktions_Actor()
    {
        R("Klassifizierer").Funktionen.Should().BeEquivalentTo(new[] { typeof(Domain.Pipeline.ImagePair.IBildpaarBeurteilung) });
        R("TrainingsSystem").Funktionen.Should().BeEquivalentTo(new[] { typeof(Domain.Pipeline.Trainingslauf.ITraining) });
        R("Bildrechner").Funktionen.Should().BeEquivalentTo(new[] { typeof(Domain.Pipeline.Bildaufbereitung.IBildaufbereitung) });
        R("Klassifizierer").DarfRechnen(typeof(Domain.Pipeline.ImagePair.IBildpaarBeurteilung)).Should().BeTrue();
        R("Inspekteur").DarfRechnen(typeof(Domain.Pipeline.ImagePair.IBildpaarBeurteilung)).Should().BeFalse();
        // Die Ausgaben einer getragenen Funktion sendet der Funktions-Actor — von sich aus darf der Akteur sie nicht hineingeben.
        R("Klassifizierer").Commands.Should().BeEquivalentTo(new[] { typeof(KlassifiziereEinzelBildDurchKi) });
        R("TrainingsSystem").DarfHinein(typeof(MeldeFortschritt)).Should().BeFalse();
    }

    [Fact]
    public void Ohne_Client_hoert_ein_Akteur_was_aus_dem_Graphen_folgt()
    {
        var k = R("Klassifizierer");
        k.VertragTyp.Should().BeNull("ein Akteur hat keinen Vertrag — nur ein Client");
        k.DarfHoeren(typeof(ImagePairKomplett)).Should().BeTrue("KlassifiziereEinzelBildDurchKi wirkt auf ImagePair ein");
        k.DarfHoeren(typeof(TrainingAngefordert)).Should().BeFalse();
    }

    [Fact]
    public void Hoeren_abgeleitet_aus_der_Projektion_hinter_den_eigenen_Queries()
    {
        // Der KIOperator fragt HoleModelle → ModellReader → ModellProjektion → deren Events.
        R("KIOperator").DarfHoeren(typeof(ModellAktiviert)).Should().BeTrue();
        R("Inspekteur").DarfHoeren(typeof(ModellAktiviert)).Should().BeFalse();
    }

    [Fact]
    public void CommandFailed_darf_jeder_hoeren()
        => R("Inspekteur").DarfHoeren(typeof(CommandFailed)).Should().BeTrue();

    // ── Tor ──

    private static AkteurTor Tor(Action<AkteurOptionen> k)
    {
        var o = new AkteurOptionen();
        k(o);
        return new AkteurTor(o, GeneratedAkteurRechte.Alle);
    }

    [Fact]
    public void Tor_ordnet_Token_zu_und_weist_ohne_Standard_ab()
    {
        var tor = Tor(o => o.Token<Inspekteur>("geheim"));
        tor.Erkenne("geheim")!.Name.Should().Be("Inspekteur");
        tor.Erkenne("falsch").Should().BeNull();
        tor.Erkenne(null).Should().BeNull();
    }

    [Fact]
    public void Tor_mit_Standard_nimmt_den_Standard_Akteur()
        => Tor(o => o.Standardmaessig<KIOperator>()).Erkenne(null)!.Name.Should().Be("KIOperator");

    [Fact]
    public void Unbekannter_Akteur_in_der_Konfiguration_bricht_den_Start()
    {
        var act = () => new ServiceCollectionStub().Konfiguriere(o => o.Token("x", "Gibtsnicht"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*Gibtsnicht*");
    }

    [Fact]
    public void Ohne_Akteur_ist_alles_erlaubt()
    {
        AkteurTor.Darf(null, typeof(StarteTraining)).Should().BeTrue();
        AkteurTor.DarfHoeren(null, typeof(TrainingAngefordert)).Should().BeTrue();
    }

    [Fact]
    public void Handshake_wird_Befugnis_statt_Selbstauskunft()
    {
        var wunsch = new CapabilitiesResult
        {
            AllowedCommands = { "StarteTraining", "KlassifiziereBildPaarDurchKi" },  // Selbstauskunft
            SubscribedEvents = { "ImagePairKomplett", "TrainingAngefordert" },
        };

        var verweigert = AkteurTor.Wende(wunsch, R("Klassifizierer"), Loese, _ => false, _ => false);

        wunsch.AllowedCommands.Should().BeEquivalentTo("KlassifiziereEinzelBildDurchKi");
        wunsch.SubscribedEvents.Should().Equal("ImagePairKomplett");
        verweigert.Should().Equal("hören: TrainingAngefordert");
    }

    [Fact]
    public void Zustaendigkeit_nur_fuer_Luecken_und_nur_einmal()
    {
        var trainer = R("KIOperator");

        Ergebnis(trainer, internBedient: false, vergeben: false).Should().Equal("HoleModelle");
        Ergebnis(trainer, internBedient: true, vergeben: false).Should().BeEmpty("einen Server-Reader kann niemand kapern");
        Ergebnis(trainer, internBedient: false, vergeben: true).Should().BeEmpty("Kardinalität eins");
        Ergebnis(R("Inspekteur"), internBedient: false, vergeben: false).Should().BeEmpty("der Inspekteur darf HoleModelle nicht");

        static List<string> Ergebnis(AkteurRechte a, bool internBedient, bool vergeben)
        {
            var r = new CapabilitiesResult { HandlingQueries = { "HoleModelle" } };
            AkteurTor.Wende(r, a, Loese, _ => internBedient, _ => vergeben);
            return r.HandlingQueries;
        }
    }

    [Fact]
    public void Verweigerter_Command_wird_targeted_CommandFailed()
    {
        var env = new CommandEnvelope
        {
            Payload = new SetzeModellAktiv(new(Guid.NewGuid())),
            Modus = new CommandModus.Client(1),
            AggregateType = "Modell",
            OriginSessionId = "session-0007",
        };

        var failed = AkteurVerweigerung.Baue(env, "Inspekteur")!;
        failed.ZielSitzung.Should().Be("session-0007");
        failed.CorrelationId.Should().Be(env.CorrelationId);
        ((CommandFailed)failed.Payload).Reason.Should().Be("Akteur 'Inspekteur' darf SetzeModellAktiv nicht");
        AkteurVerweigerung.Baue(env with { OriginSessionId = null }, "Inspekteur").Should().BeNull();
    }

    private static readonly Dictionary<string, Type> Typen = new[]
    {
        typeof(StarteTraining), typeof(KlassifiziereBildPaarDurchKi), typeof(KlassifiziereEinzelBildDurchKi), typeof(ImagePairKomplett),
        typeof(TrainingAngefordert), typeof(HoleModelle),
    }.ToDictionary(t => t.Name);

    private static Type? Loese(string name) => Typen.GetValueOrDefault(name);

    private sealed class ServiceCollectionStub
    {
        public void Konfiguriere(Action<AkteurOptionen> k) =>
            Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                .BuildServiceProvider(new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddAkteure(k));
    }
}
