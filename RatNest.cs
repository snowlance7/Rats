using BepInEx.Logging;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;
using static Rats.RatManager;

namespace Rats
{
    public class RatNest : NetworkBehaviour
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable CS8618
        public GameObject RatPrefab;
        public GameObject JermaRatPrefab;
        public GameObject RatKingPrefab;

        public GameObject RatNestMeshObj;
        public ScanNodeProperties scanNode;
        public MeshRenderer voidPlaneRenderer;

        public GameObject PoisonLiquidPlaneObj;
        public Material YellowMat;
        public ParticleSystem GasParticleSystem;

        public Animator animator;
        public AudioSource audioSource;
#pragma warning restore CS8618

        readonly float poisonUpOffset = 0.0219f;

        Vector3 poisonPlaneStart;
        Vector3 poisonPlaneEnd;

        public static HashSet<RatNest> nests = [];
        public static List<RatNest> nestsOpen => nests.Where(x => x.IsOpen).ToList();

        public static Dictionary<EnemyAI, int> enemyFoodAmount = [];

        public List<RatAI> DefenseRats = [];

        public bool IsOpen = true;

        float timeSinceSpawnRat;
        float nextRatSpawnTime;
        int food;

        public float poisonInNest;

        // Configs
        const float minRatSpawnTime = 10f;
        const float maxRatSpawnTime = 30f;
        const int foodToSpawnRat = 5;
        const int enemyFoodPerHPPoint = 10;
        const int maxRats = 50;
        const float poisonToCloseNest = 1f;
        const float ratKingSummonChancePoison = 0.5f;
        const float ratKingSummonChanceNests = 0.75f;

        public void Start()
        {
            logger.LogDebug("Rat nest spawned at: " + transform.position);

            poisonPlaneStart = PoisonLiquidPlaneObj.transform.position;
            poisonPlaneEnd = poisonPlaneStart + (Vector3.up * poisonUpOffset);

            nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);

            RatManager.Init();
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
                enemyFoodAmount.Clear();

                if (IsServer)
                {
                    foreach (RatAI rat in RatAI.Instances.ToList())
                    {
                        rat?.NetworkObject?.Despawn(destroy: true);
                    }
                }
            }

            nests.Remove(this);
            base.OnNetworkDespawn();
        }

        public void Update() // TODO: spawning rat king and spamming errors after filled with poison
        {
            timeSinceSpawnRat += Time.unscaledDeltaTime;

            if (!IsServer) { return; }

            if (IsOpen)
            {
                if (RatAI.Instances.Count < maxRats)
                {
                    if (timeSinceSpawnRat > nextRatSpawnTime)
                    {
                        timeSinceSpawnRat = 0f;
                        nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);

                        SpawnRat();
                    }
                }

                if (poisonInNest >= poisonToCloseNest)
                {
                    IsOpen = false;
                    GasParticleSystem.Play();
                    animator.SetTrigger("destroy");
                    scanNode.gameObject.SetActive(false);
                    audioSource.Play();

                    SpawnRatKing(ratKingSummonChancePoison);

                    if (nestsOpen.Count <= 1)
                    {
                        SpawnRatKing(ratKingSummonChanceNests, true);
                    }
                }
            }
        }

        public void SpawnRatKing(float spawnChance = 1f, bool rampage = false)
        {
            if (!IsServer) { logger.LogError("Only server can call functions in RatNest"); return; }

            if (RatKingAI.Instance == null && configRatKingEnable.Value)
            {
                if (UnityEngine.Random.Range(0f, 1f) < spawnChance) { return; }

                GameObject? node = Utils.GetRandomNode(outside: false);
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
            enemyFoodAmount.Add(enemy, foodAmount);
        }

        public void AddFood(int amount = 1)
        {
            if (!IsServerOrHost) { logger.LogError("Only server can call functions in RatNest"); return; }
            food += amount;

            int ratsToSpawn = food / foodToSpawnRat;
            int remainingFood = food % foodToSpawnRat;

            food = remainingFood;
            SpawnRats(ratsToSpawn);
        }

        void SpawnRat()
        {
            if (!Utils.testing && (StartOfRound.Instance.inShipPhase || StartOfRound.Instance.shipIsLeaving)) { return; }

            if (RatAI.Instances.Count < maxRats)
            {
                GameObject prefab = configUseJermaRats.Value ? JermaRatPrefab : RatPrefab;

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

            foreach (var nest in RatNest.nests)
            {
                if (nest == null || !nest.IsOpen) { continue; }
                float distance = Vector3.Distance(position, nest.transform.position);
                if (distance >= closestDistance) { continue; }
                if (checkForPath && !Utils.CalculatePath(position, nest.transform.position)) { continue; }
                closestDistance = distance;
                closestNest = nest;
            }

            return closestNest;
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
            voidPlaneRenderer.material = YellowMat;
            poisonInNest = Mathf.Min(poisonInNest + amount, poisonToCloseNest);
            float t = Mathf.Clamp01(poisonInNest / poisonToCloseNest);

            PoisonLiquidPlaneObj.transform.position = Vector3.Lerp(poisonPlaneStart, poisonPlaneEnd, t);
            logger.LogDebug("PoisonInNest: " + poisonInNest);
        }
    }
}
