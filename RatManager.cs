using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Rats.Plugin;

namespace Rats
{
    public class RatManager : MonoBehaviour
    {
        private static ManualLogSource logger = LoggerInstance;

        public static RatManager? Instance;

        public static List<EnemyVent> vents => RoundManager.Instance.allEnemyVents.ToList();

        public Dictionary<EnemyAI, int> enemyHitCount = [];

        public Dictionary<PlayerControllerB, int> playerThreatCounter = [];
        public Dictionary<EnemyAI, int> enemyThreatCounter = [];

        int batchIndex = 0;

        // Configs
        const float updateInterval = 0.2f;

        public static void Init()
        {
            if (Instance != null) { return; }
            Instance = GameObject.Instantiate(new GameObject("RatManager"), Vector3.zero, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform).AddComponent<RatManager>();
        }

        public void OnDestroy()
        {
            Instance = null;
        }

        public void Update()
        {
            // Run one batch per interval
            if (Time.frameCount % Mathf.CeilToInt(updateInterval / Time.deltaTime) == 0)
            {
                //logger.LogDebug("Running batch");
                RunBatch();
            }
        }

        void RunBatch()
        {
            int total = RatAI.Instances.Count;
            if (total == 0) return;

            // How many to process per batch (spread evenly)
            int batchSize = Mathf.Max(1, total / Mathf.CeilToInt(1f / updateInterval));

            for (int i = 0; i < batchSize; i++)
            {
                RatAI instance = RatAI.Instances[batchIndex];
                if (instance != null)
                {
                    instance.DoAIInterval();
                }

                batchIndex++;
                if (batchIndex >= total)
                    batchIndex = 0; // wrap around
            }
        }
    }
}