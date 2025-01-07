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
        public bool IsActive = false;

#pragma warning disable 0649
        public ScanNodeProperties ScanNode = null!;
        public AudioClip[] SqueakSFX = null!;
        public AudioClip[] AttackSFX = null!;
        public AudioClip[] HitSFX = null!;
        public AudioClip[] NibbleSFX = null!;
        public AudioClip[] ScreamSFX = null!;
        public Transform RatMouth = null!;
        public GameObject NestPrefab = null!;
        public GameObject RatCrownPrefab = null!;
        public GameObject RatMesh = null!;
        public GameObject CrownMesh = null!;
        public NetworkAnimator networkAnimator = null!;
#pragma warning restore 0649

        public bool IsJermaRat;

        public RatNest KingNest;
        EnemyAI? targetEnemy;

        float timeSinceCollision;
        float timeSinceAddThreat;
        float timeSinceRally;

        DeadBodyInfo? heldBody;
        bool grabbingBody;

        int groupCount;
        List<List<RatAI>> ratGroups = [];

        Coroutine? ratCoroutine;

        // Config Values
        int ratKingDamage = 10;

        float ratUpdateInterval = 0.2f;
        float ratUpdatePercentage = 0.2f;
        float rallyCooldown = 60f;
        float distanceToLoseRatKing = 20f;
        float rallyRadius = 20f;

        public enum State
        {
            Inactive,
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
                Instance.SetActive();
                if (!IsServerOrHost) { return; }
                logger.LogDebug("There is already a Rat King in the scene. Removing this one and activating the rat king.");
                NetworkObject.Despawn(true);
                return;
            }
            Instance = this;
            logger.LogDebug("Finished spawning Rat King");


            if (!IsServerOrHost) { return; }
            KingNest = GameObject.Instantiate(NestPrefab, transform).GetComponent<RatNest>(); // TODO: Test this
            KingNest.NetworkObject.Spawn();
            KingNest.IsRatKing = true;
            KingNest.IsOpen = false;
            KingNest.RatNestMesh.SetActive(false);
            KingNest.TerminalAccessibleObj.enabled = false;

            foreach (var code in KingNest.TerminalCodes)
            {
                code.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetActive()
        {
            if (IsActive) { return; }
            IsActive = true;
            RatMesh.SetActive(true);
            KingNest.IsOpen = true;
            if (!IsServerOrHost) { return; }
            SwitchToBehaviorStateCustom(State.Roaming);
        }

        public override void Start()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }
            
            timeToIncreaseThreat = configTimeToIncreaseThreat.Value;
            threatToAttackPlayer = configThreatToAttackPlayer.Value;
            threatToAttackEnemy = configThreatToAttackEnemy.Value;
            playerFoodAmount = configPlayerFoodAmount.Value;
            //ratKingDamage = configRatKingDamage.Value;

            thisEnemyIndex = RoundManager.Instance.numberOfEnemiesInScene;
            RoundManager.Instance.numberOfEnemiesInScene++;
            allAINodes = RoundManager.Instance.insideAINodes;
            RoundManager.Instance.SpawnedEnemies.Add(this);
            path1 = new NavMeshPath();
            ventAnimationFinished = true;

            if (!IsServerOrHost) { return; }

            //StartCoroutine(RatUpdater());
        }

        IEnumerator RatUpdater()
        {
            yield return null;

            while (!StartOfRound.Instance.shipIsLeaving)
            {
                yield return new WaitForSeconds(AIIntervalTime);
                if (RatManager.SpawnedRats.Count <= 0) { break; }

                ratGroups.Clear();
                groupCount = (int)(1 / ratUpdatePercentage);
                RegisterRats();

                foreach (var ratGroup in ratGroups)
                {
                    yield return new WaitForSeconds(ratUpdateInterval);

                    foreach (var rat in ratGroup)
                    {
                        rat.DoAIInterval();
                    }
                }
            }
        }

        public void RegisterRats()
        {
            foreach (var rat in RatManager.SpawnedRats)
            {
                // Assign the rat to a group based on round-robin
                int groupIndex = ratGroups.Count > 0 ? (ratGroups.Sum(group => group.Count) % groupCount) : 0;
                ratGroups[groupIndex].Add(rat);
            }
        }

        public override void Update()
        {
            base.Update();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || !IsActive)
            {
                return;
            };

            timeSinceRally += Time.deltaTime;
            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
        }

        void SwitchToBehaviorStateCustom(State state)
        {
            if (currentBehaviourStateIndex == (int)state) { return; }

            switch (state)
            {
                case State.Inactive:
                    break;
                case State.Roaming:
                    DoAnimationClientRpc("run", false);
                    break;
                case State.Hunting:
                    DoAnimationClientRpc("run", true);
                    break;
                case State.Attacking:
                    DoAnimationClientRpc("run", true);
                    break;
                case State.Rampaging:
                    DoAnimationClientRpc("run", true);
                    break;
                default:
                    break;
            }

            SwitchToBehaviourClientRpc((int)state);
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
                case (int)State.Inactive:

                    break;

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
                        SwitchToBehaviorStateCustom(State.Roaming);
                        return;
                    }

                    break;

                case (int)State.Attacking:
                    agent.speed = 6.5f;

                    if (!TargetClosestPlayerInAnyCase() || (Vector3.Distance(transform.position, targetPlayer.transform.position) > distanceToLoseRatKing && !CheckLineOfSightForPosition(targetPlayer.transform.position)))
                    {
                        logger.LogDebug("Stop Targeting");
                        targetPlayer = null;
                        SwitchToBehaviorStateCustom(State.Roaming);
                        return;
                    }

                    SetDestinationToPosition(targetPlayer.transform.position);

                    break;

                case (int)State.Rampaging:
                    agent.speed = 7f;

                    if (TargetClosestPlayerInAnyCase())
                    {
                        StopRoam();
                        SetDestinationToPosition(targetPlayer.transform.position);
                    }

                    StartRoam();

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        public void StartRampage()
        {
            if (currentBehaviourStateIndex == (int)State.Rampaging) { return; }

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (!PlayerIsTargetable(player)) { continue; }
                AddThreat(player, highThreatToAttackPlayer, false);
            }

            SwitchToBehaviorStateCustom(State.Rampaging);
        }

        public void SetInSpecialAnimationFalse()
        {
            inSpecialAnimation = false;
        }

        public void Rally(PlayerControllerB player, bool overrideRally = false)
        {
            if (timeSinceRally < rallyCooldown && !overrideRally) { return; }
            timeSinceRally = 0f;
            inSpecialAnimation = true;
            targetPlayer = player;
            networkAnimator.SetTrigger("rally");
        }

        public void FinishRallyAnim() // Animation function "rally" // TODO: Set this up in unity editor
        {
            inSpecialAnimation = false;

            foreach (var rat in SpawnedRats)
            {
                if (rat.currentRatType == RatType.DefenseRat) { return; }
                if (Vector3.Distance(rat.transform.position, transform.position) > rallyRadius) { continue; }

                rat.targetPlayer = targetPlayer;
                rat.rallyRat = true;
                rat.SwitchToBehaviorState(RatAI.State.Swarming);
            }
        }

        public void AlertHighThreatPlayer(PlayerControllerB player) // Called from ratai
        {
            if (targetPlayer != null) { return; }

            targetPlayer = player;

            if (currentBehaviourStateIndex != (int)State.Rampaging)
            {
                SwitchToBehaviorStateCustom(State.Hunting);
                Rally(targetPlayer, true);
            }
        }

        void GrabBody()
        {
            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                int limb = UnityEngine.Random.Range(0, targetPlayer.deadBody.bodyParts.Length);
                GrabBodyClientRpc(targetPlayer.actualClientId, limb);
            }
        }

        void DropBody(bool deactivate = false)
        {
            if (heldBody != null && IsServerOrHost)
            {
                DropBodyClientRpc(deactivate);
            }
        }

        public void DropBodyOnLocalClient(bool deactivate = false)
        {
            grabbingBody = false;
            targetPlayer = null;

            if (heldBody != null)
            {
                heldBody.attachedTo = null;
                heldBody.attachedLimb = null;
                heldBody.matchPositionExactly = false;
                if (deactivate) { heldBody.DeactivateBody(setActive: false); }
                heldBody = null;
            }
        }

        void HottestRat()
        {
            if (UnityEngine.Random.Range(0f, 1f) < 0.01f)
            {
                ScanNode.headerText = "HottestRat";
            }
        }

        bool FoundClosestPlayerInRange(float range, float senseRange)
        {
            TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: true);
            if (targetPlayer == null)
            {
                // Couldn't see a player, so we check if a player is in sensing distance instead
                TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: false);
                range = senseRange;
            }
            return targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.transform.position) < range;
        }

        bool FoundClosestPlayerInRange(float range)
        {
            TargetClosestPlayer();
            return targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.transform.position) < range;
        }

        bool TargetClosestPlayerInAnyCase()
        {
            mostOptimalDistance = 2000f;
            targetPlayer = null;
            for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
            {
                tempDist = Vector3.Distance(transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
                if (tempDist < mostOptimalDistance)
                {
                    mostOptimalDistance = tempDist;
                    targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
                }
            }

            return targetPlayer != null;
        }

        bool CheckLineOfSightForDeadBody()
        {
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                DeadBodyInfo deadBody = player.deadBody;
                if (deadBody == null || deadBody.deactivated) { continue; }
                path1 = new NavMeshPath();
                if (CheckLineOfSightForPosition(deadBody.grabBodyObject.transform.position, 60, 60, 5) && agent.CalculatePath(RoundManager.Instance.GetNavMeshPosition(deadBody.grabBodyObject.transform.position, RoundManager.Instance.navHit, 1.75f), path1))
                {
                    targetPlayer = deadBody.playerScript;
                    return true;
                }
            }
            return false;
        }

        void CheckForThreatsInLOS()
        {
            if (timeSinceAddThreat > timeToIncreaseThreat)
            {
                if (CheckLineOfSightForDeadBody())
                {
                    StopRoam();
                    grabbingBody = true;
                    return;
                }
                PlayerControllerB player = CheckLineOfSightForPlayer(60f, 60, 5);
                if (PlayerIsTargetable(player))
                {
                    AddThreat(player, 5);
                    return;
                }
            }
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
                    if (!agent.hasPath || timeStuck > 1f)
                    {
                        DoAnimationClientRpc("idle", UnityEngine.Random.Range(1, 3));
                        yield return new WaitForSeconds(4.7f);
                        PlaySqueakSFXClientRpc();
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
        {
            if (!isEnemyDead)
            {
                enemyHP -= force;
                if (enemyHP <= 0)
                {
                    DropBodyOnLocalClient();
                    RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
                    KillEnemyOnOwnerClient();
                    return;
                }
            }
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
            if (currentBehaviourStateIndex == (int)State.Roaming) { return false; }
            if (!playerScript.isPlayerControlled) { return false; }
            if (playerScript.inAnimationWithEnemy != null) { return false; }
            if (playerScript.sinkingValue >= 0.73f) { return false; }
            return true;
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            if (isEnemyDead) { return; }
            if (timeSinceCollision > 1f)
            {
                timeSinceCollision = 0f;
                PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                if (player == null) { return; }

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
            logIfDebug($"{player.playerUsername}: {threat} threat");

            if (!aggro) { return; }

            PlaySqueakSFXClientRpc();

            if (RatManager.PlayerThreatCounter[player] > threatToAttackPlayer || player.isPlayerDead)
            {
                targetPlayer = player;
                SwitchToBehaviorStateCustom(State.Attacking);
            }
        }

        public override void KillEnemy(bool destroy = false)
        {
            base.KillEnemy(false);
        }

        // Animator stuff

        public void FinishEatingBody()
        {
            inSpecialAnimation = false;

        }

        // RPC's

        [ClientRpc]
        public void DoAnimationClientRpc(string animationName, int value)
        {
            creatureAnimator.SetInteger(animationName, value);
        }

        [ClientRpc]
        public void DoAnimationClientRpc(string animationName, bool value)
        {
            creatureAnimator.SetBool(animationName, value);
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

        [ClientRpc]
        public void SetNestClientRpc(NetworkObjectReference nestRef)
        {
            if (nestRef.TryGet(out NetworkObject netObj))
            {
                KingNest = netObj.GetComponent<RatNest>();
            }
        }

        [ClientRpc]
        public void GrabBodyClientRpc(ulong clientId, int attachedLimbIndex)
        {
            targetPlayer = PlayerFromId(clientId);

            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                heldBody = targetPlayer.deadBody;
                heldBody.attachedTo = RatMouth;
                heldBody.attachedLimb = targetPlayer.deadBody.bodyParts[attachedLimbIndex];
                heldBody.matchPositionExactly = true;
            }

            inSpecialAnimation = true;
            grabbingBody = false;
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