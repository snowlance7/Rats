using BepInEx.Logging;
using GameNetcodeStuff;
using Mono.Cecil.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.Utilities;
using static Rats.Plugin;
using static Rats.RatAI;
using static Rats.RatManager;

// TODO: Rat king can spawn naturally or spawn when a sewer grate nest is sealed!
// TODO: Set up calling/rallying rats around the king. He will only call when a player is in line of sight and has high threat
// TODO: Rats who see a player with high threat will call to rat king if he isnt currently targetting a player. if player is high threat, rat king will not stop chasing player until they are out of the facility or dead.
namespace Rats
{
    public class RatKingAI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;
        public static RatKingAI? Instance { get; private set; }
        public bool ratKingCanDie = false;

#pragma warning disable 0649
        public ScanNodeProperties ScanNode;
        public AudioClip[] SqueakSFX;
        public AudioClip[] AttackSFX;
        public AudioClip[] HitSFX;
        public AudioClip[] NibbleSFX;
        public AudioClip[] ScreamSFX;
        public Transform RatMouth;
        public GameObject NestPrefab;
        public GameObject RatCrownPrefab;
        public GameObject RatMesh;
        public GameObject CrownMesh;
        public NetworkAnimator networkAnimator;
        public Transform NestTransform;
#pragma warning restore 0649

        int hashIdle1;
        int hashIdle2;
        int hashRun;

        public RatNest KingNest;

        float timeSinceCollision;
        float timeSinceAddThreat;
        float timeSinceRally;

        DeadBodyInfo? heldBody;

        Coroutine? ratCoroutine;

        // Config Values
        const int ratKingDamage = 25;

        const float rallyCooldown = 60f;
        const float distanceToLoseRatKing = 20f;
        const float rallyRadius = 20f;

        public enum State
        {
            Roaming,
            Hunting,
            Attacking,
            Rampaging
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance != null && Instance != this)
            {
                if (!IsServerOrHost) { return; }
                logger.LogDebug("There is already a Rat King in the scene. Removing this one.");
                NetworkObject.Despawn(true);
                return;
            }
            Instance = this;
            logger.LogDebug("Finished spawning Rat King");


            if (!IsServerOrHost) { return; }
            logger.LogDebug("Spawning KingNest");
            KingNest = GameObject.Instantiate(NestPrefab, NestTransform).GetComponent<RatNest>(); // TODO: Test this
            KingNest.NetworkObject.Spawn(true);
            KingNest.SetAsRatKingNestClientRpc();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void Start()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }

            hashIdle1 = Animator.StringToHash("idle1");
            hashIdle2 = Animator.StringToHash("idle2");
            hashRun = Animator.StringToHash("run");

            thisEnemyIndex = RoundManager.Instance.numberOfEnemiesInScene;
            RoundManager.Instance.numberOfEnemiesInScene++;
            allAINodes = RoundManager.Instance.insideAINodes;
            path1 = new NavMeshPath();
            ventAnimationFinished = true;
            if (IsServerOrHost)
            {
                SwitchToBehaviourClientRpc((int)State.Roaming);
            }
        }

        public override void Update()
        {
            base.Update();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            timeSinceRally += Time.deltaTime;
            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;

            creatureAnimator.SetBool(hashRun, currentBehaviourStateIndex != (int)State.Roaming);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || stunNormalizedTimer > 0f || inSpecialAnimation)
            {
                agent.speed = 0f;
                return;
            };

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:

                    agent.speed = 3f;
                    StartRoam();
                    CheckForThreatsInLOS();

                    break;

                case (int)State.Hunting:
                    agent.speed = 7.5f;

                    if (targetPlayer == null || targetPlayer.isPlayerDead || targetPlayer.disconnectedMidGame || !SetDestinationToPosition(targetPlayer.transform.position, true))
                    {
                        logger.LogDebug("Stop Targeting");
                        targetPlayer = null;
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        return;
                    }

                    break;

                case (int)State.Attacking:
                    agent.speed = 6.5f;

                    CheckForThreatsInLOS();

                    if (!TargetClosestPlayerInAnyCase() || (Vector3.Distance(transform.position, targetPlayer.transform.position) > distanceToLoseRatKing && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
                    {
                        logger.LogDebug("Stop Targeting");
                        targetPlayer = null;
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        return;
                    }

                    SetDestinationToPosition(targetPlayer.transform.position);

                    break;

                case (int)State.Rampaging:
                    agent.speed = 7f;

                    if (TargetClosestPlayerInAnyCase())
                    {
                        StopRoam();
                        CheckForThreatsInLOS();
                        SetDestinationToPosition(targetPlayer.transform.position);
                        return;
                    }

                    StartRoam();

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        bool ReachedDestination()
        {
            // Check if we've reached the destination
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void StartRampage()
        {
            if (currentBehaviourStateIndex == (int)State.Rampaging) { return; }

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (!PlayerIsTargetable(player)) { continue; }
                AddThreat(player, highThreatToAttackPlayer, false);
            }

            SwitchToBehaviourClientRpc((int)State.Rampaging);
        }

        public void AlertHighThreatPlayer(PlayerControllerB player) // Called from ratai
        {
            if (targetPlayer != null || inSpecialAnimation) { return; }

            targetPlayer = player;

            if (currentBehaviourStateIndex != (int)State.Rampaging)
            {
                SwitchToBehaviourClientRpc((int)State.Hunting);
                Rally(targetPlayer);
            }
        }

        public void DropBodyOnLocalClient(bool deactivate = false)
        {
            targetPlayer = null;

            if (heldBody != null)
            {
                heldBody.attachedTo = null;
                heldBody.attachedLimb = null;
                heldBody.matchPositionExactly = false;
                if (deactivate) { heldBody.DeactivateBody(setActive: false); }
                heldBody = null;
            }

            inSpecialAnimation = false;
        }

        bool TargetClosestPlayerInAnyCase()
        {
            mostOptimalDistance = 2000f;
            targetPlayer = null;
            for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
            {
                tempDist = Vector3.Distance(transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
                if (tempDist < mostOptimalDistance && StartOfRound.Instance.allPlayerScripts[i].isInsideFactory)
                {
                    mostOptimalDistance = tempDist;
                    targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
                }
            }

            return targetPlayer != null;
        }

        void CheckForThreatsInLOS()
        {
            if (timeSinceAddThreat > timeToIncreaseThreat)
            {
                PlayerControllerB player = CheckLineOfSightForPlayer(60f, 60, 5);
                if (PlayerIsTargetable(player))
                {
                    AddThreat(player);
                    return;
                }
            }
        }

        public new PlayerControllerB CheckLineOfSightForPlayer(float width = 45f, int range = 60, int proximityAwareness = -1)
        {
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                Vector3 position = StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position;
                if (Vector3.Distance(position, eye.position) < (float)range && !Physics.Linecast(eye.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    Vector3 to = position - eye.position;
                    if (Vector3.Angle(eye.forward, to) < width || (proximityAwareness != -1 && Vector3.Distance(eye.position, position) < (float)proximityAwareness))
                    {
                        return StartOfRound.Instance.allPlayerScripts[i];
                    }
                }
            }
            return null;
        }

        void StartRoam()
        {
            if (ratCoroutine != null) { return; }
            ratCoroutine = StartCoroutine(RoamCoroutine());
        }

        void StopRoam()
        {
            if (ratCoroutine == null) { return; }
            StopCoroutine(ratCoroutine);
            ratCoroutine = null;
        }

        IEnumerator RoamCoroutine()
        {
            yield return null;
            while (ratCoroutine != null)
            {
                float timeStuck = 0f;
                targetNode = GetRandomNode();
                Vector3 position = RoundManager.Instance.GetNavMeshPosition(targetNode.position, RoundManager.Instance.navHit, 1.75f, agent.areaMask);
                if (!SetDestinationToPosition(position)) { continue; }
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    //if (!agent.hasPath || timeStuck > 1f)
                    if (ReachedDestination())
                    {
                        inSpecialAnimation = true;
                        int animHash = UnityEngine.Random.Range(0, 2) == 0 ? hashIdle1 : hashIdle2;
                        networkAnimator.SetTrigger(animHash);
                        yield return new WaitUntil(() => inSpecialAnimation == false);
                        break;
                    }

                    /*if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }*/
                }
            }
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
        {
            if (!isEnemyDead && ratKingCanDie)
            {
                enemyHP -= force;
                if (enemyHP <= 0)
                {
                    DropBodyOnLocalClient();
                    RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
                    KillEnemyOnOwnerClient();
                    return;
                }

                logger.LogDebug($"RatKingHP: {enemyHP}");
            }

            if (!IsServerOrHost || playerWhoHit == null) { return; }
            AddThreat(playerWhoHit, threatToAttackPlayer);
            Rally(playerWhoHit);
        }

        public override void HitFromExplosion(float distance)
        {
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            HitEnemy(5);
        }

        public bool PlayerIsTargetable(PlayerControllerB playerScript)
        {
            if (playerScript == null) { return false; }
            if (playerScript.isPlayerDead) { return false; }
            if (!playerScript.isPlayerControlled) { return false; }
            if (playerScript.inAnimationWithEnemy != null) { return false; }
            if (playerScript.sinkingValue >= 0.73f) { return false; }
            return true;
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            if (isEnemyDead) { return; }
            if (currentBehaviourStateIndex == (int)State.Roaming) { return; }
            if (timeSinceCollision > 1f)
            {
                timeSinceCollision = 0f;
                PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
                if (!PlayerIsTargetable(player)) { return; }
                if (player == null || player != localPlayer) { return; }

                if (player.health <= ratKingDamage)
                {
                    inSpecialAnimation = true;
                    player.KillPlayer(Vector3.zero, true, CauseOfDeath.Mauling);
                    EatPlayerServerRpc(player.actualClientId);
                    return;
                }

                int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                player.DamagePlayer(ratKingDamage, true, true, CauseOfDeath.Mauling, deathAnim);
                PlayAttackSFXServerRpc();
            }
        }

        void AddThreat(PlayerControllerB player, int amount = 1, bool aggro = true)
        {
            if (player == null) { return; }

            timeSinceAddThreat = 0f;

            if (RatManager.PlayerThreatCounter.ContainsKey(player))
            {
                RatManager.PlayerThreatCounter[player] += amount;
            }
            else
            {
                RatManager.PlayerThreatCounter.Add(player, amount);
            }

            int threat = RatManager.PlayerThreatCounter[player];
            log($"{player.playerUsername}: {threat} threat");

            if (!aggro) { return; }

            PlaySqueakSFXClientRpc();

            if (RatManager.PlayerThreatCounter[player] > highThreatToAttackPlayer)
            {
                Rally(player); // TODO: runs in place after this...
            }

            if (RatManager.PlayerThreatCounter[player] > threatToAttackPlayer)
            {
                targetPlayer = player;
                SwitchToBehaviourClientRpc((int)State.Attacking);
            }
        }

        public void Rally(PlayerControllerB player)
        {
            if (timeSinceRally < rallyCooldown) { return; }
            logger.LogDebug("Rallying");
            timeSinceRally = 0f;
            inSpecialAnimation = true;
            targetPlayer = player;
            networkAnimator.SetTrigger("rally");
        }

        // Animator stuff

        public void FinishRallyAnim() // Animation function "rally" // TODO: Set this up in unity editor
        {
            if (!IsServerOrHost) { return; }
            logger.LogDebug("Finishing rally animation");
            inSpecialAnimation = false;

            foreach (var rat in SpawnedRats)
            {
                if (rat.defenseRat) { continue; }
                if (Vector3.Distance(rat.transform.position, transform.position) > rallyRadius) { continue; }

                rat.targetPlayer = targetPlayer;
                rat.rallyRat = true;
                rat.SwitchToBehaviorState(RatAI.State.Swarming);
            }
        }

        public void PlayRallySFX()
        {
            RoundManager.PlayRandomClip(creatureVoice, ScreamSFX);
            logger.LogDebug("Played rally sfx");
        }

        public void FinishEatingBody()
        {
            logger.LogDebug("Finishing eating body");
            DropBodyOnLocalClient(true);
            inSpecialAnimation = false;

            if (!IsServerOrHost) { return; }
            KingNest.AddFood(playerFoodAmount);
        }

        public void SetInSpecialAnimation()
        {
            inSpecialAnimation = true;
        }

        public void UnsetInSpecialAnimation()
        {
            logger.LogDebug("In UnsetInSpecialAnimation()");
            inSpecialAnimation = false;
            RoundManager.PlayRandomClip(creatureSFX, SqueakSFX);
        }

        // RPC's

        [ClientRpc]
        public new void SwitchToBehaviourClientRpc(int stateIndex)
        {
            ratKingCanDie = stateIndex == (int)State.Rampaging;
            base.SwitchToBehaviourClientRpc(stateIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayAttackSFXServerRpc()
        {
            if (IsServerOrHost)
            {
                PlayAttackSFXClientRpc();
            }
        }

        [ClientRpc]
        public void PlayAttackSFXClientRpc()
        {
            RoundManager.PlayRandomClip(creatureSFX, AttackSFX, true, 1, -1);
        }

        [ClientRpc]
        public void PlaySqueakSFXClientRpc(bool shortsfx = false)
        {
            if (shortsfx)
            {
                RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            }
            else
            {
                RoundManager.PlayRandomClip(creatureSFX, SqueakSFX, true, 1, -1);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddThreatServerRpc(ulong clientId)
        {
            if (IsServerOrHost)
            {
                if (timeSinceAddThreat > timeToIncreaseThreat)
                {
                    PlayerControllerB player = PlayerFromId(clientId);
                    AddThreat(player);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void EatPlayerServerRpc(ulong clientId)
        {
            if (!IsServerOrHost) { return; }

            EatPlayerClientRpc(clientId);
        }

        [ClientRpc]
        public void EatPlayerClientRpc(ulong clientId)
        {
            targetPlayer = PlayerFromId(clientId);

            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                heldBody = targetPlayer.deadBody;
                heldBody.attachedTo = RatMouth;
                heldBody.attachedLimb = targetPlayer.deadBody.bodyParts[5];
                heldBody.matchPositionExactly = true;
            }

            inSpecialAnimation = true;
            creatureAnimator.SetTrigger("eat");
            RoundManager.PlayRandomClip(creatureSFX, NibbleSFX, true, 1f, -1);
        }

        [ClientRpc]
        public void DropBodyClientRpc(bool deactivate)
        {
            DropBodyOnLocalClient(deactivate);
        }
    }
}