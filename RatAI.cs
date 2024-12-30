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
using static Rats.SewerGrate;
using static Rats.RatManager;
using static UnityEngine.VFX.VisualEffectControlTrackController;

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

        float updateDestinationInterval;
        public bool isEnemyDead;
        bool moveTowardsDestination;
        Vector3 destination;
        GameObject[] allAINodes;
        bool isOutside;
        Transform? targetNode;
        //int thisEnemyIndex;

        State currentBehaviorState = State.Routine;

        public PlayerControllerB? targetPlayer;
        public EnemyAI? targetEnemy;

        DeadBodyInfo? heldBody;
        bool holdingFood;

        public SewerGrate? Nest;

        float timeSinceCollision;
        float timeSinceAddThreat;

        Vector3 finalDestination;
        private NavMeshPath path1;
        Coroutine? ratCoroutine;
        //bool sickRat;
        bool grabbingBody;
        bool returningBodyToNest;

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

        RatType RatTypeState = RatType.Unassigned;

        public enum RatType
        {
            Unassigned,
            ScoutRat,
            DefenseRat,
            GuardRat
        }

        public enum State
        {
            Default,
            Routine,
            Swarming
        }

        public void SwitchToBehaviorState(State state)
        {
            if (currentBehaviorState == state) { return; }

            StopTaskRoutine();

            switch (state)
            {
                case State.Default:
                    switch (RatTypeState)
                    {
                        case RatType.Unassigned:
                            break;
                        case RatType.ScoutRat:
                            break;
                        case RatType.DefenseRat:
                            if (Nest != null)
                            {
                                Nest.DefenseRatCount--;
                            }
                            break;
                        case RatType.GuardRat:
                            if (RatKingAI.Instance != null)
                            {
                                RatKingAI.Instance.RatGuardCount--;
                            }
                            break;
                        default:
                            break;
                    }

                    break;
                case State.Routine:

                    Nest = GetClosestNest(); // TODO: May be unneeded?

                    if (Nest == null)
                    {
                        if (RatKingAI.Instance == null)
                        {
                            AssignRatType(RatType.Unassigned); // lost
                            break;
                        }

                        AssignRatType(RatType.GuardRat); // guarding rat king
                        break;
                    }

                    // Add food to nest
                    if (holdingFood)
                    {
                        holdingFood = false;
                        Nest.AddFood();
                    }

                    if (Nest.DefenseRatCount < maxDefenseRats) // defense
                    {
                        AssignRatType(RatType.DefenseRat);
                        break;
                    }

                    AssignRatType(RatType.ScoutRat); // scouting
                    break;

                case State.Swarming:

                    if (targetPlayer != null)
                    {
                        ratCoroutine = StartCoroutine(SwarmCoroutine(targetPlayer, 2f));
                    }
                    else if (targetEnemy != null)
                    {
                        ratCoroutine = StartCoroutine(SwarmCoroutine(targetEnemy, 2f));
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
            RoundManager.Instance.numberOfEnemiesInScene++;
            allAINodes = RoundManager.Instance.insideAINodes;
            path1 = new NavMeshPath();

            currentBehaviorState = State.Default;
            Nest = GetClosestNest();
            HottestRat();
            RatManager.SpawnedRats.Add(this);

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
                case (int)State.Default:
                    agent.speed = 5f;

                    Nest = GetClosestNest();

                    if (Nest == null)
                    {
                        SwitchToBehaviorState(State.Routine) // TODO: Continue here
                    }

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

                    if (SetDestinationToPosition(Nest.transform.position))
                    {
                        StopRoutines();
                        SetStatus("Returning to nest");
                    }
                    else
                    {
                        if (returningBodyToNest)
                        {
                            DropBody();
                        }
                        Roam();
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
                                SwitchToBehaviorState(State.Default);
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
                            SwitchToBehaviorState(State.Default);
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
                            SwitchToBehaviorState(State.Default);
                            return;
                        }

                        return;
                    }

                    SwitchToBehaviorState(State.Default);

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviorState);
                    break;
            }
        }

        SewerGrate? GetClosestNest()
        {
            float closestDistance = 4000f;
            SewerGrate? closestNest = null;

            foreach (var nest in Nests)
            {
                if (!nest.open) { continue; }
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
                    StopRoutines();
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

        void AssignRatType(RatType ratType)
        {
            RatTypeState = ratType;

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
                    Nest.DefenseRatCount += 1;

                    ratCoroutine = StartCoroutine(SwarmCoroutine(Nest.transform.position, defenseRadius));
                    SetStatus("Defending");
                    return;

                case RatType.GuardRat:
                    logIfDebug("Rat guard assigned");
                    RatKingAI.Instance.RatGuardCount++;

                    ratCoroutine = StartCoroutine(SwarmCoroutine(RatKingAI.Instance, swarmRadius));
                    SetStatus("Defending");
                    break;

                default:
                    break;
            }
        }

        EnemyVent? GetClosestVentToPosition(Vector3 pos)
        {
            float mostOptimalDistance = 2000f;
            EnemyVent? targetVent = null;
            foreach (var vent in RoundManager.Instance.allEnemyVents)
            {
                float distance = Vector3.Distance(pos, vent.floorNode.transform.position);

                if (agent.enabled && RatManager.CalculatePath(pos, vent.floorNode.transform.position) && distance < mostOptimalDistance)
                {
                    mostOptimalDistance = distance;
                    targetVent = vent;
                }
            }

            return targetVent;
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

        void Roam()
        {
            if (ratCoroutine != null) { return; }
            SetStatus("Lost");
            ratCoroutine = StartCoroutine(RoamCoroutine());
        }

        IEnumerator RoamCoroutine()
        {
            yield return null;
            while (ratCoroutine != null)
            {
                float timeStuck = 0f;
                TargetRandomNode();
                if (targetNode == null) { continue; }
                Vector3 position = RoundManager.Instance.GetNavMeshPosition(targetNode.position, RoundManager.Instance.navHit, 1.75f, agent.areaMask);
                if (!SetDestinationToPosition(position)) { continue; }
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

            SwitchToBehaviorState(State.Default);
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

            SwitchToBehaviorState(State.Default);
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

            SwitchToBehaviorState(State.Default);
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

            SwitchToBehaviorState(State.Default);
        }

        IEnumerator ScoutCoroutine()
        {
            yield return null;
            while (ratCoroutine != null && agent.enabled)
            {
                float timeStuck = 0f;
                TargetRandomNode();
                if (targetNode == null) { continue; }
                Vector3 position = targetNode.transform.position;
                if (!SetDestinationToPosition(position)) { continue; }
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStuck > timeAgentStopped)
                    {
                        SwitchToBehaviorState(State.Default);
                        ratCoroutine = null;
                        yield break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.Default);
        }

        void TargetRandomNode()
        {
            try
            {
                //logIfDebug("Choosing new target node...");

                //GameObject[] nodes = !isOutside ? GameObject.FindGameObjectsWithTag("AINode") : GameObject.FindGameObjectsWithTag("OutsideAINode");
                GameObject[] nodes = allAINodes;

                int randIndex = UnityEngine.Random.Range(0, nodes.Length);
                targetNode = nodes[randIndex].transform;
            }
            catch
            {
                targetNode = null;
                return;
            }
        }

        public void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
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

        public void StopRoutines()
        {
            StopAllCoroutines();
            ratCoroutine = null;
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
            if (currentBehaviorState == (int)State.Default) { return false; }
            if (!playerScript.isPlayerControlled) { return false; }
            if (playerScript.inAnimationWithEnemy != null) { return false; }
            if (playerScript.sinkingValue >= 0.73f) { return false; }
            return true;
        }

        public bool IsPlayerNearANest(PlayerControllerB player)
        {
            foreach (SewerGrate nest in Nests)
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
                if (isEnemyDead || currentBehaviorState == State.Default || Nest == null) { return; }
                if (timeSinceCollision > 1f)
                {
                    if (collidedEnemy != null && collidedEnemy.enemyType.canDie/* && !(stunNormalizedTimer > 0f)*/)
                    {
                        if (RatKingAI.Instance != null && RatKingAI.Instance == collidedEnemy) { return; }
                        logIfDebug("Collided with: " + collidedEnemy.enemyType.enemyName);
                        timeSinceCollision = 0f;

                        if (collidedEnemy.isEnemyDead)
                        {
                            if (SewerGrate.EnemyFoodAmount.ContainsKey(collidedEnemy))
                            {
                                if (SewerGrate.EnemyFoodAmount[collidedEnemy] <= 1)
                                {
                                    holdingFood = SewerGrate.EnemyFoodAmount[collidedEnemy] == 1;
                                    RoundManager.Instance.DespawnEnemyOnServer(collidedEnemy.NetworkObject);
                                    return;
                                }
                                else
                                {
                                    SewerGrate.EnemyFoodAmount[collidedEnemy] -= 1;
                                    holdingFood = true;
                                }
                            }
                            else
                            {
                                RatManager.EnemyHitCount.Remove(collidedEnemy);
                                Nest.AddEnemyFoodAmount(collidedEnemy);
                                SewerGrate.EnemyFoodAmount[collidedEnemy] -= 1;
                                holdingFood = true;
                            }

                            SwitchToBehaviorState(State.Default);
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

                if (RatTypeState == RatType.DefenseRat) { return; }

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

                if (RatTypeState == RatType.DefenseRat) { return; }

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
                if (RatTypeState == RatType.DefenseRat) { Nest.DefenseRatCount -= 1; }
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