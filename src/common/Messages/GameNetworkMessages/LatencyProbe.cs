using MemoryPack;

namespace MegabonkTogether.Common.Messages.GameNetworkMessages
{
    /// <summary>
    /// Round trip measurement between the host and one client. The host stamps SentAtMs with its own clock and
    /// broadcasts the probe; each client echoes it back untouched with its own connection id as OwnerId.
    /// Only the host reads the timestamp, so the clocks never need to agree
    /// </summary>
    [MemoryPackable]
    public partial class LatencyProbe : IGameNetworkMessage
    {
        public uint OwnerId { get; set; }
        public long SentAtMs { get; set; }
    }
}
