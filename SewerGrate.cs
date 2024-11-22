﻿using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;

namespace Rats
{
    internal class SewerGrate : NetworkBehaviour
    {
        private static ManualLogSource logger = LoggerInstance;
        public static List<SewerGrate> Nests = new List<SewerGrate>();
        public static Dictionary<EnemyAI, int> EnemyHitCount = new Dictionary<EnemyAI, int>();
        public static Dictionary<EnemyAI, int> EnemyFoodAmount = new Dictionary<EnemyAI, int>();

#pragma warning disable 0649
        public GameObject RatPrefab = null!;
        public TextMeshPro[] TerminalCodes = null!;
        public TerminalAccessibleObject TerminalAccessibleObj = null!;
#pragma warning restore 0649

        EnemyVent? ClosestVentToNest = null!;
        public int DefenseRatCount;

        public Dictionary<PlayerControllerB, int> PlayerThreatCounter = new Dictionary<PlayerControllerB, int>();
        //public static Dictionary<PlayerControllerB, int> PlayerFoodAmount = new Dictionary<PlayerControllerB, int>();
        public Dictionary<EnemyAI, int> EnemyThreatCounter = new Dictionary<EnemyAI, int>();
        public List<RatAI> RallyRats = new List<RatAI>();

        public static int RatCount { get { return UnityEngine.GameObject.FindObjectsOfType<RatAI>().Length; } }
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
        float minRatSpawnTime = 10f;
        float maxRatSpawnTime = 30f;
        float rallyTimeLength = 10f;
        float rallyCooldownLength = 60f;
        int foodToSpawnRat = 5;
        int enemyFoodPerHPPoint = 10;
        int maxRats = 40;
        public void Start()
        {
            logger.LogDebug("Sewer grate spawned at: " + transform.position);
            Nests.Add(this);
            
            hideCodeOnTerminal = configHideCodeOnTerminal.Value;
            minRatSpawnTime = configMinRatSpawnTime.Value;
            maxRatSpawnTime = configMaxRatSpawnTime.Value;
            foodToSpawnRat = configFoodToSpawnRat.Value;
            enemyFoodPerHPPoint = configEnemyFoodPerHPPoint.Value;
            maxRats = configMaxRats.Value;


            if (IsServerOrHost)
            {
                nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);
                //open = false; // TESTING
            }
        }

        public void Update()
        {
            if (!codeOnGrateSet)
            {
                if (TerminalAccessibleObj.objectCode != "")
                {
                    codeOnGrateSet = true;
                    SetCodes();
                    if (hideCodeOnTerminal)
                    {
                        TerminalAccessibleObj.mapRadarText.text = "??";
                    }
                }
            }

            if (IsServerOrHost)
            {
                if (open && RatCount < maxRats)
                {
                    timeSinceSpawnRat += Time.unscaledDeltaTime;

                    if (timeSinceSpawnRat > nextRatSpawnTime)
                    {
                        timeSinceSpawnRat = 0f;
                        nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);

                        SpawnRat();
                    }
                }


                /*if (rallyTimer > 0f)
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
                }*/
            }
        }

        void SetCodes()
        {
            foreach(var tmp in TerminalCodes)
            {
                tmp.text = TerminalAccessibleObj.objectCode;
            }
        }

        public EnemyVent? GetClosestVentToNest(int areaMask)
        {
            if (IsServerOrHost)
            {
                if (StartOfRound.Instance.shipIsLeaving) { return null; }
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

        public void AddEnemyFoodAmount(EnemyAI enemy)
        {
            int maxHP = enemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
            int foodAmount = maxHP * enemyFoodPerHPPoint;
            EnemyFoodAmount.Add(enemy, foodAmount);
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
            //logger.LogDebug("Spawning rats from food: " + ratsToSpawn);
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
                GameObject ratObj = GameObject.Instantiate(RatPrefab, transform.position, Quaternion.identity);
                ratObj.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
            }
        }

        public void ToggleVent()
        {
            open = !open;
        }

        public override void OnDestroy()
        {
            EnemyFoodAmount.Clear();
            EnemyHitCount.Clear();
            StopAllCoroutines();
            Nests.Remove(this);
            base.OnDestroy();
        }
    }
}
