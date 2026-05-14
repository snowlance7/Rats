using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static Rats.Plugin;
using static Rats.RatNest;
using static Rats.Configs;
using SnowyLib;
using Dawn.Utils;
using System;

namespace Rats
{
    public class RatAI : NetworkBehaviour
    {
        public static List<RatAI> Instances = [];

        public AudioClip[] squeakSFX = null!;
        public AudioClip[] attackSFX = null!;
        public AudioClip[] hitSFX = null!;
        public AudioClip[] nibbleSFX = null!;
        public AudioClip screamSFX = null!;

        public Transform ratMouthTransform = null!;
        public GameObject christmasHat = null!;

        public Animator? animator = null!;
        public Transform eye = null!;
        public AudioSource audioSource = null!;
        public SmartAgentNavigator nav = null!;
        [SerializeField] bool isJermaRat;
        
        //public NetworkVariable<int> Health = new NetworkVariable<int>(value: 100, writePerm: NetworkVariableWritePermission.Owner, readPerm: NetworkVariableReadPermission.Everyone);

        public bool isDead;

        GameObject? targetNode;
        bool moveTowardsDestination;
        Vector3 destination;

        bool isOutside;

        bool inSpecialAnimation;
        State previousBehaviorState;
        State currentBehaviorState = State.ReturnToNest;

        public PlayerControllerB? targetPlayer;
        //public EnemyAI? targetEnemy;

        DeadBodyInfo? heldBody;
        bool holdingFood;

        int hashDie;
        int hashSpeed;

        bool grabbingBody;
        bool returningBodyToNest;

        float timeSinceCollision;
        float timeSinceAddThreat;
        float timeSinceSwitchBehavior;
        float timeIdle;

        RatNest? closestNest;

        GameObject? swarmTarget;
        Vector3 swarmTargetPos;

        NavMeshPath path = new NavMeshPath();

        bool rallyRat;

        int health = 1;

        public enum State
        {
            ReturnToNest,
            Scouting,
            Swarming
        }

        // Config Values // TODO: Set up configs
        const float swarmRadiusAttacking = 3f;

        const float AIIntervalTime = 0.2f;
        private Vector3 lastPosition;
        private float currentSpeed;
        private NavMeshPath path1;
        const float defenseRadius = 5f;
        const float timeToIncreaseThreat = 3f;
        const int threatToAttackPlayer = 100;
        const int threatToAttackEnemy = 50;
        const int highPlayerThreat = 250;
        const int enemyHitsToDoDamage = 10;
        const int playerFoodAmount = 30;
        const float squeakChance = 0.1f;
        const float maxIdleTime = 1f;
        const float swarmRadiusDefault = 6f;

        public void Start()
        {
            hashDie = Animator.StringToHash("die");
            hashSpeed = Animator.StringToHash("speed");
            christmasHat.SetActive(cfgHolidayRats);
            logger.LogDebug($"Rat spawned");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instances.Add(this);
        }
        public override void OnNetworkDespawn()
        {
            ResetVariables();
            Instances.Remove(this);
            base.OnNetworkDespawn();
        }

        public void Update()
        {
            timeSinceCollision += Time.deltaTime;
            timeSinceAddThreat += Time.deltaTime;
            timeSinceSwitchBehavior += Time.deltaTime;
        }

        public void LateUpdate()
        {
            currentSpeed = ((transform.position - lastPosition).magnitude / Time.deltaTime) / 2;
            animator?.SetFloat(hashSpeed, currentSpeed);
            lastPosition = transform.position;

            timeIdle = currentSpeed > 0.1f ? 0f : maxIdleTime + Time.deltaTime;
        }

        public void DoAIInterval()
        {
            if (isDead || StartOfRound.Instance.allPlayersDead || inSpecialAnimation)
            {
                return;
            };

            if (!IsServer) { return; }

            switch (currentBehaviorState)
            {
                case State.ReturnToNest:
                    nav.agent.speed = 5f;

                    closestNest ??= GetClosestNestToPosition(transform.position);

                    if (closestNest == null)
                    {
                        EnemyVent closestVent = RoundManager.Instance.allEnemyVents.GetClosestToPosition(transform.position, (x) => x.transform.position)!;

                        nav.DoPathingToDestination(closestVent.floorNode.position);

                        if (timeIdle > maxIdleTime)
                        {
                            NetworkObject?.Despawn(destroy: true);
                        }

                        return;
                    }

                    nav.DoPathingToDestination(closestNest.transform.position);

                    if (timeIdle > maxIdleTime && ReachedDestination())
                    {
                        // Add food to nest
                        if (holdingFood)
                            closestNest.AddFood();
                        holdingFood = false;

                        if (returningBodyToNest)
                        {
                            DropBodyClientRpc(true);
                            closestNest.AddFood(playerFoodAmount);
                        }
                        
                        if (closestNest.DefenseRats.Count < cfgMaxDefenseRats)
                        {
                            swarmTarget = closestNest.gameObject;
                            SwitchToBehaviorClientRpc(State.Swarming);
                            closestNest.DefenseRats.Add(this);
                        }
                        else
                        {
                            SwitchToBehaviorClientRpc(State.Scouting);
                        }
                    }

                    break;
                case State.Scouting:
                    nav.agent.speed = 5f;

                    if (grabbingBody && targetPlayer != null)
                    {
                        GrabbableObject deadBody = targetPlayer.deadBody.grabBodyObject;
                        if (nav.TryDoPathingToDestination(deadBody.transform.position, out SmartAgentNavigator.GoToDestinationResult result) && result != SmartAgentNavigator.GoToDestinationResult.Failure)
                        {
                            if (ReachedDestination())
                            {
                                int limb = UnityEngine.Random.Range(0, targetPlayer.deadBody.bodyParts.Length);
                                GrabBodyClientRpc(targetPlayer.actualClientId, limb);
                                returningBodyToNest = true;
                            }
                            return;
                        }
                    }

                    CheckForThreatsInLOS();

                    // TODO: Switch out for pathfinding lib
                    if (timeIdle > maxIdleTime)
                        targetNode = Utils.insideAINodes.GetRandom();

                    if (targetNode != null)
                        nav.DoPathingToDestination(targetNode.transform.position);

                    break;
                case State.Swarming:
                    nav.agent.speed = 10f;

                    if (swarmTarget == null)
                    {
                        SwitchToBehaviorClientRpc((int)State.ReturnToNest);
                        return;
                    }

                    float swarmRadius = swarmRadiusAttacking;

                    if (swarmTarget.TryGetComponentInChildren(out PlayerControllerB player))
                    {
                        if (!player.isPlayerControlled || (timeIdle > maxIdleTime && timeSinceSwitchBehavior > maxIdleTime + 2f && Vector3.Distance(transform.position, player.transform.position) > cfgDistanceNeededToLoseRats))
                        {
                            SwitchToBehaviorClientRpc((int)State.ReturnToNest);
                            return;
                        }
                    }
                    else if (swarmTarget.TryGetComponentInChildren(out EnemyAICollisionDetect enemyAICollision))
                    {
                        EnemyAI enemy = enemyAICollision.mainScript;
                        if (enemy.isEnemyDead || (timeIdle > maxIdleTime && timeSinceSwitchBehavior > maxIdleTime + 1f && Vector3.Distance(transform.position, enemy.transform.position) > cfgDistanceNeededToLoseRats))
                        {
                            SwitchToBehaviorClientRpc((int)State.ReturnToNest);
                            return;
                        }
                    }
                    else
                    {
                        swarmRadius = swarmRadiusDefault;
                    }

                    if (timeIdle > maxIdleTime)
                        swarmTargetPos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(swarmTarget.transform.position, swarmRadius, RoundManager.Instance.navHit);

                    nav.DoPathingToDestination(swarmTargetPos);

                    break;
                default:
                    Plugin.logger.LogWarning("Invalid state: " + currentBehaviorState);
                    break;
            }
        }

        bool ReachedDestination()
        {
            // Check if we've reached the destination
            if (!nav.agent.pathPending)
            {
                if (nav.agent.remainingDistance <= nav.agent.stoppingDistance)
                {
                    if (!nav.agent.hasPath || nav.agent.velocity.sqrMagnitude == 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CheckLineOfSightForDeadBody()
        {
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                DeadBodyInfo deadBody = player.deadBody;
                if (deadBody == null || deadBody.deactivated) { continue; }
                path1 = new NavMeshPath();
                if (CheckLineOfSightForPosition(deadBody.grabBodyObject.transform.position, 60, 60, 5) && nav.CanPathToPoint(deadBody.grabBodyObject.transform.position))
                {
                    targetPlayer = deadBody.playerScript;
                    return true;
                }
            }
            return false;
        }

        void CheckForThreatsInLOS()
        {
            if (timeSinceAddThreat < timeToIncreaseThreat) { return; }
            if (CheckLineOfSightForDeadBody())
            {
                grabbingBody = true;
                return;
            }
            EnemyAI? enemy = CheckLineOfSightForEnemy(30);
            if (enemy != null && enemy.enemyType.canDie)
            {
                AddThreat(enemy);
            }
            PlayerControllerB? player = CheckLineOfSightForPlayer(60f, 60, 5);
            if (player != null && PlayerIsTargetable(player))
            {
                if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.itemProperties.name == "RatCrownItem" && !player.currentlyHeldObjectServer.isPocketed)
                {
                    targetPlayer = player;
                    SwitchToBehaviorClientRpc(State.Swarming);
                    return;
                }

                AddThreat(player);
                return;
            }
        }

        public bool CheckLineOfSightForPosition(Vector3 objectPosition, float width = 45f, int range = 60, float proximityAwareness = -1f, Transform? overrideEye = null)
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

        public PlayerControllerB? CheckLineOfSightForPlayer(float width = 45f, int range = 60, int proximityAwareness = -1)
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
                if (/*(RatKingAI.Instance != null && enemy == RatKingAI.Instance) || */!enemy.enemyType.canDie) { continue; }
                if (CheckLineOfSightForPosition(enemy.eye.position, 60, range, 5))
                {
                    return enemy;
                }
            }
            return null;
        }

        public void HitFromExplosion(float distance)
        {
            KillEnemyClientRpc();
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

        public void OnCollideWithPlayer(Collider other)
        {
            if (isDead) { return; }
            if (timeSinceCollision < 1f) { return; }

            timeSinceCollision = 0f;
            PlayerControllerB? player = other.gameObject.GetComponent<PlayerControllerB>();
            if (!PlayerIsTargetable(player)) { return; }
            if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.name == "RatCrownItem" && !player.currentlyHeldObjectServer.isPocketed) { return; }
            rallyRat = false;
            if (currentBehaviorState == State.Swarming) // TODO: Test this
            {
                RoundManager.PlayRandomClip(audioSource, attackSFX);

                if (player != localPlayer) { return; }
                int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                player.DamagePlayer(cfgRatDamage, true, true, CauseOfDeath.Mauling, deathAnim);
            }
        }

        public void OnCollideWithEnemy(Collider other, EnemyAI? collidedEnemy = null)
        {
            if (isDead || currentBehaviorState == State.ReturnToNest) { return; }
            if (timeSinceCollision < 1f) { return; }
            if (collidedEnemy == null || !collidedEnemy.enemyType.canDie) { return; }
            if (!cfgEnemyWhitelist.Contains(collidedEnemy.enemyType.name)) { return; }
            //if (collidedEnemy is RatKingAI) { return; }
            logger.LogDebug("Collided with: " + collidedEnemy.enemyType.enemyName);
            timeSinceCollision = 0f;

            if (collidedEnemy.isEnemyDead)
            {
                if (!RatManager.Instance!.enemyFoodPointsLeft.ContainsKey(collidedEnemy))
                {
                    RatManager.Instance.enemyThreatCounter.Remove(collidedEnemy);
                    RatManager.Instance.enemyHitCount.Remove(collidedEnemy);
                    RatManager.Instance.AddEnemyFoodAmount(collidedEnemy);
                }

                RatManager.Instance.enemyFoodPointsLeft[collidedEnemy] -= 1;
                holdingFood = true;

                if (RatManager.Instance.enemyFoodPointsLeft[collidedEnemy] <= 0)
                {
                    RatManager.Instance.enemyFoodPointsLeft.Remove(collidedEnemy);
                    RoundManager.Instance.DespawnEnemyOnServer(collidedEnemy.NetworkObject);
                }

                RoundManager.PlayRandomClip(audioSource, nibbleSFX);
                if (localPlayer.HasLineOfSightToPosition(transform.position))
                {
                    localPlayer.IncreaseFearLevelOverTime(0.8f);
                }

                if (IsServer)
                    SwitchToBehaviorClientRpc(State.ReturnToNest);
            }
            else
            {
                if (!RatManager.Instance.enemyThreatCounter.ContainsKey(collidedEnemy))
                {
                    RatManager.Instance.enemyThreatCounter.Add(collidedEnemy, 0);
                }

                if (RatManager.Instance.enemyThreatCounter[collidedEnemy] > threatToAttackEnemy)
                {
                    if (!RatManager.Instance.enemyHitCount.ContainsKey(collidedEnemy))
                    {
                        RatManager.Instance.enemyHitCount.Add(collidedEnemy, enemyHitsToDoDamage);
                    }

                    RatManager.Instance.enemyHitCount[collidedEnemy] -= 1;

                    if (RatManager.Instance.enemyHitCount[collidedEnemy] <= 0)
                    {
                        collidedEnemy.HitEnemy(1, null, true);
                        RatManager.Instance.enemyHitCount[collidedEnemy] = enemyHitsToDoDamage;
                    }
                }
            }

            if (currentBehaviorState == State.Swarming)
            {
                RoundManager.PlayRandomClip(audioSource, attackSFX);
            }
        }

        void AddThreat(EnemyAI enemy, int amount = 1)
        {
            if (enemy == null || RatManager.Instance == null) { return; }

            timeSinceAddThreat = 0f;

            if (RatManager.Instance.enemyThreatCounter.ContainsKey(enemy))
            {
                RatManager.Instance.enemyThreatCounter[enemy] += amount;
            }
            else
            {
                RatManager.Instance.enemyThreatCounter.Add(enemy, amount);
            }

            int threat = RatManager.Instance.enemyThreatCounter[enemy];
            logger.LogDebug($"{enemy.enemyType.enemyName}: {threat} threat");

            if (currentBehaviorState == State.Swarming) { return; }

            if (RatManager.Instance.enemyThreatCounter[enemy] > threatToAttackEnemy && IsServer)
            {
                swarmTarget = enemy.gameObject;
                
                SwitchToBehaviorClientRpc(State.Swarming);
            }
        }

        void AddThreat(PlayerControllerB player, int amount = 1)
        {
            if (player == null || RatManager.Instance == null) { return; }

            timeSinceAddThreat = 0f;

            if (RatManager.Instance.playerThreatCounter.ContainsKey(player))
            {
                RatManager.Instance.playerThreatCounter[player] += amount;
            }
            else
            {
                RatManager.Instance.playerThreatCounter.Add(player, amount);
            }

            int threat = RatManager.Instance.playerThreatCounter[player];
            logger.LogDebug($"{player.playerUsername}: {threat} threat");

            if (currentBehaviorState == State.Swarming) { return; }

            if (threat > threatToAttackPlayer)
            {
                targetPlayer = player;
                swarmTarget = player.gameObject;

                SwitchToBehaviorClientRpc(State.Swarming);
            }
        }

        public void FinishRunCycle() // Animation
        {
            if (UnityEngine.Random.Range(0f, 1f) < squeakChance)
            {
                RoundManager.PlayRandomClip(audioSource, squeakSFX);
            }
        }

        public void HitEnemy(int force, int playerHitBy = -1)
        {
            if (playerHitBy != -1)
            {
                PlayerControllerB player = PlayerFromId((ulong)playerHitBy);
                AddThreat(player);
            }

            health -= force;
            RoundManager.PlayRandomClip(audioSource, hitSFX);

            if (health <= 0)
            {
                KillEnemy();
            }
        }

        public void KillEnemy()
        {
            isDead = true;
            animator?.SetTrigger(hashDie);
            animator?.Update(0);

            if (!IsServer) { return; }

            StopAllCoroutines();
            nav.agent.enabled = false;
            nav.OnEnableOrDisableAgent.Invoke(false);
            ResetVariables();
        }

        public void ResetVariables()
        {
            if (previousBehaviorState == State.Swarming)
            {
                foreach (var nest in RatNest.nests)
                {
                    nest?.DefenseRats.Remove(this);
                }
            }

            closestNest = null;
        }

        public void SwitchToBehaviorOnLocalClient(State state)
        {
            if (currentBehaviorState != state)
            {
                previousBehaviorState = currentBehaviorState;
                currentBehaviorState = state;
                timeSinceSwitchBehavior = 0f;
                ResetVariables();
            }
        }

        // RPC's

        [ClientRpc]
        public void SwitchToBehaviorClientRpc(State state)
        {
            SwitchToBehaviorOnLocalClient(state);
        }

        [ClientRpc]
        public void PlayRallySFXClientRpc()
        {
            audioSource.PlayOneShot(screamSFX);
        }

        [ClientRpc]
        public void GrabBodyClientRpc(ulong clientId, int attachedLimbIndex)
        {
            targetPlayer = PlayerFromId(clientId);

            if (targetPlayer != null && targetPlayer.deadBody != null)
            {
                heldBody = targetPlayer.deadBody;
                heldBody.attachedTo = ratMouthTransform;
                heldBody.attachedLimb = targetPlayer.deadBody.bodyParts[attachedLimbIndex];
                heldBody.matchPositionExactly = true;
            }

            grabbingBody = false;
            returningBodyToNest = true;
            RoundManager.PlayRandomClip(audioSource, nibbleSFX, true, 1f, -1);
            SwitchToBehaviorOnLocalClient(State.ReturnToNest);
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
        public void HitEnemyServerRpc(int force, int playerHitBy = -1)
        {
            if (!IsServer) { return; }
            HitEnemyClientRpc(force, playerHitBy);
        }

        [ClientRpc]
        private void HitEnemyClientRpc(int force, int playerHitBy = -1)
        {
            HitEnemy(force, playerHitBy);
        }

        [ServerRpc(RequireOwnership = false)]
        public void KillEnemyServerRpc()
        {
            if (!IsServer) { return; }
            KillEnemyClientRpc();
        }

        [ClientRpc]
        private void KillEnemyClientRpc()
        {
            KillEnemy();
        }
    }

    public static class ComponentExtensions
    {
        public static bool TryGetComponentInChildren<T>(this GameObject go, out T component) where T : Component
        {
            component = go.GetComponentInChildren<T>();
            return component != null;
        }

        public static bool TryGetComponentInChildren<T>(this Component comp, out T component) where T : Component
        {
            component = comp.GetComponentInChildren<T>();
            return component != null;
        }
    }
}