using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Unity.Netcode;
using BepInEx.Logging;
using static Rats.Plugin;
using TMPro;
using System.Collections;
using GameNetcodeStuff;
using UnityEngine.AI;

namespace Rats
{
    internal class SewerGrate : NetworkBehaviour
    {
        private static ManualLogSource logger = LoggerInstance;
        public static List<SewerGrate> Nests = new List<SewerGrate>();

#pragma warning disable 0649
        public RatAI RatPrefab = null!;
        public TextMeshPro TerminalCode = null!;
        public TerminalAccessibleObject TerminalAccessibleObj = null!;
        public AISearchRoutine RatSearchRoutine = null!;
#pragma warning restore 0649

        EnemyVent? ClosestVentToNest = null!;

        public Dictionary<PlayerControllerB, int> PlayerThreatCounter = new Dictionary<PlayerControllerB, int>();
        public Dictionary<EnemyAI, int> EnemyThreatCounter = new Dictionary<EnemyAI, int>();
        public static Dictionary<EnemyAI, int> EnemyHitCount = new Dictionary<EnemyAI, int>();
        public List<RatAI> RallyRats = new List<RatAI>();
        public List<RatAI> ScoutRats = new List<RatAI>();
        public List<RatAI> DefenseRats = new List<RatAI>();

        public bool IsRallying { get { return rallyTimer > 0f; } }
        public bool CanRally { get { return rallyCooldown <= 0f; } }
        public RatAI? LeadRallyRat;
        float rallyCooldown;
        float rallyTimer;

        float timeSinceSpawnRat;
        float nextRatSpawnTime;
        public bool open = true;
        bool codeOnGrateSet = false;
        int food;

        // Config Values
        bool hideCodeOnTerminal = true;
        float minRatSpawnTime = 15f;
        float maxRatSpawnTime = 40f;
        float rallyTimeLength = 10f;
        float rallyCooldownLength = 60f;
        int foodToSpawnRat = 5;
        int maxRats = 50;
        // TODO: Fix error where leaving with ship causes crashing
        public void Start()
        {
            if (IsServerOrHost)
            {
                logger.LogDebug("Sewer grate spawned at: " + transform.position);
                Nests.Add(this);
                nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);
                //open = false; // TESTING
            }
        }

        public void Update()
        {
            if (IsServerOrHost)
            {
                if (!codeOnGrateSet)
                {
                    if (TerminalAccessibleObj.objectCode != "")
                    {
                        codeOnGrateSet = true;
                        TerminalCode.text = TerminalAccessibleObj.objectCode;
                        if (hideCodeOnTerminal)
                        {
                            TerminalAccessibleObj.mapRadarText.text = "??";
                        }
                    }
                }

                if (open)
                {
                    timeSinceSpawnRat += Time.unscaledDeltaTime;

                    if (timeSinceSpawnRat > nextRatSpawnTime)
                    {
                        timeSinceSpawnRat = 0f;
                        nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);

                        SpawnRat();
                    }
                }


                if (rallyTimer > 0f)
                {
                    rallyTimer -= Time.unscaledDeltaTime;

                    if (rallyTimer <= 0f)
                    {
                        foreach (var rat in RallyRats)
                        {
                            rat.SwitchToBehaviourStateCustom(RatAI.State.Attacking);
                        }
                        rallyCooldown = rallyCooldownLength;
                        LeadRallyRat = null;
                    }
                }

                if (rallyCooldown > 0f)
                {
                    rallyCooldown -= Time.unscaledDeltaTime;
                }
            }
        }

        public EnemyVent? GetClosestVentToNest(int areaMask)
        {
            if (IsServerOrHost)
            {
                Vector3 pos = RoundManager.Instance.GetNavMeshPosition(transform.position, RoundManager.Instance.navHit, 1.75f);

                if (ClosestVentToNest != null)
                {
                    // Make sure current vent is still accessible
                    Vector3 _closestVentPos = RoundManager.Instance.GetNavMeshPosition(ClosestVentToNest.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
                    if (NavMesh.CalculatePath(pos, _closestVentPos, areaMask, new NavMeshPath()))
                    {
                        return ClosestVentToNest;
                    }
                }

                // Not accessible so find new vent
                float mostOptimalDistance = 2000f;
                EnemyVent? targetVent = null;
                foreach (var vent in RoundManager.Instance.allEnemyVents)
                {
                    Vector3 ventPos = RoundManager.Instance.GetNavMeshPosition(vent.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
                    float distance = Vector3.Distance(pos, ventPos);
                    if (NavMesh.CalculatePath(pos, ventPos, areaMask, new NavMeshPath()) && distance < mostOptimalDistance)
                    {
                        mostOptimalDistance = distance;
                        targetVent = vent;
                    }
                }
                return targetVent;
            }

            // No vent found
            return null;
        }

        public void StartRallyTimer()
        {
            if (IsServerOrHost)
            {
                rallyTimer = rallyTimeLength;
            }
        }

        public void AddFood(int amount = 1)
        {
            food += amount;

            int ratsToSpawn = food / foodToSpawnRat;
            int remainingFood = food % foodToSpawnRat;

            food = remainingFood;
            SpawnRats(ratsToSpawn);
        }

        void SpawnRats(int amount)
        {
            if (amount == 0) { return; }
            for (int i = 0; i < amount; i++)
            {
                SpawnRat();
            }
        }

        void SpawnRat()
        {
            if (GameObject.FindObjectsOfType<RatAI>().Length < maxRats)
            {
                RatAI rat = GameObject.Instantiate(RatPrefab, transform.position, Quaternion.identity);
                rat.NetworkObject.Spawn(true);
            }
        }

        public void ToggleVent()
        {
            open = !open;
        }

        public override void OnDestroy()
        {
            Nests.Remove(this);
            base.OnDestroy();
        }
    }
}
