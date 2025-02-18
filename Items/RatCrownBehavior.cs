using BepInEx.Logging;
using Dissonance;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;


/*DissonanceComms comms = FindObjectOfType<DissonanceComms>();
float detectedVolumeAmplitude = Mathf.Clamp(comms.FindPlayer(comms.LocalPlayerName).Amplitude * 35f, 0f, 1f);
log(detectedVolumeAmplitude);*/

namespace Rats.Items
{
    internal class RatCrownBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

        readonly Vector3 posOffsetWearing = new Vector3(0.3f, -0.1f, -0.4f);
        readonly Vector3 rotOffsetWearing = new Vector3(90f, 115f, 0f);

        VoicePlayerState localPlayerComms;
        bool wearingCrown;
        PlayerControllerB previousPlayerHeldBy;
        float timeSinceRally;

        float volumeToRallyRats = 0.5f;
        float rallyCooldown = 10f;

        public override void Start()
        {
            base.Start();

            DissonanceComms comms = FindObjectOfType<DissonanceComms>();
            localPlayerComms = comms.FindPlayer(comms.LocalPlayerName);
        }

        public override void Update()
        {
            base.Update();

            if (playerHeldBy != null)
            {
                previousPlayerHeldBy = playerHeldBy;
            }

            timeSinceRally += Time.deltaTime;

            if (wearingCrown && playerHeldBy == localPlayer && timeSinceRally > rallyCooldown)
            {
                float volume = GetPlayerVolume();
                if (volume  >= volumeToRallyRats)
                {
                    log("Rallying rats with crown");
                    rallyCooldown = 0f;
                    RallyRatsServerRpc(); // TODO: TEST THIS
                }
            }
        }

        public override void LateUpdate()
        {
            if (wearingCrown)
            {
                if (parentObject != null)
                {
                    transform.rotation = parentObject.rotation;
                    transform.Rotate(rotOffsetWearing);
                    transform.position = parentObject.position;
                    Vector3 positionOffset = posOffsetWearing;
                    positionOffset = parentObject.rotation * positionOffset;
                    transform.position += positionOffset;
                }
                if (radarIcon != null)
                {
                    radarIcon.position = transform.position;
                }

                return;
            }

            base.LateUpdate();
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);

            if (playerHeldBy != null)
            {
                previousPlayerHeldBy = playerHeldBy;
                wearingCrown = buttonDown;
                playerHeldBy.playerBodyAnimator.SetBool("HoldMask", buttonDown);
                playerHeldBy.activatingItem = buttonDown;
            }
        }

        public override void PocketItem()
        {
            base.PocketItem();
            if (previousPlayerHeldBy != null)
            {
                wearingCrown = false;
                previousPlayerHeldBy.playerBodyAnimator.SetBool("HoldMask", false);
                previousPlayerHeldBy.activatingItem = false;
            }
        }

        public override void DiscardItem()
        {
            base.DiscardItem();
            if (previousPlayerHeldBy != null)
            {
                wearingCrown = false;
                previousPlayerHeldBy.playerBodyAnimator.SetBool("HoldMask", false);
                previousPlayerHeldBy.activatingItem = false;
            }
        }

        public EnemyAI? GetClosestEnemyTargettingPlayer()
        {
            EnemyAI? closestEnemy = null;
            float closestDistance = 25f;
            
            foreach (var enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (!enemy.enemyType.canDie) { continue; }
                if (enemy.targetPlayer != previousPlayerHeldBy) { continue; }
                float distance = Vector3.Distance(transform.position, enemy.transform.position);

                if (distance >= closestDistance) { continue; }

                closestEnemy = enemy;
                closestDistance = distance;
            }

            return closestEnemy;
        }

        public float GetPlayerVolume()
        {
            return Mathf.Clamp(localPlayerComms.Amplitude * 35f, 0f, 1f);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RallyRatsServerRpc()
        {
            if (!IsServerOrHost) { return; }
            EnemyAI? enemy = GetClosestEnemyTargettingPlayer();
            if (enemy == null) { return; }

            foreach (var rat in RatManager.SpawnedRats)
            {
                if (rat.targetPlayer != previousPlayerHeldBy) { continue; }
                rat.rallyRat = true;
                rat.targetPlayer = null;
                rat.targetEnemy = enemy;
                rat.SwitchToBehaviorState(RatAI.State.Swarming);
            }
        }
    }
}
