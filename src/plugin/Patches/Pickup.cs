using Assets.Scripts.Inventory__Items__Pickups.Pickups;
using HarmonyLib;
using MegabonkTogether.Services;
using Microsoft.Extensions.DependencyInjection;
using MonoMod.Utils;
using UnityEngine;

namespace MegabonkTogether.Patches
{
    [HarmonyPatch(typeof(Pickup))]
    internal static class PickupPatches
    {
        private static readonly ISynchronizationService synchronizationService = Plugin.Services.GetService<ISynchronizationService>();
        private static readonly IPlayerManagerService playerManagerService = Plugin.Services.GetService<IPlayerManagerService>();
        private static readonly IPickupManagerService pickupManagerService = Plugin.Services.GetService<IPickupManagerService>();

        /// <summary>
        /// Apply pickup if owned by local player unless pickup is time or magnet or if it's xp and shared experience is enabled
        /// Ignore on remote on non shared experience
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Pickup.ApplyPickup))]
        public static bool ApplyPickup_Prefix(Pickup __instance, ref bool? __state)
        {
            //Also while teleporting to the next stage: a pickup owned by another player must never be applied locally
            if (!synchronizationService.IsNetplaySessionActive())
            {
                return true;
            }
            __state = true;

            if (__instance.ePickup == EPickup.Time || __instance.ePickup == EPickup.Magnet)
            {
                return true;
            }

            var dynPickup = DynamicData.For(__instance);
            var ownerId = dynPickup.Get<uint?>("ownerId");
            if (ownerId.HasValue)
            {
                var isRemote = playerManagerService.IsRemoteConnectionId(ownerId.Value);

                if (isRemote)
                {
                    __state = false;


                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Send pickup applied event so other clients can acknowledge pickup consumption unless if it was ignored or from remote
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Pickup.ApplyPickup))]
        public static void ApplyPickup_Postfix(Pickup __instance, bool? __state)
        {
            if (!synchronizationService.IsNetplaySessionActive())
            {
                return;
            }

            if (__state.HasValue && !__state.Value) //Ignore if prefix already handled it
            {
                DynamicData.For(__instance).Data.Clear();
                PickupManager.Instance.DespawnPickup(__instance);
                return;
            }

            var dynPickup = DynamicData.For(__instance);
            var ownerId = dynPickup.Get<uint?>("ownerId");
            if (ownerId.HasValue && playerManagerService.IsRemoteConnectionId(ownerId.Value))
            {
                return;
            }

            if (!Plugin.CAN_SEND_MESSAGES)
            {
                return;
            }

            if (synchronizationService.IsLoadingNextLevel())
            {
                //The pickup manager is already reset for the next level: the gain itself is shared (xp / gold patches), nothing else to notify
                dynPickup.Data.Clear();
                return;
            }

            synchronizationService.OnPickupApplied(__instance);
        }

        /// <summary>
        /// Start following the player if the pickup is owned 
        /// If not ask the server to start following
        /// Skip if we have a request (Example when interacting with a pot)
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Pickup.StartFollowingPlayer))]
        public static bool StartFollowingPlayer_Prefix(Pickup __instance, ref Transform target)
        {
            if (!synchronizationService.IsNetplaySessionActive())
            {
                return true;
            }

            var netplayerId = playerManagerService.PeakNetplayerPositionRequest();
            if (netplayerId.HasValue)
            {
                target = playerManagerService.GetNetPlayerByNetplayId(netplayerId.Value).Model.transform;
                return true;
            }

            __instance.pickedUp = false;

            var dynInstance = DynamicData.For(__instance);

            var ownerId = dynInstance.Get<uint?>("ownerId");
            if (ownerId.HasValue)
            {
                return true;
            }

            if (synchronizationService.IsLoadingNextLevel())
            {
                //Leaving the stage: an owner can't be granted anymore (the host doesn't poll while loading), so unowned pickups are left behind
                //instead of being grabbed by every player locally
                return false;
            }

            var hasSent = dynInstance.Get<bool?>("hasSentAlready");
            if (hasSent.HasValue && hasSent.Value)
            {
                return false;
            }

            synchronizationService.OnWantToStartFollowingPickup(__instance);
            return false;
        }
    }
}
