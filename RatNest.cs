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
        public GameObject RatNestMesh = null!;
        public ScanNodeProperties ScanNode = null!;
        public MeshRenderer renderer;
        public Material RustMat;
#pragma warning restore 0649

        public static List<RatNest> Nests = [];
        public static LungProp? Apparatus;

        public bool IsOpen = true;
        public bool IsRatKing = false;

        public List<RatAI> DefenseRats = [];
        public static Dictionary<EnemyAI, int> EnemyFoodAmount = [];
        float timeSinceSpawnRat;
        float nextRatSpawnTime;
        int food;

        public float PoisonInNest;
        

        // Config Values
        float minRatSpawnTime = 10f;
        float maxRatSpawnTime = 30f;
        int foodToSpawnRat = 5;
        int enemyFoodPerHPPoint = 10;
        int maxRats = 40;
        float poisonToCloseNest = 1f;

        public void Start()
        {
            log("Sewer grate spawned at: " + transform.position);
            Nests.Add(this);

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

            if (RatManager.Instance == null)
            {
                RatManager.Init();
            }

            nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);
            //IsOpen = false; // TESTING
        }

        public void Update()
        {
            /*if (IsRatKing)
            {
                transform.position = RatKingAI.Instance.NestTransform.position;
                return;
            }*/

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

            if (IsOpen && PoisonInNest >= poisonToCloseNest)
            {
                IsOpen = false;
                CloseNestClientRpc();

                int openNests = GetOpenNestCount();
                if (openNests <= 0)
                {
                    SpawnRatKing();
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
            log("Spawning rats from food: " + amount);
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
                RatManager.Instance.RegisterRat(ratObj.GetComponent<RatAI>());
            }
        }

        public void DisableNest()
        {
            LoggerInstance.LogDebug("Disabling nest");
            if (IsRatKing) { return; }
            if (RatKingAI.Instance == null) { SpawnRatKing(); }
            IsOpen = false;
            if (GetOpenNestCount() <= 1)
            {
                RatKingAI.Instance.StartRampage();
            }
        }

        public override void OnDestroy()
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
            if (RatManager.Instance != null)
            {
                Destroy(RatManager.Instance.gameObject);
            }
            Nests.Clear();

            base.OnDestroy();
        }

        [ClientRpc]
        public void SetAsRatKingNestClientRpc()
        {
            IsRatKing = true;
            IsOpen = true;
            RatNestMesh.SetActive(false);

            ScanNode.enabled = false;
            RatKingAI.Instance.KingNest = this;
        }

        [ClientRpc]
        public void CloseNestClientRpc()
        {
            renderer.material = RustMat;
        }
    }
}
