using Dawn.Utils;
using GameNetcodeStuff;
using SnowyLib;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication.Generated;
using UnityEngine;
using UnityEngine.Events;
using static Dawn.Utils.SmartAgentNavigator;
using static Rats.Configs;
using static Rats.Plugin;
using static Rats.RatNest;

// TODO: Steal scrap items with dawnlib tags: food, candy, edible, etc

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

        public GameObject jermaRatObj = null!;
        public GameObject ratObj = null!;

        public Animator? animator;
        public Transform eye = null!;
        public AudioSource creatureSFX = null!;
        public AudioSource creatureVoice = null!;
        public SmartAgentNavigator nav = null!; // TODO: Put both meshes into the same rat obj and just enable which gameobject should be enabled based on config

        [HideInInspector] public bool isDead;

        GoToDestinationResult destinationResult = GoToDestinationResult.InProgress;

        public bool isOutside => nav.IsAgentOutside();

        GameObject? targetNode;

        bool inSpecialAnimation;
        State previousBehaviorState;
        State currentBehaviorState = State.ReturnToNest;

        [HideInInspector]
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

        // rallying
        public static UnityEvent<Vector3, GameObject> RallyRats = new();
        bool rallying;

        int health = 1;

        public enum State
        {
            ReturnToNest,
            Scouting,
            Swarming
        }

        float swarmRadiusDefault = 6f;

        private Vector3 lastPosition;
        private float currentSpeed;

        const float timeToIncreaseThreat = 3f;
        const float maxIdleTime = 1f;
        const float distanceNeededToLoseRats = 20f;
        const float rallyDistance = 20f;
        const float rallyTime = 15f;

        public void Start()
        {
            hashDie = Animator.StringToHash("die");
            hashSpeed = Animator.StringToHash("speed");
            christmasHat.SetActive(cfgHolidayRats);

            animator = cfgUseJermaRats ? null : animator;
            ratObj.SetActive(!cfgUseJermaRats);
            jermaRatObj.SetActive(cfgUseJermaRats);

            nav.SetAllValues(isOutside: false);
            RallyRats.AddListener(ListenForRallyCall);

            logger?.LogDebug($"Rat spawned");
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

            timeIdle = currentSpeed > 0.1f ? 0f : timeIdle + Time.deltaTime;
        }

        public void DoAIInterval()
        {
            if (isDead || StartOfRound.Instance.allPlayersDead || inSpecialAnimation)
            {
                nav.StopAgent(); // TODO: Test this
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

                    nav.TryDoPathingToDestination(closestNest.transform.position, out destinationResult);

                    if (timeIdle > maxIdleTime && destinationResult == GoToDestinationResult.Success)
                    {
                        // Add food to nest
                        if (holdingFood)
                            closestNest.AddFood();
                        holdingFood = false;

                        if (returningBodyToNest)
                        {
                            DropBodyClientRpc(true);
                            closestNest.AddFood(cfgPlayerFoodAmount);
                        }
                        
                        if (closestNest != null && closestNest.DefenseRats != null && closestNest.DefenseRats.Count < cfgMaxDefenseRats)
                        {
                            swarmTarget = closestNest.gameObject;
                            closestNest.DefenseRats.Add(this);
                            SwitchToBehaviorClientRpc(State.Swarming);
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
                        if (nav.TryDoPathingToDestination(deadBody.transform.position, out destinationResult) && destinationResult != GoToDestinationResult.Failure)
                        {
                            if (destinationResult == GoToDestinationResult.Success)
                            {
                                int limb = UnityEngine.Random.Range(0, targetPlayer.deadBody.bodyParts.Length);
                                GrabBodyClientRpc(targetPlayer.actualClientId, limb);
                                returningBodyToNest = true;
                            }
                            return;
                        }
                    }

                    CheckForThreatsInLOS();

                    if (timeIdle > maxIdleTime)
                        targetNode = Instances.Count >= (cfgMaxRats - (cfgMaxRats / 4)) ? Utils.allAINodes.GetRandom() : Utils.insideAINodes.GetRandom(); // TODO: Test this

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

                    float swarmRadius = cfgSwarmRadius;

                    if (swarmTarget.TryGetComponentInChildren(out PlayerControllerB? player))
                    {
                        if (player == null || !player.isPlayerControlled || (!rallying && timeIdle > maxIdleTime && timeSinceSwitchBehavior > maxIdleTime + 2f && Vector3.Distance(transform.position, player.transform.position) > distanceNeededToLoseRats))
                        {
                            SwitchToBehaviorClientRpc((int)State.ReturnToNest);
                            return;
                        }
                    }
                    else if (swarmTarget.TryGetComponentInChildren(out EnemyAICollisionDetect? enemyAICollision))
                    {
                        EnemyAI? enemy = enemyAICollision?.mainScript;
                        if (enemy == null || enemy.isEnemyDead || (!rallying && timeIdle > maxIdleTime && timeSinceSwitchBehavior > maxIdleTime + 1f && Vector3.Distance(transform.position, enemy.transform.position) > distanceNeededToLoseRats))
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
                    Plugin.logger?.LogWarning("Invalid state: " + currentBehaviorState);
                    break;
            }
        }

        public void ListenForRallyCall(Vector3 rallyPos, GameObject target)
        {
            if (rallying) { return; }
            rallying =  !isDead && Utils.FastDistance(rallyPos, transform.position, rallyDistance);
            if (!rallying) { return; }
            swarmTarget = target;
            SwitchToBehaviorClientRpc(State.Swarming);
        }

        bool CheckLineOfSightForPlayerDeadBody()
        {
            if (!cfgRatsTakePlayerCorpses) { return false; }

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                DeadBodyInfo deadBody = player.deadBody;
                if (deadBody == null || deadBody.deactivated) { continue; }
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
            if (CheckLineOfSightForPlayerDeadBody())
            {
                grabbingBody = true;
                return;
            }
            EnemyAI? enemy = CheckLineOfSightForEnemy(30);
            if (enemy != null && enemy.enemyType.canDie && enemy.enemyType.EnemySize == EnemySize.Tiny)
            {
                AddThreat(enemy);
            }
            PlayerControllerB? player = CheckLineOfSightForPlayer(60f, 60, 5);
            if (player != null && player.isPlayerControlled)
            {
                /*if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.itemProperties.name == "RatCrownItem" && !player.currentlyHeldObjectServer.isPocketed)
                {
                    targetPlayer = player;
                    SwitchToBehaviorClientRpc(State.Swarming);
                    return;
                }*/

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
                if (enemy == null || !enemy.enemyType.canDie || enemy.enemyType.EnemySize != EnemySize.Tiny) { continue; }
                Vector3 checkPos = enemy.eye != null ? enemy.eye.position : enemy.transform.position;
                if (CheckLineOfSightForPosition(checkPos, 60, range, 5))
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

        public void OnCollideWithPlayer(Collider other)
        {
            if (isDead) { return; }
            if (timeSinceCollision < 1f) { return; }

            timeSinceCollision = 0f;
            PlayerControllerB? player = other.gameObject.GetComponent<PlayerControllerB>();
            if (player == null || !player.isPlayerControlled) { return; }
            //if (player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer.name == "RatCrownItem" && !player.currentlyHeldObjectServer.isPocketed) { return; }
            rallying = false;
            if (currentBehaviorState == State.Swarming) // TODO: Test this
            {
                RoundManager.PlayRandomClip(creatureSFX, attackSFX);

                if (player != localPlayer) { return; }
                int deathAnim = UnityEngine.Random.Range(0, 2) == 1 ? 7 : 0;
                player.DamagePlayer(1, true, true, CauseOfDeath.Mauling, deathAnim);
            }
        }

        public void OnCollideWithEnemy(Collider other, EnemyAI? collidedEnemy = null)
        {
            if (isDead || currentBehaviorState == State.ReturnToNest) { return; }
            if (timeSinceCollision < 1f) { return; }
            if (collidedEnemy == null || !collidedEnemy.enemyType.canDie || collidedEnemy.enemyType.EnemySize != EnemySize.Tiny) { return; }
            //if (!cfgEnemyWhitelist.Contains(collidedEnemy.enemyType.name)) { return; } // TODO
            //if (collidedEnemy is RatKingAI) { return; }
            rallying = false;
            logger?.LogDebug("Collided with: " + collidedEnemy.enemyType.enemyName);
            timeSinceCollision = 0f;

            if (collidedEnemy.isEnemyDead)
            {
                enemyFoodPointsLeft[collidedEnemy] -= 1;
                holdingFood = true;

                if (enemyFoodPointsLeft[collidedEnemy] <= 0)
                {
                    enemyFoodPointsLeft.Remove(collidedEnemy);
                    RoundManager.Instance.DespawnEnemyOnServer(collidedEnemy.NetworkObject);
                }

                RoundManager.PlayRandomClip(creatureSFX, nibbleSFX);
                if (localPlayer.HasLineOfSightToPosition(transform.position))
                {
                    localPlayer.IncreaseFearLevelOverTime(0.8f);
                }

                if (IsServer)
                    SwitchToBehaviorClientRpc(State.ReturnToNest);
            }
            else
            {
                if (enemyThreatCounter[collidedEnemy] > cfgThreatToAttackEnemy)
                {
                    enemyHitCount[collidedEnemy] -= 1;

                    if (enemyHitCount[collidedEnemy] <= 0)
                    {
                        collidedEnemy.HitEnemy(1, null, true);
                        enemyHitCount[collidedEnemy] = cfgEnemyHitsToDoDamage;
                    }
                }
            }

            if (currentBehaviorState == State.Swarming)
            {
                RoundManager.PlayRandomClip(creatureSFX, attackSFX);
            }
        }

        void AddThreat(EnemyAI enemy, int amount = 1)
        {
            if (enemy == null) { return; }

            timeSinceAddThreat = 0f;

            enemyThreatCounter[enemy] += amount;

            int threat = enemyThreatCounter[enemy];
            logger?.LogDebug($"{enemy.enemyType.enemyName}: {threat} threat");

            if (currentBehaviorState == State.Swarming) { return; }

            if (threat > cfgThreatToAttackEnemy * 2 && !rallying && IsServer)
            {
                RallyCall(enemy.gameObject);
                return;
            }

            if (enemyThreatCounter[enemy] > cfgThreatToAttackEnemy && IsServer)
            {
                swarmTarget = enemy.gameObject;
                
                SwitchToBehaviorClientRpc(State.Swarming);
            }
        }

        void AddThreat(PlayerControllerB player, int amount = 1)
        {
            if (player == null || !player.isPlayerControlled) { return; }

            timeSinceAddThreat = 0f;

            if (playerThreatCounter.ContainsKey(player))
            {
                playerThreatCounter[player] += amount;
            }
            else
            {
                playerThreatCounter.Add(player, amount);
            }

            int threat = playerThreatCounter[player];
            logger?.LogDebug($"{player.playerUsername}: {threat} threat");

            if (currentBehaviorState == State.Swarming) { return; }

            if (threat > cfgThreatToAttackPlayer * 2 && !rallying)
            {
                RallyCall(player.gameObject);
                return;
            }

            if (threat > cfgThreatToAttackPlayer)
            {
                targetPlayer = player;
                swarmTarget = player.gameObject;

                SwitchToBehaviorClientRpc(State.Swarming);
            }
        }

        void RallyCall(GameObject target)
        {
            swarmTarget = target;
            inSpecialAnimation = true;
            rallying = true;
            creatureVoice.PlayOneShot(screamSFX);
            RallyRats.Invoke(transform.position, swarmTarget);

            if (cfgUseJermaRats)
            {
                IEnumerator InSpecialAnimationFalseRoutine()
                {
                    yield return null;
                    yield return new WaitForSeconds(4.7f);
                    inSpecialAnimation = false;
                }
                StartCoroutine(InSpecialAnimationFalseRoutine());
            }
            else
            {
                animator?.SetTrigger("rally");
            }
        }

        public void FinishRunCycle() // Animation
        {
            if (UnityEngine.Random.Range(0f, 1f) < cfgSqueakChance)
            {
                RoundManager.PlayRandomClip(creatureSFX, squeakSFX);
            }
        }

        public void SetInSpecialAnimationFalse() // Animation
        {
            inSpecialAnimation = false;
        }

        public void HitEnemy(int force, int playerHitBy = -1)
        {
            if (playerHitBy != -1)
            {
                PlayerControllerB player = PlayerFromId((ulong)playerHitBy);
                AddThreat(player);
            }

            health -= force;
            RoundManager.PlayRandomClip(creatureSFX, hitSFX);

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
                    nest?.DefenseRats?.Remove(this);
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
            creatureSFX.PlayOneShot(screamSFX);
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
            RoundManager.PlayRandomClip(creatureSFX, nibbleSFX, true, 1f, -1);
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

    public class RatAICollisionDetect : MonoBehaviour, IHittable
    {
        public RatAI mainScript = null!;

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                mainScript.OnCollideWithPlayer(other);
            }
            else if (other.CompareTag("Enemy"))
            {
                EnemyAICollisionDetect? enemyCollision = other.gameObject.GetComponent<EnemyAICollisionDetect>();
                if (enemyCollision != null)
                {
                    mainScript.OnCollideWithEnemy(other, enemyCollision.mainScript);
                }
            }
        }

        bool IHittable.Hit(int force, Vector3 hitDirection, PlayerControllerB? playerWhoHit, bool playHitSFX, int hitID)
        {
            int id = playerWhoHit != null ? (int)playerWhoHit.actualClientId : -1;
            mainScript.HitEnemyServerRpc(force, id);
            return true;
        }
    }
}