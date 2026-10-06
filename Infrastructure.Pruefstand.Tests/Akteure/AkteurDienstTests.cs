using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Akteure;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Modell;
using Core;
using Domain.Pipeline.ImageProcessing;
using FluentAssertions;
using Infrastructure.Akteure;
using Infrastructure.Pipeline;
using Infrastructure.PubSub;
using Xunit;

namespace Infrastructure.Pruefstand.Akteure;

/// <summary>
/// Die Kausalkette zur Laufzeit (docs/konzept-akteure.md §5.1): der Akteur reist mit dem Event (Header im Log →
/// <see cref="EventEnvelope.UserId"/>), der Konsument setzt ihn als „im Auftrag von", der EINE Emit stempelt ihn. Ohne
/// Kette (Ingress) gilt der eine Akteur mit IDarf; ein Ketten-Command hat keinen IDarf-Halter. Ein Token kann mehrere
/// Akteure verkörpern. Ohne Cluster: echte Bridge + echter <see cref="CommandEmitter"/> über die Sende-Naht.
/// </summary>
public class AkteurDienstTests
{
    private static (CommandEmitter Emitter, List<CommandEnvelope> Umschlaege) Emitter()
    {
        var umschlaege = new List<CommandEnvelope>();
        return (new CommandEmitter((_, env, _) => { umschlaege.Add(env); return Task.FromResult<CommandResult?>(null); }), umschlaege);
    }

    [Fact]
    public async Task Der_Emit_stempelt_den_Akteur_im_Auftrag()
    {
        var (emitter, umschlaege) = Emitter();
        var cmd = new NimmRangeAuf(Guid.NewGuid(), new List<Guid>(), default!);
        var k = new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "x");

        using (ImAuftrag.Von("KIOperator"))
            await emitter.EmitAsync(cmd, k, CancellationToken.None);
        await emitter.EmitAsync(cmd, k with { Diskriminator = "y" }, CancellationToken.None);

        umschlaege[0].UserId.Should().Be("KIOperator");
        umschlaege[1].UserId.Should().Be(ImAuftrag.Ohne, "ein Ketten-Command ohne Kette hat keinen Akteur — niemand darf ihn direkt");
        umschlaege[0].CommandId.Should().Be(Infrastructure.Aggregate.EmitId.Ableiten(k, cmd.AggregateId), "der Akteur ändert die Idempotenz nicht");
    }

    [Fact]
    public async Task Ohne_Kette_stempelt_der_eine_Akteur_mit_IDarf()
    {
        AkteurHerkunft.EindeutigerHalter(typeof(DateiErkannt)).Should().Be("KameraSystem");
        AkteurHerkunft.EindeutigerHalter(typeof(Domain.Projections.GetImagePair)).Should().BeNull("mehrere Akteure dürfen die Query");
        AkteurHerkunft.EindeutigerHalter(typeof(NimmRangeAuf)).Should().BeNull("Ketten-Commands darf niemand direkt");

        var (emitter, umschlaege) = Emitter();
        await emitter.EmitAsync(new LabelPhysischesProdukt(Guid.NewGuid(), default), new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "x"), CancellationToken.None);
        umschlaege.Single().UserId.Should().Be("Produktpruefer");
    }

    [Fact]
    public async Task Die_Pipeline_handelt_im_Auftrag_des_Akteurs_ihres_Events()
    {
        string? gesehen = null;
        PipelineEventPullBridge.EventDispatch dispatch = async (env, _, sendCommand, _, _, _) =>
        {
            await Task.Yield();                       // über ein await hinweg — der Auftrag reist im Fluss
            gesehen = ImAuftrag.Akteur;
            await sendCommand(new SchliesseEinfrierenAb(env.AggregateId, new List<DatensatzMitglied>()));
        };
        var pull = PipelineEventPullBridge.Wrap(dispatch, _ => _ => Task.CompletedTask, (_, _) => Task.CompletedTask);
        var stream = Guid.NewGuid();

        await pull(new EventEnvelope { AggregateId = stream, AggregateVersion = 3, UserId = "KIOperator", Payload = new ImagePairInspiziert() },
            new ProjectionWriter(stream, 3));

        gesehen.Should().Be("KIOperator");
        ImAuftrag.Akteur.Should().BeNull("der Auftrag endet mit dem Handle");

        await pull(new EventEnvelope { AggregateId = stream, AggregateVersion = 4, Payload = new ImagePairInspiziert() }, new ProjectionWriter(stream, 4));
        gesehen.Should().BeNull("ein Event ohne Akteur (Alt-Event, Automation) setzt keinen Auftrag");
    }

    [Fact]
    public void Ein_Token_verkoerpert_mehrere_Akteure_und_jeder_Command_traegt_den_der_ihn_darf()
    {
        var o = new AkteurOptionen();
        o.Token<Inspekteur, KIOperator>("blazor");
        var session = new AkteurTor(o, GeneratedAkteurRechte.Alle).Erkenne("blazor")!;

        session.Name.Should().Be("Inspekteur+KIOperator");
        session.DarfHinein(typeof(LabelBildPaar)).Should().BeTrue();
        session.DarfHinein(typeof(FriereEin)).Should().BeTrue();
        session.DarfHinein(typeof(SetzeModellAktiv)).Should().BeFalse("das darf nur der Modellfreigeber");
        session.AkteurFuer(typeof(LabelBildPaar)).Should().Be("Inspekteur");
        session.AkteurFuer(typeof(FriereEin)).Should().Be("KIOperator");
    }

    [Fact]
    public void Token_aus_der_Konfiguration_mit_Komma()
    {
        var o = new AkteurOptionen();
        o.Token("t", "Inspekteur, Produktpruefer");
        o.Tokens["t"].Should().Equal("Inspekteur", "Produktpruefer");
    }
}
