using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Projections;
using Xunit;

namespace Infrastructure.Pruefstand.Phase4;

/// <summary>
/// P4.3 / GA-1 — der Boot-/DI-Check erzwingt Co-Commit für append-artige Projektionen. Bewiesen:
///   (1) IAppendProjektion OHNE Co-Commit-Tracker → Start bricht mit klarer Meldung (der Kernfall).
///   (2) IAppendProjektion MIT Tracker → folgenlos (der Normalfall der ImagePairHistorie).
///   (3) NICHT markiert (z. B. eine Reaktion/emittierender Konsument) → folgenlos, auch ohne Tracker
///       (kein Fehlalarm — Emittenten brauchen keinen Co-Commit).
/// </summary>
public class GaEinsPruefungTests
{
    private sealed class AppendProjektionOhneStore : IAppendProjektion { }
    private sealed class GewoehnlicheReaktion { }

    private sealed class TrackerStub : IProjectionTracker
    {
        public Task<int> LastProcessedVersionAsync(string p, Guid s, CancellationToken ct) => Task.FromResult(-1);
        public Task MarkProcessedAsync(string p, Guid s, int v, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAsync(string p, Guid s, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAllAsync(string p, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void Append_ohne_CoCommit_Tracker_bricht()
    {
        var akt = () => GaEinsPruefung.PrüfeCoCommit(new AppendProjektionOhneStore(), tracker: null, "append-ohne");
        akt.Should().Throw<InvalidOperationException>()
           .WithMessage("*GA-1*")
           .WithMessage("*Co-Commit*");
    }

    [Fact]
    public void Append_mit_Tracker_ist_folgenlos()
    {
        var akt = () => GaEinsPruefung.PrüfeCoCommit(new AppendProjektionOhneStore(), new TrackerStub(), "append-mit");
        akt.Should().NotThrow();
    }

    [Fact]
    public void Nicht_markiert_ist_folgenlos_auch_ohne_Tracker()
    {
        var akt = () => GaEinsPruefung.PrüfeCoCommit(new GewoehnlicheReaktion(), tracker: null, "reaktion");
        akt.Should().NotThrow();
    }
}
