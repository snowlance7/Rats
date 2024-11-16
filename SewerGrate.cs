using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;

namespace Rats
{
    internal class SewerGrate : MonoBehaviour
    {
#pragma warning disable 0649
        public RatAI RatPrefab = null!;
#pragma warning restore 0649

        float timeSinceSpawnRat;
        float nextRatSpawnTime;

        // Config Values
        float minRatSpawnTime = 10f;
        float maxRatSpawnTime = 30f;

        public void Start()
        {
            nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);
        }

        public void Update()
        {
            timeSinceSpawnRat += Time.deltaTime;

            if (timeSinceSpawnRat > nextRatSpawnTime)
            {
                timeSinceSpawnRat = 0f;
                nextRatSpawnTime = UnityEngine.Random.Range(minRatSpawnTime, maxRatSpawnTime);

                RatAI rat = GameObject.Instantiate(RatPrefab, transform.position, Quaternion.identity);
                rat.NetworkObject.Spawn(true);
            }
        }
    }
}
