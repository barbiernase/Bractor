using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Domain.Sammelvorgang;
using FluentAssertions;
using Infrastructure.PubSub;
using Xunit;

namespace Infrastructure.Pruefstand.Garantien;

/// <summary>
/// Das eine Emit-Primitiv unter Störung (docs/11 §11.3, §11.7): der <see cref="EmitAusgang"/> trennt Entschiedenes (zugestellt, abgelehnt) von
/// Unbekanntem, keine Ausnahme verlässt den Emitter, und die Drossel begrenzt die Sends je Ziel (PF-9). Store-frei über die Sende-Naht des echten <see cref="CommandEmitter"/>.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class EmitGarantieTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<EmitAusgang> Sende(CommandEmitter emitter, Guid ziel)
        => await emitter.EmitAsync(new MeldeTeilFertig(new(ziel)), Guid.NewGuid(), Guid.NewGuid(), Ct);

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
}
