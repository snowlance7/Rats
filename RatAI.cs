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
        public static bool testing = true;

#pragma warning disable 0649
        public ScanNodeProperties ScanNode = null!;
        public AudioClip[] SqueekSFX = null!;
#pragma warning restore 0649

        EnemyAI? targetEnemy = null!;

        SewerGrate? Nest;
        EnemyVent? FromVent
        {
            get
            {
                return GetClosestVentToPosition(transform.position);
            }
        }

        EnemyVent? ToVent
        {
            get
            {
                return GetClosestVentToPosition(finalDestination);
            }
        }

        Vector3 finalDestination;

        float timeSincePlayerCollision;
        float timeSinceAddThreat;
        //float timeSinceNestCheck;

        bool sickRat;
        State defaultState;
        RatType RatTypeState;
        Coroutine? ratCoroutine = null;
        //Coroutine? returnToNestCoroutine = null;

        bool rallyRat;
        //bool returningToNest;
        //bool failedReturnToNest;

        bool AtNest { get { return Nest != null && Vector3.Distance(transform.position, Nest.transform.position) < 3f; } }

        // Constants

        // Config Values
        float sickRatChance = 0.1f;
        float defenseRadius = 5f;
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
            Rallying
        }

        public void SwitchToBehaviourStateCustom(State state)
        {
            logIfDebug("Switching to state: " + state);
            StopRoutines();

            switch (state)
            {
                case State.Roaming:
                    rallyRat = false;


                    break;
                case State.Tasking:
                    rallyRat = false;
                    AssignRatType();

                    break;
                case State.Swarming:
                    break;
                case State.Attacking:
                    break;
                case State.Rallying:
                    rallyRat = true;

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
                logIfDebug($"Rat spawned");
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

            //timeSinceNestCheck += Time.deltaTime;
            timeSincePlayerCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
            creatureAnimator.SetBool("stunned", stunNormalizedTimer > 0f);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            /*if (Nest != null && Vector3.Distance(transform.position, Nest.transform.position) < defenseRadius && TargetPlayerNearNest())
            {
                SwitchToBehaviourStateCustom(State.Attacking);
                return;
            }*/

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:

                    if (AtNest)
                    {
                        SwitchToBehaviourStateCustom(State.Tasking);
                        return;
                    }

                    if (Nest == null) { TryGetAnyNest(); }

                    if (Nest != null && SetDestinationToPosition(Nest.transform.position))
                    {
                        StopRoutines();
                        SetStatus("Returning to nest");
                    }
                    else
                    {
                        StartRoam();
                    }

                    break;
                case (int)State.Tasking:
                    //CheckForThreats();

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
                    if (!rallyRat && RatTypeState == RatType.DefenseRat && Vector3.Distance(Nest.transform.position, transform.position) > defenseRadius * 2)
                    {
                        SwitchToBehaviourStateCustom(State.Roaming);
                        return;
                    }
                    if (targetPlayer != null)
                    {
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats * 2 && !rallyRat)
                        {
                            // Target too far, return to nest
                            targetPlayer = null;
                            movingTowardsTargetPlayer = false;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius * 2) <= ratsNeededToAttack / 2.5 && !rallyRat)
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
                case (int)State.Rallying:
                    

                    /*if (AtNest)
                    {
                        if ()
                    }

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
                                if (Nest.CanRally && !Nest.IsRallying && Nest.LeadRallyRat == this)
                                {
                                    Nest.StartRallyTimer();
                                }
                                return;
                            }

                            SetDestinationToPosition(GetNavMeshPosition(Nest.transform.position));
                        }
                    }*/
                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        bool SetDestinationToPosition(Vector3 position)
        {
            position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
            finalDestination = position;
            path1 = new NavMeshPath();
            if (!agent.CalculatePath(position, path1)
                || Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.55f)
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

        bool TryGetAnyNest()
        {
            Nest = SewerGrate.Nests.FirstOrDefault();
            return Nest != null;
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
                logIfDebug("Rat defense assigned");
                RatTypeState = RatType.DefenseRat;
                Nest.DefenseRats.Add(this);
                ratCoroutine = StartCoroutine(SwarmCoroutine(Nest.transform.position, defenseRadius));
                SetStatus("Defending");
                return;
            }
            if (Nest.ScoutRats.Count() < maxScoutRats)
            {
                logIfDebug("Rat scout assigned");
                RatTypeState = RatType.ScoutRat;
                Nest.ScoutRats.Add(this);
                ratCoroutine = StartCoroutine(ScoutCoroutine());
                SetStatus("Scouting");
                return;
            } // TESTING

            logIfDebug("Search rat assigned");
            RatTypeState = RatType.SearchRat;
            StartSearch(Nest!.transform.position, Nest.RatSearchRoutine);
            SetStatus("Searching");
        }

        public override void ReachedNodeInSearch()
        {
            base.ReachedNodeInSearch();
            logIfDebug($"Rat{thisEnemyIndex}: {Nest!.RatSearchRoutine.nodesEliminatedInCurrentSearch} nodes searched!");
        }

        // Vents

        EnemyVent? GetClosestVentToPosition(Vector3 pos)
        {
            float mostOptimalDistance = 2000f;
            EnemyVent? targetVent = null;
            foreach (var vent in RoundManager.Instance.allEnemyVents)
            {
                float distance = Vector3.Distance(pos, vent.floorNode.transform.position);

                if (CalculatePath(pos, vent.floorNode.transform.position) && distance < mostOptimalDistance)
                {
                    mostOptimalDistance = distance;
                    targetVent = vent;
                }
            }

            return targetVent;
        }

        bool CalculatePath(Vector3 fromPos, Vector3 toPos)
        {
            Vector3 from = RoundManager.Instance.GetNavMeshPosition(fromPos, RoundManager.Instance.navHit, 1.75f);
            Vector3 to = RoundManager.Instance.GetNavMeshPosition(toPos, RoundManager.Instance.navHit, 1.75f);

            path1 = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, agent.areaMask, path1) && Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(to, RoundManager.Instance.navHit, 2.7f)) <= 1.55f; // TODO: Test this
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
                if (Vector3.Distance(transform.position, nest.transform.position) < 3f)
                {
                    Nest = nest;
                    return true;
                }
            }
            return false;
        }

        void WarpToVent(EnemyVent fromVent, EnemyVent toVent)
        {
            OpenVent(fromVent);
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

        /*void TryReturnToNest()
        {
            if (returnToNestCoroutine == null)
            {
                if (roamCoroutine != null)
                {
                    StopCoroutine(roamCoroutine);
                }

                returningToNest = true;
                failedReturnToNest = false;
                returnToNestCoroutine = StartCoroutine(TryReturnToNestCoroutine());
            }
        }

        IEnumerator TryReturnToNestCoroutine() // TODO: Try to rework everything so they work like this
        {
            yield return null;

            if (Nest == null)
            {
                if (!TryGetAnyNest())
                {
                    failedReturnToNest = true;
                    returningToNest = false;
                    returnToNestCoroutine = null;
                    yield break;
                }
            }

            Vector3 nestPos = GetNavMeshPosition(Nest.transform.position);
            if (SetDestinationToPosition(nestPos, true))
            {
                while (SetDestinationToPosition(nestPos, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, nestPos) < 1f)
                    {
                        logIfDebug("Returned to nest");
                        returningToNest = false;
                        returnToNestCoroutine = null;
                        yield break;
                    }
                }
            }

            EnemyVent? VentFrom = GetClosestVentToPosition(transform.position);
            EnemyVent? VentTo = GetClosestVentToPosition(Nest.transform.position);

            if (VentFrom != null && VentTo != null)
            {
                logIfDebug("GOT VENTS");
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
                    logIfDebug("Couldnt get to fromVent");
                    failedReturnToNest = true;
                    returningToNest = false;
                    returnToNestCoroutine = null;
                    yield break;
                }

                WarpToVent(VentFrom, VentTo);

                Vector3 targetNodePos = GetNavMeshPosition(Nest.transform.position);
                while (SetDestinationToPosition(targetNodePos, true))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, Nest.transform.position) < 1f)
                    {
                        logIfDebug("Returned to nest");
                        returnToNestCoroutine = null;
                        returningToNest = false;
                        yield break;
                    }
                }

                logIfDebug("Failed to return to nest");
                returnToNestCoroutine = null;
                failedReturnToNest = true;
                returningToNest = false;
                //StartRoam();
            }
        }*/

        void SetStatus(string status)
        {
            if (testing)
            {
                ScanNode.subText = status;
            }
        }

        void StartRoam()
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
                TargetRandomNode();
                Vector3 position = GetNavMeshPosition(targetNode.transform.position);
                while (SetDestinationToPosition(position))
                {
                    yield return new WaitForSeconds(1f);
                    if (Vector3.Distance(transform.position, position) < 1f)
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
            while (ratCoroutine != null)
            {
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(position, true))
                {
                    yield return null;
                    if (Vector3.Distance(transform.position, position) < 1f)
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
            while (ratCoroutine != null)
            {
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(enemy.transform.position, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(position, true))
                {
                    yield return null;
                    if (Vector3.Distance(transform.position, position) < 1f)
                    {
                        // TODO: play squeeksfx
                        break;
                    }
                }
            }
        }

        IEnumerator SwarmCoroutine(Vector3 position, float radius)
        {
            yield return null;
            while (ratCoroutine != null)
            {
                Vector3 pos = position;
                pos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(pos, true))
                {
                    yield return null;
                    if (Vector3.Distance(transform.position, pos) < 1f)
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
            while (ratCoroutine != null)
            {
                TargetRandomNode();
                Vector3 position = targetNode.transform.position;
                while (SetDestinationToPosition(position))
                {
                    yield return null;
                    if (Vector3.Distance(transform.position, position) < 1f)
                    {
                        // TODO: play squeeksfx
                        Nest.ScoutRats.Remove(this);
                        SwitchToBehaviourStateCustom(State.Roaming);
                        ratCoroutine = null;
                        yield break;
                    }
                }
            }
        }

        void TargetRandomNode()
        {
            logIfDebug("Choosing new target node...");

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
                if (enemyHP <= 0)
                {
                    KillEnemyOnOwnerClient();
                    return;
                }
            }
        }

        public void StopRoutines()
        {
            if (ratCoroutine != null)
            {
                StopCoroutine(ratCoroutine);
                ratCoroutine = null;
            }
            StopSearch(currentSearch);
        }

        public override void CancelSpecialAnimationWithPlayer()
        {
            StopRoutines();
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
                        rallyRat = false;
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

            if (Nest != null && !Nest.IsRallying)
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
                logIfDebug($"{player.playerUsername}: {threat} threat");

                if (threat > highThreatToAttackPlayer && Nest.LeadRallyRat == null && Nest.CanRally)
                {
                    targetPlayer = player;
                    Nest.LeadRallyRat = this;
                    SwitchToBehaviourStateCustom(State.Rallying);
                    return;
                }

                if (Nest.PlayerThreatCounter[player] > threatToAttackPlayer)
                {
                    targetPlayer = player;
                    SwitchToBehaviourStateCustom(State.Swarming);
                }
            }
        }

        void logIfDebug(string message)
        {
            if (testing)
            {
                LoggerInstance.LogDebug(message);
            }
        }

        public override void OnDestroy()
        {
            StopRoutines();
            base.OnDestroy();
        }

        // RPC's

        [ClientRpc]
        public void PlaySqueekSFXClientRpc(int index)
        {

        }

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