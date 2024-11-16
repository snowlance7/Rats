using BepInEx.Logging;
using GameNetcodeStuff;
using HandyCollections.Heap;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using static Rats.Plugin;

// Cheese to lure the rats away from the grate so the player can block it
// Rats will scout and do a normal search, if they see a player after 20 seconds, they will add a counter to the threatcounter, when max threat, they will run back to sewer and gather rats they touch and get defending rats and swarm around the player for 20 seconds, any rats nearby will also swarm. then attack.

namespace Rats
{
    internal class RatAI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649

#pragma warning restore 0649

        public static Dictionary<PlayerControllerB, int> PlayerThreatCounter = new Dictionary<PlayerControllerB, int>();
        public static Dictionary<EnemyAI, int> EnemyThreatCounter = new Dictionary<EnemyAI, int>();

        float timeSinceDamagePlayer;
        float timeSinceSeenPlayer;

        bool scoutRat;
        bool sickRat;
        int defaultStateIndex;
        Vector3 nestPosition;

        // Constants

        // Config Values
        float sickRatChance = 0.1f;
        float scoutRatChance = 0.75f;
        float defenseRadius = 15f;


        public enum State
        {
            Scouting,
            Defending,
            Swarming,
            Attacking,
            Fleeing
        }

        public void SwitchToBehaviourStateCustom(State state)
        {
            logger.LogDebug("Switching to state: " + state);

            switch (state)
            {
                case State.Scouting:
                    StartSearch(nestPosition);

                    break;
                case State.Defending:
                    break;
                case State.Swarming:
                    break;
                case State.Attacking:
                    break;
                case State.Fleeing:
                    break;
                default:
                    break;
            }

            SwitchToBehaviourClientRpc((int)state);
        }

        public override void Start()
        {
            base.Start();
            logger.LogDebug("Rat Spawned");

            if (IsServerOrHost)
            {
                RoundManager.Instance.SpawnedEnemies.Add(this);
                scoutRat = UnityEngine.Random.Range(0f, 100f) < scoutRatChance;
                defaultStateIndex = scoutRat ? 0 : 1;
                currentBehaviourStateIndex = defaultStateIndex;

                SewerGrate? ratSpawn = UnityEngine.GameObject.FindObjectOfType<SewerGrate>();
                if (ratSpawn != null)
                {
                    nestPosition = UnityEngine.GameObject.FindObjectOfType<SewerGrate>().transform.position;
                }
                else
                {
                    nestPosition = transform.position;
                }

                if (scoutRat)
                {
                    StartSearch(nestPosition);
                }
                else
                {
                    StartCoroutine(DefenseCoroutine());
                }
            }
        }

        public override void Update()
        {
            base.Update();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            timeSinceDamagePlayer += Time.deltaTime;
            timeSinceSeenPlayer += Time.deltaTime;
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Scouting:
                    break;
                case (int)State.Defending:
                    break;
                case (int)State.Swarming:
                    break;
                case (int)State.Attacking:
                    break;
                case (int)State.Fleeing:
                    break;

                default:
                    logger.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        IEnumerator DefenseCoroutine()
        {
            yield return null;

            while (currentBehaviourStateIndex == (int)State.Defending)
            {
                yield return null;

                Vector3 pos = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(nestPosition, defenseRadius, RoundManager.Instance.navHit, );
                if (SetDestinationToPosition(pos, true))
                {
                    yield return null;
                    yield return new WaitUntil(() => Vector3.Distance(transform.position, pos) < 1f);
                    // TODO: play squeeksfx
                }
            }
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
        {
            if (!isEnemyDead && !inSpecialAnimation)
            {
                enemyHP -= force;
                if (enemyHP <= 0)
                {
                    KillEnemyOnOwnerClient();
                    return;
                }
            }
        }

        public override void HitFromExplosion(float distance)
        {
            KillEnemyOnOwnerClient();
        }

        public override void OnCollideWithPlayer(Collider other) // This only runs on client
        {
            base.OnCollideWithPlayer(other);
            if (currentBehaviourStateIndex == (int)State.Attacking)
            {
                if (timeSinceDamagePlayer > 1f)
                {
                    PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                    if (player != null && !player.isPlayerDead && !inSpecialAnimation && !isEnemyDead)
                    {
                        timeSinceDamagePlayer = 0f;
                        int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                        player.DamagePlayer(1, true, true, CauseOfDeath.Mauling, deathAnim);
                    }
                }
            }
        }

        // RPC's

        [ServerRpc(RequireOwnership = false)]
        public new void SwitchToBehaviourServerRpc(int stateIndex)
        {
            if (IsServerOrHost)
            {
                SwitchToBehaviourStateCustom((State)stateIndex);
            }
        }
    }
}

// TODO: statuses: shakecamera, playerstun, drunkness, fear, insanity