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
using UnityEngine.Rendering.HighDefinition;
using static Rats.Plugin;
using static Rats.RatManager;

// Cheese to lure the rats away from the grate so the player can block it
// Rats will scout and do a normal search, if they see a player after 20 seconds, they will add a counter to the threatcounter, when max threat, they will run back to sewer and gather rats they touch and get defending rats and swarm around the player for 20 seconds, any rats nearby will also swarm. then attack.

namespace Rats
{
    public class RatAI : MonoBehaviour
    {
#pragma warning disable 0649
        public ScanNodeProperties ScanNode = null!;
        public AudioClip[] SqueakSFX = null!;
        public AudioClip[] AttackSFX = null!;
        public AudioClip[] HitSFX = null!;
        public AudioClip[] NibbleSFX = null!;
        public Transform TurnCompass = null!;
        public Transform RatMouth = null!;
        public GameObject ChristmasHat = null!;

        public NavMeshAgent agent = null!;
        public Animator creatureAnimator = null!;
        public float AIIntervalTime;
        public Transform eye = null!;
        public int enemyHP;
        public AudioSource creatureSFX = null!;
#pragma warning restore 0649

        //float updateDestinationInterval;
        System.Random random;
        public bool isEnemyDead;
        bool moveTowardsDestination;
        Vector3 destination;
        GameObject[] allAINodes;
        bool isOutside;
        Transform? targetNode;
        int thisEnemyIndex;

        State currentBehaviorState = State.Tasking;

        public PlayerControllerB? targetPlayer = null;
        public EnemyAI? targetEnemy = null!;

        public SewerGrate? MainNest;
        DeadBodyInfo? heldBody;
        bool holdingFood;

        EnemyVent? FromVent
        {
            get
            {
                return RatManager.GetClosestVentToPosition(transform.position);
            }
        }

        EnemyVent? ToVent
        {
            get
            {
                return RatManager.GetClosestVentToPosition(finalDestination);
            }
        }

        float timeSinceCollision;
        float timeSinceAddThreat;

        Vector3 finalDestination;
        private NavMeshPath path1;
        Coroutine? ratCoroutine = null;
        //bool sickRat;
        bool grabbingBody;
        bool returningBodyToNest;

        bool AtNest { get { return agent.enabled && MainNest != null && Vector3.Distance(transform.position, MainNest.transform.position) < 1f; } }

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
        bool makeLessSqueaks = false;

        RatType RatTypeState = RatType.Unassigned;

        public enum RatType
        {
            Unassigned,
            ScoutRat,
            DefenseRat
        }

        public enum State
        {
            Roaming,
            Tasking,
            Swarming,
            Attacking
        }

        public void StopTaskRoutine()
        {
            if (ratCoroutine != null)
            {
                StopCoroutine(ratCoroutine);
                ratCoroutine = null;
            }
        }

        public void SwitchToBehaviorState(State state)
        {
            if (currentBehaviorState == state) { return; }

            StopTaskRoutine();

            switch (state)
            {
                case State.Roaming:
                    AssignRatType();

                    break;
                case State.Tasking:
                    AssignRatType();

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
                        LoggerInstance.LogError("No target to swarm");
                    }

                    break;
                case State.Attacking:
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

            RoundManager.PlayRandomClip(creatureSFX, SqueakSFX, true, 1, -1);
            currentBehaviorState = state;
        }

        public void Start()
        {
            //makeLessSqueaks = configMakeLessSqueaks.Value;
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

            thisEnemyIndex = totalRatsSpawned;
            totalRatsSpawned++;
            random = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
            //updateDestinationInterval = AIIntervalTime;
            //RoundManager.Instance.numberOfEnemiesInScene++;
            path1 = new NavMeshPath();
            allAINodes = RoundManager.Instance.insideAINodes;
            isOutside = false;
            HottestRat();
            RatManager.Rats.Add(this);

            AssignRatType();
            StartCoroutine(DoAIInterval());

            logIfDebug($"Rat spawned");
        }

        /*public void UpdateOld()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            if (updateDestinationInterval >= 0f)
            {
                updateDestinationInterval -= Time.deltaTime;
            }
            else
            {
                DoAIInterval();
                updateDestinationInterval = AIIntervalTime;
            }

            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
            creatureAnimator.SetBool("runBackwards", returningBodyToNest && stunNormalizedTimer <= 0f);

            if (returningBodyToNest && heldBody != null)
            {
                TurnCompass.LookAt(heldBody.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }
        }*/

        public void Update()
        {
            creatureAnimator.SetBool("runBackwards", returningBodyToNest);

            if (returningBodyToNest && heldBody != null)
            {
                TurnCompass.LookAt(heldBody.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }
        }

        IEnumerator DoAIInterval()
        {
            while (true)
            {
                yield return new WaitForSeconds(AIIntervalTime);

                timeSinceAddThreat += AIIntervalTime;
                timeSinceCollision += AIIntervalTime;

                if (moveTowardsDestination)
                {
                    agent.SetDestination(destination);
                }

                switch (currentBehaviorState)
                {
                    case State.Roaming:
                        agent.speed = 5f;

                        if (AtNest)
                        {
                            if (returningBodyToNest)
                            {
                                DropBody(true);
                                MainNest.AddFood(playerFoodAmount);
                            }
                            SwitchToBehaviorState(State.Tasking);
                            break;
                        }

                        if (MainNest == null) { TryGetAnyNest(); }

                        if (MainNest != null && SetDestinationToPosition(MainNest.transform.position))
                        {
                            StopTaskRoutine();
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
                    case State.Tasking:
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
                                    SwitchToBehaviorState(State.Roaming);
                                }
                                break;
                            }
                        }

                        CheckForThreatsInLOS();

                        break;
                    case State.Swarming:
                        agent.speed = 10f;

                        if (targetPlayer != null)
                        {
                            if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats)
                            {
                                targetPlayer = null;
                                SwitchToBehaviorState(State.Roaming);
                                break;
                            }
                            if (NearbyRatCount(swarmRadius) >= ratsNeededToAttack)
                            {
                                SwitchToBehaviorState(State.Attacking);
                                break;
                            }

                            break;
                        }
                        if (targetEnemy != null)
                        {
                            if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats)
                            {
                                targetEnemy = null;
                                SwitchToBehaviorState(State.Roaming);
                                break;
                            }
                            if (NearbyRatCount(swarmRadius) >= ratsNeededToAttack)
                            {
                                SwitchToBehaviorState(State.Attacking);
                                break;
                            }

                            break;
                        }

                        SwitchToBehaviorState(State.Roaming);

                        break;
                    case State.Attacking:
                        agent.speed = 10f;

                        if (targetPlayer != null)
                        {
                            if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats)
                            {
                                // Target too far, return to nest
                                targetPlayer = null;
                                SwitchToBehaviorState(State.Roaming);
                                break;
                            }
                            if (NearbyRatCount(swarmRadius) <= ratsNeededToAttack / 3)
                            {
                                // Check if rats are defending base, keep attacking if player is near base
                                if (!IsPlayerNearANest(targetPlayer))
                                {
                                    // Rats lose confidence and run away
                                    targetPlayer = null;
                                    SwitchToBehaviorState(State.Roaming);
                                    break;
                                }
                            }

                            break;
                        }
                        if (targetEnemy != null)
                        {
                            if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats * 2)
                            {
                                // Target too far, return to nest
                                targetEnemy = null;
                                SwitchToBehaviorState(State.Roaming);
                                break;
                            }
                            if (NearbyRatCount(swarmRadius) <= ratsNeededToAttack / 3)
                            {
                                // Rats lose confidence and run away
                                targetEnemy = null;
                                SwitchToBehaviorState(State.Roaming);
                                break;
                            }

                            break;
                        }

                        SwitchToBehaviorState(State.Roaming);

                        break;

                    default:
                        LoggerInstance.LogWarning("Invalid state: " + currentBehaviorState);
                        break;
                }
            }
        }

        /*public override void DoAIIntervalOld()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || stunNormalizedTimer > 0f)
            {
                return;
            };

            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }

            if (MainNest == null && currentBehaviourStateIndex != (int)State.Roaming) { SwitchToBehaviorState(State.Roaming); return; }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:
                    agent.speed = 5f;

                    if (AtNest)
                    {
                        if (returningBodyToNest)
                        {
                            DropBody(true);
                            MainNest.AddFood(playerFoodAmount);
                        }
                        SwitchToBehaviorState(State.Tasking);
                        return;
                    }

                    if (MainNest == null) { TryGetAnyNest(); }

                    if (MainNest != null && SetDestinationToPosition(MainNest.transform.position))
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
                case (int)State.Tasking:
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
                                SwitchToBehaviorState(State.Roaming);
                            }
                            return;
                        }
                    }

                    CheckForThreatsInLOS();

                    return;
                case (int)State.Swarming:
                    agent.speed = 10f;

                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats)
                        {
                            targetPlayer = null;
                            SwitchToBehaviorState(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) >= ratsNeededToAttack)
                        {
                            SwitchToBehaviorState(State.Attacking);
                            return;
                        }

                        return;
                    }
                    if (targetEnemy != null)
                    {
                        if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats)
                        {
                            targetEnemy = null;
                            SwitchToBehaviorState(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) >= ratsNeededToAttack)
                        {
                            SwitchToBehaviorState(State.Attacking);
                            return;
                        }

                        return;
                    }

                    SwitchToBehaviorState(State.Roaming);

                    return;
                case (int)State.Attacking:
                    agent.speed = 10f;

                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats)
                        {
                            // Target too far, return to nest
                            targetPlayer = null;
                            SwitchToBehaviorState(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) <= ratsNeededToAttack / 3)
                        {
                            // Check if rats are defending base, keep attacking if player is near base
                            if (!IsPlayerNearANest(targetPlayer))
                            {
                                // Rats lose confidence and run away
                                targetPlayer = null;
                                SwitchToBehaviorState(State.Roaming);
                                return;
                            }
                        }

                        return;
                    }
                    if (targetEnemy != null)
                    {
                        if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats * 2)
                        {
                            // Target too far, return to nest
                            targetEnemy = null;
                            SwitchToBehaviorState(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) <= ratsNeededToAttack / 3)
                        {
                            // Rats lose confidence and run away
                            targetEnemy = null;
                            SwitchToBehaviorState(State.Roaming);
                            return;
                        }

                        return;
                    }

                    SwitchToBehaviorState(State.Roaming);

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }*/

        void TryGetAnyNest()
        {
            MainNest = RatManager.Nests.FirstOrDefault();
        }

        void GrabBody()
        {
            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                int limb = random.Next(0, targetPlayer.deadBody.bodyParts.Length);
                GrabBody(targetPlayer, limb);
            }
        }

        public void DropBody(bool deactivate = false)
        {
            if (heldBody == null) { return; }
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

        bool SetDestinationToPosition(Vector3 position)
        {
            position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
            finalDestination = position;
            path1 = new NavMeshPath();
            if (agent.enabled == false) { return false; }
            if (!agent.CalculatePath(position, path1))
            {
                // Cant get to destination without vent1
                if (canVent)
                {
                    // Look for vents that rat can use
                    if (FromVent != null && ToVent != null)
                    {
                        // Can path to position!
                        Vector3 fromPos = RoundManager.Instance.GetNavMeshPosition(FromVent.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);

                        // Check if near FromVent
                        if (Vector3.Distance(transform.position, fromPos) < 1f)
                        {
                            // At FromVent, so warp to ToVent
                            WarpToVent(FromVent, ToVent);
                            moveTowardsDestination = true;
                            destination = RoundManager.Instance.GetNavMeshPosition(finalDestination, RoundManager.Instance.navHit, -1f);
                            return true;
                        }

                        // Not at FromVent, set destination to FromVent and return true
                        moveTowardsDestination = true;
                        destination = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, -1f);
                        return true;
                    }
                }

                // Cant get to position with vents, return false
                return false;
            }
            // Can get to position without vents
            moveTowardsDestination = true;
            destination = RoundManager.Instance.GetNavMeshPosition(finalDestination, RoundManager.Instance.navHit, -1f);
            return true;
        }

        void HottestRat()
        {
            if (random.Next(0, 100) < 1f)
            {
                ScanNode.headerText = "HottestRat";
            }
        }

        public EnemyAI? CheckLineOfSightForEnemy(int range)
        {
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy == RatKing || !enemy.enemyType.canDie) { continue; }
                if (CheckLineOfSightForPosition(enemy.transform.position, 60, range, 5))
                {
                    return enemy;
                }
            }
            return null;
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

        int NearbyRatCount(float radius)
        {
            int count = 0;
            foreach(var rat in RatManager.Rats)
            {
                if (Vector3.Distance(transform.position, rat.transform.position) <= radius) // TODO: Just do a timer for each rat when they enter swarm mode before they attack the player. Have the timer be static so they all attack at once.
                {
                    count++;
                }
            }
            return count;
        }

        public void AssignRatType()
        {
            if (!AtNest)
            {
                RatTypeState = RatType.Unassigned;
                return;
            }

            if (holdingFood)
            {
                holdingFood = false;
                MainNest.AddFood();
            }

            if (MainNest.DefenseRatCount < maxDefenseRats)
            {
                logIfDebug("Rat defense assigned");
                RatTypeState = RatType.DefenseRat;
                MainNest.DefenseRatCount += 1;


                ratCoroutine = StartCoroutine(SwarmCoroutine(MainNest.transform.position, defenseRadius));
                SetStatus("Defending");
                return;
            }

            logIfDebug("Rat scout assigned");
            RatTypeState = RatType.ScoutRat;
            ratCoroutine = StartCoroutine(ScoutCoroutine());
            SetStatus("Scouting");
            return;
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

        void WarpToVent(EnemyVent fromVent, EnemyVent toVent)
        {
            OpenVents(fromVent, toVent);
            Vector3 warpPos = RoundManager.Instance.GetNavMeshPosition(ToVent!.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
            transform.position = warpPos;
            agent.Warp(warpPos);
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

        IEnumerator RoamCoroutine() // TODO: Fix these so they use agent.velocity instead of a distance check.
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
                        PlaySqueakSFX(true);
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.Roaming);
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
                    if (!agent.hasPath || timeStuck > AIIntervalTime)
                    {
                        PlaySqueakSFX();
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.Roaming);
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
                        PlaySqueakSFX();
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.Roaming);
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
                        PlaySqueakSFX();
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.Roaming);
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
                        PlaySqueakSFX(true);
                        SwitchToBehaviorState(State.Roaming);
                        ratCoroutine = null;
                        yield break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.Roaming);
        }

        void TargetRandomNode()
        {
            try
            {
                //logIfDebug("Choosing new target node...");

                //GameObject[] nodes = !isOutside ? GameObject.FindGameObjectsWithTag("AINode") : GameObject.FindGameObjectsWithTag("OutsideAINode");
                GameObject[] nodes = allAINodes;

                int randIndex = random.Next(0, nodes.Length);
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
                    KillEnemy();
                    return;
                }
            }
        }

        public void HitFromExplosion(float distance) // TODO: Implement this later
        {
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            KillEnemy();
        }

        public bool PlayerIsTargetable(PlayerControllerB playerScript)
        {
            if (playerScript == null) { return false; }
            if (playerScript.isPlayerDead) { return false; }
            if (currentBehaviorState == State.Roaming) { return false; }
            if (!playerScript.isPlayerControlled) { return false; }
            if (playerScript.inAnimationWithEnemy != null) { return false; }
            if (playerScript.sinkingValue >= 0.73f) { return false; }
            return true;
        }

        public bool IsPlayerNearANest(PlayerControllerB player)
        {
            foreach (SewerGrate nest in RatManager.Nests)
            {
                if (Vector3.Distance(player.transform.position, nest.transform.position) < defenseRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public PlayerControllerB? MeetsStandardPlayerCollisionConditions(Collider other, bool inKillAnimation = false, bool overrideIsInsideFactoryCheck = false)
        {
            if (isEnemyDead)
            {
                return null;
            }
            if (inKillAnimation)
            {
                return null;
            }
            PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
            if (component == null/* || component != GameNetworkManager.Instance.localPlayerController*/)
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
                if (currentBehaviorState == State.Attacking || IsPlayerNearANest(player))
                {
                    int deathAnim = random.Next(0, 2) == 1 ? 7 : 0;
                    player.DamagePlayer(ratDamage, true, false, CauseOfDeath.Mauling, deathAnim);
                    PlayAttackSFX();
                }
            }
        }

        public void OnCollideWithEnemy(Collider other, EnemyAI? collidedEnemy = null)
        {
            if (isEnemyDead || currentBehaviorState == State.Roaming || MainNest == null) { return; }
            if (timeSinceCollision > 1f)
            {
                if (collidedEnemy != null && collidedEnemy.enemyType.canDie)
                {
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
                            MainNest.AddEnemyFoodAmount(collidedEnemy);
                            SewerGrate.EnemyFoodAmount[collidedEnemy] -= 1;
                            holdingFood = true;
                        }

                        SwitchToBehaviorState(State.Roaming);
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

            if (MainNest != null)
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

                PlaySqueakSFX();

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

            if (MainNest != null)
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

                PlaySqueakSFX();

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

        public void KillEnemy(bool destroy = false)
        {
            if (destroy)
            {
                UnityEngine.GameObject.Destroy(gameObject);
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
                        creatureAnimator.SetTrigger("KillEnemy");
                    }
                }
                catch (Exception arg)
                {
                    Debug.LogError($"enemy did not have bool in animator in KillEnemy, error returned; {arg}");
                }
                StopAllCoroutines();
                agent.enabled = false;
                RatManager.Rats.Remove(this);
                if (RatTypeState == RatType.DefenseRat) { MainNest.DefenseRatCount -= 1; }
            }
        }

        public void OnDestroy()
        {
            agent.enabled = false;
            StopAllCoroutines();
            RatManager.Rats.Remove(this);
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

        public bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            return true;
        }

        public void PlayAttackSFX()
        {
            RoundManager.PlayRandomClip(creatureSFX, AttackSFX, true, 1, -1);
        }

        public void PlaySqueakSFX(bool longSqueak = false)
        {
            if (longSqueak)
            {
                RoundManager.PlayRandomClip(creatureSFX, SqueakSFX, true, 1, -1);
                return;
            }
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
        }

        public void OpenVents(EnemyVent vent1, EnemyVent vent2)
        {
            if (vent1 == null)
            {
                RoundManager.Instance.RefreshEnemyVents();
                if (vent1 == null)
                {
                    LoggerInstance.LogError("Cant get vent to open on client");
                    return;
                }
            }
            vent1.ventIsOpen = true;
            vent1.ventAnimator.SetTrigger("openVent");
            vent1.lowPassFilter.lowpassResonanceQ = 0f;

            if (vent2 == null)
            {
                RoundManager.Instance.RefreshEnemyVents();
                if (vent2 == null)
                {
                    LoggerInstance.LogError("Cant get vent to open on client");
                    return;
                }
            }
            vent2.ventIsOpen = true;
            vent2.ventAnimator.SetTrigger("openVent");
            vent2.lowPassFilter.lowpassResonanceQ = 0f;

        }

        public void GrabBody(PlayerControllerB player, int attachedLimbIndex)
        {
            targetPlayer = player;

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

        internal void HitEnemyOnLocalClient(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)
        {
            HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        }
    }
}

// TODO: statuses: shakecamera, playerstun, drunkness, fear, insanity