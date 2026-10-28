using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Bus;
using Client.Infrastructure.Versioning;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Versionen;

/// <summary>
/// Eine Version je Aggregat beim Client (Plan Pub/Sub-Neustrukturierung Phase 5): sie kommt aus Event-Pushes (der Kopf nach dem Commit) und aus
/// Antworten (die Versionen der gezeigten Aggregate) und ist die ExpectedVersion des nächsten Commands — auch für ein Aggregat, das der Client
/// nur aus einer Antwort kennt.
/// </summary>
public class VersioningModuleTests
{
    private sealed record Ereignis;

    private static (VersioningModule Versionen, ClientBus Bus) Modul()
    {
        var bus = new ClientBus();
        var v = new VersioningModule();
        v.Activate(bus, [typeof(Ereignis)]);
        return (v, bus);
    }

    [Fact]
    public void Ein_Aggregat_nur_aus_einer_Antwort_hat_die_Version_der_Antwort()
    {
        var (v, _) = Modul();
        var id = Guid.NewGuid();
        v.GetVersion(id).Should().BeNull();

        v.TrackFromDeps([new AggregateDep(id, "Datensatz", 7)]);

        v.GetVersion(id).Should().Be(7, "die Antwort nennt den Stand — er ist die ExpectedVersion des nächsten Commands");
    }

    [Fact]
    public void Eine_aeltere_Antwort_senkt_die_Version_nicht()
    {
        var (v, bus) = Modul();
        var id = Guid.NewGuid();
        bus.Publish(new Ereignis(), new MessageContext { AggregateId = id, AggregateVersion = 4, StreamHeadVersion = 5 });

        v.TrackFromDeps([new AggregateDep(id, "Datensatz", 3)]);

        v.GetVersion(id).Should().Be(5, "der Kopf nach dem Commit aus dem Push ist neuer als die nachhängende Antwort");
    }

    [Fact]
    public void Der_Kopf_nach_dem_Commit_zaehlt_Events_mit_die_der_Client_nicht_hoert()
    {
        var (v, bus) = Modul();
        var id = Guid.NewGuid();
        bus.Publish(new Ereignis(), new MessageContext { AggregateId = id, AggregateVersion = 3, StreamHeadVersion = 4 });
        v.GetVersion(id).Should().Be(4);

        bus.Publish(new Ereignis(), new MessageContext { AggregateId = id, AggregateVersion = 6 });
        v.GetVersion(id).Should().Be(6, "ohne Kopf gilt die Version des Events");

        v.TrackFromDeps([new AggregateDep(id, "Datensatz", 9)]);
        v.GetVersion(id).Should().Be(9, "eine neuere Antwort zählt wie ein Push");
    }
}
