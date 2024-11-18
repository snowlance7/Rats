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
using UnityEngine.AI;
using UnityEngine.UIElements;
using static Rats.Plugin;

// Cheese to lure the rats away from the grate so the player can block it
// Rats will scout and do a normal search, if they see a player after 20 seconds, they will add a counter to the threatcounter, when max threat, they will run back to sewer and gather rats they touch and get defending rats and swarm around the player for 20 seconds, any rats nearby will also swarm. then attack.

namespace Rats
{
    internal class RatAI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;
        public static bool testing = false;

#pragma warning disable 0649
        public ScanNodeProperties ScanNode = null!;
#pragma warning restore 0649

        EnemyAI? targetEnemy = null!;

        SewerGrate? Nest;
        //EnemyVent? NestVent;
        //EnemyVent? VentToNest;

        float timeSincePlayerCollision;
        float timeSinceAddThreat;
        float timeSinceNestCheck;

        bool sickRat;
        State defaultState;
        RatType RatTypeState;
        Coroutine? swarmCoroutine = null;
        Coroutine? scoutCoroutine = null;
        Coroutine? roamCoroutine = null;
        Coroutine? returnToNestCoroutine = null;

        bool musterRat;
        bool returningToNest;
        bool IsVenting
        {
            get
            {
                return Nest != null && NestVent != null && VentToNest != null;
            }
        }

        // Constants

        // Config Values
        float sickRatChance = 0.1f;
        float defenseRadius = 10f;
        float sewerGrateAttackRadius = 5f;
        float timeToIncreaseThreat = 3f;
        int threatToAttackPlayer = 100;
        int threatToAttackEnemy = 50;
        int highThreatToAttackPlayer = 200;
        int highThreatToAttackEnemy = 100;
        float swarmRadius = 10f;
        bool canVent = true;
        bool defenseRats = true;
        int maxDefenseRats = 5;
        bool scoutRats = true;
        int maxScoutRats = 10;
        int ratsNeededToAttack = 10;
        float distanceToLoseRats = 20f;
        int enemyHitsToDoDamage = 10;

        public enum RatType
        {
            SearchRat,
            ScoutRat,
            DefenseRat
        }

        public enum State
        {
            Roaming,
            Tasking,
            Swarming,
            Attacking,
            Mustering
        }

        public void SwitchToBehaviourStateCustom(State state)
        {
            logger.LogDebug("Switching to state: " + state);
            StopTasks();

            switch (state)
            {
                case State.Roaming:
                    musterRat = false;
                    timeSinceNestCheck = 0f;
                    if (CanReturnToNest())
                    {
                        roamCoroutine = null;
                    }
                    else
                    {
                        if (roamCoroutine == null)
                        {
                            roamCoroutine = StartCoroutine(RoamCoroutine());
                        }
                    }

                    break;
                case State.Tasking:
                    musterRat = false;
                    AssignRatType();

                    break;
                case State.Swarming:
                    break;
                case State.Attacking:
                    break;
                case State.Mustering:
                    musterRat = true;
                    if (Nest != null && Nest.LeadMusterRat == null)
                    {
                        Nest.LeadMusterRat = this;
                    }
                    break;
                default:
                    break;
            }

            SwitchToBehaviourClientRpc((int)state);
        }

        public override void Start()
        {
            base.Start();

            if (IsServerOrHost)
            {
                RoundManager.Instance.SpawnedEnemies.Add(this);
                logger.LogDebug($"Rat spawned");
                HottestRat();
                SwitchToBehaviourStateCustom(GetStartNest() ? State.Tasking : State.Roaming);
            }
        }

        public override void Update()
        {
            base.Update();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            timeSinceNestCheck += Time.deltaTime;
            timeSincePlayerCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
            creatureAnimator.SetBool("stunned", stunNormalizedTimer > 0f);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || testing)
            {
                return;
            };

            if (Nest != null && Vector3.Distance(transform.position, Nest.transform.position) < defenseRadius && TargetPlayerNearNest())
            {
                SwitchToBehaviourStateCustom(State.Attacking);
                return;
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:
                    if (timeSinceNestCheck > 10f)
                    {
                        timeSinceNestCheck = 0f;
                        TryReturnToNest();
                    }

                    break;
                case (int)State.Tasking:
                    CheckForThreats();

                    switch (RatTypeState)
                    {
                        case RatType.SearchRat:

                            break;
                        case RatType.ScoutRat:

                            break;
                        case RatType.DefenseRat:
                            break;
                        default:
                            break;
                    }

                    return;
                case (int)State.Swarming:
                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats)
                        {
                            targetPlayer = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius * 2) >= ratsNeededToAttack)
                        {
                            SwitchToBehaviourStateCustom(State.Attacking);
                            return;
                        }
                    }
                    if (targetEnemy != null)
                    {
                        if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats)
                        {
                            targetEnemy = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius * 2) >= ratsNeededToAttack)
                        {
                            SwitchToBehaviourStateCustom(State.Attacking);
                            return;
                        }
                    }

                    SwitchToBehaviourStateCustom(State.Roaming);
                    return;
                case (int)State.Attacking:
                    if (!musterRat && RatTypeState == RatType.DefenseRat && Vector3.Distance(Nest.transform.position, transform.position) > defenseRadius * 2)
                    {
                        SwitchToBehaviourStateCustom(State.Roaming);
                        return;
                    }
                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats * 2 && !musterRat)
                        {
                            // Target too far, return to nest
                            targetPlayer = null;
                            movingTowardsTargetPlayer = false;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius * 2) <= ratsNeededToAttack / 2.5 && !musterRat)
                        {
                            // Check if rats are defending base, keep attacking if player is near base
                            if (!TargetPlayerNearNest())
                            {
                                // Rats lose confidence and run away
                                targetPlayer = null;
                                movingTowardsTargetPlayer = false;
                                SwitchToBehaviourStateCustom(State.Roaming);
                                return;
                            }
                        }

                        SetMovingTowardsTargetPlayer(targetPlayer);
                        return;
                    }
                    if (targetEnemy != null)
                    {
                        if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats)
                        {
                            // Target too far, return to nest
                            targetEnemy = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius * 2) <= ratsNeededToAttack / 3)
                        {
                            // Rats lose confidence and run away
                            targetEnemy = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }

                        SetDestinationToPosition(targetEnemy.transform.position);
                    }
                    break;
                case (int)State.Mustering:

                    if (CanReturnToNest())
                    {
                        // Go to nest
                        if (IsVenting)
                        {
                            Vector3 ventPos = GetNavMeshPosition(VentToNest.floorNode.transform.position);
                            if (Vector3.Distance(transform.position, ventPos) < 1f)
                            {
                                WarpToVent(VentToNest, NestVent);
                                return;
                            }

                            SetDestinationToPosition(ventPos);
                            return;
                        }
                        else
                        {
                            if (Vector3.Distance(transform.position, Nest.transform.position) <= swarmRadius)
                            {
                                if (Nest.CanMuster && !Nest.Mustering && Nest.LeadMusterRat == this)
                                {
                                    Nest.StartMusterTimer();
                                }
                                return;
                            }

                            SetDestinationToPosition(GetNavMeshPosition(Nest.transform.position));
                        }
                    }
                    break;

                default:
                    logger.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        void TryGetAnyNest()
        {
            Nest = SewerGrate.Nests.First();
        }

        void HottestRat()
        {
            if (UnityEngine.Random.Range(0f, 1f) < 0.01f)
            {
                ScanNode.headerText = "HottestRat";
            }
        }

        void CheckForThreats()
        {
            if (timeSinceAddThreat > timeToIncreaseThreat)
            {
                PlayerControllerB player = CheckLineOfSightForClosestPlayer();
                if (player != null)
                {
                    AddThreat(player);
                    return;
                }
                EnemyAI? enemy = CheckLineOfSightForClosestEnemy();
                if (enemy != null)
                {
                    AddThreat(enemy);
                }
            }
        }

        int NearbyRatCount(float radius)
        {
            int count = 1;
            foreach(var rat in GameObject.FindObjectsOfType<RatAI>())
            {
                if (Vector3.Distance(transform.position, rat.transform.position) <= radius)
                {
                    count++;
                }
            }
            return count;
        }

        public void AssignRatType()
        {
            if (Nest.DefenseRats.Count() < maxDefenseRats)
            {
                logger.LogDebug("Rat defense assigned");
                RatTypeState = RatType.DefenseRat;
                swarmCoroutine = StartCoroutine(SwarmCoroutine(Nest.transform.position, defenseRadius));
                return;
            }
            if (Nest.ScoutRats.Count() < maxScoutRats)
            {
                logger.LogDebug("Rat scout assigned");
                RatTypeState = RatType.ScoutRat;
                scoutCoroutine = StartCoroutine(ScoutCoroutine());
                return;
            }

            logger.LogDebug("Search rat assigned");
            RatTypeState = RatType.SearchRat;
            StartSearch(Nest.transform.position);
        }

        // Vents
        
        EnemyVent? GetClosestVentToPosition(Vector3 pos)
        {
            float mostOptimalDistance = 2000f;
            EnemyVent? targetVent = null;
            foreach (var vent in RoundManager.Instance.allEnemyVents)
            {
                float distance = Vector3.Distance(pos, vent.transform.position);
                if (CalculatePath(pos, vent.transform.position) && distance < mostOptimalDistance)
                {
                    mostOptimalDistance = distance;
                    targetVent = vent;
                }
            }
            if (targetVent == null) { logger.LogDebug("Couldnt find vent close to position"); }
            else { logger.LogDebug("Found vent!"); }
            return targetVent;
        }

        bool CalculatePath(Vector3 fromPos, Vector3 toPos)
        {
            Vector3 from = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, 1.75f);
            Vector3 to = RoundManager.Instance.GetNavMeshPosition(toPos, RoundManager.Instance.navHit, 1.75f);

            path1 = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, agent.areaMask, path1);
        }

        bool CalculatePath(Vector3 toPos)
        {
            Vector3 from = GetNavMeshPosition(transform.position);
            Vector3 to = GetNavMeshPosition(toPos);
            path1 = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, agent.areaMask, path1);
        }

        public EnemyAI? CheckLineOfSightForClosestEnemy()
        {
            GameObject enemyObj = CheckLineOfSight(GameObject.FindGameObjectsWithTag("Enemy").ToList());
            if (enemyObj != null)
            {
                if (enemyObj.TryGetComponent<EnemyAI>(out EnemyAI enemy))
                {
                    return enemy;
                }
            }

            return null;
        }

        bool TargetPlayerNearNest()
        {
            if (Nest == null) { return false; }
            targetPlayer = null;
            foreach(PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (PlayerIsTargetable(player))
                {
                    if (Vector3.Distance(player.transform.position, Nest.transform.position) < defenseRadius)
                    {
                        targetPlayer = player;
                    }
                }
            }

            return targetPlayer != null;
        }

        bool GetStartNest()
        {
            foreach(var nest in SewerGrate.Nests)
            {
                if (Vector3.Distance(transform.position, nest.transform.position) < 1f)
                {
                    Nest = nest;
                    return true;
                }
            }
            return false;
        }

        bool CanReturnToNest()
        {
            if (Nest != null)
            {
                if (CalculatePath(Nest.transform.position))
                {
                    return true;
                }
                else if (canVent)
                {
                    if (NestVent == null || CalculatePath(NestVent.transform.position)) { NestVent = Nest.GetClosestVentToNest(agent.areaMask); }
                    if (VentToNest == null || CalculatePath(VentToNest.transform.position)) { VentToNest = GetClosestVentToPosition(transform.position);}

                    return IsVenting;
                }
            }

            return FindPathToNewNest();
        }

        bool FindPathToNewNest()
        {
            Nest = null;
            NestVent = null;
            VentToNest = null;
            if (!CanFindPathToNewNestWithoutVenting() && canVent)
            {
                float closestDistance = 2000f;
                foreach (var nest in SewerGrate.Nests)
                {
                    EnemyVent? closestVentToNest = nest.GetClosestVentToNest(agent.areaMask);
                    EnemyVent? closestVentToRat = GetClosestVentToPosition(transform.position);

                    if (closestVentToNest != null && closestVentToRat != null)
                    {
                        float distance = Vector3.Distance(transform.position, closestVentToRat.floorNode.transform.position) + Vector3.Distance(closestVentToNest.floorNode.transform.position, nest.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            Nest = nest;
                            NestVent = closestVentToNest;
                            VentToNest = closestVentToRat;
                        }
                    }
                }
                return IsVenting;
            }
            return Nest != null;
        }

        bool CanFindPathToNewNestWithoutVenting()
        {
            float closestDistance = 2000f;
            foreach (var nest in SewerGrate.Nests)
            {
                float distance = Vector3.Distance(nest.transform.position, transform.position);
                if (distance < closestDistance && CalculatePath(nest.transform.position))
                {
                    closestDistance = distance;
                    Nest = nest;
                }
            }
            return Nest != null;
        }

        void WarpToVent(EnemyVent fromVent, EnemyVent toVent)
        {
            OpenVent(fromVent);
            VentToNest = null;
            OpenVent(toVent);
            Vector3 warpPos = GetNavMeshPosition(toVent.floorNode.position);
            serverPosition = warpPos;
            transform.position = warpPos;
            agent.Warp(warpPos);
            SyncPositionToClients();
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

        void OpenVent(EnemyVent? vent)
        {
            if (vent == null) { return; }
            int index = GetVentIndex(vent);
            if (index != -1)
            {
                OpenVentClientRpc(index);
            }
        }

        Vector3 GetNavMeshPosition(Vector3 pos, float sampleRadius = 5, int areaMask = -1)
        {
            return RoundManager.Instance.GetNavMeshPosition(pos, RoundManager.Instance.navHit, sampleRadius, areaMask);
        }

        public void StopTasks()
        {
            if (roamCoroutine != null)
            {
                StopCoroutine(roamCoroutine);
                roamCoroutine = null;
            }

            if (swarmCoroutine != null)
            {
                StopCoroutine(swarmCoroutine);
                swarmCoroutine = null;
            }

            if (scoutCoroutine != null)
            {
                StopCoroutine(scoutCoroutine);
                scoutCoroutine = null;
            }

            if (returnToNestCoroutine != null)
            {
                StopCoroutine(returnToNestCoroutine);
                returnToNestCoroutine = null;
            }

            StopSearch(currentSearch);
        }

        void TryReturnToNest()
        {
            if (returnToNestCoroutine == null)
            {
                if (roamCoroutine != null)
                {
                    StopCoroutine(roamCoroutine);
                }

                returnToNestCoroutine = StartCoroutine(TryReturnToNestCoroutine());
            }
        }

        IEnumerator TryReturnToNestCoroutine() // TODO: Try to rework everything so they work like this
        {
            yield return null;

            Vector3 nestPos = GetNavMeshPosition(Nest.transform.position);
            if (SetDestinationToPosition(nestPos, true))
            {
                while (SetDestinationToPosition(nestPos, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, nestPos) < 1f)
                    {
                        logger.LogDebug("Returned to nest");
                        SwitchToBehaviourStateCustom(State.Tasking);
                        yield break;
                    }
                }
            }

            EnemyVent? VentFrom = GetClosestVentToPosition(transform.position);
            EnemyVent? VentTo = GetClosestVentToPosition(Nest.transform.position);

            if (VentFrom != null && VentTo != null)
            {
                logger.LogDebug("GOT VENTS");
                bool atVentFrom = false;
                while (SetDestinationToPosition(VentFrom.floorNode.transform.position, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, VentFrom.floorNode.transform.position) < 1f)
                    {
                        atVentFrom = true;
                        break;
                    }
                }
                if (!atVentFrom)
                {
                    logger.LogDebug("Couldnt get to fromVent");
                    yield break;
                }

                WarpToVent(VentFrom, VentTo);

                Vector3 targetNodePos = GetNavMeshPosition(Nest.transform.position);
                while (SetDestinationToPosition(targetNodePos, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, Nest.transform.position) < 1f)
                    {
                        logger.LogDebug("Returned to nest");
                        SwitchToBehaviourStateCustom(State.Tasking);
                        yield break;
                    }
                }

                logger.LogDebug("Failed to return to nest");
                returnToNestCoroutine = null;
                StartRoam();
            }
        }

        void StartRoam()
        {
            if (roamCoroutine != null) { StopCoroutine(roamCoroutine); }
            roamCoroutine = StartCoroutine(RoamCoroutine());
        }

        IEnumerator RoamCoroutine()
        {
            yield return null;
            while (roamCoroutine != null)
            {
                TargetRandomNode();
                destination = GetNavMeshPosition(targetNode.transform.position);
                while (SetDestinationToPosition(destination, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, destination) < 1f)
                    {
                        // TODO: play squeeksfx
                        break;
                    }
                }
            }
        }

        IEnumerator SwarmCoroutine(PlayerControllerB player, float radius)
        {
            yield return null;
            while (swarmCoroutine != null)
            {
                destination = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(destination, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, destination) < 1f)
                    {
                        // TODO: play squeeksfx
                        break;
                    }
                }
            }
        }

        IEnumerator SwarmCoroutine(EnemyAI enemy, float radius)
        {
            yield return null;
            while (swarmCoroutine != null)
            {
                destination = RoundManager.Instance.GetRandomNavMeshPositionInRadius(enemy.transform.position, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(destination, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, destination) < 1f)
                    {
                        // TODO: play squeeksfx
                        break;
                    }
                }
            }
        }

        IEnumerator SwarmCoroutine(Vector3 pos, float radius)
        {
            yield return null;
            while (swarmCoroutine != null)
            {
                destination = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(destination, true))
                {
                    yield return null;
                    if (Vector3.Distance(transform.position, destination) < 1f)
                    {
                        // TODO: play squeeksfx
                        break;
                    }
                }
            }
        }

        IEnumerator ScoutCoroutine()
        {
            yield return null;
            while (scoutCoroutine != null)
            {
                TargetRandomNode();
                destination = targetNode.transform.position;
                if (SetDestinationToPosition(destination, true))
                {
                    yield return new WaitForSecondsRealtime(1f);
                    yield return new WaitUntil(() => Vector3.Distance(transform.position, destination) < 1f);
                    // TODO: play squeeksfx
                }
                else if (canVent)
                {
                    EnemyVent? VentFrom = GetClosestVentToPosition(transform.position);
                    EnemyVent? VentTo = GetClosestVentToPosition(destination);

                    if (VentFrom != null && VentTo != null)
                    {
                        bool atVentFrom = false;
                        while (SetDestinationToPosition(VentFrom.floorNode.transform.position, true))
                        {
                            yield return new WaitForSeconds(1f);
                            if (Vector3.Distance(transform.position, VentFrom.floorNode.transform.position) < 1f)
                            {
                                atVentFrom = true;
                                break;
                            }
                        }
                        if (!atVentFrom) { continue; }

                        WarpToVent(VentFrom, VentTo);
                        
                        Vector3 targetNodePos = GetNavMeshPosition(targetNode.transform.position);
                        while (SetDestinationToPosition(targetNodePos, true))
                        {
                            yield return new WaitForSeconds(1f);
                            if (Vector3.Distance(transform.position, targetNode.transform.position) < 1f)
                            {
                                break;
                            }
                        }

                        SwitchToBehaviourStateCustom(State.Roaming);
                        break;
                    }
                }
            }
        }

        void TargetRandomNode()
        {
            logger.LogDebug("Choosing new target node...");

            GameObject[] nodes;
            if (isOutside)
            {
                nodes = RoundManager.Instance.outsideAINodes;
            }
            else
            {
                nodes = RoundManager.Instance.insideAINodes;
            }

            int randIndex = UnityEngine.Random.Range(0, nodes.Length);
            targetNode = nodes[randIndex].transform;
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
        {
            if (!isEnemyDead && !inSpecialAnimation)
            {
                enemyHP -= force;
                if (enemyHP <= 0 && IsServerOrHost)
                {
                    StopAllCoroutines();
                    AddThreat(playerWhoHit, -1);
                    KillEnemyOnOwnerClient();
                    return;
                }
            }
        }

        public override void CancelSpecialAnimationWithPlayer()
        {
            StopAllCoroutines();
            base.CancelSpecialAnimationWithPlayer();
        }

        public override void HitFromExplosion(float distance)
        {
            KillEnemyOnOwnerClient();
        }

        public override void OnCollideWithPlayer(Collider other) // This only runs on client
        {
            base.OnCollideWithPlayer(other);
            if (isEnemyDead) { return; }
            if (timeSincePlayerCollision > 5f)
            {
                timeSincePlayerCollision = 0f;
                if (currentBehaviourStateIndex == (int)State.Attacking)
                {
                    PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                    if (player != null && PlayerIsTargetable(player) && player == targetPlayer)
                    {
                        musterRat = false;
                        int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                        player.DamagePlayer(1, true, true, CauseOfDeath.Mauling, deathAnim);
                    }
                }
                else
                {
                    PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                    if (player != null && PlayerIsTargetable(player))
                    {
                        timeSinceAddThreat = 0f;
                        AddThreatServerRpc(player.actualClientId);
                    }
                }
            }
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);
        }

        void AddThreat(EnemyAI enemy)
        {

        }

        void AddThreat(PlayerControllerB player, int amount = 1)
        {
            if (player == null) { return; }
            timeSinceAddThreat = 0f;

            if (Nest != null && !Nest.Mustering)
            {
                if (!Nest.PlayerThreatCounter.ContainsKey(player))
                {
                    Nest.PlayerThreatCounter.Add(player, amount);
                }
                else
                {
                    Nest.PlayerThreatCounter[player] += amount;
                }

                int threat = Nest.PlayerThreatCounter[player];
                logger.LogDebug($"{player.playerUsername}: {threat} threat");

                if (threat > highThreatToAttackPlayer && Nest.LeadMusterRat == null && Nest.CanMuster)
                {
                    targetPlayer = player;
                    Nest.LeadMusterRat = this;
                    SwitchToBehaviourStateCustom(State.Mustering);
                    return;
                }

                if (Nest.PlayerThreatCounter[player] > threatToAttackPlayer)
                {
                    targetPlayer = player;
                    SwitchToBehaviourStateCustom(State.Swarming);
                }
            }
        }

        // RPC's

        [ClientRpc]
        public void OpenVentClientRpc(int ventIndex)
        {
            EnemyVent vent = RoundManager.Instance.allEnemyVents[ventIndex];
            vent.ventIsOpen = true;
            vent.ventAnimator.SetTrigger("openVent");
            vent.lowPassFilter.lowpassResonanceQ = 0f;
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