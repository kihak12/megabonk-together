using MegabonkTogether.Helpers;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using UnityEngine;

namespace MegabonkTogether.Services
{
    public interface IEncounterService
    {
        public bool IsClosable();

        public void AddClosedEncounterForPlayer(uint playerId);

        public void ClearClosedEncounters();

        public void Close();
        public void Unclose();

        /// <summary>
        /// Start the waiting timeout (if not already running). TimedOut is raised if the encounter is not closed in time
        /// </summary>
        public void StartTimeout();

        public event Action TimedOut;
    }

    internal class EncounterService(IPlayerManagerService playerManagerService) : IEncounterService
    {
        private const float WAIT_TIMEOUT = 45f; //Never wait forever if a player never gets (or never closes) the same encounter

        private readonly ConcurrentDictionary<uint, byte> closedEncounterPerPlayer = new();
        private bool forceClose = false;
        private int encounterGeneration = 0; //Bumped on every clear so a running timeout knows the encounter it was waiting for is gone
        private bool isTimeoutRunning = false;

        public event Action TimedOut;

        public void AddClosedEncounterForPlayer(uint playerId)
        {
            closedEncounterPerPlayer.TryAdd(playerId, 0);
        }

        public void ClearClosedEncounters()
        {
            closedEncounterPerPlayer.Clear();
            forceClose = false;
            encounterGeneration++;
            isTimeoutRunning = false;
        }

        /// <summary>
        /// Closable when every player still in the game has closed the encounter.
        /// Checking each current player (instead of counting) ignores players that left after closing
        /// </summary>
        public bool IsClosable()
        {
            if (forceClose)
            {
                return true;
            }

            return playerManagerService.GetAllPlayers().All(player => closedEncounterPerPlayer.ContainsKey(player.ConnectionId));
        }

        public void Close()
        {
            forceClose = true;
        }

        public void Unclose()
        {
            forceClose = false;
        }

        public void StartTimeout()
        {
            if (isTimeoutRunning)
            {
                return;
            }

            isTimeoutRunning = true;
            CoroutineRunner.Instance.Run(TimeoutRoutine(encounterGeneration));
        }

        private IEnumerator TimeoutRoutine(int generation)
        {
            float waited = 0f;
            while (waited < WAIT_TIMEOUT)
            {
                yield return null;

                if (generation != encounterGeneration)
                {
                    yield break; //Closed normally
                }

                waited += Time.unscaledDeltaTime; //The game is paused while waiting
            }

            isTimeoutRunning = false;
            TimedOut?.Invoke();
        }
    }
}
