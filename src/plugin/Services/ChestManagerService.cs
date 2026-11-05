
using Assets.Scripts.Inventory__Items__Pickups.Interactables;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace MegabonkTogether.Services
{
    public interface IChestManagerService
    {
        public uint AddChest(Object chestObject);

        public void PushNextChestId(uint chestId);
        public uint? SetNextChest(Object chestObject);
        public Object? GetChest(uint chestId);

        /// <summary>
        /// Forget a chest that got opened. Its id stays remembered as opened, see WasOpened
        /// </summary>
        public void RemoveChest(uint chestId);

        /// <summary>
        /// Find the id of a chest object. Also looks in the stage that was just left,
        /// so a chest opened while teleporting to the next stage can still be named to the other players
        /// </summary>
        public KeyValuePair<uint, Object> GetChestByReference(OpenChest instance);

        /// <summary>
        /// Remember that this chest was opened (by the local player, or by a message already handled)
        /// </summary>
        public void MarkOpened(uint chestId);
        public bool WasOpened(uint chestId);
        public void ResetForNextLevel();

        /// <summary>
        /// End of the netplay session
        /// </summary>
        public void Reset();
    }
    public class ChestManagerService : IChestManagerService
    {
        private readonly ConcurrentDictionary<uint, Object> chests = [];
        private readonly ConcurrentDictionary<uint, Object> previousStageChests = []; //The stage that was just left: its objects live until the scene is unloaded
        private readonly ConcurrentDictionary<uint, byte> openedChestIds = [];
        private readonly ConcurrentQueue<uint> nextIds = [];
        private uint nextChestId = 0; //Never reset: an id names one chest whatever the stage, so a late message can't hit a chest of the next stage
        public uint AddChest(Object chestObject)
        {
            var chestId = nextChestId++;

            openedChestIds.TryRemove(chestId, out _);
            chests.TryAdd(chestId, chestObject);

            return chestId;
        }

        public void PushNextChestId(uint chestId)
        {
            nextIds.Enqueue(chestId);
        }

        public uint? SetNextChest(Object chestObject)
        {
            if (nextIds.TryDequeue(out var chestId))
            {
                openedChestIds.TryRemove(chestId, out _); //A host on an older build reuses its ids on every stage
                chests.TryAdd(chestId, chestObject);
                return chestId;
            }
            else
            {
                Plugin.Log.LogWarning($"No chest id available for spawned chest.");
                return null;
            }
        }

        public Object? GetChest(uint chestId)
        {
            chests.TryGetValue(chestId, out var chestObject);
            return chestObject;
        }
        public void RemoveChest(uint chestId)
        {
            chests.TryRemove(chestId, out _);
            openedChestIds.TryAdd(chestId, 0);
        }

        public void MarkOpened(uint chestId)
        {
            openedChestIds.TryAdd(chestId, 0);
        }

        public bool WasOpened(uint chestId)
        {
            return openedChestIds.ContainsKey(chestId);
        }

        public KeyValuePair<uint, Object> GetChestByReference(OpenChest instance)
        {
            foreach (var kvp in chests)
            {
                if (kvp.Value == instance.gameObject)
                {
                    return kvp;
                }
            }

            foreach (var kvp in previousStageChests)
            {
                if (kvp.Value == instance.gameObject)
                {
                    return kvp;
                }
            }

            return new KeyValuePair<uint, Object>(0, null);
        }

        public void ResetForNextLevel()
        {
            nextIds.Clear();
            //chests.Select(kvp => kvp.Value).ToList().ForEach(GameObject.Destroy);

            previousStageChests.Clear();
            foreach (var kvp in chests)
            {
                previousStageChests.TryAdd(kvp.Key, kvp.Value);
            }

            chests.Clear();
        }

        public void Reset()
        {
            nextIds.Clear();
            chests.Clear();
            previousStageChests.Clear();
            openedChestIds.Clear();
        }
    }
}
