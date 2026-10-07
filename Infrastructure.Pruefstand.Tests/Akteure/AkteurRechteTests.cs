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

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Akteure (docs/konzept-akteure.md): die generierte Befugnis-Tabelle (deklariert aus <c>IDarf&lt;T&gt;</c>, Hören
/// aus dem Graphen abgeleitet) und das Tor am Handshake — rein, ohne gRPC/Cluster.
/// </summary>
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
        p.Trigger.Should().BeEmpty();
        p.TransientEvents.Should().BeEmpty();
        R("KameraSystem").Trigger.Should().BeEquivalentTo(new[] { typeof(Domain.ImagePair.DateiErkannt) });
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

    // ── Vertrag (Akteur-Konzept §3): Befugt = IDarf ∪ Ausgaben, Hört = exakt die Eingänge ──

    [Fact]
    public void Vertrag_steht_in_der_Tabelle()
    {
        var k = R("Klassifizierer");
        k.VertragTyp.Should().Be(typeof(IKlassifizierer));
        k.Vertrag.Keys.Should().BeEquivalentTo(new[] { typeof(ImagePairKomplett), typeof(BildVerfuegbar), typeof(ModellAktiviert) });
        k.Vertrag[typeof(ImagePairKomplett)].Should().BeEquivalentTo(new[] { typeof(KlassifiziereBildPaarDurchKi) });
        k.Vertrag[typeof(BildVerfuegbar)].Should().BeEmpty("nur zur Kenntnis");
        k.Stroeme.Should().BeEmpty();

        var t = R("TrainingsSystem");
        t.VertragTyp.Should().Be(typeof(ITrainingsSystem));
        t.Stroeme.Should().BeEquivalentTo(new[] { typeof(TrainingAngefordert) });
        t.Vertrag[typeof(TrainingAngefordert)].Should().BeEquivalentTo(new[]
            { typeof(MeldeTrainingBegonnen), typeof(MeldeFortschritt), typeof(MeldeTrainingAbgeschlossen), typeof(MeldeTrainingGescheitert) });
    }

    [Fact]
    public void Befugt_ist_IDarf_vereinigt_mit_den_Vertrags_Ausgaben()
    {
        var k = R("Klassifizierer");
        k.Commands.Should().BeEquivalentTo(new[] { typeof(KlassifiziereBildPaarDurchKi), typeof(KlassifiziereEinzelBildDurchKi) });
        R("TrainingsSystem").DarfHinein(typeof(MeldeFortschritt)).Should().BeTrue("Ausgabe seines Vertrags — kein zweites IDarf nötig");
        k.Zugesagt(typeof(ImagePairKomplett), typeof(KlassifiziereBildPaarDurchKi)).Should().BeTrue();
        k.Zugesagt(typeof(BildVerfuegbar), typeof(KlassifiziereBildPaarDurchKi)).Should().BeFalse();
    }

    [Fact]
    public void Mit_Vertrag_hoert_der_Akteur_genau_seine_Eingaenge()
    {
        var k = R("Klassifizierer");
        k.DarfHoeren(typeof(ImagePairKomplett)).Should().BeTrue();
        k.DarfHoeren(typeof(BildVerfuegbar)).Should().BeTrue();
        k.DarfHoeren(typeof(ModellAktiviert)).Should().BeTrue();
        k.DarfHoeren(typeof(ImagePairInspiziert)).Should().BeFalse("darauf reagiert er nicht (früher: alle ImagePair-Events)");
        k.DarfHoeren(typeof(TrainingAngefordert)).Should().BeFalse();

        var t = R("TrainingsSystem");
        t.DarfHoeren(typeof(TrainingAngefordert)).Should().BeTrue();
        t.DarfHoeren(typeof(TrainingAbgebrochen)).Should().BeTrue();
        t.DarfHoeren(typeof(TrainingBegonnen)).Should().BeFalse("sein eigenes Echo braucht er nicht");
    }

    [Fact]
    public void Vertrags_Hash_kommt_aus_der_kanonischen_Form()
    {
        var kanon = Akteurvertrag.Kanon("Klassifizierer", new[]
        {
            new Akteurvertrag.Zusage("ModellAktiviert", [], false),
            new Akteurvertrag.Zusage("ImagePairKomplett", ["KlassifiziereBildPaarDurchKi"], false),
            new Akteurvertrag.Zusage("BildVerfuegbar", [], false),
        });
        kanon.Should().Be("Klassifizierer:BildVerfuegbar>;ImagePairKomplett>KlassifiziereBildPaarDurchKi;ModellAktiviert>");
        R("Klassifizierer").VertragHash.Should().Be(Akteurvertrag.Hash(kanon)).And.HaveLength(16);
        R("Inspekteur").VertragHash.Should().BeEmpty();
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

        wunsch.AllowedCommands.Should().BeEquivalentTo("KlassifiziereBildPaarDurchKi", "KlassifiziereEinzelBildDurchKi");
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
            AggregateId = Guid.NewGuid(),
            Payload = new SetzeModellAktiv(Guid.NewGuid()),
            Modus = new CommandModus.Client(1),
            AggregateType = "Modell",
            OriginSessionId = "session-0007",
        };

        var failed = AkteurVerweigerung.Baue(env, "Inspekteur")!;
        failed.TargetSubscriberId.Should().Be("session-0007");
        failed.CorrelationId.Should().Be(env.CorrelationId);
        ((CommandFailed)failed.Payload).Reason.Should().Be("Akteur 'Inspekteur' darf SetzeModellAktiv nicht");
        AkteurVerweigerung.Baue(env with { OriginSessionId = null }, "Inspekteur").Should().BeNull();
    }

    private static readonly Dictionary<string, Type> Typen = new[]
    {
        typeof(StarteTraining), typeof(KlassifiziereBildPaarDurchKi), typeof(ImagePairKomplett),
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
