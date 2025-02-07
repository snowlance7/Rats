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

        readonly float downAngle = 0.7f;

        float maxFluid = 5f;
        float currentFluid;
        float pourRate = 0.5f;

        public override void Start()
        {
            base.Start();
            currentFluid = maxFluid;
            SetControlTipsForItem();
        }

        public override void SetControlTipsForItem()
        {
            string[] toolTips = itemProperties.toolTips;
            toolTips[0] = $"Pour [LMB] ({currentFluid} left)";
            HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);

            bool lookingDown = Vector3.Dot(playerHeldBy.gameplayCamera.transform.forward, Vector3.down) > downAngle;
            bool isPouring = buttonDown && lookingDown && currentFluid > 0f;

            playerHeldBy.activatingItem = isPouring;
            ItemAnimator.SetBool("pour", isPouring);

            if (isPouring)
            {
                particleSystem.Play();
                ItemAudio.Play();
            }
            else
            {
                particleSystem.Stop();
                ItemAudio.Stop();
            }

            Pour(isPouring);
        }

        public void Pour(bool pouring)
        {

        }

        IEnumerator PourCoroutine()
        {
            yield return null;

            while (playerHeldBy.activatingItem)
            {
                currentFluid -= pourRate * Time.deltaTime;

            }
        }
    }
}