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
/// Akteure (docs/konzept-akteure.md): die generierte Befugnis-Tabelle (deklariert aus <c>IDarf&lt;T&gt;</c> und <c>IHoert&lt;T&gt;</c>)
/// und das Tor am Handshake — rein, ohne gRPC/Cluster.
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
    public void Ein_Interface_ist_kein_Akteur()
        => GeneratedAkteurRechte.Alle.Keys.Should().NotContain(k => k.StartsWith("I") && char.IsUpper(k[1]), "Akteure sind Records, keine Schnittstellen");

    [Fact]
    public void Ein_Command_kann_mehrere_Akteure_haben()
    {
        R("Inspekteur").Queries.Should().Contain(typeof(GetImagePair));
        R("Produktpruefer").Queries.Should().Contain(typeof(GetImagePair));
    }

    [Fact]
    public void Reaktions_Commands_darf_nur_der_reagierende_Akteur()
        => GeneratedAkteurRechte.Alle.Where(kv => kv.Value.DarfHinein(typeof(Domain.Datensatz.NimmRangeAuf))).Select(kv => kv.Key).Should()
            .Equal(new[] { "Datensatzpfleger" }, "„nie von der GUI“: nur der Datensatzpfleger gibt ihn, als Reaktion auf RangeAngefordert");

    [Fact]
    public void IDarf_wird_nach_Art_sortiert()
    {
        var p = R("Produktpruefer");
        p.Commands.Should().BeEquivalentTo(new[] { typeof(LabelPhysischesProdukt) });
        p.Queries.Should().BeEquivalentTo(new[] { typeof(SucheImagePairs), typeof(GetImagePair) });
        p.TransientEvents.Should().BeEmpty();
        R("KameraSystem").Commands.Should().BeEquivalentTo(new[] { typeof(Domain.ImagePair.NimmBildAuf) }, "was die Instanz des Linienimports hineingibt");
    }

    [Fact]
    public void Hoeren_ist_deklariert_nicht_abgeleitet()
    {
        // Wer auf ein Aggregat einwirkt, hört dessen Events nicht schon deshalb — nur, was am Akteur als IHoert steht.
        var p = R("Produktpruefer");
        p.VertragTyp.Should().BeNull();
        p.Hoert.Should().BeEquivalentTo(new[] { typeof(ImagePairErstellt), typeof(PhysischesProduktGelabelt) });
        p.DarfHoeren(typeof(PhysischesProduktGelabelt)).Should().BeTrue();
        p.DarfHoeren(typeof(ImagePairKomplett)).Should().BeFalse("LabelPhysischesProdukt wirkt auf ImagePair ein, gehört ist es nicht");
        p.DarfHoeren(typeof(TrainingAngefordert)).Should().BeFalse();
    }

    [Fact]
    public void Hoeren_folgt_nicht_aus_den_eigenen_Queries()
    {
        // Der KIOperator fragt HoleModelle (→ ModellProjektion), hört aber nur, was er deklariert: ModellRegistriert, nicht ModellAktiviert.
        R("KIOperator").DarfHoeren(typeof(ModellRegistriert)).Should().BeTrue();
        R("KIOperator").DarfHoeren(typeof(ModellAktiviert)).Should().BeFalse();
        R("Modellfreigeber").DarfHoeren(typeof(ModellAktiviert)).Should().BeTrue();
        R("Inspekteur").DarfHoeren(typeof(ModellAktiviert)).Should().BeFalse();
    }

    // ── Reaktionen (Schritt 6): ein Decide gibt nur Fakten aus; ein Worker hört sie und gibt seine Folgen über sein IDarf hinein ──

    [Fact]
    public void Die_Worker_hoeren_Fakten_und_reagieren_ueber_ihr_IDarf()
    {
        var k = R("Klassifizierer");
        k.Arbeit.Should().BeEmpty();
        k.Hoert.Should().BeEquivalentTo(new[] { typeof(BildVerfuegbar), typeof(ImagePairKomplett), typeof(ModellAktiviert) });
        k.Commands.Should().BeEquivalentTo(new[] { typeof(KlassifiziereBildPaarDurchKi), typeof(KlassifiziereEinzelBildDurchKi) });
        k.Queries.Should().BeEquivalentTo(new[] { typeof(HoleAktivesModell) });

        var t = R("TrainingsSystem");
        t.Hoert.Should().BeEquivalentTo(new[] { typeof(TrainingAngefordert) });
        t.DarfHinein(typeof(MeldeFortschritt)).Should().BeTrue("seine Reaktion ist ein gewöhnlicher Command über sein IDarf");
        t.Queries.Should().Contain(typeof(Domain.Projections.Trainingslauf.HoleTrainingslauf), "ob der Lauf abgebrochen ist, liest er zwischen den Epochen");
    }

    [Fact]
    public void Arbeit_hat_in_der_Beispieldomaene_kein_Akteur()
        => GeneratedAkteurRechte.Alle.Values.Where(a => a.Arbeit.Count > 0).Select(a => a.Name)
            .Should().BeEmpty("Schritt 6: kein Decide gibt einen Auftrag aus, die Worker reagieren auf Fakten");

    [Fact]
    public void Vereinigung_vereinigt_auch_Arbeit_und_Hoeren()
    {
        var v = AkteurRechte.Vereinige([R("Klassifizierer"), R("Inspekteur")]);
        v.Name.Should().Be("Klassifizierer+Inspekteur");
        v.DarfHoeren(typeof(ImagePairKomplett)).Should().BeTrue();
        v.DarfHoeren(typeof(ModellAktiviert)).Should().BeTrue();
        v.DarfHoeren(typeof(ImagePairInspiziert)).Should().BeTrue();
        v.Einzelne.Select(e => e.Name).Should().Equal("Klassifizierer", "Inspekteur");
        v.AkteurFuer(typeof(LabelBildPaar)).Should().Be("Inspekteur");
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
            SubscribedEvents = { "BildVerfuegbar", "ImagePairKomplett", "TrainingAngefordert" },
        };

        var verweigert = AkteurTor.Wende(wunsch, R("Klassifizierer"), Loese);

        wunsch.AllowedCommands.Should().BeEquivalentTo("KlassifiziereBildPaarDurchKi", "KlassifiziereEinzelBildDurchKi");
        wunsch.SubscribedEvents.Should().Equal("BildVerfuegbar");
        verweigert.Should().Equal("hören: ImagePairKomplett", "hören: TrainingAngefordert");
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
        typeof(TrainingAngefordert), typeof(HoleModelle), typeof(BildVerfuegbar),
    }.ToDictionary(t => t.Name);

    private static Type? Loese(string name) => Typen.GetValueOrDefault(name);

    private sealed class ServiceCollectionStub
    {
        public void Konfiguriere(Action<AkteurOptionen> k) =>
            Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                .BuildServiceProvider(new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddAkteure(k));
    }
}
