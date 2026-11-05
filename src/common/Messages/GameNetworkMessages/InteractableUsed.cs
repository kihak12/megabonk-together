using MemoryPack;

namespace MegabonkTogether.Common.Messages
{
    [MemoryPackable]
    public partial class InteractableUsed : IGameNetworkMessage
    {
        public uint NetplayId { get; set; }
        public InteractableAction Action { get; set; } = InteractableAction.Used;
        public bool IsPortal { get; set; }
        public bool IsFinalPortal { get; set; }
        public bool IsCryptKey { get; set; }
        public uint OwnerId { get; set; }
        public bool IsMicrowaveAndHaveItem { get; set; }
        public bool IsChest { get; set; } // A chest bought on the map. Lets a player that already left the stage still get it (shared experience)
        public int ChestType { get; set; } // The game's EChest value, only meaningful with IsChest
    }

    public enum InteractableAction
    {
        Used = 0,
        Interact = 1,
        Destroy = 2
    }
}
