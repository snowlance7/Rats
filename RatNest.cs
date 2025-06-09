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

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public GameObject RatPrefab;
        public GameObject RatKingPrefab;
        public GameObject JermaRatPrefab;

        public GameObject RatNestMesh;
        public ScanNodeProperties ScanNode;
        public MeshRenderer voidPlaneRenderer;

        public GameObject PoisonLiquidPlane;
        public Material YellowMat;
        public ParticleSystem GasParticleSystem;

        public Animator NestAnimator;
        public AudioSource NestAudio;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        readonly float poisonUpOffset = 0.0219f;

        Vector3 poisonPlaneStart;
        Vector3 poisonPlaneEnd;

        public static List<RatNest> Nests = [];
        public static Dictionary<EnemyAI, int> EnemyFoodAmount = [];

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

            poisonPlaneStart = PoisonLiquidPlane.transform.position;
            poisonPlaneEnd = poisonPlaneStart + (Vector3.up * poisonUpOffset);

            if (!IsServerOrHost) { return; }

            /*if (RatManager.Instance == null)
            {
                RatManager.Init();
            }*/

            nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);
            IsOpen = false; // TESTING
        }

        public void Update() // TODO: spawning rat king and spamming errors after filled with poison
        {
            if (IsRatKing && RatKingAI.Instance != null)
            {
                transform.position = RatKingAI.Instance.NestTransform.position;
            }

            if (!IsServerOrHost) { return; }

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
                    GasParticleSystem.Play();
                    NestAnimator.SetTrigger("destroy");
                    NestAudio.Play();

                    SpawnRatKingOnServer(ratKingSummonChancePoison);

                    int openNests = GetOpenNestCount();
                    if (openNests <= 1)
                    {
                        SpawnRatKingOnServer(ratKingSummonChanceNests, true);
                    }
                }
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

        public void AddEnemyFoodAmount(EnemyAI enemy) // TODO: add enemy blacklist/whitelist
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
            if (!TESTING.testing && (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.shipIsLeaving)) { return; }

            if (RatManager.SpawnedRats.Count < maxRats)
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
        public void AddPoisonServerRpc(float amount)
        {
            if (!IsServerOrHost || IsRatKing) { return; }
            AddPoisonClientRpc(amount);
        }

        [ClientRpc]
        public void AddPoisonClientRpc(float amount)
        {
            voidPlaneRenderer.material = YellowMat;
            PoisonInNest = Mathf.Min(PoisonInNest + amount, poisonToCloseNest);
            float t = Mathf.Clamp01(PoisonInNest / poisonToCloseNest);

            PoisonLiquidPlane.transform.position = Vector3.Lerp(poisonPlaneStart, poisonPlaneEnd, t);
            log("PoisonInNest: " + PoisonInNest);
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
