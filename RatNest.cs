using BepInEx.Logging;
using Dawn;
using Dawn.Utils;
using GameNetcodeStuff;
using SnowyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static Rats.Configs;
using static Rats.Plugin;

namespace Rats
{
    public class RatNest : NetworkBehaviour
    {
        public GameObject ratPrefab = null!;

        public GameObject jermaRatPrefab = null!;

        public GameObject meshObj = null!;

        public ScanNodeProperties scanNode = null!;

        public MeshRenderer voidPlaneRenderer = null!;

        public GameObject poisonLiquidPlaneObj = null!;

        public Material yellowMat = null!;

        public ParticleSystem gasParticleSystem = null!;

        public Animator animator = null!;

        public AudioSource audioSource = null!;

        public static RatNest? mainInstance => nests.FirstOrDefault();

        public bool isMainInstance => mainInstance != null && mainInstance == this;

        public static HashSet<RatNest> nests { get; private set; } = [];

        public static List<RatNest> nestsOpen => nests.Where(x => x.isOpen).ToList();

        public static Dictionary<EnemyAI, int> enemyHitCount = [];
        public static Dictionary<PlayerControllerB, int> playerThreatCounter = [];
        public static Dictionary<EnemyAI, int> enemyThreatCounter = [];
        public static Dictionary<EnemyAI, int> enemyFoodPointsLeft = [];

        public bool isOpen = true;

        float timeSinceSpawnRat;
        float nextRatSpawnTime;

        int food;

        public float poisonInNest;

        Vector3 poisonPlaneStart;

        Vector3 poisonPlaneEnd;

        const float poisonUpOffset = 0.0219f;

        int batchIndex = 0;
        public HashSet<RatAI> DefenseRats = [];

        public void Start()
        {
            logger.LogDebug("Rat nest spawned at: " + transform.position);

            poisonPlaneStart = poisonLiquidPlaneObj.transform.position;
            poisonPlaneEnd = poisonPlaneStart + (Vector3.up * poisonUpOffset);

            nextRatSpawnTime = cfgRatSpawnTime.GetRandomInRange(Utils.randomLocal);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            nests.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            if (nests.Count <= 1)
            {
                LevelInfestations.UpdateLevelInfestation();

                if (IsServer)
                {
                    foreach (RatAI rat in RatAI.Instances.ToList())
                    {
                        rat?.NetworkObject?.Despawn(destroy: true);
                    }
                }

                enemyHitCount.Clear();
                playerThreatCounter.Clear();
                enemyThreatCounter.Clear();
                enemyFoodPointsLeft.Clear();
            }

            nests.Remove(this);
            base.OnNetworkDespawn();
        }

        public void Update() // TODO: spawning rat king and spamming errors after filled with poison
        {
            timeSinceSpawnRat += Time.unscaledDeltaTime;

            if (!IsServer) { return; }

            if (isOpen)
            {
                if (RatAI.Instances.Count < cfgMaxRats)
                {
                    if (timeSinceSpawnRat > nextRatSpawnTime)
                    {
                        timeSinceSpawnRat = 0f;
                        nextRatSpawnTime = cfgRatSpawnTime.GetRandomInRange(Utils.randomLocal);

                        SpawnRat();
                    }
                }

                if (poisonInNest >= cfgPoisonToCloseNest)
                {
                    isOpen = false;
                    CloseNestClientRpc();

                    //SpawnRatKing(ratKingSummonChancePoison);

                    if (nestsOpen.Count <= 1)
                    {
                        //SpawnRatKing(ratKingSummonChanceNests, true);
                    }
                }
            }

            if (isMainInstance)
            {


                // Run one batch per interval
                if (Time.frameCount % Mathf.CeilToInt(cfgBatchUpdateInterval / Time.deltaTime) == 0)
                {
                    //logger.LogDebug("Running batch");
                    RunBatch();
                }
            }
        }

        void RunBatch()
        {
            List<RatAI> instances = RatAI.Instances.Where(x => !x.isDead).ToList();
            int total = instances.Count;
            if (total == 0) return;

            // How many to process per batch (spread evenly)
            int batchSize = Mathf.Max(1, total / Mathf.CeilToInt(1f / cfgBatchUpdateInterval));

            for (int i = 0; i < batchSize; i++)
            {
                RatAI instance = instances[batchIndex];
                if (instance != null)
                {
                    instance.DoAIInterval();
                }

                batchIndex++;
                if (batchIndex >= total)
                    batchIndex = 0; // wrap around
            }
        }

        public void AddFood(int amount = 1)
        {
            if (!IsServer) { logger.LogError("Only server can call functions in RatNest"); return; }
            food += amount;

            int ratsToSpawn = food / cfgFoodToSpawnRat;
            int remainingFood = food % cfgFoodToSpawnRat;

            food = remainingFood;
            SpawnRats(ratsToSpawn);
        }

        void SpawnRat()
        {
            if (!Utils.testing && (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.shipIsLeaving)) { return; }

            if (RatAI.Instances.Where(x => !x.isDead).Count() < cfgMaxRats)
            {
                GameObject prefab = cfgUseJermaRats ? jermaRatPrefab : ratPrefab;

                Vector3 position = RoundManager.Instance.GetNavMeshPosition(transform.position);

                GameObject ratObj = GameObject.Instantiate(prefab, position, Quaternion.identity);
                ratObj.GetComponent<NetworkObject>().Spawn();
            }
        }

        public void SpawnRats(int amount)
        {
            if (amount == 0) { return; }
            logger.LogDebug("Spawning rats from food: " + amount);

            IEnumerator SpawnRatsCoroutine(int amount)
            {
                yield return null;

                for (int i = 0; i < amount; i++)
                {
                    yield return null;
                    SpawnRat();
                }
            }

            StartCoroutine(SpawnRatsCoroutine(amount));
        }

        public static RatNest? GetClosestNestToPosition(Vector3 position, bool checkForPath = false)
        {
            float closestDistance = Mathf.Infinity;
            RatNest? closestNest = null;

            foreach (var nest in nests)
            {
                if (nest == null || !nest.isOpen) { continue; }
                float distance = Vector3.Distance(position, nest.transform.position);
                if (distance >= closestDistance) { continue; }
                if (checkForPath && !Utils.CanPathToPoint(position, nest.transform.position)) { continue; }
                closestDistance = distance;
                closestNest = nest;
            }

            return closestNest;
        }

        public static void AddEnemyFoodAmount(EnemyAI enemy) // TODO: add enemy blacklist/whitelist
        {
            int maxHP = enemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
            int foodAmount = maxHP * cfgEnemyFoodPerHPPoint;
            enemyFoodPointsLeft.Add(enemy, foodAmount);
        }

        public void OnNestClosed() // Animation TODO
        {
            if (!IsServer) { return; }
            NetworkObject.Despawn(destroy: true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddPoisonServerRpc(float amount)
        {
            if (!IsServer) { return; }
            AddPoisonClientRpc(amount);
        }

        [ClientRpc]
        void AddPoisonClientRpc(float amount)
        {
            voidPlaneRenderer.material = yellowMat;
            poisonInNest = Mathf.Min(poisonInNest + amount, cfgPoisonToCloseNest);
            float t = Mathf.Clamp01(poisonInNest / cfgPoisonToCloseNest);

            poisonLiquidPlaneObj.transform.position = Vector3.Lerp(poisonPlaneStart, poisonPlaneEnd, t);
            //logger.LogDebug("PoisonInNest: " + poisonInNest);
        }

        [ClientRpc]
        public void CloseNestClientRpc()
        {
            isOpen = false;
            gasParticleSystem.Play();
            animator.SetTrigger("destroy");
            scanNode.gameObject.SetActive(false);
            audioSource.Play();
        }
    }
}
