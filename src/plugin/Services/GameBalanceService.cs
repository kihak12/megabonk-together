using Assets.Scripts.Actors.Enemies;
using Assets.Scripts.Managers;
using System;
using System.Linq;

namespace MegabonkTogether.Services
{
    public interface IGameBalanceService
    {
        public float GetCreditsTimerMultiplier();
        public float GetEnemyHpMultiplier(EEnemyFlag enemyFlag);

        /// <summary>
        /// Damage dealt by enemies to players. Computed on every peer (each client computes the damage it takes),
        /// so it only depends on the player count fixed at stage start
        /// </summary>
        public float GetEnemyDamageMultiplier();
        public float GetEnemySpeedMultiplier();
        public float GetEliteChanceMultiplier();
        public float GetFreeChestSpawnRateMultiplier();
        public int GetPickupXpValue();
        public void Initialize();
        public int GetMaxEnemiesSpawnable();
        public float GetBossLampRequiredCharge();
    }

    public enum DifficultyLevel
    {
        None,
        Duo,
        Trio,
        Quad,
        Five,
        Six
    }

    internal class GameBalanceService(IPlayerManagerService playerManagerService) : IGameBalanceService
    {
        private const float hpScalingPerAdditionalAlivePlayer = 0.2f; //Hp follows the alive players: enemies spawn weaker once someone is dead
        private const float damageScalingPerAdditionalPlayer = 0.1f;
        private const float eliteChanceScalingPerAdditionalPlayer = 0.15f;
        private const float speedScalingPerAdditionalPlayer = 0.03f;
        private const float speedScalingPerDifficulty = 0.15f; //Difficulty 2.0x -> +15% speed, 0.5x -> -7.5%: scaling speed fully would be unplayable
        private const float enemyCapScalingPerDifficulty = 0.5f; //Difficulty 2.0x -> +50% enemy cap, 0.5x -> -25%
        private const float creditsScalingPerDifficulty = 0.05f; //Only matters if the BaseSummoner credits patch is re enabled
        private const int maxEnemiesHardCap = 800; //FPS safety net whatever the difficulty
        private static float Difficulty => Helpers.DifficultyHelper.GetMultiplier(); //Host decided lobby option, same value on every peer
        private int playersCountAtStageStart = 0; //Connected players when the stage started: damage, elites and speed don't drop when someone dies
        private int PlayersCount => playerManagerService.GetAllPlayersAlive().Count();
        private int ConnectedPlayersCount => playersCountAtStageStart > 0 ? playersCountAtStageStart : playerManagerService.GetAllPlayers().Count();
        private static int StageIndex => MapController.runConfig?.mapData.stages.IndexOf(MapController.currentStage) ?? 0;
        private const float baseBossLampInitialChargeTimeSeconds = 3.0f;


        public int GetMaxEnemiesSpawnable()
        {
            if (GameManager.Instance.IsFinalSwarm())
            {
                return 400; // Keep the original cap during Final Swarm
            }

            int baseCap = GetDifficultyLevelByPlayers() switch
            {
                DifficultyLevel.Quad or DifficultyLevel.Five or DifficultyLevel.Six => 600,
                _ => 500,
            };

            return Math.Min(maxEnemiesHardCap, (int)Math.Round(baseCap * DifficultyScaling(enemyCapScalingPerDifficulty)));
        }

        public float GetCreditsTimerMultiplier()
        {
            float baseMultiplier = GetDifficultyLevelByPlayers() switch
            {
                DifficultyLevel.Duo => 1.01f,
                DifficultyLevel.Trio => 1.02f,
                DifficultyLevel.Quad => 1.03f,
                DifficultyLevel.Five => 1.04f,
                DifficultyLevel.Six => 1.05f,
                _ => 1.0f,
            };

            float stageMultiplier = StageIndex switch
            {
                0 => 1.0f,
                1 => 1.05f,
                2 => 1.07f,
                _ => 1.0f
            };

            return baseMultiplier * stageMultiplier * DifficultyScaling(creditsScalingPerDifficulty);
        }

        public float GetEnemyHpMultiplier(EEnemyFlag enemyFlag)
        {
            float baseMultiplier = enemyFlag switch
            {
                EEnemyFlag.Boss => 1.2f,
                EEnemyFlag.Elite => 1.05f,
                EEnemyFlag.FinalBoss => 1.25f,
                EEnemyFlag.SummonerMiniboss => 1.1f,
                EEnemyFlag.AnyBoss => 1.1f,
                EEnemyFlag.Challenge => 1.15f,
                _ => 1f
            };

            float playerScaling = 1f + AdditionalPlayers(PlayersCount) * hpScalingPerAdditionalAlivePlayer;

            float stageMultiplier = StageIndex switch
            {
                0 => 1.0f,
                1 => 1.1f,
                2 => 1.2f,
                _ => 1.0f
            };

            return baseMultiplier * playerScaling * stageMultiplier * Difficulty;
        }

        public float GetEnemyDamageMultiplier()
        {
            return (1f + AdditionalPlayers(ConnectedPlayersCount) * damageScalingPerAdditionalPlayer) * Difficulty;
        }

        public float GetEnemySpeedMultiplier()
        {
            return (1f + AdditionalPlayers(ConnectedPlayersCount) * speedScalingPerAdditionalPlayer) * DifficultyScaling(speedScalingPerDifficulty);
        }

        public float GetEliteChanceMultiplier()
        {
            return (1f + AdditionalPlayers(ConnectedPlayersCount) * eliteChanceScalingPerAdditionalPlayer) * Difficulty;
        }

        /// <summary>
        /// Damped difficulty factor: 1 at 1.0x, moving by sensitivity for each +1.0 of difficulty (0.5 -> 1 + 0.5 * sensitivity at 2.0x)
        /// </summary>
        private static float DifficultyScaling(float sensitivity)
        {
            return 1f + (Difficulty - 1f) * sensitivity;
        }

        private static int AdditionalPlayers(int playersCount)
        {
            return Math.Max(0, playersCount - 1);
        }

        public float GetFreeChestSpawnRateMultiplier()
        {
            float baseMultiplier = GetDifficultyLevelByPlayers() switch
            {
                DifficultyLevel.Duo => 1.5f,
                DifficultyLevel.Trio => 1.8f,
                DifficultyLevel.Quad => 2.0f,
                DifficultyLevel.Five => 2.2f,
                DifficultyLevel.Six => 2.4f,
                _ => 1f,
            };

            float stageMultiplier = StageIndex switch
            {
                0 => 1.0f,
                1 => 1.1f,
                2 => 1.15f,
                _ => 1.0f
            };

            return baseMultiplier * stageMultiplier;
        }

        public int GetPickupXpValue()
        {
            return PlayersCount switch
            {
                1 => 1,
                >= 2 and <= 4 => 2,
                5 => 3,
                _ => 1
            };
        }

        public void Initialize()
        {
            playersCountAtStageStart = playerManagerService.GetAllPlayers().Count();

            var creditsMultiplier = GetCreditsTimerMultiplier();
            var enemyHpMultiplier = GetEnemyHpMultiplier(EEnemyFlag.None);
            var chestSpawnMultiplier = GetFreeChestSpawnRateMultiplier();
            var xpValue = GetPickupXpValue();

            Plugin.Log.LogInfo($"[GameBalance] Initialized for {PlayersCount} players ({playersCountAtStageStart} connected), Stage {StageIndex + 1}, Difficulty: {GetDifficultyLevelByPlayers()}");
            Plugin.Log.LogInfo($"[GameBalance] Credits Timer Multiplier (Disabled): {creditsMultiplier:F2}x");
            Plugin.Log.LogInfo($"[GameBalance] Basic Enemy HP Base Multiplier: {enemyHpMultiplier:F2}x");
            Plugin.Log.LogInfo($"[GameBalance] Enemy Damage Multiplier: {GetEnemyDamageMultiplier():F2}x, Speed: {GetEnemySpeedMultiplier():F2}x, Elite Chance: {GetEliteChanceMultiplier():F2}x");
            Plugin.Log.LogInfo($"[GameBalance] Difficulty option: {Helpers.DifficultyHelper.Format(Difficulty)} -> HP x{Difficulty:F2}, Damage x{Difficulty:F2}, Speed x{DifficultyScaling(speedScalingPerDifficulty):F2}, Elite chance x{Difficulty:F2}, Enemy cap x{DifficultyScaling(enemyCapScalingPerDifficulty):F2}");
            Plugin.Log.LogInfo($"[GameBalance] Free Chest Spawn Rate Multiplier: {chestSpawnMultiplier:F2}x");

            if (Plugin.Instance.Mode.EnabledSharedExperience.HasValue && !Plugin.Instance.Mode.EnabledSharedExperience.Value)
            {
                Plugin.Log.LogInfo($"[GameBalance] XP Value: {xpValue}");
            }
        }

        private DifficultyLevel GetDifficultyLevelByPlayers()
        {
            return PlayersCount switch
            {
                2 => DifficultyLevel.Duo,
                3 => DifficultyLevel.Trio,
                4 => DifficultyLevel.Quad,
                5 => DifficultyLevel.Five,
                6 => DifficultyLevel.Six,
                _ => DifficultyLevel.None
            };
        }

        public float GetBossLampRequiredCharge()
        {
            return GetDifficultyLevelByPlayers() switch
            {
                DifficultyLevel.Duo => baseBossLampInitialChargeTimeSeconds * 2f,
                DifficultyLevel.Trio => baseBossLampInitialChargeTimeSeconds * 2.5f,
                DifficultyLevel.Quad => baseBossLampInitialChargeTimeSeconds * 3f,
                DifficultyLevel.Five => baseBossLampInitialChargeTimeSeconds * 3.5f,
                DifficultyLevel.Six => baseBossLampInitialChargeTimeSeconds * 4f,
                _ => baseBossLampInitialChargeTimeSeconds,
            };
        }

    }
}
