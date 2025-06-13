﻿using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Extras;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;

namespace Rats
{
    public static class Utils
    {
        private static ManualLogSource logger = LoggerInstance;

        public static bool inTestRoom => StartOfRound.Instance?.testRoom != null;
        public static bool testing = false;
        public static bool spawningAllowed = true;
        public static bool trailerMode = false;

        public static GameObject[]? outsideAINodes;
        public static GameObject[]? insideAINodes;
        public static Vector3[]? outsideNodePositions;
        public static Vector3[]? insideNodePositions;

        public static void ChatCommand(string[] args)
        {
            switch (args[0])
            {
                case "/spawning":
                    spawningAllowed = !spawningAllowed;
                    HUDManager.Instance.DisplayTip("Spawning Allowed", spawningAllowed.ToString());
                    break;
                case "/hazards":
                    Dictionary<string, GameObject> hazards = Utils.GetAllHazards();

                    foreach (var hazard in hazards)
                    {
                        logger.LogDebug(hazard);
                    }
                    break;
                case "/testing":
                    testing = !testing;
                    HUDManager.Instance.DisplayTip("Testing", testing.ToString());
                    break;
                case "/surfaces":
                    foreach (var surface in StartOfRound.Instance.footstepSurfaces)
                    {
                        logger.LogDebug(surface.surfaceTag);
                    }
                    break;
                case "/enemies":
                    foreach (var enemy in Utils.GetEnemies())
                    {
                        logger.LogDebug(enemy.enemyType.name);
                    }
                    break;
                case "/refresh":
                    RoundManager.Instance.RefreshEnemiesList();
                    HoarderBugAI.RefreshGrabbableObjectsInMapList();
                    break;
                case "/levels":
                    foreach (var level in StartOfRound.Instance.levels)
                    {
                        logger.LogDebug(level.name);
                    }
                    break;
                case "/dungeon":
                    logger.LogDebug(RoundManager.Instance.dungeonGenerator.Generator.DungeonFlow.name);
                    break;
                case "/dungeons":
                    foreach (var dungeon in RoundManager.Instance.dungeonFlowTypes)
                    {
                        logger.LogDebug(dungeon.dungeonFlow.name);
                    }
                    break;
                default:
                    break;
            }
        }

        public static void LogChat(string msg)
        {
            HUDManager.Instance.AddChatMessage(msg, "Server");
        }

        public static void RegisterItem(bool enable, string itemPath, string levelRarities = "", string customLevelRarities = "", int minValue = 0, int maxValue = 0)
        {
            if (!enable) { return; }
            Item item = ModAssets!.LoadAsset<Item>(itemPath);
            if (item == null) { LoggerInstance.LogError($"Error: Couldn't get prefab from {itemPath}"); return; }
            LoggerInstance.LogDebug($"Got {item.name} prefab");

            item.minValue = minValue;
            item.maxValue = maxValue;

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(item.spawnPrefab);
            Utilities.FixMixerGroups(item.spawnPrefab);
            LethalLib.Modules.Items.RegisterScrap(item, GetLevelRarities(levelRarities), GetCustomLevelRarities(customLevelRarities));
        }

        public static void RegisterShopItem(bool enable, string itemPath, int price)
        {
            if (!enable) { return; }
            Item item = ModAssets!.LoadAsset<Item>(itemPath);
            if (item == null) { LoggerInstance.LogError($"Error: Couldn't get prefab from {itemPath}"); return; }
            LoggerInstance.LogDebug($"Got {item.name} prefab");

            item.creditsWorth = price;

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(item.spawnPrefab);
            Utilities.FixMixerGroups(item.spawnPrefab);
            LethalLib.Modules.Items.RegisterShopItem(item, price);
        }

        public static void RegisterEnemy(bool enable, string enemyPath, string tnPath, string tkPath, string levelRarities = "", string customLevelRarities = "")
        {
            if (!enable) { return; }
            EnemyType enemy = ModAssets!.LoadAsset<EnemyType>(enemyPath);
            if (enemy == null) { LoggerInstance.LogError($"Error: Couldn't get prefab from {enemyPath}"); return; }
            LoggerInstance.LogDebug($"Got {enemy.name} prefab");

            TerminalNode tn = ModAssets.LoadAsset<TerminalNode>(tnPath);
            TerminalKeyword tk = ModAssets.LoadAsset<TerminalKeyword>(tkPath);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(enemy.enemyPrefab);
            Enemies.RegisterEnemy(enemy, GetLevelRarities(levelRarities), GetCustomLevelRarities(customLevelRarities), tn, tk);
        }

        // Xu's code for registering map objects with configs
        public static GameObject? RegisterInsideMapObjectWithConfig(string mapObjectPath, string configString)
        {
            SpawnableMapObjectDef SpawnPrefab = ModAssets.LoadAsset<SpawnableMapObjectDef>(mapObjectPath);
            if (SpawnPrefab == null) { LoggerInstance.LogError("Error: Couldnt get map object prefab from assets"); return null; }
            LoggerInstance.LogDebug("Registering map object network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(SpawnPrefab.spawnableMapObject.prefabToSpawn);

            /*SpawnableMapObjectDef mapObjDef = ScriptableObject.CreateInstance<SpawnableMapObjectDef>();
            mapObjDef.spawnableMapObject = new SpawnableMapObject
            {
                prefabToSpawn = prefab
            };*/


            (Dictionary<Levels.LevelTypes, string> spawnRateByLevelType, Dictionary<string, string> spawnRateByCustomLevelType) = ConfigParsingWithCurve(configString);


            foreach (var entry in spawnRateByLevelType)
            {
                //AnimationCurve animationCurve = CreateCurveFromString(entry.Value, prefab.name);
                AnimationCurve animationCurve = CreateCurveFromString(entry.Value, SpawnPrefab.spawnableMapObject.prefabToSpawn.name);
                MapObjects.RegisterMapObject(SpawnPrefab, entry.Key, (level) => animationCurve);
            }
            foreach (var entry in spawnRateByCustomLevelType)
            {
                //AnimationCurve animationCurve = CreateCurveFromString(entry.Value, prefab.name);
                AnimationCurve animationCurve = CreateCurveFromString(entry.Value, SpawnPrefab.spawnableMapObject.prefabToSpawn.name);
                MapObjects.RegisterMapObject(SpawnPrefab, Levels.LevelTypes.None, new string[] { entry.Key }, (level) => animationCurve);
            }

            return SpawnPrefab.spawnableMapObject.prefabToSpawn;
        }

        private static (Dictionary<Levels.LevelTypes, string> spawnRateByLevelType, Dictionary<string, string> spawnRateByCustomLevelType) ConfigParsingWithCurve(string configMoonRarity)
        {
            Dictionary<Levels.LevelTypes, string> spawnRateByLevelType = new();
            Dictionary<string, string> spawnRateByCustomLevelType = new();
            foreach (string entry in configMoonRarity.Split('|').Select(s => s.Trim()))
            {
                string[] entryParts = entry.Split('-').Select(s => s.Trim()).ToArray();

                if (entryParts.Length != 2) continue;

                string name = entryParts[0].ToLowerInvariant();

                if (name == "custom")
                {
                    name = "modded";
                }

                if (System.Enum.TryParse(name, true, out Levels.LevelTypes levelType))
                {
                    spawnRateByLevelType[levelType] = entryParts[1];
                }
                else
                {
                    // Try appending "Level" to the name and re-attempt parsing
                    string modifiedName = name + "level";
                    if (System.Enum.TryParse(modifiedName, true, out levelType))
                    {
                        spawnRateByLevelType[levelType] = entryParts[1];
                    }
                    else
                    {
                        spawnRateByCustomLevelType[name] = entryParts[1];
                    }
                }
            }
            return (spawnRateByLevelType, spawnRateByCustomLevelType);
        }

        public static AnimationCurve CreateCurveFromString(string keyValuePairs, string nameOfThing)
        {
            // Split the input string into individual key-value pairs
            string[] pairs = keyValuePairs.Split(';').Select(s => s.Trim()).ToArray();
            if (pairs.Length == 0)
            {
                if (int.TryParse(keyValuePairs, out int result))
                {
                    return new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, result));
                }
                else
                {
                    LoggerInstance.LogError($"Invalid key-value pairs format: {keyValuePairs}");
                    return new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));
                }
            }
            List<Keyframe> keyframes = new();

            // Iterate over each pair and parse the key and value to create keyframes
            foreach (string pair in pairs)
            {
                string[] splitPair = pair.Split(',').Select(s => s.Trim()).ToArray();
                if (splitPair.Length == 2 &&
                    float.TryParse(splitPair[0], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float time) &&
                    float.TryParse(splitPair[1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    keyframes.Add(new Keyframe(time, value));
                }
                else
                {
                    LoggerInstance.LogError($"Failed config for hazard: {nameOfThing}");
                    LoggerInstance.LogError($"Split pair length: {splitPair.Length}");
                    LoggerInstance.LogError($"Could parse first value: {float.TryParse(splitPair[0], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float key1)}, instead got: {key1}, with splitPair0 being: {splitPair[0]}");
                    LoggerInstance.LogError($"Could parse second value: {float.TryParse(splitPair[1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float value2)}, instead got: {value2}, with splitPair1 being: {splitPair[1]}");
                    LoggerInstance.LogError($"Invalid key,value pair format: {pair}");
                }
            }

            // Create the animation curve with the generated keyframes and apply smoothing
            var curve = new AnimationCurve(keyframes.ToArray());
            for (int i = 0; i < keyframes.Count; i++)
            {
                curve.SmoothTangents(i, 0.5f); // Adjust the smoothing as necessary
            }

            return curve;
        }

        public static Dictionary<Levels.LevelTypes, int>? GetLevelRarities(string? levelsString)
        {
            try
            {
                Dictionary<Levels.LevelTypes, int> levelRaritiesDict = new Dictionary<Levels.LevelTypes, int>();

                if (levelsString != null && levelsString != "")
                {
                    string[] levels = levelsString.Split(',');

                    foreach (string level in levels)
                    {
                        string[] levelSplit = level.Split(':');
                        if (levelSplit.Length != 2) { continue; }
                        string levelType = levelSplit[0].Trim();
                        string levelRarity = levelSplit[1].Trim();

                        if (Enum.TryParse<Levels.LevelTypes>(levelType, out Levels.LevelTypes levelTypeEnum) && int.TryParse(levelRarity, out int levelRarityInt))
                        {
                            levelRaritiesDict.Add(levelTypeEnum, levelRarityInt);
                        }
                        else
                        {
                            LoggerInstance.LogError($"Error: Invalid level rarity: {levelType}:{levelRarity}");
                        }
                    }
                }
                return levelRaritiesDict;
            }
            catch (Exception e)
            {
                LoggerInstance.LogError($"Error: {e}");
                return null;
            }
        }

        public static Dictionary<string, int>? GetCustomLevelRarities(string? levelsString)
        {
            try
            {
                Dictionary<string, int> customLevelRaritiesDict = new Dictionary<string, int>();

                if (levelsString != null)
                {
                    string[] levels = levelsString.Split(',');

                    foreach (string level in levels)
                    {
                        string[] levelSplit = level.Split(':');
                        if (levelSplit.Length != 2) { continue; }
                        string levelType = levelSplit[0].Trim();
                        string levelRarity = levelSplit[1].Trim();

                        if (int.TryParse(levelRarity, out int levelRarityInt))
                        {
                            customLevelRaritiesDict.Add(levelType, levelRarityInt);
                        }
                        else
                        {
                            LoggerInstance.LogError($"Error: Invalid level rarity: {levelType}:{levelRarity}");
                        }
                    }
                }
                return customLevelRaritiesDict;
            }
            catch (Exception e)
            {
                LoggerInstance.LogError($"Error: {e}");
                return null;
            }
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

        public static void FreezePlayer(PlayerControllerB player, bool value)
        {
            player.disableInteract = value;
            player.disableLookInput = value;
            player.disableMoveInput = value;
        }

        public static void DespawnItemInSlotOnClient(int itemSlot)
        {
            HUDManager.Instance.itemSlotIcons[itemSlot].enabled = false;
            localPlayer.DestroyItemInSlotAndSync(itemSlot);
        }

        public static void MakePlayerInvisible(PlayerControllerB player, bool value)
        {
            GameObject scavengerModel = player.gameObject.transform.Find("ScavengerModel").gameObject;
            if (scavengerModel == null) { LoggerInstance.LogError("ScavengerModel not found"); return; }
            scavengerModel.transform.Find("LOD1").gameObject.SetActive(!value);
            scavengerModel.transform.Find("LOD2").gameObject.SetActive(!value);
            scavengerModel.transform.Find("LOD3").gameObject.SetActive(!value);
            scavengerModel.transform.Find("metarig/spine/spine.001/spine.002/spine.003/LevelSticker").gameObject.SetActive(!value);
            scavengerModel.transform.Find("metarig/spine/spine.001/spine.002/spine.003/BetaBadge").gameObject.SetActive(!value);

        }

        public static List<SpawnableEnemyWithRarity> GetEnemies()
        {
            List<SpawnableEnemyWithRarity> enemies = new List<SpawnableEnemyWithRarity>();
            enemies = GameObject.Find("Terminal")
                .GetComponentInChildren<Terminal>()
                .moonsCatalogueList
                .SelectMany(x => x.Enemies.Concat(x.DaytimeEnemies).Concat(x.OutsideEnemies))
                .Where(x => x != null && x.enemyType != null && x.enemyType.name != null)
                .GroupBy(x => x.enemyType.name, (k, v) => v.First())
                .ToList();

            return enemies;
        }

        public static EnemyVent GetClosestVentToPosition(Vector3 pos)
        {
            float mostOptimalDistance = 2000f;
            EnemyVent targetVent = null!;
            foreach (var vent in RoundManager.Instance.allEnemyVents)
            {
                float distance = Vector3.Distance(pos, vent.floorNode.transform.position);

                if (distance < mostOptimalDistance)
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

        public static T? GetClosestGameObjectOfType<T>(Vector3 position) where T : Component
        {
            T[] objects = GameObject.FindObjectsOfType<T>();
            T closest = null!;
            float closestDistance = Mathf.Infinity;

            foreach (T obj in objects)
            {
                float distance = Vector3.Distance(position, obj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = obj;
                }
            }

            return closest;
        }

        public static Dictionary<string, GameObject> GetAllHazards()
        {
            Dictionary<string, GameObject> hazards = new Dictionary<string, GameObject>();
            List<SpawnableMapObject> spawnableMapObjects = (from x in StartOfRound.Instance.levels.SelectMany((SelectableLevel level) => level.spawnableMapObjects)
                                                            group x by ((UnityEngine.Object)x.prefabToSpawn).name into g
                                                            select g.First()).ToList();
            foreach (SpawnableMapObject item in spawnableMapObjects)
            {
                hazards.Add(item.prefabToSpawn.name, item.prefabToSpawn);
            }
            return hazards;
        }

        public static Vector3 GetRandomNavMeshPositionInAnnulus(Vector3 center, float minRadius, float maxRadius, int sampleCount = 10)
        {
            Vector3 randomDirection;
            float y = center.y;

            // Make sure minRadius is less than maxRadius
            if (minRadius >= maxRadius)
            {
                logger.LogWarning("minRadius should be less than maxRadius. Returning original position.");
                return center;
            }

            // Try a few times to get a valid point
            for (int i = 0; i < sampleCount; i++)
            {
                // Get a random direction
                randomDirection = UnityEngine.Random.insideUnitSphere;
                randomDirection.y = 0f;
                randomDirection.Normalize();

                // Random distance between min and max radius
                float distance = UnityEngine.Random.Range(minRadius, maxRadius);

                // Calculate the new position
                Vector3 pos = center + randomDirection * distance;
                pos.y = y;

                // Check if it's on the NavMesh
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            logger.LogWarning("Unable to find valid NavMesh position in annulus. Returning original position.");
            return center;
        }


        public static List<Vector3> GetEvenlySpacedNavMeshPositions(Vector3 center, int count, float minRadius, float maxRadius)
        {
            List<Vector3> positions = new List<Vector3>();

            // Validate
            if (count <= 0 || minRadius > maxRadius)
            {
                logger.LogWarning("Invalid parameters for turret spawn positions.");
                return positions;
            }

            float y = center.y;
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                // Angle in degrees
                float angle = i * angleStep;

                // Convert angle to radians
                float radians = angle * Mathf.Deg2Rad;

                // Use random radius between min and max for some variation (optional)
                float radius = UnityEngine.Random.Range(minRadius, maxRadius);

                // Direction on XZ plane
                float x = Mathf.Cos(radians) * radius;
                float z = Mathf.Sin(radians) * radius;

                Vector3 pos = new Vector3(center.x + x, y, center.z + z);

                // Try to snap to NavMesh
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    positions.Add(hit.position);
                }
                else
                {
                    logger.LogWarning($"Could not find valid NavMesh position for turret {i}. Skipping.");
                }
            }

            return positions;
        }

        private static GameObject[] FindOutsideAINodes()
        {
            if (outsideAINodes == null || outsideAINodes.Length == 0 || outsideAINodes[0] == null)
            {
                outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
                logger.LogInfo("Finding outside AI nodes.");
                outsideNodePositions = new Vector3[outsideAINodes.Length];

                for (int i = 0; i < outsideAINodes.Length; i++)
                {
                    outsideNodePositions[i] = outsideAINodes[i].transform.position;
                }
            }
            return outsideAINodes;
        }

        private static GameObject[] FindInsideAINodes()
        {
            if (insideAINodes == null || insideAINodes.Length == 0 || insideAINodes[0] == null)
            {
                insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
                logger.LogInfo("Finding inside AI nodes.");
                insideNodePositions = new Vector3[insideAINodes.Length];
                for (int i = 0; i < insideAINodes.Length; i++)
                {
                    insideNodePositions[i] = insideAINodes[i].transform.position;
                }
            }
            return insideAINodes;
        }

        public static PlayerControllerB[] GetNearbyPlayers(Vector3 position, float distance = 10f, List<PlayerControllerB>? ignoredPlayers = null)
        {
            List<PlayerControllerB> players = [];

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == null || !player.isPlayerControlled) { continue; }
                if (ignoredPlayers != null && ignoredPlayers.Contains(player)) { continue; }
                if (Vector3.Distance(position, player.transform.position) > distance) { continue; }
                players.Add(player);
            }

            return players.ToArray();
        }
    }

    [HarmonyPatch]
    public class UtilsPatches
    {
        private static ManualLogSource logger = LoggerInstance;

        [HarmonyPrefix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnInsideEnemiesFromVentsIfReady))]
        public static bool SpawnInsideEnemiesFromVentsIfReadyPrefix()
        {
            if (!Utils.spawningAllowed) { return false; }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnDaytimeEnemiesOutside))]
        public static bool SpawnDaytimeEnemiesOutsidePrefix()
        {
            if (!Utils.spawningAllowed) { return false; }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnEnemiesOutside))]
        public static bool SpawnEnemiesOutsidePrefix()
        {
            if (!Utils.spawningAllowed) { return false; }
            return true;
        }
    }
}
