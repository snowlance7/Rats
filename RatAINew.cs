using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;

// Cheese to lure the rats away from the grate so the player can block it
// Rats will scout and do a normal search, if they see a player after 20 seconds, they will add a counter to the threatcounter, when max threat, they will run back to sewer and gather rats they touch and get defending rats and swarm around the player for 20 seconds, any rats nearby will also swarm. then attack.

namespace Rats
{
    internal class RatAINew : NetworkBehaviour
    {
        public static bool testing = true;

#pragma warning disable 0649
        public GameObject NestPrefab = null!;
        public ScanNodeProperties ScanNode = null!;
        public AudioClip[] SqueakSFX = null!;
        public AudioClip[] AttackSFX = null!;
        public AudioClip[] HitSFX = null!;
        public AudioClip[] NibbleSFX = null!;
        public Transform TurnCompass = null!;
        public Transform RatMouth = null!;
#pragma warning restore 0649

        EnemyAI? targetEnemy = null!;
        bool holdingFood;

        SewerGrateNew Nest = null!;
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

        float timeSinceCollision;
        float timeSinceAddThreat;
        float timeSincePounce;
        float timeSinceSyncedAIInterval;

        Coroutine? ratCoroutine = null;
        bool sickRat;
        bool rallyRat;
        bool grabbingBody;
        bool returningBodyToNest;

        bool AtNest { get { return agent.enabled && Nest != null && Vector3.Distance(transform.position, Nest.transform.position) < 3f; } }

        // Constants
        const float stuckTimeFailsafe = 2f;

        // Config Values
        float pounceAttackCooldown = 5f;
        //float sickRatChance = 0.1f;
        float defenseRadius = 5f;
        float timeToIncreaseThreat = 3f;
        int threatToAttackPlayer = 100;
        int threatToAttackEnemy = 50;
        //int highThreatToAttackPlayer = 200;
        //int highThreatToAttackEnemy = 100;
        float swarmRadius = 10f;
        bool canVent = true;
        bool defenseRats = true;
        int maxDefenseRats = 10;
        bool scoutRats = true;
        int maxScoutRats = 10;
        int ratsNeededToAttack = 10;
        float distanceToLoseRats = 15f;
        int enemyHitsToDoDamage = 10;
        int playerFoodAmount = 30;

        RatType RatTypeState = RatType.Unassigned;

        public enum RatType
        {
            Unassigned,
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
            if (currentBehaviourStateIndex == (int)state) { return; }
            loggerIfDebug("Switching to state: " + state);
            StopRoutines();
            
            switch (state)
            {
                case State.Roaming:
                    rallyRat = false;
                    AssignRatType();

                    break;
                case State.Tasking:
                    rallyRat = false;
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
                case State.Rallying:
                    agent.speed = 7;
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

            RoundManager.Instance.SpawnedEnemies.Add(this);
            loggerIfDebug($"Rat spawned");
            HottestRat();

            if (IsServerOrHost)
            {
                SpawnNestIfNonExist();
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

            if (timeSinceSyncedAIInterval > AIIntervalTime)
            {
                timeSinceSyncedAIInterval = 0f;
                DoSyncedAIInterval();
            }

            timeSinceSyncedAIInterval += Time.deltaTime;
            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
            timeSincePounce += Time.deltaTime;
            creatureAnimator.SetBool("stunned", stunNormalizedTimer > 0f);
            creatureAnimator.SetBool("runBackwards", returningBodyToNest && stunNormalizedTimer <= 0f);

            if (returningBodyToNest)
            {
                TurnCompass.LookAt(targetPlayer.deadBody.grabBodyObject.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, TurnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }
        }

        public void DoSyncedAIInterval()
        {
            if (stunNormalizedTimer > 0f)
            {
                agent.speed = 0f;
            }

            if (localPlayer.HasLineOfSightToPosition(transform.position))
            {
                if (currentBehaviourStateIndex == (int)State.Attacking)
                {
                    localPlayer.IncreaseFearLevelOverTime(0.05f);
                }
                else if (currentBehaviourStateIndex == (int)State.Swarming)
                {
                    localPlayer.IncreaseFearLevelOverTime(0.2f);
                }
                else
                {
                    localPlayer.IncreaseFearLevelOverTime(0.02f);
                }
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || stunNormalizedTimer > 0f)
            {
                return;
            };

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:
                    agent.speed = 5f;

                    if (AtNest)
                    {
                        if (returningBodyToNest)
                        {
                            DropBody(true);
                            Nest.AddFood(playerFoodAmount);
                        }
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
                        if (Vector3.Distance(targetPlayer.transform.position, transform.position) > distanceToLoseRats && !rallyRat)
                        {
                            // Target too far, return to nest
                            targetPlayer = null;
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                        if (NearbyRatCount(swarmRadius) <= ratsNeededToAttack / 2 && !rallyRat)
                        {
                            // Check if rats are defending base, keep attacking if player is near base
                            if (!TargetPlayerNearNest())
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
                case (int)State.Rallying:
                    agent.speed = 7f;

                    break;

                default:
                    LoggerInstance.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        void GrabBody()
        {
            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                GrabbableObject deadBody = targetPlayer.deadBody.grabBodyObject;
                targetPlayer.deadBody.canBeGrabbedBackByPlayers = false;
                deadBody.grabbable = false;
                deadBody.grabbableToEnemies = false;
                deadBody.hasHitGround = false;
                deadBody.parentObject = RatMouth;
                deadBody.transform.SetParent(RatMouth);
                deadBody.GrabItemFromEnemy(this);
                grabbingBody = false;
                returningBodyToNest = true;
                GrabBodyClientRpc(targetPlayer.actualClientId);
            }
        }

        void DropBody(bool deactivate = false)
        {
            if (targetPlayer != null && returningBodyToNest)
            {
                GrabbableObject deadBodyObj = targetPlayer.deadBody.grabBodyObject;
                if (deactivate)
                {
                    deadBodyObj.parentObject = null;
                    deadBodyObj.transform.SetParent(null, worldPositionStays: true);
                    targetPlayer.deadBody.DeactivateBody(setActive: false);
                }
                else
                {
                    deadBodyObj.parentObject = null;
                    deadBodyObj.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
                    //deadBodyObj.EnablePhysics(enable: true);
                    deadBodyObj.fallTime = 0f;
                    deadBodyObj.startFallingPosition = deadBodyObj.transform.parent.InverseTransformPoint(deadBodyObj.transform.position);
                    deadBodyObj.targetFloorPosition = deadBodyObj.transform.parent.InverseTransformPoint(transform.position);
                    deadBodyObj.floorYRot = -1;
                    deadBodyObj.DiscardItemFromEnemy();
                    targetPlayer.deadBody.canBeGrabbedBackByPlayers = false;
                    deadBodyObj.grabbable = false;
                    deadBodyObj.grabbableToEnemies = false;
                }
                returningBodyToNest = false;
                grabbingBody = false;
                DropBodyClientRpc(targetPlayer.actualClientId, deactivate);
                targetPlayer = null;
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

        void SpawnNestIfNonExist()
        {
            if (SewerGrateNew.Nests.FirstOrDefault() == null)
            {
                Instantiate(NestPrefab, transform.position, Quaternion.identity).GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
            }
        }

        bool TryGetAnyNest()
        {
            Nest = SewerGrateNew.Nests.FirstOrDefault();
            return Nest != null;
        }

        void HottestRat()
        {
            if (Random.Range(0f, 1f) < 0.01f)
            {
                ScanNode.headerText = "HottestRat";
            }
        }

        bool CheckLineOfSightForDeadBody()
        {
            foreach (var deadBody in FindObjectsOfType<DeadBodyInfo>())
            {
                if (deadBody.deactivated) { continue; }
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
                    returningBodyToNest = true;
                    return;
                }
                PlayerControllerB player = CheckLineOfSightForPlayer(60f, 60, 5);
                if (player != null && PlayerIsTargetable(player))
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
            int count = 1;
            foreach (var rat in FindObjectsOfType<RatAI>())
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
            if (Nest == null)
            {
                RatTypeState = RatType.Unassigned;
                SwitchToBehaviourStateCustom(State.Roaming);
                return;
            }

            Nest.RallyRats.Remove(this);
            Nest.DefenseRats.Remove(this);
            Nest.ScoutRats.Remove(this);

            if (!AtNest)
            {
                RatTypeState = RatType.Unassigned;
                return;
            }

            if (holdingFood)
            {
                holdingFood = false;
                Nest.AddFood();
            }

            if (Nest.DefenseRats.Count() < maxDefenseRats)
            {
                loggerIfDebug("Rat defense assigned");
                RatTypeState = RatType.DefenseRat;
                Nest.DefenseRats.Add(this);


                ratCoroutine = StartCoroutine(SwarmCoroutine(Nest.transform.position, defenseRadius));
                SetStatus("Defending");
                return;
            }
            loggerIfDebug("Rat scout assigned");
            RatTypeState = RatType.ScoutRat;
            Nest.ScoutRats.Add(this);
            ratCoroutine = StartCoroutine(ScoutCoroutine());
            SetStatus("Scouting");
            return;/* TESTING
            if (Nest.ScoutRats.Count() < maxScoutRats)
            {
                loggerIfDebug("Rat scout assigned");
                RatTypeState = RatType.ScoutRat;
                Nest.ScoutRats.Add(this);
                ratCoroutine = StartCoroutine(ScoutCoroutine());
                SetStatus("Scouting");
                return;
            }

            

            loggerIfDebug("Search rat assigned");
            RatTypeState = RatType.SearchRat;
            StartSearch(Nest.transform.position);
            SetStatus("Searching");*/
        }

        public override void ReachedNodeInSearch()
        {
            base.ReachedNodeInSearch();
            PlaySqueakSFXClientRpc();
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

        public EnemyAI? CheckLineOfSightForEnemy(int range)
        {
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.enemyType == enemyType) { continue; }
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
                if (enemy.enemyType == enemyType) { continue; }
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    target = enemy;
                    closestDistance = distance;
                }
            }

            return target;
        }

        public bool PlayerIsTargetable(PlayerControllerB playerScript)
        {
            if (playerScript.isPlayerControlled && playerScript.inAnimationWithEnemy == null && playerScript.sinkingValue < 0.73f && currentBehaviourStateIndex != (int)State.Roaming)
            {
                return true;
            }
            return false;
        }

        bool TargetPlayerNearNest()
        {
            if (Nest == null) { return false; }
            //PlayerControllerB tempPlayer = targetPlayer;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
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
            foreach (var nest in SewerGrateNew.Nests)
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
            Vector3 warpPos = RoundManager.Instance.GetNavMeshPosition(ToVent.floorNode.transform.position, RoundManager.Instance.navHit, 1.75f);
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

        void SetStatus(string status)
        {
            if (testing)
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
                Vector3 position = RoundManager.Instance.GetNavMeshPosition(targetNode.position, RoundManager.Instance.navHit, 1.75f, agent.areaMask);
                while (SetDestinationToPosition(position))
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (Vector3.Distance(transform.position, position) < 1f || timeStuck > stuckTimeFailsafe)
                    {
                        PlaySqueakSFXClientRpc();
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
            loggerIfDebug("Starting swarm on player");
            while (ratCoroutine != null && player != null)
            {
                float timeStuck = 0f;
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(position, true))
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (Vector3.Distance(transform.position, position) < 1f || timeStuck > stuckTimeFailsafe)
                    {
                        if (currentBehaviourStateIndex != (int)State.Attacking)
                        {
                            PlaySqueakSFXClientRpc(true);
                        }
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
                while (SetDestinationToPosition(position, true))
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (Vector3.Distance(transform.position, position) < 1f || timeStuck > stuckTimeFailsafe)
                    {
                        if (currentBehaviourStateIndex != (int)State.Attacking)
                        {
                            PlaySqueakSFXClientRpc(true);
                        }
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
                float timeStuck = 0f;
                Vector3 pos = position;
                pos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, radius, RoundManager.Instance.navHit);
                while (SetDestinationToPosition(pos, true))
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (Vector3.Distance(transform.position, pos) < 1f || timeStuck > stuckTimeFailsafe)
                    {
                        PlaySqueakSFXClientRpc(true);
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
                Vector3 position = targetNode.transform.position;
                while (SetDestinationToPosition(position))
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (Vector3.Distance(transform.position, position) < 1f || timeStuck > stuckTimeFailsafe)
                    {
                        PlaySqueakSFXClientRpc();
                        Nest.ScoutRats.Remove(this);
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

        void TargetRandomNode()
        {
            loggerIfDebug("Choosing new target node...");

            GameObject[] nodes = !isOutside ? GameObject.FindGameObjectsWithTag("AINode") : GameObject.FindGameObjectsWithTag("OutsideAINode");

            int randIndex = Random.Range(0, nodes.Length);
            targetNode = nodes[randIndex].transform;
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
        {
            if (!isEnemyDead && !inSpecialAnimation)
            {
                enemyHP -= force;
                if (enemyHP <= 0)
                {
                    RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
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
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            KillEnemyOnOwnerClient();
        }

        bool PlayerIsNearNest(PlayerControllerB player)
        {
            return Nest != null && Vector3.Distance(Nest.transform.position, player.transform.position) < defenseRadius;
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            if (isEnemyDead || currentBehaviourStateIndex == (int)State.Roaming || Nest == null) { return; }
            if (timeSinceCollision > 1f)
            {
                PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                if (player != null && PlayerIsTargetable(player))
                {
                    if (!IsServerOrHost)
                    {
                        timeSinceCollision = 0f;
                    }
                    CollideWithPlayerServerRpc(player.actualClientId);
                }
            }
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);
            if (IsServerOrHost)
            {
                if (isEnemyDead || currentBehaviourStateIndex == (int)State.Roaming || Nest == null) { return; }
                if (timeSinceCollision > 1f)
                {
                    if (collidedEnemy != null && collidedEnemy.enemyType != enemyType && collidedEnemy.enemyType.canDie && !(stunNormalizedTimer > 0f))
                    {
                        loggerIfDebug("Collided with: " + collidedEnemy.enemyType.enemyName);
                        timeSinceCollision = 0f;

                        if (collidedEnemy.isEnemyDead)
                        {
                            if (SewerGrateNew.EnemyFoodAmount.ContainsKey(collidedEnemy))
                            {
                                if (SewerGrateNew.EnemyFoodAmount[collidedEnemy] <= 1)
                                {
                                    holdingFood = SewerGrateNew.EnemyFoodAmount[collidedEnemy] == 1;
                                    collidedEnemy.NetworkObject.Despawn(true);
                                    return;
                                }
                                else
                                {
                                    SewerGrateNew.EnemyFoodAmount[collidedEnemy] -= 1;
                                    holdingFood = true;
                                }
                            }
                            else
                            {
                                SewerGrateNew.EnemyHitCount.Remove(collidedEnemy);
                                Nest.AddEnemyFoodAmount(collidedEnemy);
                                SewerGrateNew.EnemyFoodAmount[collidedEnemy] -= 1;
                                holdingFood = true;
                            }

                            SwitchToBehaviourStateCustom(State.Roaming);
                        }
                        else
                        {
                            if (!SewerGrateNew.EnemyHitCount.ContainsKey(collidedEnemy))
                            {
                                SewerGrateNew.EnemyHitCount.Add(collidedEnemy, enemyHitsToDoDamage);
                            }

                            SewerGrateNew.EnemyHitCount[collidedEnemy] -= 1;

                            if (SewerGrateNew.EnemyHitCount[collidedEnemy] <= 0)
                            {
                                collidedEnemy.HitEnemy(1, null, true);
                                SewerGrateNew.EnemyHitCount[collidedEnemy] = enemyHitsToDoDamage;
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

            if (Nest != null && !Nest.IsRallying)
            {
                timeSinceAddThreat = 0f;

                if (Nest.EnemyThreatCounter.ContainsKey(enemy))
                {
                    Nest.EnemyThreatCounter[enemy] += amount;
                }
                else
                {
                    Nest.EnemyThreatCounter.Add(enemy, amount);
                }

                PlaySqueakSFXClientRpc(true);

                int threat = Nest.EnemyThreatCounter[enemy];
                loggerIfDebug($"{enemy.enemyType.enemyName}: {threat} threat");

                if (RatTypeState == RatType.DefenseRat) { return; }

                /*if (threat > highThreatToAttackPlayer && Nest.LeadRallyRat == null && Nest.CanRally)
                {
                    targetPlayer = player;
                    Nest.LeadRallyRat = this;
                    SwitchToBehaviourStateCustom(State.Rallying);
                    return;
                }*/

                if (Nest.EnemyThreatCounter[enemy] > threatToAttackPlayer || enemy.isEnemyDead)
                {
                    SetTarget(enemy);
                    SwitchToBehaviourStateCustom(State.Swarming);
                }
            }
        }

        void AddThreat(PlayerControllerB player, int amount = 1)
        {
            if (player == null) { return; }

            if (Nest != null && !Nest.IsRallying)
            {
                timeSinceAddThreat = 0f;

                if (Nest.PlayerThreatCounter.ContainsKey(player))
                {
                    Nest.PlayerThreatCounter[player] += amount;
                }
                else
                {
                    Nest.PlayerThreatCounter.Add(player, amount);
                }

                PlaySqueakSFXClientRpc(true);

                int threat = Nest.PlayerThreatCounter[player];
                loggerIfDebug($"{player.playerUsername}: {threat} threat");

                if (RatTypeState == RatType.DefenseRat) { return; }

                /*if (threat > highThreatToAttackPlayer && Nest.LeadRallyRat == null && Nest.CanRally)
                {
                    targetPlayer = player;
                    Nest.LeadRallyRat = this;
                    SwitchToBehaviourStateCustom(State.Rallying);
                    return;
                }*/

                if (Nest.PlayerThreatCounter[player] > threatToAttackPlayer || player.isPlayerDead)
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

        void loggerIfDebug(string message)
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

        [ServerRpc(RequireOwnership = false)]
        public void CollideWithPlayerServerRpc(ulong clientId)
        {
            if (IsServerOrHost)
            {
                if (timeSinceCollision > 1f)
                {
                    timeSinceCollision = 0f;
                    PlayerControllerB player = PlayerFromId(clientId);

                    if (player.isPlayerDead)
                    {
                        loggerIfDebug("Collided with PLAYER BODY"); // Testing
                        return;
                    }

                    if (currentBehaviourStateIndex == (int)State.Attacking || PlayerIsNearNest(player))
                    {
                        if (targetPlayer != null && player == targetPlayer)
                        {
                            rallyRat = false;
                            if (Nest != null)
                            {
                                Nest.RallyRats.Remove(this);
                            }
                        }

                        DamagePlayerClientRpc(clientId);
                    }
                }
            }
        }

        [ClientRpc]
        public void DamagePlayerClientRpc(ulong clientId, int damage = 1)
        {
            if (localPlayer.actualClientId == clientId)
            {
                int deathAnim = Random.Range(0, 2) == 1 ? 7 : 0;
                localPlayer.DamagePlayer(damage, true, true, CauseOfDeath.Mauling, deathAnim);
            }

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

        [ClientRpc]
        public void SetNestClientRpc(NetworkObjectReference nestRef)
        {
            if (nestRef.TryGet(out NetworkObject netObj))
            {
                Nest = netObj.GetComponent<SewerGrateNew>();
            }
        }

        [ClientRpc]
        public void GrabBodyClientRpc(ulong clientId)
        {
            if (!IsServerOrHost)
            {
                targetPlayer = PlayerFromId(clientId);

                if (targetPlayer != null && targetPlayer.deadBody != null)
                {
                    GrabbableObject deadBody = targetPlayer.deadBody.grabBodyObject;
                    targetPlayer.deadBody.canBeGrabbedBackByPlayers = false;
                    deadBody.grabbable = false;
                    deadBody.grabbableToEnemies = false;
                    deadBody.hasHitGround = false;
                    deadBody.parentObject = RatMouth;
                    deadBody.transform.SetParent(RatMouth);
                    deadBody.GrabItemFromEnemy(this);
                    grabbingBody = false;
                    returningBodyToNest = true;
                }
            }
        }

        [ClientRpc]
        public void DropBodyClientRpc(ulong clientId, bool deactivate)
        {
            if (!IsServerOrHost)
            {
                targetPlayer = PlayerFromId(clientId);

                if (targetPlayer != null)
                {
                    returningBodyToNest = false;

                    GrabbableObject deadBodyObj = targetPlayer.deadBody.grabBodyObject;
                    if (deactivate)
                    {
                        deadBodyObj.parentObject = null;
                        deadBodyObj.transform.SetParent(null, worldPositionStays: true);
                        targetPlayer.deadBody.DeactivateBody(setActive: false);
                    }
                    else
                    {
                        deadBodyObj.parentObject = null;
                        deadBodyObj.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
                        //deadBodyObj.EnablePhysics(enable: true);
                        deadBodyObj.fallTime = 0f;
                        deadBodyObj.startFallingPosition = deadBodyObj.transform.parent.InverseTransformPoint(deadBodyObj.transform.position);
                        deadBodyObj.targetFloorPosition = deadBodyObj.transform.parent.InverseTransformPoint(transform.position);
                        deadBodyObj.floorYRot = -1;
                        deadBodyObj.DiscardItemFromEnemy();
                        targetPlayer.deadBody.canBeGrabbedBackByPlayers = false;
                        deadBodyObj.grabbable = false;
                        deadBodyObj.grabbableToEnemies = false;
                    }
                    grabbingBody = false;
                    targetPlayer = null;
                }
            }
        }
    }
}

// TODO: statuses: shakecamera, playerstun, drunkness, fear, insanity