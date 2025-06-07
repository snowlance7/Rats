using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;
using static Rats.RatManager;
using static Steamworks.InventoryItem;

namespace Rats
{
    public class RatNest : NetworkBehaviour
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public GameObject RatPrefab = null!;
        public GameObject RatKingPrefab = null!;
        public GameObject JermaRatPrefab = null!;
        public GameObject RatNestMesh = null!;
        public ScanNodeProperties ScanNode = null!;
        public MeshRenderer renderer;
        public Material GoldMat;
        public Material RustMat;
        public MeshRenderer planeRenderer;
        public Material YellowMat;
#pragma warning restore 0649

        public static List<RatNest> Nests = [];
        public static Dictionary<EnemyAI, int> EnemyFoodAmount = [];
        public LungProp? Apparatus;
        bool appyPulled;

        public bool IsOpen = true;
        public bool IsRatKing = false;

        public List<RatAI> DefenseRats = [];
        float timeSinceSpawnRat;
        float nextRatSpawnTime;
        int food;

        public float PoisonInNest;

        public void Start()
        {
            log("Sewer grate spawned at: " + transform.position);
            Nests.Add(this);

            if (Apparatus == null)
            {
                Apparatus = FindObjectsOfType<LungProp>().Where(x => x.isLungDocked).FirstOrDefault();
            }

            AddPoison(Time.deltaTime);

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
            if (IsRatKing && RatKingAI.Instance != null)
            {
                transform.position = RatKingAI.Instance.NestTransform.position;
            }

            if (!IsServerOrHost) { return; }

            if (Apparatus != null && !Apparatus.isLungDocked && !appyPulled)
            {
                appyPulled = true;
                SpawnRatKingOnServer(ratKingSummonChanceApparatus);
            }

            if (IsOpen)
            {
                if (SpawnedRats.Count < maxRats)
                {
                    timeSinceSpawnRat += Time.unscaledDeltaTime;

                    if (timeSinceSpawnRat > nextRatSpawnTime)
                    {
                        timeSinceSpawnRat = 0f;
                        nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);

                        SpawnRat();
                    }
                }

                if (PoisonInNest >= poisonToCloseNest)
                {
                    IsOpen = false;

                    SpawnRatKingOnServer(ratKingSummonChancePoison);

                    int openNests = GetOpenNestCount();
                    if (openNests <= 1)
                    {
                        SpawnRatKingOnServer(ratKingSummonChanceNests, true);
                    }
                }
            }
        }

        public void AddPoison(float amount)
        {
            if (IsRatKing) { return; }

            PoisonInNest = Mathf.Min(PoisonInNest + amount, poisonToCloseNest);
            //float t = Mathf.Clamp01(PoisonInNest / poisonToCloseNest);

            //renderer.material.Lerp(GoldMat, RustMat, t);
            log("PoisonInNest: " + PoisonInNest);

            if (PoisonInNest >= poisonToCloseNest)
            {
                planeRenderer.material = YellowMat;
            }
        }

        public void SpawnRatKingOnServer(float spawnChance = 1f, bool rampage = false)
        {
            if (!IsServerOrHost) { return; }

            if (RatKingAI.Instance == null && configEnableRatKing.Value)
            {
                if (UnityEngine.Random.Range(0f, 1f) > spawnChance) { return; }

                Transform? node = GetRandomNode();
                if (node == null) { LoggerInstance.LogError("node was null cant spawn rat king"); return; }
                RatKingAI ratKing = GameObject.Instantiate(RatKingPrefab, node.transform.position, Quaternion.identity).GetComponent<RatKingAI>();
                ratKing.NetworkObject.Spawn(true);
            }

            if (rampage && RatKingAI.Instance != null)
            {
                RatKingAI.Instance.StartRampage();
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

        void SpawnRat()
        {
            if (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.shipIsLeaving) { return; }

            if (RatManager.SpawnedRats.Count < maxRats || TESTING.testing)
            {
                GameObject prefab = configUseJermaRats.Value ? JermaRatPrefab : RatPrefab;

                GameObject ratObj = GameObject.Instantiate(prefab, transform.position, Quaternion.identity);
                ratObj.GetComponent<NetworkObject>().Spawn();
                RatManager.Instance.RegisterRat(ratObj.GetComponent<RatAI>());
            }
        }

        public void SpawnRats(int amount)
        {
            if (amount == 0) { return; }
            log("Spawning rats from food: " + amount);
            StartCoroutine(SpawnRatsCoroutine(amount));
        }

        IEnumerator SpawnRatsCoroutine(int amount)
        {
            yield return null;

            for (int i = 0; i < amount; i++)
            {
                yield return null;
                SpawnRat();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SyncPoisonStateServerRpc(float poisonAmount)
        {
            if (!IsServerOrHost) { return; }
            SyncPoisonStateClientRpc(poisonAmount);
        }

        [ClientRpc]
        public void SyncPoisonStateClientRpc(float poisonAmount)
        {
            if (IsRatKing) { return; }

            PoisonInNest = Mathf.Min(poisonAmount, poisonToCloseNest);
            //float t = Mathf.Clamp01(PoisonInNest / poisonToCloseNest);

            //renderer.material.Lerp(GoldMat, RustMat, t);
            log("PoisonInNest: " + PoisonInNest);

            if (PoisonInNest >= poisonToCloseNest)
            {
                planeRenderer.material = YellowMat;
            }
        }

        [ClientRpc]
        public void SetAsRatKingNestClientRpc()
        {
            IsRatKing = true;
            IsOpen = true;
            RatNestMesh.SetActive(false);

            ScanNode.enabled = false;

            if (RatKingAI.Instance == null) { return; }
            RatKingAI.Instance.KingNest = this;
        }
    }
}
