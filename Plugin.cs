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
        public static Plugin PluginInstance;
        public static ManualLogSource LoggerInstance;
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        public static PlayerControllerB localPlayer { get { return GameNetworkManager.Instance.localPlayerController; } }
        public static bool IsServerOrHost { get { return NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost; } }
        public static PlayerControllerB PlayerFromId(ulong id) { return StartOfRound.Instance.allPlayerScripts[StartOfRound.Instance.ClientPlayerList[id]]; }


        public static AssetBundle? ModAssets;

        // Configs


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
