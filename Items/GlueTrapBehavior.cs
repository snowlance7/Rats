using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rats.Items
{
    internal class GlueTrapBehavior : PhysicsProp
    {
        int ratsOnGlueTrap;

        // Configs
        int maxRatsOnGlueTrap = 10;
        int scrapValuePerRat = 3;

        public override void ActivatePhysicsTrigger(Collider other)
        {
            if (ratsOnGlueTrap >= maxRatsOnGlueTrap) { return; }
            if (!other.gameObject.TryGetComponent(out RatAI rat)) { return; }
            rat.KillEnemy();
            rat.transform.SetParent(this.transform);
            ratsOnGlueTrap++;
            SetScrapValue(scrapValue + scrapValuePerRat);
        }
    }
}