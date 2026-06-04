using Dawn;
using Dawn.Utils;
using GameNetcodeStuff;
using HarmonyLib;
using SnowyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
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
        public static List<GrabbableObject> StealableObjects = [];

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
        public SmartAgentNavigator nav = null!;

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

        GrabbableObject? targetObject;
        GrabbableObject? heldObject;

        DeadBodyInfo? targetBody;
        DeadBodyInfo? heldBody;

        bool holdingFood;

        int hashDie;
        int hashSpeed;

        float timeSinceCollision;
        float timeSinceCheck;
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

        const float timeToDoChecks = 3f;
        const float maxIdleTime = 1f;
        const float distanceNeededToLoseRats = 20f;
        const float rallyDistance = 20f;
        const float scentRange = 20f;

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
            timeSinceCheck += Time.deltaTime;
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
            if (!IsServer) { return; }

            if (isDead || StartOfRound.Instance.allPlayersDead || inSpecialAnimation)
            {
                nav.StopAgent(); // TODO: Test this
                return;
            }
            ;

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

                        if (heldBody != null)
                        {
                            closestNest.AddFood(cfgPlayerFoodAmount);
                            DropObjectClientRpc(despawn: true);
                        }

                        if (heldObject != null)
                        {
                            closestNest.AddFood(Mathf.CeilToInt(heldObject.itemProperties.weight * 2.5f));
                            DropObjectClientRpc(despawn: true);
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

                    if (targetBody != null)
                    {
                        if (nav.TryDoPathingToDestination(targetBody.transform.position, out destinationResult) && destinationResult != GoToDestinationResult.Failure)
                        {
                            if (destinationResult == GoToDestinationResult.Success)
                            {
                                int limb = UnityEngine.Random.Range(0, targetBody.bodyParts.Length);
                                GrabBodyClientRpc(targetBody.playerScript.actualClientId, limb);
                                targetBody = null;
                            }
                            return;
                        }
                    }

                    if (targetObject != null)
                    {
                        if (nav.TryDoPathingToDestination(targetObject.transform.position, out destinationResult) && destinationResult != GoToDestinationResult.Failure)
                        {
                            if (destinationResult == GoToDestinationResult.Success)
                            {
                                GrabObjectClientRpc(targetObject.NetworkObject);
                                targetObject = null;
                            }
                            return;
                        }
                    }

                    if (timeSinceCheck > timeToDoChecks)
                    {
                        timeSinceCheck = 0f;
                        DoChecks();
                    }

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
                        if (player == null || !player.isPlayerControlled || (!rallying && timeIdle > maxIdleTime && timeSinceSwitchBehavior > maxIdleTime + 2f && (transform.position - player.transform.position).sqrMagnitude > distanceNeededToLoseRats * distanceNeededToLoseRats))
                        {
                            SwitchToBehaviorClientRpc((int)State.ReturnToNest);
                            return;
                        }
                    }
                    else if (swarmTarget.TryGetComponentInChildren(out EnemyAICollisionDetect? enemyAICollision))
                    {
                        EnemyAI? enemy = enemyAICollision?.mainScript;
                        if (enemy == null || enemy.isEnemyDead || (!rallying && timeIdle > maxIdleTime && timeSinceSwitchBehavior > maxIdleTime + 1f && (transform.position - enemy.transform.position).sqrMagnitude > distanceNeededToLoseRats * distanceNeededToLoseRats))
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
            rallying =  !isDead && (rallyPos - transform.position).sqrMagnitude < rallyDistance * rallyDistance;
            if (!rallying) { return; }
            swarmTarget = target;
            SwitchToBehaviorClientRpc(State.Swarming);
        }

        void DoChecks()
        {
            CheckForThreatsInLOS();
            CheckLineOfSightForPlayerDeadBody();
            CheckForItemsToSteal();
        }

        void CheckLineOfSightForPlayerDeadBody()
        {
            if (!cfgRatsTakePlayerCorpses) { return; }

            targetBody = null;

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                DeadBodyInfo deadBody = player.deadBody;
                if (deadBody == null || deadBody.deactivated) { continue; }
                if (CheckLineOfSightForPosition(deadBody.grabBodyObject.transform.position, 60, 60, 5) && nav.CanPathToPoint(deadBody.grabBodyObject.transform.position))
                {
                    targetBody = deadBody;
                    return;
                }
            }
        }

        void CheckForThreatsInLOS()
        {
            EnemyAI? enemy = CheckLineOfSightForEnemy(30);
            if (enemy != null && enemy.enemyType.canDie && enemy.enemyType.EnemySize == EnemySize.Tiny)
                AddThreat(enemy);

            PlayerControllerB? player = CheckLineOfSightForPlayer(60f, 60, 5);
            if (player != null && player.isPlayerControlled)
                AddThreat(player);
        }

        void CheckForItemsToSteal()
        {
            if (targetBody != null || heldBody != null) { return; }
            targetObject = null;
            float closestDistance = scentRange * scentRange;

            foreach (var grabbableObject in StealableObjects.ToList())
            {
                if (!IsObjectGrabbable(grabbableObject)) { continue; }

                float distance = (transform.position - grabbableObject.transform.position).sqrMagnitude;
                if (distance >= closestDistance) { continue; }

                targetObject = grabbableObject;
                closestDistance = distance;
            }
        }

        bool IsObjectGrabbable(GrabbableObject obj)
        {
            return heldObject == null
                && heldBody == null
                && obj != null
                && obj.grabbable
                && obj.grabbableToEnemies
                && !obj.isHeld
                && !obj.isHeldByEnemy
                && obj.hasHitGround
                && obj.playerHeldBy == null;
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
            if ((transform.position - objectPosition).sqrMagnitude < range * range && !Physics.Linecast(transform.position, objectPosition, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                Vector3 to = objectPosition - transform.position;
                if (Vector3.Angle(transform.forward, to) < width || (base.transform.position - objectPosition).sqrMagnitude < proximityAwareness * proximityAwareness)
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
                if ((position - eye.position).sqrMagnitude < range * range && !Physics.Linecast(eye.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    Vector3 to = position - eye.position;
                    if (Vector3.Angle(eye.forward, to) < width || (proximityAwareness != -1 && (eye.position - position).sqrMagnitude < proximityAwareness * proximityAwareness))
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


                AddThreat(player);
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

            if (heldObject != null || heldBody != null)
                DropObjectOnLocalClient(despawn: false);
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
        public void GrabObjectClientRpc(NetworkObjectReference netRef)
        {
            netRef.TryGet(out NetworkObject netObj);
            GrabbableObject grabbableObject = netObj.GetComponent<GrabbableObject>();
            grabbableObject.parentObject = ratMouthTransform;
            grabbableObject.hasHitGround = false;
            grabbableObject.isHeldByEnemy = true;
            //grabbableObject.GrabItemFromEnemy(this);
            grabbableObject.EnablePhysics(false);
            HoarderBugAI.grabbableObjectsInMap.Remove(grabbableObject.gameObject);
            heldObject = grabbableObject;

            RoundManager.PlayRandomClip(creatureSFX, nibbleSFX, true, 1f, -1);
            SwitchToBehaviorOnLocalClient(State.ReturnToNest);
        }

        [ClientRpc]
        public void GrabBodyClientRpc(ulong clientId, int attachedLimbIndex)
        {
            targetPlayer = PlayerFromId(clientId);

            heldBody = targetPlayer.deadBody;
            heldBody.attachedTo = ratMouthTransform;
            heldBody.attachedLimb = targetPlayer.deadBody.bodyParts[attachedLimbIndex];
            heldBody.matchPositionExactly = true;

            RoundManager.PlayRandomClip(creatureSFX, nibbleSFX, true, 1f, -1);
            SwitchToBehaviorOnLocalClient(State.ReturnToNest);
        }

        [ClientRpc]
        public void DropObjectClientRpc(bool despawn)
        {
            DropObjectOnLocalClient(despawn);
        }

        public void DropObjectOnLocalClient(bool despawn)
        {
            if (heldBody != null)
            {
                heldBody.attachedTo = null;
                heldBody.attachedLimb = null;
                heldBody.matchPositionExactly = false;
                if (despawn) { heldBody.DeactivateBody(setActive: false); }
                heldBody = null;
            }
            if (heldObject != null)
            {
                Vector3 targetFloorPosition = heldObject.transform.position.GetFloorPosition();
                heldObject.parentObject = null;
                heldObject.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
                heldObject.EnablePhysics(enable: true);
                heldObject.fallTime = 0f;
                heldObject.startFallingPosition = heldObject.transform.parent.InverseTransformPoint(heldObject.transform.position);
                heldObject.targetFloorPosition = heldObject.transform.parent.InverseTransformPoint(targetFloorPosition);
                //heldObject.floorYRot = -1; // TODO: Test this?
                heldObject.DiscardItemFromEnemy();
                heldObject.isHeldByEnemy = false;

                if (despawn)
                    heldObject.NetworkObject.Despawn(destroy: true);
                else
                    HoarderBugAI.grabbableObjectsInMap.Add(heldObject.gameObject);

                heldObject = null;
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

    [HarmonyPatch]
    internal static class RatAIPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Landmine), nameof(Landmine.SpawnExplosion))]
        public static void Landmine_SpawnExplosion_Postfix(Vector3 explosionPosition, bool spawnExplosionEffect = false, float killRange = 1f, float damageRange = 1f, int nonLethalDamage = 50, float physicsForce = 0f, GameObject overridePrefab = null, bool goThroughCar = false)
        {
            if (!IsServerOrHost) { return; }
            foreach (RatAI rat in RatAI.Instances)
            {
                if (rat == null || rat.isDead) { continue; }
                float distance = Vector3.Distance(rat.transform.position, explosionPosition);
                if (distance < damageRange)
                {
                    rat.HitFromExplosion(distance);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.Start))]
        public static void GrabbableObject_Start_Postfix(GrabbableObject __instance)
        {
            if (!IsServerOrHost) { return; }

            var info = __instance.itemProperties.GetDawnInfo();

            if (info == null || !(info.AllTags().Any(x => cfgFoodItemTags.Contains(x.ToString())) || cfgFoodItemNames.Contains(info.TypedKey.ToString()))) { return; } // TODO: Test this
            RatAI.StealableObjects.Add(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.OnDestroy))]
        public static void GrabbableObject_OnDestroy_Postfix(GrabbableObject __instance)
        {
            if (!IsServerOrHost) { return; }

            RatAI.StealableObjects.Remove(__instance);
        }
    }
}