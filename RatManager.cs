using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;
using static UnityEngine.VFX.VisualEffectControlTrackController;

namespace Rats
{
    public static class RatManager
    {
        //public static int totalRatsSpawned;
        public static bool testing = true; // TODO: TESTING
        public static List<RatAI> SpawnedRats = [];
        public static List<EnemyVent> Vents { get { return RoundManager.Instance.allEnemyVents.ToList(); } }
        public static Dictionary<EnemyAI, int> EnemyHitCount = [];
        public static Dictionary<PlayerControllerB, int> PlayerThreatCounter = [];
        public static Dictionary<EnemyAI, int> EnemyThreatCounter = [];

        public static VentSettings currentVentSettings = VentSettings.AlwaysVent;
        public enum VentSettings
        {
            NoVenting,
            AlwaysVent,
            VentWhenNoPath,
            VentWhenReturningToNest
        }

        // Global Configs

        public static float defenseRadius = 5f;
        public static float timeToIncreaseThreat = 3f;
        public static int threatToAttackPlayer = 100;
        public static int threatToAttackEnemy = 50;
        public static int highThreatToAttackPlayer = 250;
        public static int enemyHitsToDoDamage = 10;
        public static int playerFoodAmount = 30;

        public static void InitConfigs()
        {
            defenseRadius = configDefenseRadius.Value;
            timeToIncreaseThreat = configTimeToIncreaseThreat.Value;
            threatToAttackPlayer = configThreatToAttackPlayer.Value;
            threatToAttackEnemy = configThreatToAttackEnemy.Value;
            enemyHitsToDoDamage = configEnemyHitsToDoDamage.Value;
            playerFoodAmount = configPlayerFoodAmount.Value;
        }

        public static EnemyVent? GetClosestVentToPosition(Vector3 pos)
        {
            float mostOptimalDistance = 2000f;
            EnemyVent? targetVent = null;
            foreach (var vent in Vents)
            {
                float distance = Vector3.Distance(pos, vent.floorNode.transform.position);

                if (CalculatePath(pos, vent.floorNode.transform.position) && distance < mostOptimalDistance)
                {
                    mostOptimalDistance = distance;
                    targetVent = vent;
                }
            }

            return targetVent;
        }

        public static bool CalculatePath(Vector3 fromPos, Vector3 toPos)
        {
            Vector3 from = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, 1.75f);
            Vector3 to = RoundManager.Instance.GetNavMeshPosition(toPos, RoundManager.Instance.navHit, 1.75f);

            NavMeshPath path = new();
            return NavMesh.CalculatePath(from, to, -1, path) && Vector3.Distance(path.corners[path.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(to, RoundManager.Instance.navHit, 2.7f)) <= 1.55f; // TODO: Test this
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
    }
}
