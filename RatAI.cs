using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.Utilities;
using static Rats.Plugin;
using static Rats.RatNest;
using static Rats.RatManager;
using Unity.Netcode.Components;
using static UnityEngine.ParticleSystem.PlaybackState;
using UnityEngine.InputSystem.HID;

namespace Rats
{
    public class RatAI : NetworkBehaviour
    {
#pragma warning disable 0649
        public ScanNodeProperties ScanNode;
        public AudioClip[] SqueakSFX;
        public AudioClip[] AttackSFX;
        public AudioClip[] HitSFX;
        public AudioClip[] NibbleSFX;
        public Transform RatMouth;
        public GameObject ChristmasHat;

        public NavMeshAgent agent;
        public Animator creatureAnimator;
        public float AIIntervalTime;
        public Transform eye;
        public int enemyHP;
        public AudioSource creatureSFX;
#pragma warning restore 0649

        public bool IsJermaRat;

        float updateDestinationInterval;
        public bool isEnemyDead;
        bool moveTowardsDestination;
        Vector3 destination;
        //GameObject[] allAINodes;
        bool isOutside;
        Transform? targetNode;
        //int thisEnemyIndex;

        State currentBehaviorState = State.Routine;

        public PlayerControllerB? targetPlayer;
        public EnemyAI? targetEnemy;

        DeadBodyInfo? heldBody;
        bool holdingFood;

        bool grabbingBody;
        bool returningBodyToNest;

        public RatNest Nest;

        float timeSinceCollision;
        float timeSinceAddThreat;

        Vector3 finalDestination;
        private NavMeshPath path1;
        Coroutine? ratCoroutine;
        //bool sickRat;

        bool AtNest { get { return agent.enabled && Nest != null && Vector3.Distance(transform.position, Nest.transform.position) < 1f; } }

        // Constants
        const float timeAgentStopped = 1f;

        // Config Values
        //float sickRatChance = 0.1f;
        float defenseRadius = 5f;
        float timeToIncreaseThreat = 3f;
        int threatToAttackPlayer = 100;
        int threatToAttackEnemy = 50;
        float swarmRadius = 10f;
        bool canVent = true;
        int maxDefenseRats = 10;
        int ratsNeededToAttack = 5;
        float distanceToLoseRats = 25f;
        int enemyHitsToDoDamage = 10;
        int playerFoodAmount = 30;
        int ratDamage = 2;

        RatType currentRatType = RatType.Unassigned;
        private bool walking;
        System.Random random;

        public enum RatType
        {
            Unassigned,
            ScoutRat,
            DefenseRat
        }

        public enum State
        {
            ReturnToNest,
            Routine,
            Swarming
        }

        public void StopTaskRoutine()
        {
            if (ratCoroutine != null)
            {
                StopCoroutine(ratCoroutine);
                ratCoroutine = null;
            }
        }

        public void Start()
        {
            AIIntervalTime = configAIIntervalTime.Value;
            defenseRadius = configDefenseRadius.Value;
            timeToIncreaseThreat = configTimeToIncreaseThreat.Value;
            threatToAttackPlayer = configThreatToAttackPlayer.Value;
            threatToAttackEnemy = configThreatToAttackEnemy.Value;
            swarmRadius = configSwarmRadius.Value;
            canVent = configCanVent.Value;
            maxDefenseRats = configMaxDefenseRats.Value;
            ratsNeededToAttack = configRatsNeededToAttack.Value;
            distanceToLoseRats = configDistanceNeededToLoseRats.Value;
            enemyHitsToDoDamage = configEnemyHitsToDoDamage.Value;
            playerFoodAmount = configPlayerFoodAmount.Value;
            ratDamage = configRatDamage.Value;
            ChristmasHat.SetActive(configHolidayRats.Value);

            updateDestinationInterval = AIIntervalTime;
            path1 = new NavMeshPath();

            currentBehaviorState = State.ReturnToNest;
            Nest = GetClosestNest();
            HottestRat();
            RatManager.SpawnedRats.Add(this);
            random = new System.Random(StartOfRound.Instance.randomMapSeed * RatManager.SpawnedRats.Count);

            if (IsServerOrHost)
            {
                SwitchToBehaviorState(State.Routine);
            }

            logIfDebug($"Rat spawned");
        }

        public void Update()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;

            bool _walking = agent.velocity.sqrMagnitude >= 0.01f; // TODO: Check if this causes errors on clients

            if (walking != _walking)
            {
                creatureAnimator.SetInteger("idle", _walking ? 0 : random.Next(1, 3)); // TODO: Test this
                walking = _walking;
            }

            if (IsServerOrHost) // TODO: Remove this when adding rat king
            {
                if (updateDestinationInterval >= 0f)
                {
                    updateDestinationInterval -= Time.deltaTime;
                }
                else
                {
                    DoAIInterval();
                    updateDestinationInterval = AIIntervalTime + UnityEngine.Random.Range(-0.015f, 0.015f);
                }
            }
        }

        public void SwitchToBehaviorState(State state)
        {
            if (currentBehaviorState == state) { return; }

            StopTaskRoutine();

            switch (state)
            {
                case State.ReturnToNest:
                    
                    ResetRatType();
                    Nest = GetClosestNest();

                    break;
                case State.Routine:

                    // Add food to nest
                    if (holdingFood)
                    {
                        Nest.AddFood();
                    }
                    holdingFood = false;

                    if (Nest.DefenseRats.Count < maxDefenseRats) // defense
                    {
                        AssignRatType(RatType.DefenseRat);
                        break;
                    }

                    AssignRatType(RatType.ScoutRat); // scouting
                    break;

                case State.Swarming:

                    if (targetPlayer != null)
                    {
                        ratCoroutine = StartCoroutine(SwarmCoroutine(targetPlayer, swarmRadius));
                    }
                    else if (targetEnemy != null)
                    {
                        ratCoroutine = StartCoroutine(SwarmCoroutine(targetEnemy, swarmRadius));
                    }
                    else
                    {
                        LoggerInstance.LogError("No target to attack");
                    }

                    break;
                default:
                    break;
            }

            SwitchToBehaviorClientRpc(state);
        }

        public void ResetRatType()
        {
            currentRatType = RatType.Unassigned;

            foreach (var nest in RatNest.Nests)
            {
                nest.DefenseRats.Remove(this);
            }
        }

        public void DoAIInterval()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }

            switch (currentBehaviorState)
            {
                case (int)State.ReturnToNest:
                    agent.speed = 5f;

                    if (AtNest)
                    {
                        if (returningBodyToNest)
                        {
                            DropBody(true);
                            Nest.AddFood(playerFoodAmount);
                        }
                        SwitchToBehaviorState(State.Routine);
                        return;
                    }

                    if (Nest == null || !Nest.IsOpen)
                    {
                        Nest = GetClosestNest();
                        if (Nest == null) { LoggerInstance.LogError("Cant find a nest! This should not happen!"); return; } // TODO: Do rat spin
                    }

                    if (SetDestinationToPosition(Nest.transform.position))
                    {
                        StopTaskRoutine();
                        SetStatus("Returning to nest");
                    }

                    break;
                case State.Routine:
                    agent.speed = 5f;

                    if (grabbingBody)
                    {
                        GrabbableObject deadBody = targetPlayer.deadBody.grabBodyObject;
                        if (SetDestinationToPosition(deadBody.transform.position, true))
                        {
                            if (Vector3.Distance(transform.position, deadBody.transform.position) < 1f)
                            {
                                GrabBody();
                                returningBodyToNest = true;
                                SwitchToBehaviorState(State.ReturnToNest);
                            }
                            return;
                        }
                    }

                    CheckForThreatsInLOS();

                    return;
                case State.Swarming:
                    agent.speed = 10f;

                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats)
                        {
                            // Target too far, return to nest
                            targetPlayer = null;
                            SwitchToBehaviorState(State.ReturnToNest);
                            return;
                        }

                        return;
                    }
                    if (targetEnemy != null)
                    {
                        if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats * 2)
                        {
                            // Target too far, return to nest
                            targetEnemy = null;
                            SwitchToBehaviorState(State.ReturnToNest);
                            return;
                        }

                        return;
                    }

                    SwitchToBehaviorState(State.ReturnToNest);

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviorState);
                    break;
            }
        }

        void AssignRatType(RatType ratType)
        {
            currentRatType = ratType;

            switch (ratType)
            {
                case RatType.Unassigned:
                    break;

                case RatType.ScoutRat:
                    logIfDebug("Rat scout assigned");
                    ratCoroutine = StartCoroutine(ScoutCoroutine());
                    SetStatus("Scouting");
                    return;

                case RatType.DefenseRat:
                    logIfDebug("Rat defense assigned");

                    Nest.DefenseRats.Add(this);
                    ratCoroutine = StartCoroutine(SwarmCoroutine(Nest.transform.position, defenseRadius));
                    SetStatus("Defending");
                    return;

                default:
                    break;
            }
        }

        RatNest GetClosestNest()
        {
            float closestDistance = 4000f;
            RatNest? closestNest = RatKingAI.Instance.KingNest;

            foreach (var nest in Nests)
            {
                if (!nest.IsOpen) { continue; }
                float distance = Vector3.Distance(transform.position, nest.transform.position);
                if (distance >= closestDistance) { continue; }
                closestDistance = distance;
                closestNest = nest;
            }

            return closestNest;
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
            if (heldBody != null)
            {
                DropBodyClientRpc(deactivate);
            }
        }

        public bool SetDestinationToPosition(Vector3 position, bool checkForPath = false)
        {
            if (checkForPath)
            {
                position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
                path1 = new NavMeshPath();
                if (!agent.CalculatePath(position, path1))
                {
                    return false;
                }
                if (Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.55f)
                {
                    return false;
                }
            }
            moveTowardsDestination = true;
            destination = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, -1f);
            return true;
        }

        void HottestRat()
        {
            if (UnityEngine.Random.Range(0f, 1f) < 0.01f)
            {
                ScanNode.headerText = "HottestRat";
            }
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
                    Nest = GetClosestNest();
                    StopTaskRoutine();
                    grabbingBody = true;
                    return;
                }
                PlayerControllerB player = CheckLineOfSightForPlayer(60f, 60, 5);
                if (PlayerIsTargetable(player))
                {
                    AddThreat(player);
                    return;
                }
                EnemyAI? enemy = CheckLineOfSightForEnemy(30);
                if (enemy != null && enemy.enemyType.canDie)
                {
                    AddThreat(enemy);
                }
            }
        }

        public bool CheckLineOfSightForPosition(Vector3 objectPosition, float width = 45f, int range = 60, float proximityAwareness = -1f, Transform overrideEye = null)
        {
            if (!isOutside)
            {
                if (objectPosition.y > -80f)
                {
                    return false;
                }
            }
            else if (objectPosition.y < -100f)
            {
                return false;
            }
            Transform transform = ((overrideEye != null) ? overrideEye : ((!(eye == null)) ? eye : base.transform));
            if (Vector3.Distance(transform.position, objectPosition) < (float)range && !Physics.Linecast(transform.position, objectPosition, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                Vector3 to = objectPosition - transform.position;
                if (Vector3.Angle(transform.forward, to) < width || Vector3.Distance(base.transform.position, objectPosition) < proximityAwareness)
                {
                    return true;
                }
            }
            return false;
        }

        public PlayerControllerB CheckLineOfSightForPlayer(float width = 45f, int range = 60, int proximityAwareness = -1)
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

        public EnemyAI? CheckLineOfSightForEnemy(int range)
        {
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if ((RatKingAI.Instance != null && enemy == RatKingAI.Instance) || !enemy.enemyType.canDie) { continue; }
                if (CheckLineOfSightForPosition(enemy.transform.position, 60, range, 5))
                {
                    return enemy;
                }
            }
            return null;
        }

        void WarpToVent(EnemyVent fromVent, EnemyVent toVent)
        {
            OpenVents(fromVent, toVent);
            Vector3 warpPos = RoundManager.Instance.GetNavMeshPosition(toVent!.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
            transform.position = warpPos;
            agent.Warp(warpPos);
        }

        void WarpToVent(EnemyVent toVent)
        {
            OpenVents(toVent);
            Vector3 warpPos = RoundManager.Instance.GetNavMeshPosition(toVent!.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
            transform.position = warpPos;
            agent.Warp(warpPos);
        }

        void Warp(Vector3 pos)
        {
            pos = RoundManager.Instance.GetNavMeshPosition(pos, RoundManager.Instance.navHit, 1.75f);
            transform.position = pos;
            agent.Warp(pos);
        }

        int GetVentIndex(EnemyVent? vent)
        {
            for (int i = 0; i < RoundManager.Instance.allEnemyVents.Length; i++)
            {
                if (RoundManager.Instance.allEnemyVents[i] == vent)
                {
                    return i;
                }
            }
            return -1;
        }

        void OpenVents(EnemyVent? vent, EnemyVent? vent2 = null)
        {
            if (vent == null) { return; }
            int index = -1;
            int index2 = -1;

            if (!vent.ventIsOpen)
            {
                index = GetVentIndex(vent);
            }

            if (vent2 != null && !vent2.ventIsOpen)
            {
                index2 = GetVentIndex(vent2);
            }

            if (index == -1 && index2 == -1) { return; }

            OpenVentsClientRpc(index, index2);
        }

        void SetStatus(string status)
        {
            if (configEnableDebugging.Value)
            {
                ScanNode.subText = status;
            }
        }

        IEnumerator SwarmCoroutine(PlayerControllerB player, float radius)
        {
            yield return null;
            logIfDebug("Starting swarm on player");
            while (ratCoroutine != null && player != null)
            {
                if (player == null || player.isPlayerDead) { break; }
                float timeStuck = 0f;
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(position, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.ReturnToNest);
        }

        IEnumerator SwarmCoroutine(EnemyAI enemy, float radius)
        {
            yield return null;
            while (ratCoroutine != null && enemy != null)
            {
                float timeStuck = 0f;
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(enemy.transform.position, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(position, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.ReturnToNest);
        }

        IEnumerator SwarmCoroutine(Vector3 position, float radius)
        {
            yield return null;
            while (ratCoroutine != null)
            {
                //logIfDebug("Choosing new position");
                float timeStuck = 0f;
                Vector3 pos = position;
                pos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(pos, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.ReturnToNest);
        }

        IEnumerator ScoutCoroutine()
        {
            yield return null;
            while (ratCoroutine != null && agent.enabled)
            {
                float timeStuck = 0f;
                targetNode = GetRandomNode(isOutside);
                if (targetNode == null) { continue; }
                Vector3 position = targetNode.transform.position;
                if (!SetDestinationToPosition(position)) { continue; }
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        SwitchToBehaviorState(State.ReturnToNest);
                        ratCoroutine = null;
                        yield break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.ReturnToNest);
        }

        public void HitEnemy(int force = 0, PlayerControllerB? playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
        {
            if (!isEnemyDead)
            {
                enemyHP -= force;
                if (enemyHP <= 0)
                {
                    DropBody();
                    RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
                    KillEnemyOnOwnerClient();
                    return;
                }
            }
        }

        public void HitEnemyOnLocalClient(int force = 1, Vector3 hitDirection = default(Vector3), PlayerControllerB? playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            int playerWhoHit2 = -1;
            if (playerWhoHit != null)
            {
                playerWhoHit2 = (int)playerWhoHit.playerClientId;
                HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            }
            HitEnemyServerRpc(force, playerWhoHit2, playHitSFX, hitID);
        }

        public void HitFromExplosion(float distance)
        {
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            KillEnemyOnOwnerClient();
        }

        public bool PlayerIsTargetable(PlayerControllerB playerScript)
        {
            if (playerScript == null) { return false; }
            if (playerScript.isPlayerDead) { return false; }
            if (currentBehaviorState == (int)State.ReturnToNest) { return false; }
            if (!playerScript.isPlayerControlled) { return false; }
            if (playerScript.inAnimationWithEnemy != null) { return false; }
            if (playerScript.sinkingValue >= 0.73f) { return false; }
            return true;
        }

        public bool IsPlayerNearANest(PlayerControllerB player)
        {
            foreach (RatNest nest in Nests)
            {
                if (Vector3.Distance(player.transform.position, nest.transform.position) < defenseRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public PlayerControllerB? MeetsStandardPlayerCollisionConditions(Collider other)
        {
            if (isEnemyDead)
            {
                return null;
            }
            /*if (stunNormalizedTimer >= 0f)
            {
                return null;
            }*/
            PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
            if (component == null || component != GameNetworkManager.Instance.localPlayerController)
            {
                return null;
            }
            if (!PlayerIsTargetable(component))
            {
                Debug.Log("Player is not targetable");
                return null;
            }
            return component;
        }

        public void OnCollideWithPlayer(Collider other)
        {
            if (isEnemyDead) { return; }
            if (timeSinceCollision > 1f)
            {
                timeSinceCollision = 0f;
                PlayerControllerB? player = MeetsStandardPlayerCollisionConditions(other);
                if (player == null) { return; }
                if (currentBehaviorState == State.Swarming || IsPlayerNearANest(player))
                {
                    int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                    player.DamagePlayer(ratDamage, true, true, CauseOfDeath.Mauling, deathAnim);
                    PlayAttackSFXServerRpc();
                }
            }
        }

        public void OnCollideWithEnemy(Collider other, EnemyAI? collidedEnemy = null)
        {
            if (IsServerOrHost)
            {
                if (isEnemyDead || currentBehaviorState == State.ReturnToNest || Nest == null) { return; }
                if (timeSinceCollision > 1f)
                {
                    if (collidedEnemy != null && collidedEnemy.enemyType.canDie/* && !(stunNormalizedTimer > 0f)*/)
                    {
                        if (RatKingAI.Instance != null && RatKingAI.Instance == collidedEnemy) { return; }
                        logIfDebug("Collided with: " + collidedEnemy.enemyType.enemyName);
                        timeSinceCollision = 0f;

                        if (collidedEnemy.isEnemyDead)
                        {
                            if (RatNest.EnemyFoodAmount.ContainsKey(collidedEnemy))
                            {
                                if (RatNest.EnemyFoodAmount[collidedEnemy] <= 1)
                                {
                                    holdingFood = RatNest.EnemyFoodAmount[collidedEnemy] == 1;
                                    RoundManager.Instance.DespawnEnemyOnServer(collidedEnemy.NetworkObject);
                                    return;
                                }
                                else
                                {
                                    RatNest.EnemyFoodAmount[collidedEnemy] -= 1;
                                    holdingFood = true;
                                }
                            }
                            else
                            {
                                RatManager.EnemyHitCount.Remove(collidedEnemy);
                                Nest.AddEnemyFoodAmount(collidedEnemy);
                                RatNest.EnemyFoodAmount[collidedEnemy] -= 1;
                                holdingFood = true;
                            }

                            SwitchToBehaviorState(State.ReturnToNest);
                        }
                        else
                        {
                            if (!RatManager.EnemyHitCount.ContainsKey(collidedEnemy))
                            {
                                RatManager.EnemyHitCount.Add(collidedEnemy, enemyHitsToDoDamage);
                            }

                            RatManager.EnemyHitCount[collidedEnemy] -= 1;

                            if (RatManager.EnemyHitCount[collidedEnemy] <= 0)
                            {
                                collidedEnemy.HitEnemy(1, null, true);
                                RatManager.EnemyHitCount[collidedEnemy] = enemyHitsToDoDamage;
                            }
                        }
                    }
                }
            }

            if (holdingFood)
            {
                RoundManager.PlayRandomClip(creatureSFX, NibbleSFX, true, 1f, -1);
                if (collidedEnemy != null && localPlayer.HasLineOfSightToPosition(collidedEnemy.transform.position))
                {
                    localPlayer.IncreaseFearLevelOverTime(0.8f);
                }
            }
        }

        void AddThreat(EnemyAI enemy, int amount = 1)
        {
            if (enemy == null) { return; }

            if (Nest != null)
            {
                timeSinceAddThreat = 0f;

                if (RatManager.EnemyThreatCounter.ContainsKey(enemy))
                {
                    RatManager.EnemyThreatCounter[enemy] += amount;
                }
                else
                {
                    RatManager.EnemyThreatCounter.Add(enemy, amount);
                }

                PlaySqueakSFXClientRpc();

                int threat = RatManager.EnemyThreatCounter[enemy];
                logIfDebug($"{enemy.enemyType.enemyName}: {threat} threat");

                if (currentRatType == RatType.DefenseRat) { return; }

                if (RatManager.EnemyThreatCounter[enemy] > threatToAttackEnemy || enemy.isEnemyDead)
                {
                    SetTarget(enemy);
                    SwitchToBehaviorState(State.Swarming);
                }
            }
        }

        void AddThreat(PlayerControllerB player, int amount = 1)
        {
            if (player == null) { return; }

            if (Nest != null)
            {
                timeSinceAddThreat = 0f;

                if (RatManager.PlayerThreatCounter.ContainsKey(player))
                {
                    RatManager.PlayerThreatCounter[player] += amount;
                }
                else
                {
                    RatManager.PlayerThreatCounter.Add(player, amount);
                }

                PlaySqueakSFXClientRpc();

                int threat = RatManager.PlayerThreatCounter[player];
                logIfDebug($"{player.playerUsername}: {threat} threat");

                if (currentRatType == RatType.DefenseRat) { return; }

                if (RatManager.PlayerThreatCounter[player] > threatToAttackPlayer || player.isPlayerDead)
                {
                    SetTarget(player);
                    SwitchToBehaviorState(State.Swarming);
                }
            }
        }

        public void SetTarget(PlayerControllerB player)
        {
            targetEnemy = null;
            targetPlayer = player;
        }

        public void SetTarget(EnemyAI enemy)
        {
            targetPlayer = null;
            targetEnemy = enemy;
        }

        /*public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB? setStunnedByPlayer = null)
        {
            if (isEnemyDead)
            {
                return;
            }
            if (setToStunned)
            {
                if (!(postStunInvincibilityTimer >= 0f))
                {
                    stunnedByPlayer = setStunnedByPlayer;
                    postStunInvincibilityTimer = 0.5f;
                    stunNormalizedTimer = 5;
                }
            }
            else
            {
                stunnedByPlayer = null;
                if (stunNormalizedTimer > 0f)
                {
                    stunNormalizedTimer = 0f;
                }
            }
        }*/

        public void KillEnemyOnOwnerClient(bool overrideDestroy = false)
        {
            if (!base.IsOwner)
            {
                return;
            }
            if (isEnemyDead)
            {
                return;
            }
            Debug.Log($"Kill enemy called! destroy: {overrideDestroy}");
            if (overrideDestroy)
            {
                if (base.IsServer)
                {
                    Debug.Log("Kill enemy called on server, destroy true");
                    KillEnemy(destroy: true);
                }
                else
                {
                    KillEnemyServerRpc(destroy: true);
                }
            }
            else
            {
                KillEnemy();
                if (base.NetworkObject.IsSpawned)
                {
                    KillEnemyServerRpc(destroy: false);
                }
            }
        }

        public void KillEnemy(bool destroy = false)
        {
            if (destroy)
            {
                Debug.Log("Destroy enemy called");
                if (base.IsServer)
                {
                    Debug.Log("Despawn network object in kill enemy called!");
                    if (NetworkObject.IsSpawned)
                    {
                        NetworkObject.Despawn();
                    }
                }
            }
            else
            {
                if (ScanNode != null && (bool)ScanNode.gameObject.GetComponent<Collider>())
                {
                    ScanNode.gameObject.GetComponent<Collider>().enabled = false;
                }
                isEnemyDead = true;
                try
                {
                    if (creatureAnimator != null)
                    {
                        creatureAnimator.SetBool("stunned", value: false);
                        creatureAnimator.SetTrigger("KillEnemy");
                    }
                }
                catch (Exception arg)
                {
                    Debug.LogError($"enemy did not have bool in animator in KillEnemy, error returned; {arg}");
                }

                StopAllCoroutines();
                agent.enabled = false;
                RatManager.SpawnedRats.Remove(this);
                ResetRatType();
            }
        }

        public override void OnDestroy()
        {
            agent.enabled = false;
            StopAllCoroutines();

            if (NetworkObject != null && NetworkObject.IsSpawned && IsSpawned)
            {
                NetworkObject.OnNetworkBehaviourDestroyed(this);
            }
            if (!m_VarInit)
            {
                InitializeVariables();
            }
            for (int i = 0; i < NetworkVariableFields.Count; i++)
            {
                NetworkVariableFields[i].Dispose();
            }
        }

        // RPC's

        [ServerRpc(RequireOwnership = false)]
        public void HitEnemyServerRpc(int force, int playerWhoHit, bool playHitSFX, int hitID = -1)
        {
            if (!IsServerOrHost) { return; }

            HitEnemyClientRpc(force, playerWhoHit, playHitSFX, hitID);
        }

        [ClientRpc]
        public void HitEnemyClientRpc(int force, int playerWhoHit, bool playHitSFX, int hitID = -1)
        {
            if (playerWhoHit == -1)
            {
                HitEnemy(force, null, playHitSFX, hitID);
            }
            else
            {
                HitEnemy(force, StartOfRound.Instance.allPlayerScripts[playerWhoHit], playHitSFX, hitID);
            }
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
        public void SwitchToBehaviorClientRpc(State state)
        {
            if (currentBehaviorState != state)
            {
                currentBehaviorState = state;
                RoundManager.PlayRandomClip(creatureSFX, SqueakSFX, true, 1, -1);
            }
        }

        [ClientRpc]
        public void PlaySqueakSFXClientRpc(bool longSqueak = false)
        {
            if (longSqueak)
            {
                RoundManager.PlayRandomClip(creatureSFX, SqueakSFX, true, 1, -1);
                return;
            }
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
        }

        [ClientRpc]
        public void OpenVentsClientRpc(int ventIndex, int ventIndex2 = -1)
        {
            if (ventIndex != -1)
            {
                EnemyVent vent = RoundManager.Instance.allEnemyVents[ventIndex];
                if (vent == null)
                {
                    RoundManager.Instance.RefreshEnemyVents();
                    vent = RoundManager.Instance.allEnemyVents[ventIndex];
                    if (vent == null)
                    {
                        LoggerInstance.LogError("Cant get vent to open on client");
                        return;
                    }
                }
                vent.ventIsOpen = true;
                vent.ventAnimator.SetTrigger("openVent");
                vent.lowPassFilter.lowpassResonanceQ = 0f;
            }
            if (ventIndex2 != -1)
            {
                EnemyVent vent = RoundManager.Instance.allEnemyVents[ventIndex2];
                if (vent == null)
                {
                    RoundManager.Instance.RefreshEnemyVents();
                    vent = RoundManager.Instance.allEnemyVents[ventIndex2];
                    if (vent == null)
                    {
                        LoggerInstance.LogError("Cant get vent to open on client");
                        return;
                    }
                }
                vent.ventIsOpen = true;
                vent.ventAnimator.SetTrigger("openVent");
                vent.lowPassFilter.lowpassResonanceQ = 0f;
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

            grabbingBody = false;
            returningBodyToNest = true;
            RoundManager.PlayRandomClip(creatureSFX, NibbleSFX, true, 1f, -1);
        }

        [ClientRpc]
        public void DropBodyClientRpc(bool deactivate)
        {
            returningBodyToNest = false;
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

        [ServerRpc(RequireOwnership = false)]
        public void KillEnemyServerRpc(bool destroy)
        {
            if (!IsServerOrHost) { return; }

            if (destroy)
            {
                KillEnemy(destroy);
            }
            else
            {
                KillEnemyClientRpc(destroy);
            }
        }

        [ClientRpc]
        public void KillEnemyClientRpc(bool destroy)
        {
            if (!isEnemyDead)
            {
                KillEnemy(destroy);
            }
        }
    }
}

// TODO: statuses: shakecamera, playerstun, drunkness, fear, insanity