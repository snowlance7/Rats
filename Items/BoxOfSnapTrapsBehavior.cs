using UnityEngine;
using static Rats.Plugin;
using static Rats.Configs;

namespace Rats.Items
{
    internal class BoxOfSnapTrapsBehavior : PhysicsProp
    {
        public GameObject snapTrapPrefab = null!;
        public ScanNodeProperties scanNode = null!;

        // Configs
        int snapTrapAmount;

        public override void Start()
        {
            base.Start();
            scanNode.subText = "";
            snapTrapAmount = cfgSnapTrapAmount;
        }

        public override void SetControlTipsForItem()
        {
            if (playerHeldBy != localPlayer) { return; }
            string[] toolTips = itemProperties.toolTips;
            toolTips[0] = $"Drop Snap Trap [LMB] ({snapTrapAmount} left)"; 
            HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);

            if (buttonDown && snapTrapAmount > 0)
            {
                if (!Physics.Raycast(transform.position, -Vector3.up, out var hitInfo, 80f, 268437761, QueryTriggerInteraction.Ignore)) { return; }
                GameObject.Instantiate(snapTrapPrefab, hitInfo.point, playerHeldBy.transform.rotation);
                snapTrapAmount--;
                SetControlTipsForItem();
            }
        }

        public override void GrabItem()
        {
            base.GrabItem();
            SetControlTipsForItem();
        }

        public override int GetItemDataToSave()
        {
            return snapTrapAmount;
        }

        public override void LoadItemSaveData(int saveData)
        {
            snapTrapAmount = saveData;
        }
    }

    internal class SnapTrapBehavior : MonoBehaviour
    {
        public AudioSource ItemAudio = null!;
        public AudioClip[] SetSFX = null!;
        public AudioClip[] SnapSFX = null!;
        public Animator ItemAnimator = null!;
        public GameObject TriggerMesh = null!;
        public GameObject RatMesh = null!;
        public GameObject JermaRatMesh = null!;
        public Rigidbody rb = null!;
        public Transform LaunchDirection = null!;
        public BoxCollider collider = null!;

        bool triggered;
        Quaternion ratRotationOffset;
        Vector3 ratPositionOffset;

        // Configs
        float minLaunchForce = 10f;
        float maxLaunchForce = 15f;

        public void Start()
        {
            RoundManager.PlayRandomClip(ItemAudio, SetSFX);
        }

        public void OnTriggerEnter(Collider other) // TODO: Test this
        {
            if (triggered) { return; }
            Plugin.logger.LogDebug("In OnTriggerEnter()");
            if (!other.gameObject.TryGetComponent(out RatAICollisionDetect ratCollision)) { return; }
            Plugin.logger.LogDebug("Got rat collision");
            ratCollision.mainScript.KillEnemy();
            if (IsServerOrHost)
            {
                ratCollision.mainScript.NetworkObject.Despawn(false);
            }
            Destroy(ratCollision.mainScript.gameObject);
            ItemAnimator.SetTrigger("trigger");
            RoundManager.PlayRandomClip(ItemAudio, SnapSFX);
            TriggerMesh.SetActive(false);

            if (cfgUseJermaRats)
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

            GameObject.Destroy(this.gameObject, cfgSnapTrapsDespawnTime);
        }
    }
}
