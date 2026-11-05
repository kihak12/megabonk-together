using MemoryPack;

namespace MegabonkTogether.Common.Messages.GameNetworkMessages
{
    /// <summary>
    /// The host's game clocks, so the stage timer is the same for everyone.
    /// The host sends them every few seconds (RequesterId = 0) and a client only realigns when it drifted too far.
    /// A player can also ask for them from the pause menu: a client sends IsRequest to the host,
    /// which answers to everyone with RequesterId set, and clients then realign whatever the gap
    /// </summary>
    [MemoryPackable]
    public partial class TimerSync : IGameNetworkMessage
    {
        public uint OwnerId { get; set; }
        public uint RequesterId { get; set; } // 0 = periodic sync from the host
        public bool IsRequest { get; set; }
        public int StageIndex { get; set; }
        public float StageTimer { get; set; }
        public float RunTimer { get; set; }
        public float FinalSwarmTimer { get; set; }
        public float DifficultyTimer { get; set; }
    }
}
