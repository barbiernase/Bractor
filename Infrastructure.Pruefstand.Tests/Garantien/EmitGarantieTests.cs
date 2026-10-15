using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Sammelvorgang;
using FluentAssertions;
using Infrastructure.Prozess;
using Infrastructure.PubSub;
using Xunit;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Das eine Emit-Primitiv unter Störung (docs/11 §11.3, §11.7): der <see cref="EmitAusgang"/> trennt Entschiedenes (zugestellt, abgelehnt) von
/// Unbekanntem, keine Ausnahme verlässt den Emitter, die Drossel begrenzt die Sends je Ziel, und der Dirigent wiederholt einen unbekannten Send
/// erst nach einem Backoff und nie doppelt gleichzeitig (PF-9). Store-frei über die Sende-Naht des echten <see cref="CommandEmitter"/>.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class EmitGarantieTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static Task<EmitAusgang> Sende(CommandEmitter emitter, Guid ziel)
        => emitter.EmitAsync(new MeldeTeilFertig(ziel), Guid.NewGuid(), Guid.NewGuid(), Ct);

    [Theory]
    [InlineData("erfolg", EmitAusgang.Zugestellt)]
    [InlineData("abgelehnt", EmitAusgang.Abgelehnt)]
    [InlineData("technisch", EmitAusgang.Unbekannt)]
    [InlineData("null", EmitAusgang.Unbekannt)]
    [InlineData("wirft", EmitAusgang.Unbekannt)]
    [InlineData("frist", EmitAusgang.Unbekannt)]
    public async Task Der_Ausgang_trennt_Entschiedenes_von_Unbekanntem(string antwort, EmitAusgang erwartet)
    {
        var emitter = new CommandEmitter(async (_, _, ct) => antwort switch
        {
            "erfolg" => new CommandResult { Success = true },
            "abgelehnt" => new CommandResult { Success = false, Abgelehnt = true, ErrorMessage = "Bestand reicht nicht" },
            "technisch" => new CommandResult { Success = false, ErrorMessage = "DB-Timeout vor dem Append" },
            "null" => null,
            "wirft" => throw new InvalidOperationException("Identität nicht auflösbar"),
            _ => await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => (CommandResult?)null),
        }, sendeFrist: TimeSpan.FromMilliseconds(50));

        (await Sende(emitter, Guid.NewGuid())).Should().Be(erwartet);
    }

    [Fact]
    public async Task Die_Drossel_begrenzt_die_Sends_je_Ziel_und_laesst_andere_Ziele_frei()
    {
        var drossel = new EmitDrossel(jeZiel: 2);
        var frei = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gleichzeitig = 0;
        var hoechstens = 0;
        var heiss = Guid.NewGuid();
        var emitter = new CommandEmitter(async (ziel, _, ct) =>
        {
            if (ziel.Identity == heiss.ToString())
            {
                var jetzt = Interlocked.Increment(ref gleichzeitig);
                lock (drossel) hoechstens = Math.Max(hoechstens, jetzt);
                await frei.Task.WaitAsync(ct);
                Interlocked.Decrement(ref gleichzeitig);
            }
            return new CommandResult { Success = true };
        }, sendeFrist: TimeSpan.FromSeconds(5), drossel: drossel);

        var ansHeisse = Enumerable.Range(0, 6).Select(_ => Sende(emitter, heiss)).ToList();
        await Task.Delay(100);
        (await Sende(emitter, Guid.NewGuid())).Should().Be(EmitAusgang.Zugestellt, "ein anderes Ziel wartet nicht hinter dem heißen");
        hoechstens.Should().Be(2, "höchstens zwei Sends sind gleichzeitig beim heißen Ziel");

        frei.SetResult();
        (await Task.WhenAll(ansHeisse)).Should().OnlyContain(a => a == EmitAusgang.Zugestellt);
        drossel.Unterwegs($"{heiss}").Should().Be(0);
    }

    [Fact]
    public async Task Eine_volle_Drossel_meldet_unbekannt_statt_zu_haengen()
    {
        var drossel = new EmitDrossel(jeZiel: 1);
        var ziel = Guid.NewGuid();
        var emitter = new CommandEmitter(async (_, _, ct) => { await Task.Delay(Timeout.Infinite, ct); return null; },
            sendeFrist: TimeSpan.FromMilliseconds(100), drossel: drossel);

        var ausgaenge = await Task.WhenAll(Sende(emitter, ziel), Sende(emitter, ziel));

        ausgaenge.Should().OnlyContain(a => a == EmitAusgang.Unbekannt);
    }

    // ── Dirigent: Sende-Takt je Vorgang (PF-9) ──

    private sealed class Takt
    {
        public readonly List<Guid> Gesendet = new();
        public readonly List<Guid> Geweckt = new();
        public readonly List<TimeSpan> Gewartet = new();
        public EmitAusgang Antwort = EmitAusgang.Unbekannt;
        public TaskCompletionSource Warten = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly DetachedProzessSend Sender;

        public Takt()
        {
            Sender = new DetachedProzessSend(
                (_, _, vorgang, _) => { lock (Gesendet) Gesendet.Add(vorgang); return Task.FromResult(Antwort); },
                (korrelation, _, _, _) => { lock (Geweckt) Geweckt.Add(korrelation); return Task.CompletedTask; },
                backoff: n => TimeSpan.FromSeconds(n),
                warte: (t, _) => { lock (Gewartet) Gewartet.Add(t); return Warten.Task; });
        }
    }

    [Fact]
    public async Task Nach_unbekanntem_Ausgang_weckt_der_Dirigent_erst_nach_dem_Backoff_und_sendet_nicht_doppelt()
    {
        var t = new Takt();
        var korrelation = Guid.NewGuid();
        var vorgang = Guid.NewGuid();
        var cmd = new MeldeTeilFertig(Guid.NewGuid());

        await t.Sender.SendeAsync(korrelation, cmd, vorgang, Ct);
        await Task.Delay(50);
        t.Geweckt.Should().BeEmpty("nach einem unbekannten Ausgang keine sofortige Selbst-Weckung — sie trieb den Retry-Sturm");
        t.Gewartet.Should().Equal(TimeSpan.FromSeconds(1));

        // Während des Backoffs wecken Signal, Poll oder Backstop den Manager; der Fold sieht den Schritt offen — gesendet wird trotzdem nicht.
        await t.Sender.SendeAsync(korrelation, cmd, vorgang, Ct);
        await t.Sender.SendeAsync(korrelation, cmd, vorgang, Ct);
        t.Gesendet.Should().HaveCount(1);
        t.Sender.IstGesperrt(vorgang).Should().BeTrue();

        // Backoff vorbei: jetzt weckt er sich, der Fold sendet erneut — der zweite Versuch wartet länger.
        var erstesWarten = t.Warten;
        t.Warten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        erstesWarten.SetResult();
        await Task.Delay(50);
        t.Geweckt.Should().Equal(korrelation);
        await t.Sender.SendeAsync(korrelation, cmd, vorgang, Ct);
        await Task.Delay(50);
        t.Gesendet.Should().HaveCount(2);
        t.Gewartet.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Nach_entschiedenem_Ausgang_weckt_der_Dirigent_sofort()
    {
        var t = new Takt { Antwort = EmitAusgang.Abgelehnt };
        var korrelation = Guid.NewGuid();
        var vorgang = Guid.NewGuid();

        await t.Sender.SendeAsync(korrelation, new MeldeTeilFertig(Guid.NewGuid()), vorgang, Ct);
        await Task.Delay(50);

        t.Geweckt.Should().Equal(korrelation);
        t.Gewartet.Should().BeEmpty();
        t.Sender.IstGesperrt(vorgang).Should().BeFalse();
    }
}
