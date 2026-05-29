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
    internal class RatPoisonBehavior : PhysicsProp // TODO: Make it act like weed killer and kill cadaver weeds
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

        public void Awake()
        {
            var i = itemProperties;
            i.itemName = "Rat Poison";
            i.twoHanded = false;
            i.twoHandedAnimation = false;
            i.disableHandsOnWall = false;
            i.canBeGrabbedBeforeGameStart = true;
            i.disallowUtilitySlot = false;
            i.weight = 1.2f;
            i.itemIsTrigger = false;
            i.holdButtonUse = true;
            i.isConductiveMetal = false;

            i.requiresBattery = false;
            i.batteryUsage = 0;
            i.automaticallySetUsingPower = false;

            i.grabAnim = "";
            i.useAnim = "";
            i.pocketAnim = "";
            i.throwAnim = "";
            i.grabAnimationTime = 1;

            i.syncGrabFunction = false;
            i.syncUseFunction = false;
            i.syncDiscardFunction = false;
            i.syncInteractLRFunction = false;

            i.saveItemVariable = true;

            i.isDefensiveWeapon = false;
            i.toolTips = ["Pour [LMB]"];
            i.verticalOffset = 0;
            i.floorYOffset = 90;
            i.allowDroppingAheadOfPlayer = true;
            i.restingRotation = new Vector3(0, 0, 0);
            i.rotationOffset = new Vector3(180, 180, 60);
            i.positionOffset = new Vector3(-0.65f, 0.55f, 0f);
            i.meshOffset = false;
            i.usableInSpecialAnimations = false;
            i.canBeInspected = false;
        }

        public override void Start()
        {
            base.Start();
            scanNode.subText = "";
            currentFluid = cfgRatPoisonMaxFluid;
            pourRate = cfgRatPoisonPourRate;
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