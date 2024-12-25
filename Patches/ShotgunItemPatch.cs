using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Rats
{
    [HarmonyPatch(typeof(ShotgunItem))]
    internal class ShotgunItemPatch
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShotgunItem.ShootGun))]
        public static void ShootgunPostfix(ShotgunItem __instance, Vector3 shotgunPosition, Vector3 shotgunForward)
        {
            if (__instance.playerHeldBy == null) { return; }
            Ray ray = new Ray(shotgunPosition - shotgunForward * 10f, shotgunForward);
            RaycastHit val = default(RaycastHit);
            int num4 = Physics.SphereCastNonAlloc(ray, 5f, __instance.enemyColliders, 15f, 524288, (QueryTriggerInteraction)2);

            for (int i = 0; i < num4; i++)
            {
                Debug.Log("Raycasting enemy");
                if (!__instance.enemyColliders[i].transform.GetComponent<RatAICollisionDetect>())
                {
                    break;
                }
                RatAI mainScript = __instance.enemyColliders[i].transform.GetComponent<RatAICollisionDetect>().mainScript;
                IHittable hit;
                if (__instance.enemyColliders[i].transform.TryGetComponent<IHittable>(out hit))
                {
                    float num5 = Vector3.Distance(shotgunPosition, __instance.enemyColliders[i].point);
                    int num6 = ((num5 < 3.7f) ? 5 : ((!(num5 < 6f)) ? 2 : 3));
                    Debug.Log($"Hit enemy, hitDamage: {num6}");
                    hit.Hit(num6, shotgunForward, __instance.playerHeldBy, playHitSFX: true);
                }
            }
        }
    }
}