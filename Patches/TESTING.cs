using BepInEx.Logging;
using DunGen;
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
    public class TESTING : MonoBehaviour
    {
        private static ManualLogSource logger = LoggerInstance;

        public static bool ignorePlayerThreat;

        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {
            if (!Utils.testing) { return; }


        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            string msg = __instance.chatTextField.text;
            string[] args = msg.Split(" ");
            //logger.LogDebug(msg);

            switch (args[0])
            {
                case "/threat":
                    ignorePlayerThreat = !ignorePlayerThreat;
                    HUDManager.Instance.DisplayTip("Rats: ignorePlayerThreat", ignorePlayerThreat.ToString());
                    break;
                default:
                    Utils.ChatCommand(args);
                    break;
            }
        }
    }
}