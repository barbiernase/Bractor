using Abstractions;
using Domain.ImagePair;
using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Prozess;

/// <summary>
/// Der Signal-Pfad des Korrelations-Routers (PF-12): ein Auslöser-Signal startet seinen Vorgang ohne Read (der Signal-Typ nennt das Event,
/// die Korrelation folgt aus Stream und Version); jedes andere Signal liest genau sein Event — nie den Rest des Streams.
/// </summary>
public class KorrelationsRouterTests
{
    private sealed class Store : IEventStoreRepository
    {
        public readonly Dictionary<(Guid, int), EventEnvelope> Events = new();
        public int Einzelreads;

        public Task<EventEnvelope?> ReadEventAsync(Guid s, int v, CancellationToken ct)
        {
            Einzelreads++;
            return Task.FromResult(Events.GetValueOrDefault((s, v)));
        }
        public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid s, int v, CancellationToken ct)
            => throw new InvalidOperationException("PF-12: der Router liest keinen Stream-Rest");
        public Task AppendEventsAsync(Guid a, int v, IReadOnlyList<IEvent> e, string? c = null, string? ca = null, string? at = null, string? ak = null, IReadOnlyList<Beilage>? b = null)
            => throw new NotSupportedException();
        public Task<StreamChanges> ReadChangedStreamsAsync(long a, IReadOnlyCollection<string>? t, int l, CancellationToken ct) => throw new NotSupportedException();
        public Task<PartitionsSeite> ReadPartitionAsync(long n, int p, int ps, IReadOnlyCollection<string>? t, IReadOnlyCollection<long> a, int l, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new() => throw new NotSupportedException();
    }

    private static readonly IReadOnlyDictionary<Type, Type> Signale = new Dictionary<Type, Type>
    {
        [typeof(ImagePairInspiziert)] = typeof(StateChangeViaImagePairInspiziert),
        [typeof(ImagePairKomplett)] = typeof(StateChangeViaImagePairKomplett),
    };

    private static (KorrelationsRouter Router, Store Store, List<(Guid Korr, string? Start)> Geweckt) Welt()
    {
        var store = new Store();
        var geweckt = new List<(Guid, string?)>();
        var router = new KorrelationsRouter(store, new Dictionary<Type, string> { [typeof(ImagePairInspiziert)] = "P" },
            (k, _, _, start, _) => { geweckt.Add((k, start)); return Task.CompletedTask; }, Signale);
        return (router, store, geweckt);
    }

    [Fact]
    public async Task Ein_Ausloeser_Signal_startet_seinen_Vorgang_ohne_Read()
    {
        var (router, store, geweckt) = Welt();
        var stream = Guid.NewGuid();

        await router.OnSignalAsync(new StateChangeViaImagePairInspiziert(stream, 4));

        geweckt.Should().Equal((ProzessId.Für("P", stream, 4), "P"));
        store.Einzelreads.Should().Be(0);
    }

    [Fact]
    public async Task Ein_anderes_Signal_liest_genau_sein_Event_und_weckt_ueber_dessen_Korrelation()
    {
        var (router, store, geweckt) = Welt();
        var (stream, korr) = (Guid.NewGuid(), Guid.NewGuid());
        store.Events[(stream, 9)] = new EventEnvelope { AggregateId = stream, AggregateVersion = 9, Payload = new ImagePairKomplett(), CorrelationId = korr.ToString() };

        await router.OnSignalAsync(new StateChangeViaImagePairKomplett(stream, 9));

        geweckt.Should().Equal((korr, (string?)null));
        store.Einzelreads.Should().Be(1);
    }
}
