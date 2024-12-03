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

// Cheese to lure the rats away from the grate so the player can block it
// Rats will scout and do a normal search, if they see a player after 20 seconds, they will add a counter to the threatcounter, when max threat, they will run back to sewer and gather rats they touch and get defending rats and swarm around the player for 20 seconds, any rats nearby will also swarm. then attack.

namespace Rats
{
    public class RatAI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public ScanNodeProperties ScanNode = null!;
        public AudioClip[] SqueakSFX = null!;
        public AudioClip[] AttackSFX = null!;
        public AudioClip[] HitSFX = null!;
        public AudioClip[] NibbleSFX = null!;
        public Transform TurnCompass = null!;
        public Transform RatMouth = null!;
        public GameObject ChristmasHat = null!;
#pragma warning restore 0649

        public SewerGrate? MainNest;
        DeadBodyInfo? heldBody;
        EnemyAI? targetEnemy;
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

        public void SwitchToBehaviourStateCustom(State state)
        {
            if (currentBehaviourStateIndex == (int)state) { return; }

            StopRoutines();

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

            SwitchToBehaviourClientRpc((int)state);
        }

        public override void Start()
        {
            makeLessSqueaks = configMakeLessSqueaks.Value;
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

            thisEnemyIndex = RoundManager.Instance.numberOfEnemiesInScene;
            RoundManager.Instance.numberOfEnemiesInScene++;
            allAINodes = RoundManager.Instance.insideAINodes;
            if (!RoundManager.Instance.SpawnedEnemies.Contains(this)) { RoundManager.Instance.SpawnedEnemies.Add(this); }
            path1 = new NavMeshPath();
            ventAnimationFinished = true;
            currentBehaviourStateIndex = (int)State.Tasking;
            HottestRat();
            RatManager.Rats.Add(this);

            if (IsServerOrHost)
            {
                AssignRatType();
            }

            logIfDebug($"Rat spawned");
        }

        public override void Update()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            creatureAnimator.SetBool("stunned", stunNormalizedTimer > 0f);
            if (stunNormalizedTimer >= 0f)
            {
                stunNormalizedTimer -= Time.deltaTime / enemyType.stunTimeMultiplier;
                agent.speed = 0f;
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

            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
            creatureAnimator.SetBool("runBackwards", returningBodyToNest && stunNormalizedTimer <= 0f);

            if (returningBodyToNest && heldBody != null)
            {
                TurnCompass.LookAt(heldBody.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }
        }

        public override void DoAIInterval()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || stunNormalizedTimer > 0f)
            {
                return;
            };

            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }

            if (MainNest == null && currentBehaviourStateIndex != (int)State.Roaming) { SwitchToBehaviourStateCustom(State.Roaming); return; }

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
                        SwitchToBehaviourStateCustom(State.Tasking);
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
                                SwitchToBehaviourStateCustom(State.Roaming);
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
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) >= ratsNeededToAttack)
                        {
                            SwitchToBehaviourStateCustom(State.Attacking);
                            return;
                        }

                        return;
                    }
                    if (targetEnemy != null)
                    {
                        if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats)
                        {
                            targetEnemy = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) >= ratsNeededToAttack)
                        {
                            SwitchToBehaviourStateCustom(State.Attacking);
                            return;
                        }

                        return;
                    }

                    SwitchToBehaviourStateCustom(State.Roaming);

                    return;
                case (int)State.Attacking:
                    agent.speed = 10f;

                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats)
                        {
                            // Target too far, return to nest
                            targetPlayer = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) <= ratsNeededToAttack / 3)
                        {
                            // Check if rats are defending base, keep attacking if player is near base
                            if (!IsPlayerNearANest(targetPlayer))
                            {
                                // Rats lose confidence and run away
                                targetPlayer = null;
                                SwitchToBehaviourStateCustom(State.Roaming);
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
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) <= ratsNeededToAttack / 3)
                        {
                            // Rats lose confidence and run away
                            targetEnemy = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }

                        return;
                    }

                    SwitchToBehaviourStateCustom(State.Roaming);

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        void TryGetAnyNest()
        {
            MainNest = RatManager.Nests.FirstOrDefault();
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

        bool SetDestinationToPosition(Vector3 position)
        {
            position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
            finalDestination = position;
            path1 = new NavMeshPath();
            if (agent.enabled == false) { return false; }
            if (!agent.CalculatePath(position, path1))
            {
                // Cant get to destination without vent
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
                            movingTowardsTargetPlayer = false;
                            destination = RoundManager.Instance.GetNavMeshPosition(finalDestination, RoundManager.Instance.navHit, -1f);
                            return true;
                        }

                        // Not at FromVent, set destination to FromVent and return true
                        moveTowardsDestination = true;
                        movingTowardsTargetPlayer = false;
                        destination = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, -1f);
                        return true;
                    }
                }

                // Cant get to position with vents, return false
                return false;
            }
            // Can get to position without vents
            moveTowardsDestination = true;
            movingTowardsTargetPlayer = false;
            destination = RoundManager.Instance.GetNavMeshPosition(finalDestination, RoundManager.Instance.navHit, -1f);
            return true;
        }

        void HottestRat()
        {
            if (UnityEngine.Random.Range(0f, 1f) < 0.05f)
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

        public EnemyAI? CheckLineOfSightForEnemy(int range)
        {
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.enemyType == enemyType || !enemy.enemyType.canDie) { continue; }
                if (CheckLineOfSightForPosition(enemy.transform.position, 60, range, 5))
                {
                    return enemy;
                }
            }
            return null;
        }

        public EnemyAI? GetClosestEnemy(float range)
        {
            EnemyAI? target = null;
            float closestDistance = range;

            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.enemyType == enemyType || !enemy.enemyType.canDie) { continue; }
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    target = enemy;
                    closestDistance = distance;
                }
            }

            return target;
        }

        bool TargetPlayerNearNest()
        {
            if (MainNest == null) { return false; }
            foreach(PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (PlayerIsTargetable(player))
                {
                    if (Vector3.Distance(player.transform.position, MainNest.transform.position) < defenseRadius)
                    {
                        targetPlayer = player;
                    }
                }
            }

            return targetPlayer != null;
        }

        void WarpToVent(EnemyVent fromVent, EnemyVent toVent)
        {
            OpenVents(fromVent, toVent);
            Vector3 warpPos = RoundManager.Instance.GetNavMeshPosition(ToVent!.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
            transform.position = warpPos;
            agent.Warp(warpPos);
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

            SwitchToBehaviourStateCustom(State.Roaming);
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
                        PlaySqueakSFX();
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviourStateCustom(State.Roaming);
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

            SwitchToBehaviourStateCustom(State.Roaming);
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

            SwitchToBehaviourStateCustom(State.Roaming);
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
                        SwitchToBehaviourStateCustom(State.Roaming);
                        ratCoroutine = null;
                        yield break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStuck += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviourStateCustom(State.Roaming);
        }

        void PlaySqueakSFX(bool longSqueak = false)
        {
            if (!configMakeLessSqueaks.Value)
            {
                PlaySqueakSFXClientRpc(longSqueak);
            }
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

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
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

        public override void CancelSpecialAnimationWithPlayer()
        {
            StopRoutines();
        }

        public override void HitFromExplosion(float distance)
        {
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            KillEnemyOnOwnerClient();
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

        public override void OnCollideWithPlayer(Collider other)
        {
            if (isEnemyDead) { return; }
            if (timeSinceCollision > 1f)
            {
                timeSinceCollision = 0f;
                PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                if (player == null) { return; }
                if (currentBehaviourStateIndex == (int)State.Attacking || IsPlayerNearANest(player))
                {
                    int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                    player.DamagePlayer(ratDamage, true, true, CauseOfDeath.Mauling, deathAnim);
                    PlayAttackSFXServerRpc();
                }
            }
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI? collidedEnemy = null)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);
            if (IsServerOrHost)
            {
                if (isEnemyDead || currentBehaviourStateIndex == (int)State.Roaming || MainNest == null) { return; }
                if (timeSinceCollision > 1f)
                {
                    if (collidedEnemy != null && collidedEnemy.enemyType != this.enemyType && collidedEnemy.enemyType.canDie && !(stunNormalizedTimer > 0f))
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
                                    collidedEnemy.NetworkObject.Despawn(true);
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

                            SwitchToBehaviourStateCustom(State.Roaming);
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

                PlaySqueakSFXClientRpc();

                int threat = RatManager.EnemyThreatCounter[enemy];
                logIfDebug($"{enemy.enemyType.enemyName}: {threat} threat");

                if (RatTypeState == RatType.DefenseRat) { return; }

                if (RatManager.EnemyThreatCounter[enemy] > threatToAttackEnemy || enemy.isEnemyDead)
                {
                    SetTarget(enemy);
                    SwitchToBehaviourStateCustom(State.Swarming);
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

                PlaySqueakSFXClientRpc();

                int threat = RatManager.PlayerThreatCounter[player];
                logIfDebug($"{player.playerUsername}: {threat} threat");
                
                if (RatTypeState == RatType.DefenseRat) { return; }

                if (RatManager.PlayerThreatCounter[player] > threatToAttackPlayer || player.isPlayerDead)
                {
                    SetTarget(player);
                    SwitchToBehaviourStateCustom(State.Swarming);
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

        public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB? setStunnedByPlayer = null)
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
        }

        public override void KillEnemy(bool destroy = false)
        {
            if (destroy && enemyType.canBeDestroyed)
            {
                Debug.Log("Destroy enemy called");
                if (base.IsServer)
                {
                    Debug.Log("Despawn network object in kill enemy called!");
                    if (thisNetworkObject.IsSpawned)
                    {
                        thisNetworkObject.Despawn();
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
                RatManager.Rats.Remove(this);
                if (RatTypeState == RatType.DefenseRat) { MainNest.DefenseRatCount -= 1; }
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

            if (RoundManager.Instance.SpawnedEnemies.Contains(this))
            {
                RoundManager.Instance.SpawnedEnemies.Remove(this);
            }

            RatManager.Rats.Remove(this);
        }

        public new bool SetDestinationToPosition(Vector3 position, bool checkForPath = false)
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
            movingTowardsTargetPlayer = false;
            destination = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, -1f);
            return true;
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
        public new void SwitchToBehaviourClientRpc(int stateIndex)
        {
            if (currentBehaviourStateIndex != stateIndex)
            {
                currentBehaviourStateIndex = stateIndex;
                currentBehaviourState = enemyBehaviourStates[stateIndex];
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
        public void OpenVentsClientRpc(int ventIndex, int ventIndex2)
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
    }
}

// TODO: statuses: shakecamera, playerstun, drunkness, fear, insanity