using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Netcode;
using static Rats.Plugin;
using UnityEngine;
using System.Diagnostics.CodeAnalysis;

namespace Rats.Patches
{
    [HarmonyPatch(typeof(RoundManager))]
    internal class RoundManagerPatch // FOR SPAWNING ITEMS IN SCPDUNGEON
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(RoundManager.DespawnPropsAtEndOfRound))]
        public static void DespawnPropsAtEndOfRoundPostfix(RoundManager __instance) // SCPFlow
        {
            if (IsServerOrHost)
            {
                SewerGrate.PlayerFoodAmount.Clear();
                SewerGrate.EnemyFoodAmount.Clear();
                SewerGrate.EnemyHitCount.Clear();
            }
        }
    }
}
