using FluentAssertions;
using Infrastructure.PubSub;

namespace Infrastructure.Pruefstand.PubSub;

/// <summary>
/// ★ Befund 9: <c>GetShardIndex</c> darf NIE werfen (früher <c>Math.Abs(int.MinValue)</c> →
/// OverflowException) und muss immer einen gültigen Shard-Index liefern. Das Vorzeichen-Bit-Maskieren
/// (<c>&amp; 0x7FFFFFFF</c>) ist total — für JEDEN Hash, inkl. <c>int.MinValue</c>.
/// </summary>
public class BrokerIdentityTests
{
    [Fact]
    public void GetShardIndex_ist_immer_im_Bereich_und_wirft_nie()
    {
        for (int i = 0; i < 10_000; i++)
        {
            var idx = BrokerIdentity.GetShardIndex($"sub-{i}-{System.Guid.NewGuid():N}");
            idx.Should().BeInRange(0, PubSubConfiguration.ShardCount - 1);
        }
    }
}
