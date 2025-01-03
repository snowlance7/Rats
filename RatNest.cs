using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;
using static Rats.RatManager;

namespace Rats
{
    public class RatNest : NetworkBehaviour
    {
#pragma warning disable 0649
        public GameObject RatPrefab = null!;
        public GameObject RatKingPrefab = null!;
        public GameObject JermaRatPrefab = null!;
        public GameObject JermaRatKingPrefab = null!;
        public TextMeshPro[] TerminalCodes = null!;
        public TerminalAccessibleObject TerminalAccessibleObj = null!;
        public GameObject RatNestMesh = null!;
#pragma warning restore 0649

        public static List<RatNest> Nests = [];
        public static LungProp? Apparatus;

        public bool IsOpen = true;
        public bool IsRatKing = false;

        public List<RatAI> DefenseRats = [];
        public static Dictionary<EnemyAI, int> EnemyFoodAmount = [];
        float timeSinceSpawnRat;
        float nextRatSpawnTime;
        bool codeOnGrateSet = false;
        int food;

        // Config Values
        bool hideCodeOnTerminal = true;
        float minRatSpawnTime = 10f;
        float maxRatSpawnTime = 30f;
        int foodToSpawnRat = 5;
        int enemyFoodPerHPPoint = 10;
        int maxRats = 40;

        public void Start()
        {
            logIfDebug("Sewer grate spawned at: " + transform.position);
            Nests.Add(this);

            hideCodeOnTerminal = configHideCodeOnTerminal.Value;
            minRatSpawnTime = configMinRatSpawnTime.Value;
            maxRatSpawnTime = configMaxRatSpawnTime.Value;
            foodToSpawnRat = configFoodToSpawnRat.Value;
            enemyFoodPerHPPoint = configEnemyFoodPerHPPoint.Value;
            maxRats = configMaxRats.Value;
            IsLoggingEnabled = configEnableDebugging.Value;

            if (Apparatus == null)
            {
                Apparatus = FindObjectsOfType<LungProp>().Where(x => x.isLungDocked).FirstOrDefault();
            }

            if (!IsServerOrHost) { return; }

            if (RatKingAI.Instance == null)
            {
                SpawnRatKing();
            }

            nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);
            //IsOpen = false; // TESTING
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

            if (Apparatus != null && !Apparatus.isLungDocked)
            {
                // TODO: Radiated rats???
            }

            if (!IsServerOrHost) { return; }

            if (!IsRatKing && IsOpen && SpawnedRats.Count < maxRats)
            {
                timeSinceSpawnRat += Time.unscaledDeltaTime;

                if (timeSinceSpawnRat > nextRatSpawnTime)
                {
                    timeSinceSpawnRat = 0f;
                    nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);

                    SpawnRat();
                }
            }
        }

        void SpawnRatKing()
        {
            if (!IsServerOrHost) { return; }

            if (RatKingAI.Instance == null)
            {
                Transform? node = GetRandomNode();
                if (node == null) { LoggerInstance.LogError("node was null cant spawn rat king"); return; }
                RatKingAI ratKing = GameObject.Instantiate(RatKingPrefab, node.transform.position, Quaternion.identity).GetComponent<RatKingAI>();
                ratKing.NetworkObject.Spawn();
            }
        }

        void SetCodes()
        {
            foreach(var tmp in TerminalCodes)
            {
                tmp.text = TerminalAccessibleObj.objectCode;
            }
        }

        public void AddEnemyFoodAmount(EnemyAI enemy)
        {
            int maxHP = enemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
            int foodAmount = maxHP * enemyFoodPerHPPoint;
            EnemyFoodAmount.Add(enemy, foodAmount);
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
            logIfDebug("Spawning rats from food: " + amount);
            for (int i = 0; i < amount; i++)
            {
                SpawnRat();
            }
        }

        void SpawnRat()
        {
            if (RatManager.SpawnedRats.Count < maxRats)
            {
                GameObject ratObj = GameObject.Instantiate(RatPrefab, transform.position, Quaternion.identity);
                ratObj.GetComponent<NetworkObject>().Spawn();
            }
        }

        public void DisableNest() // TODO: Reset this in terminal settings
        {
            LoggerInstance.LogDebug("Disabling nest");
            if (!RatKingAI.Instance.IsActive) { RatKingAI.Instance.SetActive(); }
            if (IsRatKing) { return; }
            IsOpen = false;
            if (GetOpenNestCount() <= 1)
            {
                RatKingAI.Instance.StartRampage();
            }
        }

        public override void OnDestroy()
        {
            if (IsRatKing)
            {
                EnemyHitCount.Clear();
                EnemyThreatCounter.Clear();
                PlayerThreatCounter.Clear();
                EnemyFoodAmount.Clear();
                Apparatus = null;

                foreach (RatAI rat in SpawnedRats)
                {
                    rat.NetworkObject.Despawn(true);
                }

                SpawnedRats.Clear();

                Nests.Clear();
            }

            base.OnDestroy();
        }
    }
}
