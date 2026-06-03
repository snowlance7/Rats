using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using static Rats.Plugin;
using static Rats.Configs;

namespace Rats.Items
{
    internal class RatPoisonBehavior : PhysicsProp // TODO: Make it act like weed killer and kill cadaver weeds and fill gas in company cruiser
    {
        public AudioSource audioSource = null!;
        public ParticleSystem particleSystem = null!;
        public Animator animator = null!;
        public Transform pourDirection = null!;
        public ScanNodeProperties scanNode = null!;

        public PlayerControllerB? lastPlayerHeldBy;

        readonly float downAngle = 0.7f;
        bool pouring;
        float currentFluid;

        float pourRate;
        CadaverGrowthAI? cadaverGrowthAI;

        public void Awake()
        {
            itemProperties.floorYOffset = 90;
            itemProperties.rotationOffset = new Vector3(180, 180, 60);
            itemProperties.positionOffset = new Vector3(-0.65f, 0.55f, 0f);
            itemProperties.restingRotation = new Vector3(0, 0, 0);
            itemProperties.syncUseFunction = true; // TODO
            itemProperties.weight = 1.2f;
        }

        public override void Start()
        {
            base.Start();
            scanNode.subText = "";
            currentFluid = cfgMaxFluid;
            pourRate = cfgPourRate;
        }

        public override void Update()
        {
            base.Update();

            if (playerHeldBy != null)
            {
                lastPlayerHeldBy = playerHeldBy;
            }

            if (pouring)
            {
                currentFluid -= pourRate * Time.deltaTime;
                SetControlTipsForItem();

                if (currentFluid <= 0)
                {
                    SetPouring(false);
                    currentFluid = 0;
                    return;
                }

                if (!isPlayerLookingDown())
                {
                    SetPouring(false);
                    return;
                }

                if (playerHeldBy == null || localPlayer != playerHeldBy) { return; }

                // Run on client
                if (Physics.Raycast(pourDirection.position, -Vector3.up, out var hitInfo, 80f))
                {
                    if (hitInfo.collider.gameObject.TryGetComponent(out RatNest nest) && nest.poisonInNest < cfgPoisonToCloseNest)
                    {
                        nest.AddPoisonServerRpc(pourRate * Time.deltaTime);
                    }
                }
                if (cadaverGrowthAI == null)
                {
                    cadaverGrowthAI = FindObjectOfType<CadaverGrowthAI>();
                }
                if (cadaverGrowthAI != null && Physics.Raycast(pourDirection.position, -Vector3.up, out var hitInfo2, 80f, 268437761, QueryTriggerInteraction.Ignore))
                {
                    cadaverGrowthAI.DestroyPlantAtPosition(hitInfo2.point, playEffect: true); // TODO: Test this
                }
            }
        }

        public override void SetControlTipsForItem()
        {
            if (playerHeldBy == null) { return; }
            if (playerHeldBy != localPlayer) { return; }
            string[] toolTips = itemProperties.toolTips;
            toolTips[0] = $"Pour [LMB] ({currentFluid.ToString("F1")}L left)";
            HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
        }

        public override void ItemActivate(bool used, bool buttonDown = true) // Synced TODO: create rpcs
        {
            base.ItemActivate(used, buttonDown);

            bool lookingDown = isPlayerLookingDown();
            bool isPouring = buttonDown && lookingDown && currentFluid > 0f;

            SetPouring(isPouring);
        }

        bool isPlayerLookingDown() => Vector3.Dot(playerHeldBy.gameplayCamera.transform.forward, Vector3.down) > downAngle;

        void SetPouring(bool _pouring)
        {
            if (lastPlayerHeldBy == null) { return; }
            pouring = _pouring;
            lastPlayerHeldBy.activatingItem = pouring;
            animator.SetBool("pour", pouring);

            if (pouring)
            {
                particleSystem.Play();
                audioSource.Play();
            }
            else
            {
                particleSystem.Stop();
                audioSource.Stop();
            }
        }

        public override void DiscardItem()
        {
            base.DiscardItem();
            SetPouring(false);
        }

        public override void GrabItem()
        {
            base.GrabItem();
            SetControlTipsForItem();
        }

        public override int GetItemDataToSave() => (int)currentFluid;
        public override void LoadItemSaveData(int saveData) => currentFluid = (float)saveData;
    }
}