using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;
using static UnityEngine.VFX.VisualEffectControlTrackController;

namespace Rats
{
    public static class RatManager
    {
        private static ManualLogSource logger = LoggerInstance;
        public static bool testing = false; // TESTING
        //public static RatKingAI? RatKing { get; set; }
        public static List<RatAI> Rats = [];
        public static List<SewerGrate> Nests = [];
        public static List<EnemyVent> Vents { get { return RoundManager.Instance.allEnemyVents.ToList(); } }
        public static Dictionary<EnemyAI, int> EnemyHitCount = [];
        public static Dictionary<PlayerControllerB, int> PlayerThreatCounter = [];
        public static Dictionary<EnemyAI, int> EnemyThreatCounter = [];

        public static EnemyVent? GetClosestVentToPosition(Vector3 pos)
        {
            float mostOptimalDistance = 2000f;
            EnemyVent? targetVent = null;
            foreach (var vent in Vents)
            {
                float distance = Vector3.Distance(pos, vent.floorNode.transform.position);

                if (CalculatePath(pos, vent.floorNode.transform.position) && distance < mostOptimalDistance)
                {
                    mostOptimalDistance = distance;
                    targetVent = vent;
                }
            }

            return targetVent;
        }

        public static bool CalculatePath(Vector3 fromPos, Vector3 toPos)
        {
            Vector3 from = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, 1.75f);
            Vector3 to = RoundManager.Instance.GetNavMeshPosition(toPos, RoundManager.Instance.navHit, 1.75f);

            NavMeshPath path = new();
            return NavMesh.CalculatePath(from, to, -1, path) && Vector3.Distance(path.corners[path.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(to, RoundManager.Instance.navHit, 2.7f)) <= 1.55f; // TODO: Test this
        }
    }
}
