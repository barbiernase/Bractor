using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Infrastructure.Aggregate;
using Infrastructure.PubSub;

namespace Infrastructure.Pruefstand.Phase3;

/// <summary>
/// Neubau-P3 — das EINE Emit-Primitiv (EM-1). Beweist in-memory (Fake-Send, kein echter Cluster,
/// <c>memory/hang-diagnose-in-memory.md</c>):
///  • <b>W1</b>: der Sender stempelt eine DETERMINISTISCHE CommandId (gleiche Kausalität → gleiche Id) →
///    eine wiederholte Zustellung ist beim Empfänger als Duplikat erkennbar (Inbox-Dedup). Der alte
///    Pipeline-Pfad stempelte pro Retry eine ZUFÄLLIGE Id → Doppelwirkung.
///  • <b>W2</b>: ein nie zurückkehrender Send blockiert den Turn NICHT — das Primitiv bounded den Token
///    selbst (kein <c>CancellationToken.None</c>).
/// </summary>
public class EmitPrimitivTests
{
    [Fact]
    public void EmitId_ist_deterministisch()
    {
        var k = new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "ReserviereBetrag");
        var ziel = Guid.NewGuid();

        EmitId.Ableiten(k, ziel).Should().Be(EmitId.Ableiten(k, ziel));
        EmitId.Ableiten(k, ziel).Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void EmitId_andere_Ursache_oder_Ziel_ergibt_andere_Id()
    {
        var kA = new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "d");
        var kB = kA with { Ursache = Guid.NewGuid() };
        var ziel = Guid.NewGuid();

        EmitId.Ableiten(kA, ziel).Should().NotBe(EmitId.Ableiten(kB, ziel));           // andere Ursache
        EmitId.Ableiten(kA, ziel).Should().NotBe(EmitId.Ableiten(kA, Guid.NewGuid())); // anderes Ziel (Fan-out)
    }

    [Fact]
    public async Task W1_wiederholter_Emit_traegt_dieselbe_deterministische_CommandId()
    {
        // Fake-Send: Quittung IMMER verloren (null) — der Emitter muss trotzdem deterministisch stempeln.
        var gesendet = new List<Guid>();
        var emitter = new CommandEmitter(
            (id, env, ct) => { gesendet.Add(env.CommandId); return Task.FromResult<CommandResult?>(null); });

        var ziel = Guid.NewGuid();
        var k = new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "ReserviereBetrag");
        var cmd = new ReserviereBetrag(ziel, 10m);

        await emitter.EmitAsync(cmd, k, default);
        await emitter.EmitAsync(cmd, k, default);   // Re-Wake nach verlorener Quittung

        gesendet.Should().HaveCount(2);
        gesendet[0].Should().Be(gesendet[1]);                  // gleiche Id → der Empfänger dedupliziert (W1 weg)
        gesendet[0].Should().Be(EmitId.Ableiten(k, ziel));
    }

    [Fact]
    public async Task W1_Emit_nutzt_den_Modus_Emittiert_ohne_Version()
    {
        CommandEnvelope? gesehen = null;
        var emitter = new CommandEmitter(
            (id, env, ct) => { gesehen = env; return Task.FromResult<CommandResult?>(null); });

        await emitter.EmitAsync(new ReserviereBetrag(Guid.NewGuid(), 1m),
            new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "x"), default);

        gesehen!.Modus.Should().BeOfType<CommandModus.Emittiert>();   // interne Emitter behaupten KEINE Version (§4.2)
    }

    [Fact]
    public async Task Prozess_Ueberladung_stempelt_den_vorgegebenen_vorgang_als_CommandId()
    {
        // Treiber-Fold (§5 Weg a): der Prozess-Manager gibt den deterministischen `vorgang` als CommandId vor,
        // damit der Fold den durablen Ausgang über CausationId == vorgang matcht (ohne Quittung).
        CommandEnvelope? gesehen = null;
        var emitter = new CommandEmitter(
            (id, env, ct) => { gesehen = env; return Task.FromResult<CommandResult?>(null); });

        var vorgang = Guid.NewGuid();
        var korrelation = Guid.NewGuid();
        await emitter.EmitAsync(new ReserviereBetrag(Guid.NewGuid(), 7m), commandId: vorgang, korrelation: korrelation, default);

        gesehen!.CommandId.Should().Be(vorgang, "der Fold matcht CausationId == vorgang");
        gesehen!.CorrelationId.Should().Be(korrelation.ToString(), "die Korrelation reist ins Ziel-Event-Metadatum (Router)");
        gesehen!.Modus.Should().BeOfType<CommandModus.Emittiert>("interne Emitter behaupten KEINE Version (§4.2)");
    }

    [Fact]
    public async Task W2_nie_zurueckkehrender_Send_blockiert_den_Turn_nicht()
    {
        // Fake-Send hängt für immer — bis der bounded Token des Emitters ihn abbricht.
        var emitter = new CommandEmitter(
            async (id, env, ct) => { await Task.Delay(Timeout.Infinite, ct); return (CommandResult?)null; },
            sendeFrist: TimeSpan.FromMilliseconds(200));

        var emit = emitter.EmitAsync(
            new ReserviereBetrag(Guid.NewGuid(), 5m),
            new EmitKausalität(Guid.NewGuid(), Guid.NewGuid(), "ReserviereBetrag"),
            default);

        var fertig = await Task.WhenAny(emit, Task.Delay(TimeSpan.FromSeconds(5)));

        fertig.Should().BeSameAs(emit, "das bounded Token bricht den hängenden Send ab — kein Infinit-Hang (W2)");
        await emit;   // wirft nicht — OperationCanceledException wird im Primitiv geschluckt
    }
}
