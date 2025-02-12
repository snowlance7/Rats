using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items
{
    internal class SnapTrapBehavior : MonoBehaviour
    {
        public AudioSource ItemAudio;
        public AudioClip[] SetSFX;
        public AudioClip[] SnapSFX;
        public Animator ItemAnimator;
        public GameObject TriggerMesh;
        public GameObject RatMesh;
        public GameObject JermaRatMesh;
        public Rigidbody rb;
        public Transform LaunchDirection;
        public BoxCollider collider;

        bool triggered;
        Quaternion ratRotationOffset;
        Vector3 ratPositionOffset;
        float minLaunchForce = 10f;
        float maxLaunchForce = 15f;
        float destroyTime = 10f;

        public void Start()
        {
            RoundManager.PlayRandomClip(ItemAudio, SetSFX);
        }

        public void OnTriggerEnter(Collider other) // TODO: Test this
        {
            if (triggered) { return; }
            Plugin.LoggerInstance.LogDebug("In OnTriggerEnter()");
            if (!other.gameObject.TryGetComponent(out RatAICollisionDetect ratCollision)) { return; }
            Plugin.LoggerInstance.LogDebug("Got rat collision");
            ratCollision.mainScript.KillEnemy();
            if (IsServerOrHost)
            {
                ratCollision.mainScript.NetworkObject.Despawn(false);
            }
            Destroy(ratCollision.mainScript.gameObject);
            ItemAnimator.SetTrigger("trigger");
            RoundManager.PlayRandomClip(ItemAudio, SnapSFX);
            TriggerMesh.SetActive(false);

            if (configUseJermaRats.Value)
            {
                JermaRatMesh.SetActive(true);
            }
            else
            {
                RatMesh.SetActive(true);
            }

            collider.isTrigger = false;
            collider.providesContacts = true;
            rb.isKinematic = false;

            Vector3 torque = new Vector3(UnityEngine.Random.Range(0f, 5f), 0f, UnityEngine.Random.Range(-3f, -10f));

            rb.AddForce(LaunchDirection.forward * UnityEngine.Random.Range(minLaunchForce, maxLaunchForce), ForceMode.Impulse);
            rb.AddTorque(torque, ForceMode.Impulse);
            rb.useGravity = true;
            triggered = true;

            GameObject.Destroy(this.gameObject, destroyTime);
        }
    }
}