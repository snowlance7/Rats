using HarmonyLib;
using SnowyLib;
using System;
using System.Linq;
using UnityEngine;
using static Rats.Plugin;
using static Rats.Configs;
using System.Collections.Generic;

namespace Rats
{
    internal static class LevelInfestations
    {
        public static Dictionary<string, int> levelInfestations = new Dictionary<string, int>();

        public static string currentLevel = "";
        const string saveName = "RatLevelInfestations";

        public static void UpdateLevelInfestation()
        {
            if (!cfgEnableInfestationSystem) { return; }
            if (currentLevel == "") { return; }
            if (!levelInfestations.ContainsKey(currentLevel)) { levelInfestations.Add(currentLevel, 0); }
            levelInfestations[currentLevel] = Mathf.Min(RatAI.Instances.Where(x => !x.isDead).Count(), cfgMaxRats);
        }

        public static void SpawnRatsForLevel()
        {
            if (!cfgEnableInfestationSystem) { return; }
            currentLevel = StartOfRound.Instance.currentLevel.name;
            if (!levelInfestations.ContainsKey(currentLevel)) { return; }
            int infestation = levelInfestations[currentLevel];
            if (infestation <= 0 || RatNest.nests.Count == 0) { return; }
            int ratsPerNestToSpawn = infestation / RatNest.nests.Count;
            foreach (var nest in RatNest.nests)
            {
                nest.SpawnRats(ratsPerNestToSpawn);
            }
        }

        public static void LoadData()
        {
            if (!cfgEnableInfestationSystem) { return; }
            logger.LogDebug("Loading infestation data");
            levelInfestations = ES3.Load<Dictionary<string, int>>(saveName, GameNetworkManager.Instance.currentSaveFileName);
            Log();
        }

        public static void SaveData()
        {
            if (!cfgEnableInfestationSystem) { return; }
            logger.LogDebug("Saving infestation data");
            ES3.Save<Dictionary<string, int>>(saveName, levelInfestations, GameNetworkManager.Instance.currentSaveFileName);
        }

        public static void ResetData()
        {
            if (!cfgEnableInfestationSystem) { return; }
            levelInfestations.Clear();
            ES3.DeleteKey(saveName, GameNetworkManager.Instance.currentSaveFileName);
        }

        public static void Log()
        {
            foreach (var levelInfestation in levelInfestations)
            {
                logger.LogDebug($"{levelInfestation.Key}: {levelInfestation.Value}");
            }
        }
    }

    [HarmonyPatch]
    internal static class LevelInfestationsPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnShipLandedMiscEvents))]
        public static void StartOfRound_OnShipLandedMiscEvents_Postfix()
        {
            try
            {
                if (!IsServerOrHost) { return; }
                LevelInfestations.SpawnRatsForLevel();
            }
            catch (Exception e)
            {
                logger.LogError(e);
                return;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveGameValues))]
        public static void GameNetworkManager_SaveGameValues_Postfix()
        {
            try
            {
                if (!IsServerOrHost) { return; }
                LevelInfestations.SaveData();
            }
            catch (Exception e)
            {
                logger.LogError(e);
                return;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
        public static void StartOfRound_Start_Postfix()
        {
            try
            {
                if (!IsServerOrHost) { return; }
                LevelInfestations.SaveData();
            }
            catch (Exception e)
            {
                logger.LogError(e);
                return;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetSavedGameValues))]
        public static void GameNetworkManager_ResetSavedGameValues_Postfix()
        {
            try
            {
                if (!IsServerOrHost) { return; }
                LevelInfestations.ResetData();
            }
            catch (Exception e)
            {
                logger.LogError(e);
                return;
            }
        }
    }
}