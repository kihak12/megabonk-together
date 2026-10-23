using MemoryPack;

namespace MegabonkTogether.Common.Messages.GameNetworkMessages
{
    /// <summary>
    /// Shared experience: absolute xp state of a player, used to re-align players that drifted apart.
    /// Receivers only ever raise their xp to the sent value (never lower it).
    /// A player with more xp than the sender answers with its own state (IsReply) so the sender gets raised too
    /// </summary>
    [MemoryPackable]
    public partial class XpSync : IGameNetworkMessage
    {
        public uint OwnerId { get; set; }
        public int Xp { get; set; }
        public int Level { get; set; }
        public float LeftOverXp { get; set; }
        public bool IsReply { get; set; }
    }
}
