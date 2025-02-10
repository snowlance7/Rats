using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items
{
    internal class SnapTrapBehavior : NetworkBehaviour
    {
        public AudioSource ItemAudio;
        public Animator ItemAnimator;
        public Transform RatPosition;
        public GameObject TriggerMesh;
        public Rigidbody rb;
        public Transform LaunchDirection;
        public BoxCollider collider;

        RatAI ratInTrap;
        bool triggered;
        Quaternion ratRotationOffset;
        Vector3 ratPositionOffset;
        float launchForce = 10f;
        float destroyTime;

        public void OnTriggerEnter(Collider other) // TODO: Test this
        {
            if (triggered) { return; }
            Plugin.LoggerInstance.LogDebug("In OnTriggerEnter()");
            if (!other.gameObject.TryGetComponent(out RatAICollisionDetect ratCollision)) { return; }
            ratInTrap = ratCollision.mainScript;
            ratInTrap.KillEnemy();
            ratInTrap.agent.enabled = false;
            ratInTrap.transform.position = RatPosition.position;
            ratInTrap.gameObject.transform.SetParent(RatPosition, true);
            ItemAnimator.SetTrigger("trigger");
            ItemAudio.Play();
            TriggerMesh.SetActive(false);

            collider.isTrigger = false;
            collider.providesContacts = true;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(LaunchDirection.forward * launchForce, ForceMode.Impulse);
            triggered = true;

            if (IsServerOrHost) { NetworkObject.Despawn(false); }
            GameObject.Destroy(this.gameObject, 5f);
        }

        public override void OnDestroy()
        {
            if (!IsServerOrHost) { return; }
            ratInTrap.NetworkObject.Despawn(true);
            base.OnDestroy();
        }
    }
}