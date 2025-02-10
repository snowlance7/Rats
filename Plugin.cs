using AmazingAssets.TerrainToMesh;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Extras;
using LethalLib.Modules;
using Rats;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Rats
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin PluginInstance;
        public static ManualLogSource LoggerInstance;
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        public static PlayerControllerB localPlayer { get { return GameNetworkManager.Instance.localPlayerController; } }
        public static bool IsServerOrHost { get { return NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost; } }
        public static PlayerControllerB PlayerFromId(ulong id) { return StartOfRound.Instance.allPlayerScripts[StartOfRound.Instance.ClientPlayerList[id]]; }


        public static AssetBundle ModAssets;

        public static bool IsLoggingEnabled;

        // Configs

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // General
        public static ConfigEntry<bool> configHolidayRats;

        // Debugging
        public static ConfigEntry<bool> configEnableDebugging;

        // RatKing
        public static ConfigEntry<bool> configEnableRatKing;
        public static ConfigEntry<bool> configUseJermaRatKing;
        public static ConfigEntry<string> configRatKingLevelRarities;
        public static ConfigEntry<string> configRatKingCustomLevelRarities;

        // KingNest
        public static ConfigEntry<string> configSewerGrateSpawnWeightCurve;
        public static ConfigEntry<int> configMinRatSpawnTime;
        public static ConfigEntry<int> configMaxRatSpawnTime;
        public static ConfigEntry<int> configFoodToSpawnRat;
        public static ConfigEntry<int> configEnemyFoodPerHPPoint;
        public static ConfigEntry<int> configMaxRats;

        // SpawnedRats
        public static ConfigEntry<bool> configUseJermaRats;
        public static ConfigEntry<float> configAIIntervalTime;
        public static ConfigEntry<float> configDefenseRadius;
        public static ConfigEntry<float> configTimeToIncreaseThreat;
        public static ConfigEntry<int> configThreatToAttackPlayer;
        public static ConfigEntry<int> configThreatToAttackEnemy;
        public static ConfigEntry<float> configSwarmRadius;
        public static ConfigEntry<bool> configCanVent;
        public static ConfigEntry<int> configMaxDefenseRats;
        public static ConfigEntry<int> configRatsNeededToAttack;
        public static ConfigEntry<float> configDistanceNeededToLoseRats;
        public static ConfigEntry<int> configEnemyHitsToDoDamage;
        public static ConfigEntry<int> configPlayerFoodAmount;
        public static ConfigEntry<int> configRatDamage;

        // RatPoison
        public static ConfigEntry<int> configRatPoisonPrice;

        // GlueTrap
        public static ConfigEntry<int> configGlueTrapPrice;

        // Box Of Snap traps
        public static ConfigEntry<int> configBoxOfSnapTrapsPrice;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.


        private void Awake()
        {
            if (PluginInstance == null)
            {
                PluginInstance = this;
            }

            LoggerInstance = PluginInstance.Logger;

            harmony.PatchAll();

            InitializeNetworkBehaviours();

            // Configs

            // General
            configHolidayRats = Config.Bind("General", "Holiday Rats", false, "Rats spawn with a santa hat");

            // Debugging
            configEnableDebugging = Config.Bind("Debugging", "Enable Debugging", false, "Allows debug logs to show in the logs");

            // RatKing
            configUseJermaRatKing = Config.Bind("Rat King", "Use Jerma Rat King", false, "Uses a lower quality model for the rat king with no animations. Can help with performance if enabled.");
            configEnableRatKing = Config.Bind("Rat King", "Enable Rat King", true, "Set to false to disable spawning the rat king.");
            configRatKingLevelRarities = Config.Bind("Rat King Rarities", "Level Rarities", "All: 25", "Rarities for each level. Example formatting: ExperimentationLevel:5, AssuranceLevel:6, VowLevel:9, OffenseLevel:10, AdamanceLevel:10, MarchLevel:10, RendLevel:75, DineLevel:75, TitanLevel:75, ArtificeLevel:20, EmbrionLevel:25, Modded:15");
            configRatKingCustomLevelRarities = Config.Bind("Rat King Rarities", "Custom Level Rarities", "", "Rarities for modded levels. Same formatting as level rarities.");

            // KingNest
            configSewerGrateSpawnWeightCurve = Config.Bind("Nest", "Spawn Weight Curve", "Vanilla - 0,0 ; 1,3 | Custom - 0,0 ; 1,3", "The MoonName - CurveSpawnWeight for the SewerGrate(Rat nest).");
            configMinRatSpawnTime = Config.Bind("Nest", "Minimum Rat Spawn Time", 5, "The minimum time in seconds before a rat can spawn from the nest.");
            configMaxRatSpawnTime = Config.Bind("Nest", "Maximum Rat Spawn Time", 20, "The maximum time in seconds before a rat can spawn from the nest.");
            configFoodToSpawnRat = Config.Bind("Nest", "Food Required to Spawn Rat", 5, "The amount of food needed in the nest to spawn a new rat.");
            configEnemyFoodPerHPPoint = Config.Bind("Nest", "Food Per HP Point", 10, "How much food points one HP will equal for enemies. ex: if 10 thumper will give 40 food points.");
            configMaxRats = Config.Bind("Nest", "Maximum Rats", 40, "The maximum number of rats that can be on the map. Lowering this can improve performance.");

            // SpawnedRats
            configUseJermaRats = Config.Bind("Rats", "Use Jerma Rats", false, "Uses a lower quality model for the rats with no animations. Can help with performance if enabled.");
            configAIIntervalTime = Config.Bind("Rats", "AI Interval Time", 0.3f, "The interval in which rats will update their AI (Changing position, doing complex calculations, etc). Setting this higher can improve performance but can also make the rats freeze in place more often while lower values makes them constantly moving but can decrease performance.");
            configDefenseRadius = Config.Bind("Rats", "Defense Radius", 5f, "The radius in which defense rats protect the nest.");
            configTimeToIncreaseThreat = Config.Bind("Rats", "Time to Increase Threat", 2.5f, "The time needed to add a threat point for a player when they are in line of sight of the rat.");
            configThreatToAttackPlayer = Config.Bind("Rats", "Threat to Attack Player", 100, "The threat level at which rats begin attacking the player.");
            configThreatToAttackEnemy = Config.Bind("Rats", "Threat to Attack Enemy", 50, "The threat level at which rats begin attacking enemy entities.");
            configSwarmRadius = Config.Bind("Rats", "Swarm Radius", 10f, "The radius in which rats swarm around their target.");
            configCanVent = Config.Bind("Rats", "Can Vent", true, "If set to true, allows rats to travel through vents. This can also increase performance if set to false.");
            configMaxDefenseRats = Config.Bind("Rats", "Maximum Defense Rats", 10, "The maximum number of defense rats assigned to protect the nest.");
            configRatsNeededToAttack = Config.Bind("Rats", "Rats Needed to Attack", 5, "The minimum number of rats required to start an attack.");
            configDistanceNeededToLoseRats = Config.Bind("Rats", "Distance Needed to Lose Rats", 25f, "The distance the player must be from rats to lose them.");
            configEnemyHitsToDoDamage = Config.Bind("Rats", "Enemy Hits to Do Damage", 10, "The amount of attacks needed to do 1 shovel hit of damage to an enemy. If 10, thumper will need to be attacked 40 times by a rat.");
            configPlayerFoodAmount = Config.Bind("Rats", "Player Food Amount", 30, "How much food points a player corpse gives when brought to the nest.");
            configRatDamage = Config.Bind("Rats", "Rat Damage", 2, "The damage dealt by a rat when attacking.");

            // RatPoison
            configRatPoisonPrice = Config.Bind("Rat Poison", "Store Price", 40, "The cost of rat poison in the store.");

            // GlueTrap
            configGlueTrapPrice = Config.Bind("Glue Trap", "Store Price", 10, "The cost of the glue trap in the store.");

            // BoxOfSnapTraps
            configBoxOfSnapTrapsPrice = Config.Bind("Box Of Snap Traps", "Store Price", 30, "The cost of the box of snap traps in the store.");

            // Loading Assets
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ModAssets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location), "rats_assets"));
            if (ModAssets == null)
            {
                Logger.LogError($"Failed to load custom assets.");
                return;
            }
            LoggerInstance.LogDebug($"Got AssetBundle at: {Path.Combine(sAssemblyLocation, "rats_assets")}");

            string ratKingPath = configUseJermaRatKing.Value ? "Assets/ModAssets/JermaRatKingEnemy.asset" : "Assets/ModAssets/RatKingEnemy.asset";
            EnemyType RatKing = ModAssets.LoadAsset<EnemyType>(ratKingPath);
            if (RatKing == null) { LoggerInstance.LogError("Error: Couldnt get Rat from assets"); return; }
            LoggerInstance.LogDebug($"Got Rat prefab");

            TerminalNode RatTN = ModAssets.LoadAsset<TerminalNode>("Assets/ModAssets/Bestiary/RatKingTN.asset");
            TerminalKeyword RatTK = ModAssets.LoadAsset<TerminalKeyword>("Assets/ModAssets/Bestiary/RatKingTK.asset");

            LoggerInstance.LogDebug("Registering enemy network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(RatKing.enemyPrefab);
            LoggerInstance.LogDebug("Registering enemy...");
            Enemies.RegisterEnemy(RatKing, GetLevelRarities(configRatKingLevelRarities.Value), GetCustomLevelRarities(configRatKingCustomLevelRarities.Value), RatTN, RatTK);

            SpawnableMapObjectDef RatSpawnPrefab = ModAssets.LoadAsset<SpawnableMapObjectDef>("Assets/ModAssets/RatSpawn.asset");
            if (RatSpawnPrefab == null) { LoggerInstance.LogError("Error: Couldnt get RatSpawnPrefab from assets"); return; }
            LoggerInstance.LogDebug("Registering rat spawn network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(RatSpawnPrefab.spawnableMapObject.prefabToSpawn);

            LoggerInstance.LogDebug($"Registering RatSpawn");
            RegisterInsideMapObjectWithConfig(RatSpawnPrefab, configSewerGrateSpawnWeightCurve.Value);

            // Rat poison
            Item RatPoison = ModAssets.LoadAsset<Item>("Assets/ModAssets/RatPoisonItem.asset");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(RatPoison.spawnPrefab);
            LethalLib.Modules.Utilities.FixMixerGroups(RatPoison.spawnPrefab);
            LethalLib.Modules.Items.RegisterShopItem(RatPoison, configRatPoisonPrice.Value);

            // Glue trap
            Item GlueTrap = ModAssets.LoadAsset<Item>("Assets/ModAssets/GlueTrapItem.asset");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(GlueTrap.spawnPrefab);
            LethalLib.Modules.Utilities.FixMixerGroups(GlueTrap.spawnPrefab);
            LethalLib.Modules.Items.RegisterShopItem(GlueTrap, configGlueTrapPrice.Value);

            // Box Of Snap Traps
            Item SnapTraps = ModAssets.LoadAsset<Item>("Assets/ModAssets/BoxOfSnapTrapsItem.asset");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(SnapTraps.spawnPrefab);
            LethalLib.Modules.Utilities.FixMixerGroups(SnapTraps.spawnPrefab);
            LethalLib.Modules.Items.RegisterShopItem(SnapTraps, configBoxOfSnapTrapsPrice.Value);

            // Finished
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        }

        public static void log(string message)
        {
            if (!IsLoggingEnabled && !TESTING.testing) { return; }
            LoggerInstance.LogDebug(message);
        }

        // Xu's code for registering map objects with configs
        protected void RegisterInsideMapObjectWithConfig(SpawnableMapObjectDef mapObjDef, string configString)
        {
            /*SpawnableMapObjectDef mapObjDef = ScriptableObject.CreateInstance<SpawnableMapObjectDef>();
            mapObjDef.spawnableMapObject = new SpawnableMapObject
            {
                prefabToSpawn = prefab
            };*/


            (Dictionary<Levels.LevelTypes, string> spawnRateByLevelType, Dictionary<string, string> spawnRateByCustomLevelType) = ConfigParsingWithCurve(configString);


            foreach (var entry in spawnRateByLevelType)
            {
                //AnimationCurve animationCurve = CreateCurveFromString(entry.Value, prefab.name);
                AnimationCurve animationCurve = CreateCurveFromString(entry.Value, mapObjDef.spawnableMapObject.prefabToSpawn.name);
                MapObjects.RegisterMapObject(mapObjDef, entry.Key, (level) => animationCurve);
            }
            foreach (var entry in spawnRateByCustomLevelType)
            {
                //AnimationCurve animationCurve = CreateCurveFromString(entry.Value, prefab.name);
                AnimationCurve animationCurve = CreateCurveFromString(entry.Value, mapObjDef.spawnableMapObject.prefabToSpawn.name);
                MapObjects.RegisterMapObject(mapObjDef, Levels.LevelTypes.None, new string[] { entry.Key }, (level) => animationCurve);
            }
        }
        protected (Dictionary<Levels.LevelTypes, string> spawnRateByLevelType, Dictionary<string, string> spawnRateByCustomLevelType) ConfigParsingWithCurve(string configMoonRarity)
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

        public AnimationCurve CreateCurveFromString(string keyValuePairs, string nameOfThing)
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

        public Dictionary<Levels.LevelTypes, int> GetLevelRarities(string levelsString)
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
                Logger.LogError($"Error: {e}");
                return null!;
            }
        }

        public Dictionary<string, int> GetCustomLevelRarities(string levelsString)
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
                Logger.LogError($"Error: {e}");
                return null!;
            }
        }

        private static void InitializeNetworkBehaviours()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            LoggerInstance.LogDebug("Finished initializing network behaviours");
        }
    }
}
