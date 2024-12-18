using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;
using static Rats.RatManager;

namespace Rats
{
    public class SewerGrate : NetworkBehaviour
    {
#pragma warning disable 0649
        public GameObject RatPrefab = null!;
        public TextMeshPro[] TerminalCodes = null!;
        public TerminalAccessibleObject TerminalAccessibleObj = null!;
        
#pragma warning restore 0649

        public static LungProp? Apparatus;
        public EnemyVent? NestVent;

        public bool accessible { get { return NestVent != null; } }
        public int DefenseRatCount;
        public static Dictionary<EnemyAI, int> EnemyFoodAmount = [];
        float timeSinceSpawnRat;
        float nextRatSpawnTime;
        public bool open = true;
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

            if (Apparatus != null && !Apparatus.isLungDocked) { open = true; }

            if (IsServerOrHost)
            {
                if (open && RatManager.Rats.Count < maxRats)
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
            logIfDebug("Spawning rats from food: " + ratsToSpawn);
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
            if (RatManager.Rats.Count < maxRats)
            {
                SpawnRatClientRpc();
            }
        }

        public void ToggleVent()
        {
            if (Apparatus != null && !Apparatus.isLungDocked) { return; }
            open = !open;
        }

        public override void OnDestroy()
        {
            EnemyHitCount.Clear();
            EnemyThreatCounter.Clear();
            PlayerThreatCounter.Clear();
            EnemyFoodAmount.Clear();
            Apparatus = null;
            StopAllCoroutines();

            foreach (RatAI rat in RatManager.Rats)
            {
                UnityEngine.GameObject.Destroy(rat);
            }

            totalRatsSpawned = 0;

            base.OnDestroy();
        }

        [ClientRpc]
        public void SpawnRatClientRpc()
        {
            GameObject ratObj = GameObject.Instantiate(RatPrefab, transform.position, Quaternion.identity);
            ratObj.GetComponent<RatAI>().MainNest = this;
        }
    }
}
