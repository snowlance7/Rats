using GameNetcodeStuff;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;
using static Rats.RatManager;
using static Rats.RatNest;

namespace Rats
{
    public class RatAI : NetworkBehaviour
    {
#pragma warning disable 0649
        public AudioClip[] SqueakSFX;
        public AudioClip[] AttackSFX;
        public AudioClip[] HitSFX;
        public AudioClip[] NibbleSFX;
        public AudioClip ScreamSFX;
        public AudioClip[] HappyBirthdayRatSFX;
        public Transform RatMouth;
        public GameObject ChristmasHat;

        public NavMeshAgent agent;
        public Animator creatureAnimator;
        public float AIIntervalTime;
        public Transform eye;
        public int enemyHP;
        public AudioSource creatureSFX;
        public AudioSource creatureVoice;
#pragma warning restore 0649

        public bool IsJermaRat;

        float updateDestinationInterval;
        public bool isEnemyDead;
        bool moveTowardsDestination;
        Vector3 destination;
        bool isOutside;
        Transform? targetNode;
        bool inSpecialAnimation;

        State currentBehaviorState = State.Routine;

        public PlayerControllerB? targetPlayer;
        public EnemyAI? targetEnemy;

        DeadBodyInfo? heldBody;
        bool holdingFood;

        int hashDie;

        bool grabbingBody;
        bool returningBodyToNest;

        public RatNest? Nest;

        float timeSinceCollision;
        float timeSinceAddThreat;
        float spinTimer;

        private NavMeshPath path1;
        Coroutine? ratCoroutine;
        //bool sickRat;
        public bool rallyRat;
        public bool defenseRat;
        bool pathingToNest;

        // Constants
        const float idleTime = 1f;

        // Config Values // TODO: Set up configs
        float swarmRadius = 10f;
        int maxDefenseRats = 10;
        float distanceToLoseRats = 25f;
        int ratDamage = 2;

        public enum State
        {
            ReturnToNest,
            Routine,
            Swarming
        }

        public void StopTaskRoutine()
        {
            StopAllCoroutines();
            ratCoroutine = null;
        }

        public void Start()
        {
            hashDie = Animator.StringToHash("die");

            AIIntervalTime = configAIIntervalTime.Value;
            swarmRadius = configSwarmRadius.Value;
            maxDefenseRats = configMaxDefenseRats.Value;
            distanceToLoseRats = configDistanceNeededToLoseRats.Value;
            ratDamage = configRatDamage.Value;
            ChristmasHat.SetActive(configHolidayRats.Value);

            updateDestinationInterval = AIIntervalTime;
            path1 = new NavMeshPath();

            currentBehaviorState = State.ReturnToNest;
            Nest = GetClosestNest(transform.position);
            RatManager.SpawnedRats.Add(this);

            if (IsServerOrHost)
            {
                SwitchToBehaviorState(State.Routine);
            }

            log($"Rat spawned");
        }

        public void Update()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
        }

        public void SwitchToBehaviorState(State state)
        {
            if (currentBehaviorState == state) { return; }

            StopTaskRoutine();

            switch (state)
            {
                case State.ReturnToNest:
                    
                    ResetRat();
                    Nest = GetClosestNest(transform.position, true);

                    break;
                case State.Routine:

                    if (Nest.DefenseRats.Count < maxDefenseRats) // defense
                    {
                        Nest.DefenseRats.Add(this);
                        defenseRat = true;
                        ratCoroutine = StartCoroutine(SwarmCoroutine(Nest, defenseRadius));
                        break;
                    }

                    ratCoroutine = StartCoroutine(ScoutCoroutine());
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

        public void ResetRat()
        {
            foreach (var nest in RatNest.Nests)
            {
                nest.DefenseRats.Remove(this);
            }

            rallyRat = false;
            defenseRat = false;
        }

        public void DoAIInterval()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || inSpecialAnimation)
            {
                return;
            };

            //LoggerInstance.LogDebug("Doing AI interval");

            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }

            switch (currentBehaviorState)
            {
                case (int)State.ReturnToNest:
                    agent.speed = 5f;

                    if (pathingToNest && ReachedDestination())
                    {
                        // Add food to nest
                        if (holdingFood)
                        {
                            Nest.AddFood();
                        }
                        holdingFood = false;

                        if (returningBodyToNest)
                        {
                            DropBody(true);
                            Nest.AddFood(playerFoodAmount);
                        }

                        pathingToNest = false;
                        SwitchToBehaviorState(State.Routine);
                        return;
                    }

                    if (Nest == null || !Nest.IsOpen || !SetDestinationToPosition(Nest.transform.position, checkForPath: true))
                    {
                        Nest = GetClosestNest(transform.position, true);
                        if (Nest == null)
                        {
                            pathingToNest = false;
                            Roam();
                            return;
                        }
                    }

                    pathingToNest = true;
                    RoamStop();

                    break;
                case State.Routine:
                    agent.speed = 5f;

                    if (grabbingBody)
                    {
                        GrabbableObject deadBody = targetPlayer.deadBody.grabBodyObject;
                        if (SetDestinationToPosition(deadBody.transform.position, true))
                        {
                            //if (Vector3.Distance(transform.position, deadBody.transform.position) < 1f)
                            if (ReachedDestination())
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
                        if (rallyRat)
                        {
                            if (!SetDestinationToPosition(targetPlayer.transform.position, true))
                            {
                                rallyRat = false;
                                SwitchToBehaviorState(State.ReturnToNest);
                            }
                            return;
                        }

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
                        if (rallyRat)
                        {
                            if (!SetDestinationToPosition(targetEnemy.transform.position, true))
                            {
                                rallyRat = false;
                                SwitchToBehaviorState(State.ReturnToNest);
                            }
                            return;
                        }

                        if (Vector3.Distance(targetEnemy.transform.position, transform.position) > distanceToLoseRats)
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
                    Nest = GetClosestNest(transform.position);
                    StopTaskRoutine();
                    grabbingBody = true;
                    return;
                }
                PlayerControllerB player = CheckLineOfSightForPlayer(60f, 60, 5);
                if (PlayerIsTargetable(player))
                {
                    if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.name == "RatCrown" && !player.currentlyHeldObjectServer.isPocketed)
                    {
                        targetPlayer = player;
                        targetEnemy = null;
                        SwitchToBehaviorState(State.Swarming);
                        return;
                    }

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

        void Roam()
        {
            if (ratCoroutine != null)
            {
                StopCoroutine(ratCoroutine);
            }

            ratCoroutine = StartCoroutine(RoamCoroutine());
        }

        void RoamStop()
        {
            if (ratCoroutine != null)
            {
                StopAllCoroutines();
                ratCoroutine = null;
            }
        }

        IEnumerator RoamCoroutine()
        {
            yield return null;
            while (ratCoroutine != null && agent.enabled)
            {
                float timeStopped = 0f;
                targetNode = GetRandomNode(isOutside);
                if (targetNode == null) { continue; }
                Vector3 position = targetNode.transform.position;
                if (!SetDestinationToPosition(position)) { continue; }
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStopped > idleTime)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStopped += AIIntervalTime;
                    }
                }
            }
        }

        IEnumerator SwarmCoroutine(PlayerControllerB player, float radius)
        {
            yield return null;
            log("Starting swarm on player");
            while (ratCoroutine != null && player != null)
            {
                if (player == null || player.isPlayerDead) { break; }
                float timeStopped = 0f;
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(player.transform.position, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(position, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStopped > idleTime)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStopped += AIIntervalTime;
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
                float timeStopped = 0f;
                Vector3 position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(enemy.transform.position, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(position, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStopped > idleTime)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStopped += AIIntervalTime;
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
                float timeStopped = 0f;
                Vector3 pos = position;
                pos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(pos, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStopped > idleTime)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStopped += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.ReturnToNest);
        }

        IEnumerator SwarmCoroutine(RatNest nest, float radius)
        {
            yield return null;
            while (ratCoroutine != null)
            {
                float timeStopped = 0f;
                Vector3 pos = nest.transform.position;
                pos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(pos, radius, RoundManager.Instance.navHit);
                SetDestinationToPosition(pos, false);
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStopped > idleTime)
                    {
                        break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStopped += AIIntervalTime;
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
                float timeStopped = 0f;
                targetNode = GetRandomNode(isOutside);
                if (targetNode == null) { continue; }
                Vector3 position = targetNode.transform.position;
                if (!SetDestinationToPosition(position)) { continue; }
                while (agent.enabled)
                {
                    yield return new WaitForSeconds(AIIntervalTime);
                    if (!agent.hasPath || timeStopped > idleTime)
                    {
                        SwitchToBehaviorState(State.ReturnToNest);
                        ratCoroutine = null;
                        yield break;
                    }

                    if (agent.velocity == Vector3.zero)
                    {
                        timeStopped += AIIntervalTime;
                    }
                }
            }

            SwitchToBehaviorState(State.ReturnToNest);
        }

        public void HitEnemy(int force = 0, PlayerControllerB? playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
        {
            if (isEnemyDead) { return; }
            enemyHP -= force;
            if (enemyHP > 0) { return; }
            DropBody();
            RoundManager.PlayRandomClip(creatureSFX, HitSFX, true, 1, -1);
            KillEnemyOnOwnerClient();
            if (IsServerOrHost && playerWhoHit != null)
            {
                AddThreat(playerWhoHit, 1);
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

        public void OnCollideWithPlayer(Collider other)
        {
            if (isEnemyDead) { return; }
            if (timeSinceCollision > 1f)
            {
                timeSinceCollision = 0f;
                PlayerControllerB? player = other.gameObject.GetComponent<PlayerControllerB>();
                if (player == null || !PlayerIsTargetable(player)) { return; }
                if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.name == "RatCrown" && !player.currentlyHeldObjectServer.isPocketed) { return; }
                rallyRat = false;
                if (currentBehaviorState == State.Swarming || IsPlayerNearANest(player)) // TODO: Test this
                {
                    RoundManager.PlayRandomClip(creatureSFX, AttackSFX);

                    if (player != localPlayer) { return; }
                    int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                    player.DamagePlayer(ratDamage, true, true, CauseOfDeath.Mauling, deathAnim);
                }
            }
        }

        public void OnCollideWithEnemy(Collider other, EnemyAI? collidedEnemy = null)
        {
            if (IsServerOrHost)
            {
                rallyRat = false;
                if (isEnemyDead || currentBehaviorState == State.ReturnToNest || Nest == null) { return; }
                if (timeSinceCollision > 1f)
                {
                    if (collidedEnemy != null && collidedEnemy.enemyType.canDie)
                    {
                        if (RatKingAI.Instance != null && RatKingAI.Instance == collidedEnemy) { return; }
                        log("Collided with: " + collidedEnemy.enemyType.enemyName);
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

            if (currentBehaviorState == State.Swarming)
            {
                RoundManager.PlayRandomClip(creatureSFX, AttackSFX);
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

                int threat = RatManager.EnemyThreatCounter[enemy];
                log($"{enemy.enemyType.enemyName}: {threat} threat");

                if (defenseRat) { return; }

                if (RatManager.EnemyThreatCounter[enemy] > threatToAttackEnemy || enemy.isEnemyDead)
                {
                    targetPlayer = null;
                    targetEnemy = enemy;
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

                int threat = RatManager.PlayerThreatCounter[player];
                log($"{player.playerUsername}: {threat} threat");

                if (defenseRat) { return; }

                if (threat > highThreatToAttackPlayer && RatKingAI.Instance != null && RatKingAI.Instance.currentBehaviourStateIndex == (int)RatKingAI.State.Roaming)
                {
                    if (RatKingAI.Instance.targetPlayer == null)
                    {
                        log("Calling Rat King");
                        targetEnemy = null;
                        targetPlayer = player;
                        PlayRallySFXClientRpc();
                        RatKingAI.Instance.AlertHighThreatPlayer(player);
                        SwitchToBehaviorState(State.Swarming);
                    }
                }
                if (threat > threatToAttackPlayer || player.isPlayerDead)
                {
                    targetEnemy = null;
                    targetPlayer = player;
                    SwitchToBehaviorState(State.Swarming);
                }
            }
        }

        public void FinishRunCycle() // Animation function
        {
            if (UnityEngine.Random.Range(0f, 1f) < squeakChance)
            {
                RoundManager.PlayRandomClip(creatureSFX, SqueakSFX);
            }
        }

        public void KillEnemyOnOwnerClient()
        {
            if (!IsServerOrHost)
            {
                return;
            }
            if (isEnemyDead)
            {
                return;
            }
            Debug.Log($"Kill enemy called!");

            KillEnemy();
            if (base.NetworkObject.IsSpawned)
            {
                KillEnemyServerRpc();
            }
        }

        public void KillEnemy()
        {
            isEnemyDead = true;
            creatureAnimator.SetTrigger(hashDie);
            creatureAnimator.Update(0);

            StopAllCoroutines();
            agent.enabled = false;
            RatManager.SpawnedRats.Remove(this);
            ResetRat();
        }

        // RPC's

        [ClientRpc]
        public void PlayRallySFXClientRpc()
        {
            creatureVoice.PlayOneShot(ScreamSFX);
        }

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
        public void KillEnemyServerRpc()
        {
            if (!IsServerOrHost) { return; }

            KillEnemyClientRpc();
        }

        [ClientRpc]
        public void KillEnemyClientRpc()
        {
            if (!isEnemyDead)
            {
                KillEnemy();
            }
        }
    }
}