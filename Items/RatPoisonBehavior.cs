using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace Rats.Items
{
    internal class RatPoisonBehavior : PhysicsProp
    {
        public AudioSource ItemAudio;
        public ParticleSystem particleSystem;
        public Animator ItemAnimator;
        public Transform PourDirection;

        readonly float downAngle = 0.7f;

        float maxFluid = 5f;
        float currentFluid;
        float pourRate = 0.1f;
        bool pouring;

        public override void Start()
        {
            base.Start();
            currentFluid = maxFluid;
            SetControlTipsForItem();
        }

        public override void SetControlTipsForItem()
        {
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

        bool isPlayerLookingDown()
        {
            return Vector3.Dot(playerHeldBy.gameplayCamera.transform.forward, Vector3.down) > downAngle;
        }

        void SetPouring(bool value)
        {
            pouring = value;
            playerHeldBy.activatingItem = pouring;
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

        public override void Update()
        {
            base.Update();
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

                if (Physics.Raycast(PourDirection.position, -Vector3.up, out var hitInfo, 80f))
                {
                    if (hitInfo.collider.gameObject.TryGetComponent(out RatNest nest))
                    {
                        Plugin.LoggerInstance.LogDebug("Pouring into nest");
                        nest.AddPoison(pourRate);
                    }
                }
            }
        }

        public override int GetItemDataToSave()
        {
            return (int)currentFluid;
        }

        public override void LoadItemSaveData(int saveData)
        {
            currentFluid = (float)saveData;
        }
    }
}