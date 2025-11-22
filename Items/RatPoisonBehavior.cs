using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items
{
    internal class RatPoisonBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public AudioSource ItemAudio;
        public ParticleSystem particleSystem;
        public Animator ItemAnimator;
        public Transform PourDirection;
        public ScanNodeProperties ScanNode;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public PlayerControllerB? lastPlayerHeldBy;

        readonly float downAngle = 0.7f;
        bool pouring;
        float currentFluid;

        float pourRate;

        public override void Start()
        {
            base.Start();
            ScanNode.subText = "";
            currentFluid = configRatPoisonMaxFluid.Value;
            pourRate = configRatPoisonPourRate.Value;
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
                if (Physics.Raycast(PourDirection.position, -Vector3.up, out var hitInfo, 80f))
                {
                    if (hitInfo.collider.gameObject.TryGetComponent(out RatNest nest) && !nest.IsRatKing && nest.poisonInNest < RatManager.poisonToCloseNest)
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

        public override void ItemActivate(bool used, bool buttonDown = true)
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
            ItemAnimator.SetBool("pour", pouring);

            if (pouring)
            {
                particleSystem.Play();
                ItemAudio.Play();
            }
            else
            {
                particleSystem.Stop();
                ItemAudio.Stop();
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