using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items.GlueTraps
{
    internal class GlueBoardBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

        public BoxCollider GlueCollider;

        int ratsOnGlueTrap;

        const float minSlowTime = 1f;
        const float maxSlowTime = 2.5f;

        // Configs
        int maxRatsOnGlueTrap = 10;

        public override void Start()
        {
            base.Start();

            /*propColliders = base.gameObject.GetComponentsInChildren<Collider>();
            originalScale = base.transform.localScale;
            fallTime = 1f;
            hasHitGround = true;
            reachedFloorTarget = true;
            targetFloorPosition = base.transform.localPosition;

            MeshRenderer[] componentsInChildren = base.gameObject.GetComponentsInChildren<MeshRenderer>();
            for (int j = 0; j < componentsInChildren.Length; j++)
            {
                componentsInChildren[j].renderingLayerMask = 1u;
            }
            SkinnedMeshRenderer[] componentsInChildren2 = base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int k = 0; k < componentsInChildren2.Length; k++)
            {
                componentsInChildren2[k].renderingLayerMask = 1u;
            }*/

            GrabbableObjectPhysicsTrigger glueTrigger = GlueCollider.gameObject.AddComponent<GrabbableObjectPhysicsTrigger>();
            glueTrigger.itemScript = this;
        }

        public override void ActivatePhysicsTrigger(Collider other)
        {
            base.ActivatePhysicsTrigger(other);

            Plugin.LoggerInstance.LogDebug("In ActivatePhysicsTrigger()");
            if (ratsOnGlueTrap >= maxRatsOnGlueTrap && !grabbable)
            {
                //SetColliders();
                grabbable = true;
                return;
            }

            if (!IsServerOrHost) { return; }

            if (!other.gameObject.TryGetComponent(out RatAICollisionDetect ratCollision)) { return; }

            float delay = UnityEngine.Random.Range(minSlowTime, maxSlowTime);
            StartCoroutine(KillRatCoroutine(ratCollision.mainScript, delay));
        }

        IEnumerator KillRatCoroutine(RatAI rat, float delay)
        {
            float elapsedTime = 0f;
            float startSpeed = 0.5f;

            while (elapsedTime < delay)
            {
                rat.agent.speed = Mathf.Lerp(startSpeed, 0f, elapsedTime / delay);
                elapsedTime += Time.deltaTime;
                logger.LogDebug("Elapsed Time: " + elapsedTime);
                yield return null;
            }

            logger.LogDebug("Finished slowing, calling rpc now");

            rat.agent.speed = 0f;
            ParentRatToBoardClientRpc(rat.NetworkObject);
        }

        [ClientRpc]
        public void ParentRatToBoardClientRpc(NetworkObjectReference netRef)
        {
            logger.LogDebug("In ParentRatToBoardClientRpc()");
            if (!netRef.TryGet(out NetworkObject netObj)) {  return; }
            RatAI rat = netObj.GetComponent<RatAI>();
            rat.KillEnemy();
            rat.agent.enabled = false;
            rat.gameObject.transform.SetParent(transform);
            ratsOnGlueTrap++;
        }
    }
}