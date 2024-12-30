using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Rats
{
    [HarmonyPatch(typeof(Landmine))]
    internal class LandminePatch
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Landmine.SpawnExplosion))]
        public static void SpawnExplosionPostfix(Vector3 explosionPosition, bool spawnExplosionEffect = false, float killRange = 1f, float damageRange = 1f, int nonLethalDamage = 50, float physicsForce = 0f, GameObject overridePrefab = null, bool goThroughCar = false)
        {
            foreach(var rat in RatManager.SpawnedRats)
            {
                float distance = Vector3.Distance(rat.transform.position, explosionPosition);
                if (distance < damageRange)
                {
                    rat.HitFromExplosion(distance);
                }
            }
        }
    }
}