using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Projections;
using Xunit;

namespace Infrastructure.Pruefstand.Konsumenten;

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

    /// <summary>Trägt nur IProjectionTracker — KEINE Atomaritäts-Zusage (die Methoden ruft der Check nie).</summary>
    private class NurTrackerStub : IProjectionTracker
    {
        public Task<PartitionsCheckpoint> LadePartitionAsync(string p, int partition, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, int>> LadeGuardsAsync(string p, IReadOnlyCollection<Guid> s, CancellationToken ct) => throw new NotSupportedException();
        public void BeginneBatch() { }
        public void Stream(Guid streamId) { }
        public Task<int> CommitAsync(PartitionsCheckpoint stand, IReadOnlyDictionary<Guid, int> guards, CancellationToken ct) => throw new NotSupportedException();
        public Task ResetAllAsync(string p, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Trägt ICoCommitTracker — die Atomaritäts-Zusage (Effekte + Fortschritt in EINER Transaktion).</summary>
    private sealed class CoCommitTrackerStub : NurTrackerStub, ICoCommitTracker { }

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
