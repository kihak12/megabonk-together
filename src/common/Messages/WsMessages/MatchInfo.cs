using MegabonkTogether.Common.Messages.WsMessages;
using MemoryPack;

namespace MegabonkTogether.Common.Messages
{
    [MemoryPackable]
    public partial class MatchInfo : IWsMessage
    {
        public IEnumerable<PeerInfo> Peers { get; set; } = Array.Empty<PeerInfo>();
        public uint Seed { get; set; }
        public bool EnabledSharedExperience { get; set; }
        public bool EnabledSharedKills { get; set; }
        public float Difficulty { get; set; } = 1.0f;
    }

    [MemoryPackable]
    public partial class PeerInfo
    {
        public uint ConnectionId { get; set; }
        public bool? IsHost { get; set; }
        public uint HostConnectionId { get; set; }
        public string Name { get; set; }
    }

}
