using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rats.Items
{
    internal class GlueBoardBehavior : MonoBehaviour
    {
        int ratsOnGlueTrap;
        bool destroying;

        // Configs
        int maxRatsOnGlueTrap = 10;

        public void OnTriggerEnter(Collider other)
        {
            if (destroying) { return; }
            Plugin.LoggerInstance.LogDebug("In OnTriggerEnter()");
            if (ratsOnGlueTrap >= maxRatsOnGlueTrap)
            {
                destroying = true;
                Destroy(this.gameObject, 30f);
                return;
            }
            if (!other.gameObject.TryGetComponent(out RatAICollisionDetect ratCollision)) { return; }
            RatAI rat = ratCollision.mainScript;
            rat.KillEnemy();
            rat.gameObject.transform.SetParent(this.transform);
            ratsOnGlueTrap++;
        }
    }
}