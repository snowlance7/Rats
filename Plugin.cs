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
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Rats
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin? PluginInstance;
        public static ManualLogSource? LoggerInstance;
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        public static PlayerControllerB localPlayer { get { return GameNetworkManager.Instance.localPlayerController; } }
        public static bool IsServerOrHost { get { return NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost; } }
        public static PlayerControllerB PlayerFromId(ulong id) { return StartOfRound.Instance.allPlayerScripts[StartOfRound.Instance.ClientPlayerList[id]]; }


        public static AssetBundle? ModAssets;

        // Configs

        // Nest
        public static ConfigEntry<bool>? configHideCodeOnTerminal;
        public static ConfigEntry<int>? configMinRatSpawnTime;
        public static ConfigEntry<int>? configMaxRatSpawnTime;
        public static ConfigEntry<int>? configFoodToSpawnRat;
        public static ConfigEntry<int>? configEnemyFoodPerHPPoint;
        public static ConfigEntry<int>? configMaxRats;

        // Rats
        public static ConfigEntry<float>? configDefenseRadius;
        public static ConfigEntry<float>? configTimeToIncreaseThreat;
        public static ConfigEntry<int>? configThreatToAttackPlayer;
        public static ConfigEntry<int>? configThreatToAttackEnemy;
        public static ConfigEntry<float>? configSwarmRadius;
        public static ConfigEntry<bool>? configCanVent;
        public static ConfigEntry<int>? configMaxDefenseRats;
        public static ConfigEntry<int>? configRatsNeededToAttack;
        public static ConfigEntry<float>? configDistanceNeededToLoseRats;
        public static ConfigEntry<int>? configEnemyHitsToDoDamage;
        public static ConfigEntry<int>? configPlayerFoodAmount;
        public static ConfigEntry<int>? configRatDamage;


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

            // Nest
            configHideCodeOnTerminal = Config.Bind("Nest", "Hide Code On Terminal", true, "If set to true, will make the code on the ship monitor for the nest be ??, requiring the player to find the nest physically to turn off its spawning.");
            configMinRatSpawnTime = Config.Bind("Nest", "Minimum Rat Spawn Time", 5, "The minimum time in seconds before a rat can spawn from the nest.");
            configMaxRatSpawnTime = Config.Bind("Nest", "Maximum Rat Spawn Time", 30, "The maximum time in seconds before a rat can spawn from the nest.");
            configFoodToSpawnRat = Config.Bind("Nest", "Food Required to Spawn Rat", 5, "The amount of food needed in the nest to spawn a new rat.");
            configEnemyFoodPerHPPoint = Config.Bind("Nest", "Food Per HP Point", 10, "How much food points one HP will equal for enemies. ex: if 10 thumper will give 40 food points.");
            configMaxRats = Config.Bind("Nest", "Maximum Rats", 40, "The maximum number of rats that can be on the map. Lowering this can improve performance.");

            // Rats
            configDefenseRadius = Config.Bind("Rats", "Defense Radius", 5f, "The radius in which defense rats protect the nest.");
            configTimeToIncreaseThreat = Config.Bind("Rats", "Time to Increase Threat", 3f, "The time needed to add a threat point for a player when they are in line of sight of the rat.");
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


            // Loading Assets
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ModAssets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location), "rats_assets"));
            if (ModAssets == null)
            {
                Logger.LogError($"Failed to load custom assets.");
                return;
            }
            LoggerInstance.LogDebug($"Got AssetBundle at: {Path.Combine(sAssemblyLocation, "rats_assets")}");

            EnemyType Rat = ModAssets.LoadAsset<EnemyType>("Assets/ModAssets/RatEnemy.asset");
            if (Rat == null) { LoggerInstance.LogError("Error: Couldnt get Rat from assets"); return; }
            LoggerInstance.LogDebug($"Got Rat prefab");
            TerminalNode RatTN = ModAssets.LoadAsset<TerminalNode>("Assets/ModAssets/Bestiary/RatTN.asset");
            TerminalKeyword RatTK = ModAssets.LoadAsset<TerminalKeyword>("Assets/ModAssets/Bestiary/RatTK.asset");

            LoggerInstance.LogDebug("Registering enemy network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Rat.enemyPrefab);
            LoggerInstance.LogDebug("Registering enemy...");
            Enemies.RegisterEnemy(Rat, null, null, RatTN, RatTK);

            SpawnableMapObjectDef RatSpawn = ModAssets.LoadAsset<SpawnableMapObjectDef>("Assets/ModAssets/RatSpawn.asset");
            if (RatSpawn == null) { LoggerInstance.LogError("Error: Couldnt get RatSpawn from assets"); return; }
            LoggerInstance.LogDebug("Registering rat spawn network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(RatSpawn.spawnableMapObject.prefabToSpawn);
            LoggerInstance.LogDebug($"Registering RatSpawn");
            MapObjects.RegisterMapObject(RatSpawn, Levels.LevelTypes.All, (level) => RatSpawn.spawnableMapObject.numberToSpawn);

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
