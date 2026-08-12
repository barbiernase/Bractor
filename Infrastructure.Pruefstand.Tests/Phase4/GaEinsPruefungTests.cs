using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Projections;
using Xunit;

namespace Infrastructure.Pruefstand.Phase4;

/// <summary>
/// P4.3 / GA-1 — der Boot-/DI-Check erzwingt ECHTEN Co-Commit (ICoCommitTracker) für append-artige
/// Projektionen. Bewiesen:
///   (1) IAppendProjektion OHNE jeden Tracker → Start bricht mit klarer Meldung.
///   (2) IAppendProjektion mit bloßem IProjectionTracker (NICHT co-committend) → bricht ebenfalls
///       (der geschlossene false-green: früher grün, jetzt fail-fast).
///   (3) IAppendProjektion mit ICoCommitTracker → folgenlos (der Normalfall der ImagePairHistorie).
///   (4) NICHT markiert (z. B. eine Reaktion/emittierender Konsument) → folgenlos, auch ohne Tracker.
/// </summary>
public class GaEinsPruefungTests
{
    private sealed class AppendProjektionOhneStore : IAppendProjektion { }
    private sealed class GewoehnlicheReaktion { }

    /// <summary>Trägt nur IProjectionTracker — dual-write-artig, KEINE Atomaritäts-Zusage.</summary>
    private sealed class NurTrackerStub : IProjectionTracker
    {
        public Task<int> LastProcessedVersionAsync(string p, Guid s, CancellationToken ct) => Task.FromResult(-1);
        public Task MarkProcessedAsync(string p, Guid s, int v, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAsync(string p, Guid s, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAllAsync(string p, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Trägt ICoCommitTracker — die Atomaritäts-Zusage (Effekt + Marke in EINER Transaktion).</summary>
    private sealed class CoCommitTrackerStub : ICoCommitTracker
    {
        public Task<int> LastProcessedVersionAsync(string p, Guid s, CancellationToken ct) => Task.FromResult(-1);
        public Task MarkProcessedAsync(string p, Guid s, int v, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAsync(string p, Guid s, CancellationToken ct) => Task.CompletedTask;
        public Task ResetAllAsync(string p, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void Append_ohne_Tracker_bricht()
    {
        var akt = () => GaEinsPruefung.PrüfeCoCommit(new AppendProjektionOhneStore(), tracker: null, "append-ohne");
        akt.Should().Throw<InvalidOperationException>()
           .WithMessage("*GA-1*")
           .WithMessage("*ICoCommitTracker*");
    }

    [Fact]
    public void Append_mit_bloßem_IProjectionTracker_bricht()
    {
        // Der geschlossene false-green: ein Tracker existiert, committet aber NICHT gemeinsam.
        var akt = () => GaEinsPruefung.PrüfeCoCommit(new AppendProjektionOhneStore(), new NurTrackerStub(), "append-dualwrite");
        akt.Should().Throw<InvalidOperationException>()
           .WithMessage("*GA-1*")
           .WithMessage("*ICoCommitTracker*");
    }

    [Fact]
    public void Append_mit_CoCommit_Tracker_ist_folgenlos()
    {
        var akt = () => GaEinsPruefung.PrüfeCoCommit(new AppendProjektionOhneStore(), new CoCommitTrackerStub(), "append-cocommit");
        akt.Should().NotThrow();
    }

    [Fact]
    public void Nicht_markiert_ist_folgenlos_auch_ohne_Tracker()
    {
        var akt = () => GaEinsPruefung.PrüfeCoCommit(new GewoehnlicheReaktion(), tracker: null, "reaktion");
        akt.Should().NotThrow();
    }
}
