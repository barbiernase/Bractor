using System;
using Abstractions;
using Cqrs.Testing;
using Domain.Sammelvorgang;
using Domain.Teilauftrag;
using Xunit;

namespace Infrastructure.Pruefstand.Aggregate;

/// <summary>
/// End-to-end (store-frei, echte generierte Logik über <see cref="SagaSzenario"/>): Fan-out → Fan-in.
///
/// So SETZT man die Barriere ein:
///   1. Fan-out (Client/Pipeline): <c>StarteSammelvorgang(N)</c> + N × <c>StarteTeilauftrag(…, batch)</c>.
///   2. Jeder Worker wird fertig → <see cref="TeilFertigProzess"/> mappt das auf <c>MeldeTeilFertig</c>.
///   3. Das Zähl-Aggregat feuert <c>SammelvorgangAbgeschlossen</c> erst beim N-ten Teil (N = Laufzeitwert).
/// </summary>
public class SammelvorgangKaskadeTests
{
    private static readonly IAggregateHandlerFactory Fabrik = new Infrastructure.AggregateHandlerFactory();

    [Fact]
    public void Fanout_dann_Fanin_Barriere_feuert_erst_beim_letzten_Teil()
    {
        var batch = Guid.NewGuid();
        var w1 = Guid.NewGuid();
        var w2 = Guid.NewGuid();
        var w3 = Guid.NewGuid();

        var bauer = SagaSzenario.Mit(Fabrik, new TeilFertigProzess());

        // 1) Fan-out — N=3 (Laufzeitwert) + drei Teilaufträge, alle an dieselbe Barriere gebunden.
        bauer.Wenn(new StarteSammelvorgang(batch, 3));
        bauer.Wenn(new StarteTeilauftrag(w1, batch));
        bauer.Wenn(new StarteTeilauftrag(w2, batch));
        bauer.Wenn(new StarteTeilauftrag(w3, batch));

        // 2) Zwei Teile fertig → Prozess feuert je MeldeTeilFertig; Barriere noch NICHT erreicht.
        bauer.Wenn(new SchließeTeilauftragAb(w1))
             .Feuert<MeldeTeilFertig>()
             .EndzustandVon<Sammelvorgang>(batch, s => !s.Abgeschlossen && s.Fertig == 1, "1 von 3");

        bauer.Wenn(new SchließeTeilauftragAb(w2))
             .EndzustandVon<Sammelvorgang>(batch, s => !s.Abgeschlossen && s.Fertig == 2, "2 von 3");

        // 3) Der dritte Teil schließt die Barriere — hier feuert SammelvorgangAbgeschlossen.
        bauer.Wenn(new SchließeTeilauftragAb(w3))
             .Feuert<MeldeTeilFertig>()
             .EndzustandVon<Sammelvorgang>(batch, s => s.Abgeschlossen && s.Fertig == 3,
                 "erst beim 3. (letzten) Teil ist die Barriere erreicht");
    }
}
