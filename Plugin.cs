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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public static Plugin PluginInstance;
        public static ManualLogSource LoggerInstance;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        public static PlayerControllerB localPlayer { get { return GameNetworkManager.Instance.localPlayerController; } }
        public static bool IsServerOrHost { get { return NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost; } }
        public static PlayerControllerB PlayerFromId(ulong id) { return StartOfRound.Instance.allPlayerScripts[StartOfRound.Instance.ClientPlayerList[id]]; }


        public static AssetBundle? ModAssets;

        // Configs

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        // General
        public static ConfigEntry<bool> configHolidayRats;

        // Performance
        public static ConfigEntry<int> configMaxRats;
        public static ConfigEntry<float> configAIIntervalTime;
        public static ConfigEntry<int> configBatchGroupCount;
        public static ConfigEntry<float> configBatchUpdateInterval;

        // RatKing
        public static ConfigEntry<bool> configRatKingEnable;
        public static ConfigEntry<string> configRatKingLevelRarities;
        public static ConfigEntry<string> configRatKingCustomLevelRarities;
        public static ConfigEntry<float> configRatKingSummonChanceRatDeath;
        public static ConfigEntry<float> configRatKingSummonChancePoison;
        public static ConfigEntry<float> configRatKingSummonChanceNests;
        public static ConfigEntry<int> configRatKingDamage;
        public static ConfigEntry<float> configRatKingRallyCooldown;
        public static ConfigEntry<float> configRatKingLoseDistance;
        public static ConfigEntry<float> configRatKingIdleTime;

        // KingNest
        public static ConfigEntry<string> configSewerGrateSpawnWeightCurve;
        public static ConfigEntry<int> configMinRatSpawnTime;
        public static ConfigEntry<int> configMaxRatSpawnTime;
        public static ConfigEntry<int> configFoodToSpawnRat;
        public static ConfigEntry<int> configEnemyFoodPerHPPoint;

        // Rats
        public static ConfigEntry<bool> configUseJermaRats;
        public static ConfigEntry<float> configDefenseRadius;
        public static ConfigEntry<float> configTimeToIncreaseThreat;
        public static ConfigEntry<int> configThreatToAttackPlayer;
        public static ConfigEntry<int> configHighPlayerThreat;
        public static ConfigEntry<int> configThreatToAttackEnemy;
        public static ConfigEntry<float> configSwarmRadius;
        public static ConfigEntry<int> configMaxDefenseRats;
        public static ConfigEntry<float> configDistanceNeededToLoseRats;
        public static ConfigEntry<int> configEnemyHitsToDoDamage;
        public static ConfigEntry<int> configPlayerFoodAmount;
        public static ConfigEntry<int> configRatDamage;
        public static ConfigEntry<float> configSqueakChance;
        public static ConfigEntry<string> configEnemyWhitelist;
        private ConfigEntry<string> configRatsTakePlayerCorpses;

        // RatPoison
        public static ConfigEntry<bool> configRatPoisonEnable;
        public static ConfigEntry<int> configRatPoisonPrice;
        public static ConfigEntry<float> configRatPoisonMaxFluid;
        public static ConfigEntry<float> configRatPoisonPourRate;
        public static ConfigEntry<float> configPoisonToCloseNest;

        // GlueTrap
        public static ConfigEntry<bool> configGlueTrapEnable;
        public static ConfigEntry<int> configGlueTrapPrice;
        public static ConfigEntry<int> configGlueBoardAmount;
        public static ConfigEntry<int> configScrapValuePerRat;
        public static ConfigEntry<int> configMaxRatsOnGlueTrap;

        // Box Of Snap traps
        public static ConfigEntry<bool> configSnapTrapsEnable;
        public static ConfigEntry<int> configSnapTrapsPrice;
        public static ConfigEntry<int> configSnapTrapAmount;
        public static ConfigEntry<float> configSnapTrapsDespawnTime;

        // Rat Crown
        public static ConfigEntry<bool> configRatCrownEnable;
        public static ConfigEntry<int> configRatCrownMinValue;
        public static ConfigEntry<int> configRatCrownMaxValue;

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

            // Performance
            configMaxRats = Config.Bind("Performance", "Maximum Rats", 50, "The maximum number of rats that can be on the map. Lowering this can improve performance.");
            configAIIntervalTime = Config.Bind("Performance", "AI Interval Time", 0.3f, "The interval in which rats will update their AI (Changing position, doing complex calculations, etc). Setting this higher can improve performance but can also make the rats freeze in place more often while lower values makes them constantly moving but can decrease performance. Funnily enough the rats move more rat like when this is set higher.");
            configBatchGroupCount = Config.Bind("Performance", "Batch Group Count", 5, "The amount of groups the rats will be split into to update. (if you dont know what this means, just leave this config alone)");
            configBatchUpdateInterval = Config.Bind("Performance", "Batch Update Interval", 0.2f, "The amount of time between each group update. (if you dont know what this means, just leave this config alone)");

            // RatKing
            configRatKingEnable = Config.Bind("Rat King", "Enable", true, "Set to false to disable spawning the rat king.");
            configRatKingLevelRarities = Config.Bind("Rat King Rarities", "Level Rarities", "All: 20", "Rarities for each level. Example formatting: ExperimentationLevel:5, AssuranceLevel:6, VowLevel:9, OffenseLevel:10, AdamanceLevel:10, MarchLevel:10, RendLevel:75, DineLevel:75, TitanLevel:75, ArtificeLevel:20, EmbrionLevel:25, Modded:15");
            configRatKingCustomLevelRarities = Config.Bind("Rat King Rarities", "Custom Level Rarities", "", "Rarities for modded levels. Same formatting as level rarities.");
            configRatKingSummonChanceRatDeath = Config.Bind("Rat King", "Rat Death Summon Chance", 0.01f, "The chance the rat king will spawn when killing a rat at high threat.");
            configRatKingSummonChancePoison = Config.Bind("Rat King", "Poison Summon Chance", 0.5f, "The chance the rat king will spawn when disabling a nest with rat poison.");
            configRatKingSummonChanceNests = Config.Bind("Rat King", "All Nests Disabled Summon Chance", 0.85f, "The chance the rat king will spawn when all the nests are disabled.");
            configRatKingDamage = Config.Bind("Rat King", "Damage", 25, "The amount of damage the rat king does.");
            configRatKingRallyCooldown = Config.Bind("Rat King", "Rally Cooldown", 30f, "The cooldown for the rat kings rally ability.");
            configRatKingLoseDistance = Config.Bind("Rat King", "Distance to Lose Rat King", 20f, "The distance from the rat king you need to be to lose him. Does not apply when rampaged or hunting.");
            configRatKingIdleTime = Config.Bind("Rat King", "Idle Time", 5f, "The amount of time the rat king will spend idling when reaching a destination during his roam routine.");

            // Nest
            configSewerGrateSpawnWeightCurve = Config.Bind("Nest", "Spawn Weight Curve", "Vanilla - 0,0 ; 1,2 | Custom - 0,0 ; 1,2", "The MoonName - CurveSpawnWeight for the SewerGrate(Rat nest).");
            configMinRatSpawnTime = Config.Bind("Nest", "Minimum Rat Spawn Time", 5, "The minimum time in seconds before a rat can spawn from the nest.");
            configMaxRatSpawnTime = Config.Bind("Nest", "Maximum Rat Spawn Time", 20, "The maximum time in seconds before a rat can spawn from the nest.");
            configFoodToSpawnRat = Config.Bind("Nest", "Food Required to Spawn Rat", 5, "The amount of food needed in the nest to spawn a new rat.");
            configEnemyFoodPerHPPoint = Config.Bind("Nest", "Food Per HP Point", 10, "How much food points one HP will equal for enemies. ex: if 10 thumper will give 40 food points.");

            // Rats
            configUseJermaRats = Config.Bind("Rats", "Use Jerma Rats", false, "Uses a lower quality model for the rats with no animations. Can help with performance if enabled.");
            configDefenseRadius = Config.Bind("Rats", "Defense Radius", 5f, "The radius in which defense rats protect the nest.");
            configTimeToIncreaseThreat = Config.Bind("Rats", "Time to Increase Threat", 2.5f, "The time needed to add a threat point for a player when they are in line of sight of the rat.");
            configThreatToAttackPlayer = Config.Bind("Rats", "Threat to Attack Player", 100, "The threat level at which rats begin attacking the player.");
            configHighPlayerThreat = Config.Bind("Rats", "High Player Threat", 250, "The threat level at which rats will call for the rat king and the rat king will attack players.");
            configThreatToAttackEnemy = Config.Bind("Rats", "Threat to Attack Enemy", 50, "The threat level at which rats begin attacking enemy entities.");
            configSwarmRadius = Config.Bind("Rats", "Swarm Radius", 3f, "The radius in which rats swarm around their target.");
            configMaxDefenseRats = Config.Bind("Rats", "Maximum Defense Rats", 10, "The maximum number of defense rats assigned to protect the nest.");
            configDistanceNeededToLoseRats = Config.Bind("Rats", "Distance Needed to Lose Rats", 25f, "The distance the player must be from rats to lose them.");
            configEnemyHitsToDoDamage = Config.Bind("Rats", "Enemy Hits to Do Damage", 10, "The amount of attacks needed to do 1 shovel hit of damage to an enemy. If 10, thumper will need to be attacked 40 times by a rat.");
            configPlayerFoodAmount = Config.Bind("Rats", "Player Food Amount", 30, "How much food points a player corpse gives when brought to the nest.");
            configRatDamage = Config.Bind("Rats", "Rat Damage", 2, "The damage dealt by a rat when attacking.");
            configSqueakChance = Config.Bind("Rats", "Squeak Chance", 0.01f, "The chance a rat will squeak when completing a run cycle (every second)");
            configEnemyWhitelist = Config.Bind("Rats", "Enemy Whitelist", "Centipede,HoarderBug,Butler,Crawler,SandSpider,RatKingEnemy", "Whitelist of enemies the rats can kill and eat. Names should be the enemyType.name value of each enemy. You can find a list of these names in the README.");
            configRatsTakePlayerCorpses = Config.Bind("Rats", "Rats Take Player Corpes", true, "If this is true, allows rats to drag players to their nest to eat."); // TODO: Add this in

            // RatPoison
            configRatPoisonEnable = Config.Bind("Rat Poison", "Enable", true, "Whether or not to enable");
            configRatPoisonPrice = Config.Bind("Rat Poison", "Store Price", 40, "The cost of rat poison in the store.");
            configRatPoisonMaxFluid = Config.Bind("Rat Poison", "Max Fluid", 5f, "The amount of rat poison in a container of rat poison.");
            configRatPoisonPourRate = Config.Bind("Rat Poison", "Pour Rate", 0.1f, "How fast the rat poison pours out of the container.");
            configPoisonToCloseNest = Config.Bind("Rat Poison", "Poison To Close Nest", 1f, "The amount of poison you need to pour in a rat nest to disable it. Disabling a nest prevents rats from spawning and has a chance to spawn the rat king.");

            // GlueTrap
            configGlueTrapEnable = Config.Bind("Glue Trap", "Enable", true, "Whether or not to enable");
            configGlueTrapPrice = Config.Bind("Glue Trap", "Store Price", 20, "The cost of the glue trap in the store.");
            configGlueBoardAmount = Config.Bind("Glue Trap", "Glue Board Amount", 4, "The amount of glue boards you get in the glue trap item.");
            configScrapValuePerRat = Config.Bind("Glue Trap", "Scrap Value Per Rat", 2, "The scrap value added to the glue board per rat stuck.");
            configMaxRatsOnGlueTrap = Config.Bind("Glue Trap", "Maximum Rats on Glue Trap", 5, "The maximum number of rats that can be caught on a single glue trap before it becomes full.");

            // BoxOfSnapTraps
            configSnapTrapsEnable = Config.Bind("Snap Traps", "Enable", true, "Whether or not to enable");
            configSnapTrapsPrice = Config.Bind("Snap Traps", "Store Price", 50, "The cost of the box of snap traps in the store.");
            configSnapTrapAmount = Config.Bind("Snap Traps", "Snap Trap Amount", 100, "The amount of snap traps that come with a Box Of Snap Traps.");
            configSnapTrapsDespawnTime = Config.Bind("Snap Traps", "Despawn Time", 10f, "The time for snap traps to despawn after being triggered.");

            // Rat Crown
            configRatCrownEnable = Config.Bind("Rat Crown", "Enable", true, "Whether or not to enable");
            configRatCrownMinValue = Config.Bind("Rat Crown", "Minimum Scrap Value", 300, "The minimum scrap value of the crown.");
            configRatCrownMaxValue = Config.Bind("Rat Crown", "Maximum Scrap Value", 500, "The maximum scrap value of the crown.");

            // Loading Assets
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ModAssets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location), "rats_assets"));
            if (ModAssets == null)
            {
                Logger.LogError($"Failed to load custom assets.");
                return;
            }
            LoggerInstance.LogDebug($"Got AssetBundle at: {Path.Combine(sAssemblyLocation, "rats_assets")}");

            // Rat King
            Utils.RegisterEnemy(configRatKingEnable.Value, "Assets/ModAssets/RatKingEnemy.asset", "Assets/ModAssets/Bestiary/RatKingTN.asset", "Assets/ModAssets/Bestiary/RatKingTK.asset", configRatKingLevelRarities.Value, configRatKingCustomLevelRarities.Value);

            SpawnableMapObjectDef RatSpawnPrefab = ModAssets.LoadAsset<SpawnableMapObjectDef>("Assets/ModAssets/RatSpawn.asset");
            if (RatSpawnPrefab == null) { LoggerInstance.LogError("Error: Couldnt get RatSpawnPrefab from assets"); return; }
            LoggerInstance.LogDebug("Registering rat spawn network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(RatSpawnPrefab.spawnableMapObject.prefabToSpawn);

            // Rat Spawn
            LoggerInstance.LogDebug($"Registering RatSpawn");
            RatManager.RatNestPrefab = Utils.RegisterInsideMapObjectWithConfig("Assets/ModAssets/RatSpawn.asset", configSewerGrateSpawnWeightCurve.Value);
            if (RatManager.RatNestPrefab == null) { LoggerInstance.LogError("Couldnt load rat nest prefab, make sure the spawn weight config is set up correctly"); return; }

            // Rat
            GameObject rat = ModAssets.LoadAsset<GameObject>("Assets/ModAssets/Rat.prefab");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(rat);

            // Jerma Rat
            GameObject jermaRat = ModAssets.LoadAsset<GameObject>("Assets/ModAssets/JermaRat.prefab");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(jermaRat);

            // Rat poison
            Utils.RegisterShopItem(configRatPoisonEnable.Value, "Assets/ModAssets/RatPoisonItem.asset", configRatPoisonPrice.Value);

            // Glue trap
            Utils.RegisterShopItem(configGlueTrapEnable.Value, "Assets/ModAssets/GlueTrapItem.asset", configGlueTrapPrice.Value);

            // Glue Board
            Utils.RegisterItem(configGlueTrapEnable.Value, "Assets/ModAssets/GlueBoardItem.asset");

            // Box Of Snap Traps
            Utils.RegisterShopItem(configSnapTrapsEnable.Value, "Assets/ModAssets/BoxOfSnapTrapsItem.asset", configSnapTrapsPrice.Value);

            // Rat Crowwn
            Utils.RegisterItem(configRatCrownEnable.Value, "Assets/ModAssets/RatCrownItem.asset", "", "", configRatCrownMinValue.Value, configRatCrownMaxValue.Value);

            // Finished
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
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
