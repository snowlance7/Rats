using BepInEx.Logging;
using Dissonance;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;

/* bodyparts
 * 0 head
 * 1 right arm
 * 2 left arm
 * 3 right leg
 * 4 left leg
 * 5 chest
 * 6 feet
 * 7 right hip
 * 8 crotch
 * 9 left shoulder
 * 10 right shoulder */

namespace Rats
{
    [HarmonyPatch]
    internal class TESTING : MonoBehaviour
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;
        public static bool testing = false;
        public static bool noSpawning = false;

        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {

        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            string msg = __instance.chatTextField.text;
            string[] args = msg.Split(" ");
            //log(msg);

            switch (args[0])
            {
                case "/testing":
                    testing = !testing;
                    HUDManager.Instance.DisplayTip("Testing", testing.ToString());
                    break;
                case "/spawnRat":
                    RatNest? nest = RatManager.GetClosestNest(localPlayer.transform.position);
                    logger.LogDebug("Spawning rats from TESTING");
                    nest?.SpawnRats(int.Parse(args[1]));
                    logger.LogDebug($"Spawning {args[1]} rats");
                    break;
                case "/spawnNest":
                    Vector3 position = localPlayer.transform.position + localPlayer.transform.forward * 1f;
                    RatManager.SpawnNest(position);
                    break;
                case "/spawning":
                    noSpawning = !noSpawning;
                    HUDManager.Instance.DisplayTip("NoSpawning", noSpawning.ToString());
                    break;
                case "/enemies":
                    foreach (var enemy in GetEnemies())
                    {
                        log(enemy.enemyType.name);
                    }
                    break;
                case "/refresh":
                    RoundManager.Instance.RefreshEnemiesList();
                    HoarderBugAI.RefreshGrabbableObjectsInMapList();
                    break;
                case "/levels":
                    foreach (var level in StartOfRound.Instance.levels)
                    {
                        log(level.name);
                    }
                    break;
                case "/dungeon":
                    log(RoundManager.Instance.dungeonGenerator.Generator.DungeonFlow.name);
                    break;
                case "/dungeons":
                    foreach (var dungeon in RoundManager.Instance.dungeonFlowTypes)
                    {
                        log(dungeon.dungeonFlow.name);
                    }
                    break;
                default:
                    break;
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnEnemyFromVent))]
        public static bool SpawnEnemyFromVentPreFix()
        {
            if (noSpawning) { return false; }
            return true;
        }

        public static List<SpawnableEnemyWithRarity> GetEnemies()
        {
            log("Getting enemies");
            List<SpawnableEnemyWithRarity> enemies = new List<SpawnableEnemyWithRarity>();
            enemies = GameObject.Find("Terminal")
                .GetComponentInChildren<Terminal>()
                .moonsCatalogueList
                .SelectMany(x => x.Enemies.Concat(x.DaytimeEnemies).Concat(x.OutsideEnemies))
                .Where(x => x != null && x.enemyType != null && x.enemyType.name != null)
                .GroupBy(x => x.enemyType.name, (k, v) => v.First())
                .ToList();

            log($"Enemy types: {enemies.Count}");
            return enemies;
        }

        public static void LogChat(string msg)
        {
            HUDManager.Instance.AddChatMessage(msg, "Server");
        }

        public static Vector3 GetSpeed()
        {
            float num3 = localPlayer.movementSpeed / localPlayer.carryWeight;
            if (localPlayer.sinkingValue > 0.73f)
            {
                num3 = 0f;
            }
            else
            {
                if (localPlayer.isCrouching)
                {
                    num3 /= 1.5f;
                }
                else if (localPlayer.criticallyInjured && !localPlayer.isCrouching)
                {
                    num3 *= localPlayer.limpMultiplier;
                }
                if (localPlayer.isSpeedCheating)
                {
                    num3 *= 15f;
                }
                if (localPlayer.movementHinderedPrev > 0)
                {
                    num3 /= 2f * localPlayer.hinderedMultiplier;
                }
                if (localPlayer.drunkness > 0f)
                {
                    num3 *= StartOfRound.Instance.drunknessSpeedEffect.Evaluate(localPlayer.drunkness) / 5f + 1f;
                }
                if (!localPlayer.isCrouching && localPlayer.crouchMeter > 1.2f)
                {
                    num3 *= 0.5f;
                }
                if (!localPlayer.isCrouching)
                {
                    float num4 = Vector3.Dot(localPlayer.playerGroundNormal, localPlayer.walkForce);
                    if (num4 > 0.05f)
                    {
                        localPlayer.slopeModifier = Mathf.MoveTowards(localPlayer.slopeModifier, num4, (localPlayer.slopeModifierSpeed + 0.45f) * Time.deltaTime);
                    }
                    else
                    {
                        localPlayer.slopeModifier = Mathf.MoveTowards(localPlayer.slopeModifier, num4, localPlayer.slopeModifierSpeed / 2f * Time.deltaTime);
                    }
                    num3 = Mathf.Max(num3 * 0.8f, num3 + localPlayer.slopeIntensity * localPlayer.slopeModifier);
                }
            }

            Vector3 vector3 = new Vector3(0f, 0f, 0f);
            int num5 = Physics.OverlapSphereNonAlloc(localPlayer.transform.position, 0.65f, localPlayer.nearByPlayers, StartOfRound.Instance.playersMask);
            for (int i = 0; i < num5; i++)
            {
                vector3 += Vector3.Normalize((localPlayer.transform.position - localPlayer.nearByPlayers[i].transform.position) * 100f) * 1.2f;
            }
            int num6 = Physics.OverlapSphereNonAlloc(localPlayer.transform.position, 1.25f, localPlayer.nearByPlayers, 524288);
            for (int j = 0; j < num6; j++)
            {
                EnemyAICollisionDetect component = localPlayer.nearByPlayers[j].gameObject.GetComponent<EnemyAICollisionDetect>();
                if (component != null && component.mainScript != null && !component.mainScript.isEnemyDead && Vector3.Distance(localPlayer.transform.position, localPlayer.nearByPlayers[j].transform.position) < component.mainScript.enemyType.pushPlayerDistance)
                {
                    vector3 += Vector3.Normalize((localPlayer.transform.position - localPlayer.nearByPlayers[j].transform.position) * 100f) * component.mainScript.enemyType.pushPlayerForce;
                }
            }

            Vector3 vector4 = localPlayer.walkForce * num3 * localPlayer.sprintMultiplier + new Vector3(0f, localPlayer.fallValue, 0f) + vector3;
            vector4 += localPlayer.externalForces;
            return vector4;
        }
    }
}