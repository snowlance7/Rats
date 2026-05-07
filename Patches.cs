using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using static Rats.Plugin;

namespace Rats
{
    [HarmonyPatch(typeof(Landmine))]
    internal class Patches
    {
        private static ManualLogSource logger = Plugin.logger;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Landmine.SpawnExplosion))]
        public static void SpawnExplosionPostfix(Vector3 explosionPosition, bool spawnExplosionEffect = false, float killRange = 1f, float damageRange = 1f, int nonLethalDamage = 50, float physicsForce = 0f, GameObject overridePrefab = null, bool goThroughCar = false)
        {
            if (!IsServerOrHost) { return; }
            foreach(RatAI rat in RatAI.Instances)
            {
                if (rat == null || rat.isDead) { continue; }
                float distance = Vector3.Distance(rat.transform.position, explosionPosition);
                if (distance < damageRange)
                {
                    rat.HitFromExplosion(distance);
                }
            }
        }
    }
}