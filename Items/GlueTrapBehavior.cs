using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rats.Items
{
    internal class GlueTrapBehavior : PhysicsProp
    {
        public BoxCollider mainCollider;
        int ratsOnGlueTrap;

        // Configs
        int maxRatsOnGlueTrap = 10;
        int scrapValuePerRat = 3;

        public override void OnHitGround()
        {
            base.OnHitGround();
            mainCollider.enabled = true;
        }

        public override void GrabItem()
        {
            base.GrabItem();
            mainCollider.enabled = false;
        }

        public override void ActivatePhysicsTrigger(Collider other)
        {
            Plugin.LoggerInstance.LogDebug("In ActivatePhysicsTrigger()");
            if (ratsOnGlueTrap >= maxRatsOnGlueTrap) { return; }
            if (!other.gameObject.TryGetComponent(out RatAICollisionDetect ratCollision)) { return; }
            RatAI rat = ratCollision.mainScript;
            rat.KillEnemy();
            rat.gameObject.transform.SetParent(this.transform);
            ratsOnGlueTrap++;
            SetScrapValue(scrapValue + scrapValuePerRat);
        }
    }
}