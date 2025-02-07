using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Rats.Items
{
    internal class SnapTrapBehavior : MonoBehaviour
    {
        public AudioSource ItemAudio;
        public Animator ItemAnimator;
        public Transform RatPosition;
        public GameObject TriggerMesh;
        public Rigidbody rb;
        public Transform LaunchDirection;
        public BoxCollider collider;

        Quaternion ratRotationOffset;
        Vector3 ratPositionOffset;
        float launchForce = 10f;

        public void OnTriggerEnter(Collider other)
        {
            if (!other.gameObject.TryGetComponent(out RatAI rat)) { return; }
            rat.KillEnemy();
            ItemAnimator.SetTrigger("trigger");
            ItemAudio.Play();
            TriggerMesh.SetActive(false);
            rat.transform.position = RatPosition.position;
            rat.transform.SetParent(RatPosition);

            collider.isTrigger = false;
            collider.providesContacts = true;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.AddForce(LaunchDirection.forward * launchForce, ForceMode.Impulse);
        }
    }
}
