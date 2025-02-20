using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;

namespace Rats
{
    public class RatManager : MonoBehaviour
    {
        public static RatManager? Instance { get; private set; }

        public static GameObject RatNestPrefab;

        public static List<RatAI> SpawnedRats = [];
        List<List<RatAI>> ratGroups = new List<List<RatAI>>(); // Groups for batch updates

        public static List<EnemyVent> Vents { get { return RoundManager.Instance.allEnemyVents.ToList(); } }
        public static Dictionary<EnemyAI, int> EnemyHitCount = [];
        public static Dictionary<PlayerControllerB, int> PlayerThreatCounter = [];
        public static Dictionary<EnemyAI, int> EnemyThreatCounter = [];

        //// Global Configs

        public static float defenseRadius = 5f;
        public static float timeToIncreaseThreat = 3f;
        public static int threatToAttackPlayer = 100;
        public static int threatToAttackEnemy = 50;
        public static int highThreatToAttackPlayer = 250;
        public static int enemyHitsToDoDamage = 10;
        public static int playerFoodAmount = 30;
        public static float ratKingSummonChancePoison = 0.5f;
        public static float ratKingSummonChanceApparatus = 0.01f;
        public static float ratKingSummonChanceNests = 0.8f;
        public static float squeakChance = 0.1f;

        // Nests
        public static float minRatSpawnTime = 10f;
        public static float maxRatSpawnTime = 30f;
        public static int foodToSpawnRat = 5;
        public static int enemyFoodPerHPPoint = 10;
        public static int maxRats = 50;
        public static float poisonToCloseNest = 1f;

        static float updateInterval = 0.2f; // Time between group updates
        static int groupCount = 5;

        public static void InitConfigs()
        {
            updateInterval = configBatchUpdateInterval.Value;
            groupCount = configBatchGroupCount.Value;
            defenseRadius = configDefenseRadius.Value;
            timeToIncreaseThreat = configTimeToIncreaseThreat.Value;
            threatToAttackPlayer = configThreatToAttackPlayer.Value;
            threatToAttackEnemy = configThreatToAttackEnemy.Value;
            enemyHitsToDoDamage = configEnemyHitsToDoDamage.Value;
            playerFoodAmount = configPlayerFoodAmount.Value;
            ratKingSummonChancePoison = configRatKingSummonChancePoison.Value;
            ratKingSummonChanceApparatus = configRatKingSummonChanceApparatus.Value;
            ratKingSummonChanceNests = configRatKingSummonChanceNests.Value;
            squeakChance = configSqueakChance.Value;

            minRatSpawnTime = configMinRatSpawnTime.Value;
            maxRatSpawnTime = configMaxRatSpawnTime.Value;
            foodToSpawnRat = configFoodToSpawnRat.Value;
            enemyFoodPerHPPoint = configEnemyFoodPerHPPoint.Value;
            maxRats = configMaxRats.Value;
            IsLoggingEnabled = configEnableDebugging.Value;
            poisonToCloseNest = configPoisonToCloseNest.Value;
        }

        public static void Init()
        {
            if (Instance == null)
            {
                Instance = GameObject.Instantiate(new GameObject("RatManager"), Vector3.zero, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform).AddComponent<RatManager>();
                LoggerInstance.LogDebug("Init RatManager");
            }
        }

        public void Start()
        {
            LoggerInstance.LogDebug("Starting batch updater");
            StartCoroutine(BatchUpdateRoutine());
        }

        public void OnDestroy()
        {
            if (!IsServerOrHost) { return; }
            EnemyHitCount.Clear();
            EnemyThreatCounter.Clear();
            PlayerThreatCounter.Clear();
            RatNest.EnemyFoodAmount.Clear();

            foreach (RatAI rat in SpawnedRats.ToList())
            {
                if (!rat.NetworkObject.IsSpawned) { continue; }
                rat.NetworkObject.Despawn(true);
            }

            SpawnedRats.Clear();

            /*foreach (RatNest nest in RatNest.Nests.ToList())
            {
                if (!nest.NetworkObject.IsSpawned) { continue; }
                nest.NetworkObject.Despawn(true);
            }*/

            RatNest.Nests.Clear();
            Instance = null;
        }

        public void RegisterRat(RatAI rat)
        {
            SpawnedRats.Add(rat);
            RecalculateGroups();
        }

        public void RemoveRat(RatAI rat)
        {
            SpawnedRats.Remove(rat);
            RecalculateGroups();
        }

        private void RecalculateGroups()
        {
            ratGroups.Clear();

            if (SpawnedRats.Count == 0 || groupCount <= 0)
                return; // No need to process if no rats exist

            int baseSize = SpawnedRats.Count / groupCount; // Base number of rats per group
            int remainder = SpawnedRats.Count % groupCount; // Extra rats to distribute

            int index = 0;
            for (int i = 0; i < groupCount; i++)
            {
                int currentSize = baseSize + (i < remainder ? 1 : 0); // Distribute extra rats evenly
                ratGroups.Add(SpawnedRats.Skip(index).Take(currentSize).ToList());
                index += currentSize;
            }
        }

        private IEnumerator BatchUpdateRoutine()
        {
            int groupIndex = 0;
            while (true)
            {
                if (ratGroups.Count > 0) // Ensure there are groups to update
                {
                    groupIndex %= ratGroups.Count; // Prevent out-of-bounds index

                    if (ratGroups[groupIndex].Count > 0)
                    {
                        log($"Updating {ratGroups[groupIndex].Count} rats in group {groupIndex}");
                        foreach (RatAI rat in ratGroups[groupIndex])
                        {
                            rat.DoAIInterval(); // Call the update method on each rat
                        }
                    }

                    groupIndex++; // Move to the next group
                }

                yield return new WaitForSeconds(updateInterval);
            }
        }


        public static bool CalculatePath(Vector3 fromPos, Vector3 toPos)
        {
            try
            {
                Vector3 from = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, 1.75f);
                Vector3 to = RoundManager.Instance.GetNavMeshPosition(toPos, RoundManager.Instance.navHit, 1.75f);

                NavMeshPath path = new();
                return NavMesh.CalculatePath(from, to, -1, path) && Vector3.Distance(path.corners[path.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(to, RoundManager.Instance.navHit, 2.7f)) <= 1.55f; // TODO: Test this
            }
            catch
            {
                return false;
            }
        }

        public static Transform? GetRandomNode(bool outside = false)
        {
            try
            {
                GameObject[] nodes = outside ? RoundManager.Instance.outsideAINodes : RoundManager.Instance.insideAINodes;

                int randIndex = UnityEngine.Random.Range(0, nodes.Length);
                return nodes[randIndex].transform;
            }
            catch
            {
                return null;
            }
        }

        public static int GetOpenNestCount()
        {
            return RatNest.Nests.Where(x => x.IsOpen).Count();
        }

        public static RatNest? GetClosestNest(Vector3 position, bool checkForPath = false)
        {
            float closestDistance = 4000f;
            RatNest closestNest = null!;
            if (RatKingAI.Instance != null)
            {
                closestNest = RatKingAI.Instance.KingNest;
            }

            foreach (var nest in RatNest.Nests)
            {
                if (nest == null || !nest.IsOpen) { continue; }
                float distance = Vector3.Distance(position, nest.transform.position);
                if (distance >= closestDistance) { continue; }
                if (checkForPath && !CalculatePath(position, nest.transform.position)) { continue; }
                closestDistance = distance;
                closestNest = nest;
            }

            return closestNest;
        }

        public static void SpawnNest(Vector3 position)
        {
            if (!IsServerOrHost) { return; }
            GameObject ratNest = Instantiate(RatNestPrefab, position, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
            ratNest.GetComponent<NetworkObject>().Spawn(true);
        }
    }
    public static class ListExtensions
    {
        public static List<List<T>> SplitIntoChunks<T>(this List<T> source, int chunkSize)
        {
            return source
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / chunkSize)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();
        }
    }
}