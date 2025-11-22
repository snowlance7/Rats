using AsmResolver.PE.Platforms;
using BepInEx.Logging;
using Dissonance.Datastructures;
using GameNetcodeStuff;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using static SCP2006.Plugin;
using static SCP2006.Utils;
using static UnityEngine.Rendering.DebugManager;

/* Animations:
- speed (float)
- sneaking (bool)
- scare
- laugh
- resting (bool)
- handOut (bool)
- think
- wave
- shush
- shocked
 */

namespace SCP2006
{
    internal class SCP2006AI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

        public static int DEBUG_scareIndex = -1;
        public static bool DEBUG_testingTransformations = false;

        public static SCP2006AI? Instance { get; private set; }

#pragma warning disable CS8618
        public GameObject DEBUG_hudOverlay;

        public ParticleSystem particleSystem;

        public ScareDef[] scareDefs;

        public Transform turnCompass;

        public Transform handTransform;

        public Collider tapeTriggerCollider;

        public AudioClip laughSFX;
        public AudioClip tapeGivenSFX;

        // Voice lines
        public AudioClip[] baitSFX;
        public AudioClip[] scareSFX;
        public AudioClip[] hitSFX;
        public AudioClip[] reactionFailedSFX;
        public AudioClip[] reactionSuccessSFX;
        public AudioClip[] roamingSFX;
        public AudioClip[] seenSneakingSFX;
        public AudioClip[] sneakingSFX;
        public AudioClip[] watchingTapeSFX;

        [NonSerialized]
        static Dictionary<ScareDef, int> learnedScares;

        public ScareDef currentScareDef;

        Terminal terminalScript;
        System.Random audioRandom;
#pragma warning restore CS8618

        public enum AudioArray
        {
            Bait,
            Scare,
            Hit,
            ReactionFailed,
            ReactionSuccess,
            Roaming,
            SeenSneaking,
            Sneaking,
            WatchingTape
        }

        HashSet<string> triggeredActions = new HashSet<string>();
        int score = 0;

        // point values per action
        readonly Dictionary<string, int> points = new Dictionary<string, int>()
        {
            { "Yell", 4 },
            { "CameraTurn", 1 },
            { "Jump", 1 },
            { "Run", 3 },
            { "Attack", 2 },
            { "CloseDoor", 2 },
            { "NoLineOfSight", 3 }
        };

        Vector3 mainEntranceOutsidePosition;
        Vector3 mainEntranceInsidePosition;

        bool resting;
        VHSTapeBehavior? heldTape;

        bool facePlayer;
        float turnCompassSpeed = 30f;

        bool isInsideFactory => !isOutside;

        float timeSinceLearnScare;
        float timeSinceLastSeen;
        float timeSinceSwitchBehavior;
        float timeSinceLastHit;
        float timeSinceLastVoiceLine;
        float timeSinceStartResting;
        float idleTime;
        float tapeRefusalCooldown;

        int hashSpeed;

        EnemyAI? seenEnemy;
        EnemyAI? mimicEnemy;

        bool spottedByOtherPlayer;

        int currentFootstepSurfaceIndex;
        int previousFootstepClip;
        
        Vector2 lastCameraAngles;
        Vector3 lastPosition;
        bool refusedTape;
        List<Transform> ignoredNodes = [];
        float currentSpeed;
        bool waved;


        // LOS Stuff
        PlayerControllerB? closestPlayer;
        PlayerControllerB? closestVisiblePlayer;
        PlayerControllerB? closestPlayerVisibleTo;

        float targetPlayerDistance;
        float closestPlayerDistance;
        float closestVisiblePlayerDistance;
        float closestPlayerVisibleToDistance;

        float timeInLOSWithPlayer;
        float timeHasLOSToPlayer;
        float timeHasLOSToTargetPlayer;
        float timeTargetPlayerHasLOS;

        bool hasLOSToTargetPlayer;
        bool targetPlayerHasLOS;
        bool hasLOSToPlayer => closestVisiblePlayer != null;
        bool playerHasLOS => closestPlayerVisibleTo != null;
        bool inLineOfSightWithPlayer => hasLOSToPlayer && (playerHasLOS || targetPlayerHasLOS);





        public enum ReactionType { Yell, Sprint, FastTurn, Jump}

        // Configs
        const float learnScareCooldown = 5f;
        //const float targetPlayerCooldown = 10f;
        const float distanceToStartScare = 10f;
        const float distanceToStopScare = 15f;
        const float spottedLOSCooldown = 10f;
        const float timeToStopScare = 15f;
        const float lineOfSightOffset = 2f;
        const float playerScreamMinVolume = 0.9f;
        const float reactionDelay = 1f;
        const int scareSuccessfulScore = 5;
        const float maxTurnSpeed = 1000f;
        const float reactionTime = 5f;
        const float maxWantingTime = 15f;
        const float roamingVoiceLineCooldown = 20f;
        const float maxTapeWatchingTime = 60f;
        const float maxIdleTime = 10;

        float scareCooldown = 60f;
        private bool faceEnemy;
        private bool retreating;
        const float waveCooldownOffset = 10f;
        const float learnScareCooldownOffset = -20f;
        const float scareSuccessCooldownOffset = 60;
        const float scareFailCooldownOffset = 30;
        
        const float maxScareCooldown = 60f;

        const float minPlayerInLOSTimeToScare = 3f;
        const float timeInLOSToBeSpotted = 1f;

        const float minSneakTimeToScare = 10f;

        const float maxTapeRefusalCooldown = 15f;

        public enum State
        {
            Roaming,
            Sneaking,
            Scaring,
            Reaction,
            Spotted,
            Wanting,
            Resting
        }

        public override void Start()
        {
            base.Start();
            logger.LogDebug("SCP-2006 Spawned");
            mainEntranceInsidePosition = RoundManager.FindMainEntrancePosition(getTeleportPosition: true);
            mainEntranceOutsidePosition = RoundManager.FindMainEntrancePosition(getTeleportPosition: true, getOutsideEntrance: true);

            ignoredNodes = [];
            learnedScares = [];
            audioRandom = new System.Random(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);

            hashSpeed = Animator.StringToHash("speed");
            
            if (!learnedScares.ContainsKey(scareDefs[0]))
                learnedScares.Add(scareDefs[0], 1);

            if (!learnedScares.ContainsKey(scareDefs[1]))
                learnedScares.Add(scareDefs[1], 1);

            terminalScript = GameObject.FindObjectOfType<Terminal>();

            TVSetUnlockable.onTapeEjected.AddListener(OnTapeEjected);

            if (isBeta)
            {
                Instantiate(DEBUG_hudOverlay, Vector3.zero, Quaternion.identity);
                //DebugEnemy = true;
            }

            if (Utils.testing && DEBUG_testingTransformations)
            {
                IEnumerator TestTransformations()
                {
                    yield return null;
                    
                    foreach(var scare in scareDefs)
                    {
                        currentScareDef = scare;
                        SpawnMimicEnemy();

                        yield return new WaitForSeconds(5f);

                        int index = currentScareDef != null && currentScareDef.enemyTypeName != "" ? UnityEngine.Random.Range(0, currentScareDef.scareSFX.Length) : UnityEngine.Random.Range(0, scareSFX.Length);
                        ScareClientRpc(index, localPlayer.actualClientId);

                        yield return new WaitForSeconds(currentScareDef.animTime + 3f);
                        DespawnMimicEnemy();
                        yield return new WaitForSeconds(3f);
                    }
                }

                StartCoroutine(TestTransformations());
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance != null && Instance != this)
            {
                logger.LogDebug("There is already a SCP-2006 in the scene. Removing this one.");
                if (!IsServer) { return; }
                NetworkObject.Despawn(true);
                return;
            }
            Instance = this;
            logger.LogDebug("Finished spawning SCP-2006");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (Instance == this)
            {
                Instance = null;
                if (isBeta)
                {
                    Destroy(TestingHUDOverlay.Instance?.gameObject);
                }
            }
        }

        public void CustomEnemyAIUpdate()
        {
            if (inSpecialAnimation)
            {
                agent.speed = 0f;
                return;
            }

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

        public override void Update()
        {
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || DEBUG_testingTransformations)
            {
                return;
            }

            timeSinceLearnScare += Time.deltaTime;
            timeSinceLastSeen += Time.deltaTime;
            timeSinceSwitchBehavior += Time.deltaTime;
            timeSinceLastHit += Time.deltaTime;
            timeSinceLastVoiceLine += Time.deltaTime;
            timeSinceStartResting += Time.deltaTime;

            CustomEnemyAIUpdate();

            timeInLOSWithPlayer = inLineOfSightWithPlayer ? timeInLOSWithPlayer + Time.deltaTime : 0f;
            timeHasLOSToPlayer = hasLOSToPlayer ? timeHasLOSToPlayer + Time.deltaTime : 0f;
            timeHasLOSToTargetPlayer = hasLOSToTargetPlayer ? timeHasLOSToTargetPlayer + Time.deltaTime : 0f;
            timeTargetPlayerHasLOS = targetPlayerHasLOS ? timeTargetPlayerHasLOS + Time.deltaTime : 0;

            if (scareCooldown > 0)
            {
                scareCooldown -= Time.deltaTime;
            }

            if (tapeRefusalCooldown > 0)
            {
                tapeRefusalCooldown -= Time.deltaTime;
            }
            else
            {
                refusedTape = false;
            }
        }

        public void LateUpdate()
        {
            MimicEnemyUpdate();

            if (IsServer)
            {
                if (facePlayer && (targetPlayer != null || closestPlayer != null))
                {
                    Vector3 lookPosition = targetPlayer != null ? targetPlayer.gameplayCamera.transform.position : closestPlayer.gameplayCamera.transform.position;
                    turnCompass.LookAt(lookPosition);
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), turnCompassSpeed * Time.deltaTime);
                }
                if (faceEnemy && seenEnemy != null)
                {
                    turnCompass.LookAt(seenEnemy.eye.position);
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), turnCompassSpeed * Time.deltaTime);
                }
            }

            currentSpeed = ((transform.position - lastPosition).magnitude / Time.deltaTime) / 2;
            creatureAnimator.SetFloat(hashSpeed, currentSpeed);
            lastPosition = transform.position;
            
            idleTime = currentSpeed > 0.1f ? 0f : idleTime + Time.deltaTime;

            tapeTriggerCollider.enabled = currentSpeed < 0.1f && currentBehaviourStateIndex == (int)State.Wanting && localPlayer.currentlyHeldObjectServer is VHSTapeBehavior;
        }

        public void MimicEnemyUpdate()
        {
            if (mimicEnemy != null)
            {
                //mimicEnemy.transform.position = transform.position;
                //mimicEnemy.transform.rotation = transform.rotation;

                if (mimicEnemy.enabled && mimicEnemy.agent != null && mimicEnemy.agent.enabled)
                {
                    //mimicEnemy.targetPlayer = targetPlayer; // TODO: Test this
                    mimicEnemy.agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
                    mimicEnemy.agent.speed = agent.speed;
                    mimicEnemy.agent.angularSpeed = agent.angularSpeed;
                    mimicEnemy.agent.acceleration = agent.acceleration;
                    mimicEnemy.agent.stoppingDistance = agent.stoppingDistance;
                    mimicEnemy.movingTowardsTargetPlayer = false;
                    mimicEnemy.SetDestinationToPosition(transform.position);
                }
            }
        }

        public override void DoAIInterval()
        {
            if (currentBehaviourStateIndex == (int)State.Reaction && targetPlayer != null && localPlayer == targetPlayer)
            {
                if (localPlayer.isSprinting && !triggeredActions.Contains("Run"))
                {
                    triggeredActions.Add("Run");
                    TriggerReactionServerRpc("Run");
                    return;
                }
                if (localPlayer.isJumping && !triggeredActions.Contains("Jump"))
                {
                    triggeredActions.Add("Jump");
                    TriggerReactionServerRpc("Jump");
                    return;
                }
            }

            if (!IsServer) { return; }

            if (isBeta)
            {
                TestingHUDOverlay.Instance!.toggle.isOn = retreating;
                TestingHUDOverlay.Instance.label.text = currentSpeed.ToString();
            }

            if (moveTowardsDestination && !(Utils.isBeta && Utils.DEBUG_disableMoving))
            {
                agent.SetDestination(destination);
            }

            hasLOSToTargetPlayer = targetPlayer != null && HasLineOfSightToPlayer(targetPlayer);
            targetPlayerDistance = targetPlayer != null ? Vector3.Distance(transform.position, targetPlayer.transform.position) : -1;
            targetPlayerHasLOS = targetPlayer != null && targetPlayer.HasLineOfSightToPosition(transform.position + Vector3.up * lineOfSightOffset);

            closestPlayer = GetClosestPlayer();
            closestVisiblePlayer = GetClosestPlayer(requireLineOfSight: true);

            closestPlayerDistance = closestPlayer != null ? Vector3.Distance(closestPlayer.transform.position, transform.position) : -1;

            closestVisiblePlayerDistance = closestVisiblePlayer != null ? Vector3.Distance(closestVisiblePlayer.transform.position, transform.position) : -1;
            
            closestPlayerVisibleTo = GetClosestPlayerVisibleTo();

            if (inLineOfSightWithPlayer)
            {
                timeSinceLastSeen = 0f;
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:
                    agent.speed = 5;
                    refusedTape = false;

                    // Switch to resting phase if can get to ship again
                    if (heldTape != null && CanPathToTVSet())
                    {
                        SwitchToBehaviourClientRpc((int)State.Resting);
                        return;
                    }

                    CheckForPlayerWithTape();

                    // Wave if running into player
                    if (!waved && inLineOfSightWithPlayer && closestPlayerDistance < 5f)
                    {
                        waved = true;
                        inSpecialAnimation = true;
                        facePlayer = true;
                        DoAnimationClientRpc("wave");
                        AddOffsetToScareCooldown(waveCooldownOffset);
                        return;
                    }
                    
                    // Switch to scare phase if cooldown is up
                    if (scareCooldown <= 0
                        && !playerHasLOS)
                    {
                        targetPlayer = GetFarthestPlayerFromPosition(transform.position, 10f);
                        if (targetPlayer != null)
                        {
                            currentScareDef = GetWeightedRandomScare();
                            PlayRandomClipClientRpc(AudioArray.Sneaking, targetPlayer.actualClientId, 1f);
                            SwitchToBehaviourClientRpc((int)State.Sneaking);
                            return;
                        }
                    }

                    // Check for enemy to learn scare from in line of sight
                    if (timeSinceLearnScare > learnScareCooldown)
                    {
                        GameObject? seenEnemyObj = CheckLineOfSight(GetLearnableEnemies());

                        if (seenEnemyObj != null)
                        {
                            if (seenEnemyObj.TryGetComponent(out seenEnemy))
                            {
                                timeSinceLearnScare = 0f;
                                ScareDef? def = scareDefs.Where(x => x.enemyTypeName == seenEnemy!.enemyType.name).FirstOrDefault();
                                if (def == null) { return; }
                                AddScarePoint(def);

                                inSpecialAnimation = true;
                                faceEnemy = true;
                                DoAnimationClientRpc("think");
                                AddOffsetToScareCooldown(learnScareCooldownOffset);
                            }
                        }
                    }

                    // Roam Logic
                    if (HasReachedTargetNode())
                    {
                        targetNode = Utils.inTestRoom ? Utils.GetRandomNode(outside: false)?.transform : Utils.GetRandomNode()?.transform;
                    }
                    if (targetNode != null && !SetDestinationToPositionUseEntrance(targetNode.position, checkForPath: true))
                    {
                        targetNode = null;
                    }
                    else if (timeSinceLastVoiceLine > roamingVoiceLineCooldown)
                    {
                        timeSinceLastVoiceLine = 0f;
                        PlayRandomClipClientRpc(AudioArray.Roaming);
                    }

                    break;

                case (int)State.Sneaking: // TODO: figure this shit out
                    facePlayer = true;
                    
                    // Get player or scare def if they become null or dead
                    if (targetPlayer == null || !targetPlayer.isPlayerControlled)
                    {
                        targetPlayer = Utils.GetFarthestPlayerFromPosition(transform.position);
                        return;
                    }

                    if (isInsideFactory != targetPlayer.isInsideFactory)
                    {
                        SetDestinationToEntrance();
                        return;
                    }

                    if (timeTargetPlayerHasLOS > timeInLOSToBeSpotted && targetPlayerDistance < 15f)
                    {
                        SpottedClientRpc();
                        return;
                    }

                    if (timeSinceSwitchBehavior > minSneakTimeToScare
                        && timeHasLOSToTargetPlayer > minPlayerInLOSTimeToScare
                        && targetPlayerDistance <= distanceToStartScare)
                    {
                        SpawnMimicEnemy();
                        SwitchToBehaviourClientRpc((int)State.Scaring);
                        return;
                    }


                    SneakTargetPlayer(); // TODO: Test this

                    /*if (!spottedByOtherPlayer && InLineOfSightWithPlayer())
                    {
                        spottedByOtherPlayer = true;
                        DoAnimationClientRpc("shush");
                    }*/

                    break;

                case (int)State.Scaring:
                    agent.speed = 5;
                    agent.stoppingDistance = distanceToStartScare * 1.5f;
                    facePlayer = true;

                    if (targetPlayer!.HasLineOfSightToPosition(transform.position + Vector3.up * lineOfSightOffset))
                    {
                        triggeredActions.Clear();
                        score = 0;

                        int index = currentScareDef != null && currentScareDef.enemyTypeName != "" ? UnityEngine.Random.Range(0, currentScareDef.scareSFX!.Length) : UnityEngine.Random.Range(0, scareSFX.Length);

                        ScareClientRpc(index, targetPlayer.actualClientId);
                        //SwitchToBehaviourClientRpc((int)State.Reaction);
                        return;
                    }

                    if (!SetDestinationToPosition(targetPlayer.transform.position, true))
                    {
                        DespawnMimicEnemy();
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        return;
                    }

                    if (targetPlayerDistance > distanceToStopScare || timeSinceSwitchBehavior > timeToStopScare)
                    {
                        DespawnMimicEnemy();
                        SwitchToBehaviourClientRpc((int)State.Sneaking);
                        return;
                    }

                    break;

                case (int)State.Spotted:
                    agent.speed = 8;

                    if (timeSinceLastSeen > spottedLOSCooldown)
                    {
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        return;
                    }

                    CheckForPlayerWithTape();

                    if (isOutside)
                    {
                        SetDestinationToEntrance();
                        return;
                    }
                    
                    if (closestPlayer == null) { return; }
                    AvoidPlayer(closestPlayer, 28f);

                    break;

                case (int)State.Wanting:
                    agent.speed = 5;
                    agent.stoppingDistance = 5;
                    facePlayer = true;

                    if (timeSinceSwitchBehavior > maxWantingTime || !GetClosestPlayerWithTape(10f, out PlayerControllerB? playerWithTape1) || !SetDestinationToPosition(playerWithTape1!.transform.position, true))
                    {
                        tapeRefusalCooldown = maxTapeRefusalCooldown;
                        refusedTape = true;
                        SwitchToBehaviourClientRpc((int)State.Spotted);
                        return;
                    }

                    break;

                case (int)State.Reaction:
                    agent.speed = 0;
                    
                    if (targetPlayer == null || currentScareDef == null || !targetPlayer.isPlayerControlled)
                    {
                        SwitchToBehaviourClientRpc((int)State.Spotted);
                        return;
                    }

                    if (timeSinceSwitchBehavior > reactionDelay)
                    {
                        //if (targetPlayer.isSprinting) { TriggerReaction("Run"); }
                        //if (targetPlayer.isJumping) { TriggerReaction("Jump"); }
                        TrackCameraMovement();
                        if (!triggeredActions.Contains("NoLineOfSight") && !hasLOSToTargetPlayer) { TriggerReaction("NoLineOfSight"); }
                    }

                    if (timeSinceSwitchBehavior - reactionDelay > currentScareDef.animTime + reactionTime)
                    {
                        DespawnMimicEnemy(); // TODO: test

                        // Count up points
                        if (score >= scareSuccessfulScore)
                        {
                            AddScarePoint(currentScareDef);
                            inSpecialAnimation = true;
                            DoAnimationClientRpc("laugh");
                            AddOffsetToScareCooldown(scareSuccessCooldownOffset);
                        }
                        else
                        {
                            RemoveScarePoint(currentScareDef);
                            PlayRandomClipClientRpc(AudioArray.ReactionFailed);
                            AddOffsetToScareCooldown(scareFailCooldownOffset);
                        }

                        SwitchToBehaviourClientRpc((int)State.Spotted);
                        return;
                    }

                    break;

                case (int)State.Resting:
                    agent.speed = 5;

                    if (resting)
                    {
                        if (timeSinceStartResting > maxTapeWatchingTime)
                        {
                            AddScarePoint(TVSetUnlockable.Instance.tapeInVHS.currentScareDef);
                            resting = false;
                            agent.enabled = true;
                            Teleport(transform.position, true);
                            SwitchToBehaviourClientRpc((int)State.Spotted);
                            AddOffsetToScareCooldown(maxScareCooldown);
                        }
                        return;
                    }

                    if (TVSetUnlockable.Instance != null)
                    {
                        if (Vector3.Distance(transform.position, TVSetUnlockable.Instance.transform.position) < 1f)
                        {
                            moveTowardsDestination = false;
                            agent.ResetPath();
                            agent.enabled = false;

                            transform.position = TVSetUnlockable.Instance.sitPositonSCP2006.position;
                            transform.rotation = TVSetUnlockable.Instance.sitPositonSCP2006.rotation;

                            WatchTapeClientRpc();
                            resting = true;
                            timeSinceStartResting = 0f;
                            return;
                        }

                        if (idleTime > maxIdleTime)
                        {
                            SwitchToBehaviourClientRpc((int)State.Roaming);
                            return;
                        }

                        SetDestinationToPositionUseEntrance(TVSetUnlockable.Instance.transform.position);
                    }
                    else
                    {
                        Vector3 terminalPos = terminalScript.transform.position;

                        if (Vector3.Distance(transform.position, terminalPos) < 3f)
                        {
                            // TODO: Make and use 'using terminal' animation?
                            agent.ResetPath();
                            UnlockableItem tvUnlockable = StartOfRound.Instance.unlockablesList.unlockables.Where(x => x.unlockableName == "TVSet").First();
                            int index = StartOfRound.Instance.unlockablesList.unlockables.IndexOf(tvUnlockable);
                            
                            if (tvUnlockable.alreadyUnlocked || tvUnlockable.hasBeenUnlockedByPlayer)
                            {
                                StartOfRound.Instance.SpawnUnlockable(index);
                            }
                            else
                            {
                                StartOfRound.Instance.UnlockShipObject(index);
                            }

                            return;
                        }

                        if (idleTime > maxIdleTime)
                        {
                            SwitchToBehaviourClientRpc((int)State.Roaming);
                            return;
                        }

                        SetDestinationToPositionUseEntrance(terminalPos);
                    }

                    break;

                default:
                    logger.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        bool CanPathToPlayer(PlayerControllerB player)
        {
            if (isInsideFactory != player.isInsideFactory)
            {
                if (isInsideFactory)
                {
                    return Utils.CalculatePath(transform.position, mainEntranceInsidePosition) && Utils.CalculatePath(mainEntranceOutsidePosition, player.transform.position);
                }
                else
                {
                    return Utils.CalculatePath(transform.position, mainEntranceOutsidePosition) && Utils.CalculatePath(mainEntranceInsidePosition, player.transform.position);
                }
            }
            else
            {
                return Utils.CalculatePath(transform.position, player.transform.position);
            }
        }

        Vector3 GetPointOnPathTowardsPlayer(Vector3 enemyPos, Vector3 playerPos, float offsetFromPlayer)
        {
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(enemyPos, playerPos, NavMesh.AllAreas, path))
                return playerPos; // fallback

            // compute total path length
            float total = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
                total += Vector3.Distance(path.corners[i], path.corners[i + 1]);

            float targetDistFromStart = Mathf.Max(0f, total - offsetFromPlayer);

            // walk the path to find the exact point
            float accum = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                float segLen = Vector3.Distance(path.corners[i], path.corners[i + 1]);

                if (accum + segLen >= targetDistFromStart)
                {
                    float remain = targetDistFromStart - accum;
                    float t = remain / segLen;
                    return Vector3.Lerp(path.corners[i], path.corners[i + 1], t);
                }

                accum += segLen;
            }

            return playerPos; // fallback
        }


        void CheckForPlayerWithTape()
        {
            if (heldTape == null
                && !refusedTape
                && GetClosestPlayerWithTape(10f, out PlayerControllerB? playerWithTape)
                && playerWithTape != null
                && SetDestinationToPosition(playerWithTape.transform.position, true)
                && CanPathToTVSet())
            {
                targetPlayer = playerWithTape;
                SwitchToBehaviourClientRpc((int)State.Wanting);
                return;
            }
        }

        public void AvoidPlayer(PlayerControllerB playerToAvoid, float optimalDistance = 0f)
        {
            Transform transform = ChooseFarthestNodeFromPosition(playerToAvoid.transform.position, true);
            if (transform != null && mostOptimalDistance > optimalDistance && Physics.Linecast(transform.transform.position, playerToAvoid.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                targetNode = transform;
                SetDestinationToPosition(targetNode.position);
            }
        }

        public void AvoidTargetPlayer(float optimalDistance = 0f)
        {
            Transform transform = ChooseFarthestNodeFromPosition(targetPlayer.transform.position, checkLOSToTargetPlayer: true);
            if (transform != null && mostOptimalDistance > optimalDistance && Physics.Linecast(transform.transform.position, targetPlayer.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                targetNode = transform;
                SetDestinationToPosition(targetNode.position);
            }
        }

        /*public bool PathIsIntersectedByLineOfSight(Vector3 targetPos, bool avoidLineOfSight = true, bool checkLOSToTargetPlayer = false)
        {
            pathDistance = 0f;
            if (agent.isOnNavMesh && !agent.CalculatePath(targetPos, path1))
            {
                if (DebugEnemy)
                {
                    Debug.Log($"Path could not be calculated: {targetPos}");
                }
                return true;
            }
            if (path1 == null || path1.corners.Length == 0)
            {
                return true;
            }
            if (Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(targetPos, RoundManager.Instance.navHit, 2.7f)) > 1.55f)
            {
                if (DebugEnemy)
                {
                    Debug.Log($"Path is not complete; final waypoint of path was too far from target position: {targetPos}");
                }
                return true;
            }
            bool flag = false;
            if (avoidLineOfSight)
            {
                for (int k = 1; k < path1.corners.Length; k++)
                {
                    if (!flag && k > 8 && Vector3.Distance(path1.corners[k - 1], path1.corners[k]) < 2f)
                    {
                        if (DebugEnemy)
                        {
                            Debug.Log($"Distance between corners {k} and {k - 1} under 3 meters; skipping LOS check");
                        }
                        flag = true;
                        continue;
                    }
                    if (targetPlayer != null && checkLOSToTargetPlayer && !Physics.Linecast(Vector3.Lerp(path1.corners[k - 1], path1.corners[k], 0.5f) + Vector3.up * 0.25f, targetPlayer.transform.position + Vector3.up * 0.25f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                    {
                        return true;
                    }
                    if (Physics.Linecast(path1.corners[k - 1], path1.corners[k], 262144))
                    {
                        if (DebugEnemy)
                        {
                            Debug.Log($"{enemyType.enemyName}: The path is blocked by line of sight at corner {k}");
                        }
                        return true;
                    }
                    if (k > 15)
                    {
                        if (DebugEnemy)
                        {
                            Debug.Log(enemyType.enemyName + ": Reached corner 15, stopping checks now");
                        }
                        return false;
                    }
                }
            }
            return false;
        }*/

        public Transform ChooseFarthestNodeFromPosition(Vector3 pos, bool avoidLineOfSight = false, int offset = 0, bool doAsync = false, int maxAsyncIterations = 50, bool capDistance = false, bool checkLOSToTargetPlayer = false)
        {
            if (!doAsync || gotFarthestNodeAsync || getFarthestNodeAsyncBookmark <= 0 || nodesTempArray == null || nodesTempArray.Length == 0)
            {
                nodesTempArray = allAINodes.OrderByDescending((GameObject x) => Vector3.Distance(pos, x.transform.position)).ToArray();
            }
            Transform result = nodesTempArray[0].transform;
            int num = 0;
            if (doAsync)
            {
                if (getFarthestNodeAsyncBookmark >= nodesTempArray.Length)
                {
                    getFarthestNodeAsyncBookmark = 0;
                }
                num = getFarthestNodeAsyncBookmark;
                gotFarthestNodeAsync = false;
            }
            for (int i = num; i < nodesTempArray.Length; i++)
            {
                if (doAsync && i - getFarthestNodeAsyncBookmark > maxAsyncIterations)
                {
                    gotFarthestNodeAsync = false;
                    getFarthestNodeAsyncBookmark = i;
                    return null;
                }
                if ((!capDistance || !(Vector3.Distance(base.transform.position, nodesTempArray[i].transform.position) > 60f)) && !PathIsIntersectedByLineOfSight(nodesTempArray[i].transform.position, calculatePathDistance: false, avoidLineOfSight: avoidLineOfSight, checkLOSToTargetPlayer: checkLOSToTargetPlayer))
                {
                    mostOptimalDistance = Vector3.Distance(pos, nodesTempArray[i].transform.position);
                    result = nodesTempArray[i].transform;
                    if (offset == 0 || i >= nodesTempArray.Length - 1)
                    {
                        break;
                    }
                    offset--;
                }
            }
            getFarthestNodeAsyncBookmark = 0;
            gotFarthestNodeAsync = true;
            return result;
        }

        // Returns 0..1, where 1 = directly looking at SCP-2006, 0 = fully turned away
        float GetVisibilityFromAngle(PlayerControllerB player)
        {
            if (player == null) return 0f;

            Vector3 toSCP = (transform.position - player.transform.position).normalized;
            Vector3 playerForward = player.gameplayCamera.transform.forward;

            // angle in degrees between player's view direction and SCP-2006
            float angle = Vector3.Angle(playerForward, toSCP);

            // convert 0°–180° to 1–0 linearly
            float visibility = 1f - Mathf.Clamp01(angle / 180f);
            return visibility;
        }

        float CalculateSpeed(float visibility, float minSpeed, float maxSpeed)
        {
            return Mathf.Lerp(minSpeed, maxSpeed, visibility);
        }

        /*void SneakTargetPlayer()
        {
            float visibility = GetVisibilityFromAngle(targetPlayer);
            agent.speed = CalculateSpeed(visibility, 6f, 15f);

            if (visibility < 0.5f)
            {
                if (hasLOSToTargetPlayer)
                {
                    StopMovement();
                    return;
                }

                SetDestinationToPosition(targetPlayer.transform.position);
            }
            else
            {
                AvoidTargetPlayer();
            }
        }*/

        void SneakTargetPlayer()
        {
            agent.speed = Mathf.Clamp(targetPlayerDistance, 7f, 15f);

            Vector3 losCheckPosition = GetPointOnPathTowardsPlayer(transform.position, targetPlayer.transform.position, 3f); // TODO: Test this

            if (!targetPlayer.HasLineOfSightToPosition(losCheckPosition + Vector3.up * lineOfSightOffset))
            {
                agent.speed = Mathf.Clamp(targetPlayerDistance, 7f, 15f);
                retreating = false;
                if (hasLOSToTargetPlayer)
                {
                    StopMovement();
                    return;
                }

                SetDestinationToPosition(targetPlayer.transform.position);
            }
            else
            {
                if (!targetPlayerHasLOS) // TODO: Test
                {
                    StopMovement();
                    return;
                }
                agent.speed = 15f;
                retreating = true;
                AvoidTargetPlayer(10f);
            }
        }

        public PlayerControllerB? GetClosestPlayerVisibleTo()
        {
            PlayerControllerB? result = null;
            float closestDistance = Mathf.Infinity;

            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == null || !player.isPlayerControlled) { continue; }
                float distance = Vector3.Distance(player.transform.position, transform.position);
                if (distance > closestDistance || !player.HasLineOfSightToPosition(transform.position + Vector3.up * lineOfSightOffset)) { continue; }
                closestDistance = distance;
                result = player;
            }

            closestPlayerVisibleToDistance = closestDistance;
            return result;
        }

        private void StopMovement()
        {
            agent.speed = 0f;
            agent.velocity = Vector3.zero;
            moveTowardsDestination = false;
        }

        void AddOffsetToScareCooldown(float offset)
        {
            scareCooldown += offset;
            scareCooldown = Mathf.Min(scareCooldown, maxScareCooldown);
        }

        List<GameObject> GetLearnableEnemies()
        {
            List<GameObject> enemies = [];

            foreach(var enemy in RoundManager.Instance.SpawnedEnemies)
            {
                ScareDef? def = scareDefs.FirstOrDefault(x => x.enemyTypeName == enemy.enemyType.name);
                if (def == null) { continue; }
                if (learnedScares.ContainsKey(def)) { continue; }
                enemies.Add(enemy.gameObject);
            }

            return enemies;
        }

        public bool CanPathToTVSet()
        {
            Vector3 shipPosition = TVSetUnlockable.Instance != null ? TVSetUnlockable.Instance.transform.position : terminalScript.transform.position;
            if (isInsideFactory)
            {
                return Utils.CalculatePath(transform.position, mainEntranceInsidePosition) && Utils.CalculatePath(mainEntranceOutsidePosition, shipPosition);
            }
            return Utils.CalculatePath(transform.position, shipPosition);
        }

        bool GetClosestPlayerWithTape(float distance, out PlayerControllerB? playerWithTape)
        {
            playerWithTape = null;
            float closestDistance = distance;

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == null || !PlayerIsTargetable(player) || player.currentlyHeldObjectServer is not VHSTapeBehavior) { continue; }
                float _distance = Vector3.Distance(transform.position, player.transform.position);
                if (_distance > closestDistance) { continue; }
                playerWithTape = player;
                closestDistance = _distance;
            }

            return playerWithTape != null;
        }

        public void TriggerReaction(string actionName)
        {
            if (!IsServer) { return; }
            // ignore if already triggered once
            if (triggeredActions.Contains(actionName)) return;

            triggeredActions.Add(actionName);

            if (points.TryGetValue(actionName, out int p))
                score += p;

            logger.LogDebug($"Triggered {actionName}: +{points[actionName]} (total {score})");
        }

        void AddScarePoint(ScareDef scareDef)
        {
            if (scareDef == null) { return; }
            if (!learnedScares.ContainsKey(scareDef)) { learnedScares.Add(scareDef, 0); }

            learnedScares[scareDef]++;
            logger.LogDebug("Added scare point for: " + scareDef);
        }

        void RemoveScarePoint(ScareDef scareDef)
        {
            if (!learnedScares.ContainsKey(scareDef)) { return; }

            learnedScares[scareDef]--;
            if (learnedScares[scareDef] < 1) { learnedScares.Remove(scareDef); }
            logger.LogDebug("Removed scare point for: " + scareDef);
        }

        ScareDef GetWeightedRandomScare()
        {
            if (Utils.testing && DEBUG_scareIndex >= 0)
            {
                return scareDefs[DEBUG_scareIndex];
            }
            Dictionary<ScareDef, int> scares = learnedScares.Where(x => x.Key.enemyTypeName == "" || x.Key.outside == isOutside).ToDictionary(x => x.Key, x => x.Value);

            ScareDef[] _scareDefs = scares.Keys.ToArray();
            int[] weights = scares.Values.ToArray();

            int randomIndex = GetRandomWeightedIndex(weights);
            return _scareDefs[randomIndex];
        }

        public int GetRandomWeightedIndex(int[] weights)
        {
            if (weights == null || weights.Length == 0)
            {
                Debug.Log("Could not get random weighted index; array is empty or null.");
                return -1;
            }
            int num = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] >= 0)
                {
                    num += weights[i];
                }
            }
            if (num <= 0)
            {
                return UnityEngine.Random.Range(0, weights.Length);
            }
            float num2 = UnityEngine.Random.Range(0f, 1f);
            float num3 = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (!((float)weights[i] <= 0f))
                {
                    num3 += (float)weights[i] / (float)num;
                    if (num3 >= num2)
                    {
                        return i;
                    }
                }
            }
            Debug.LogError("Error while calculating random weighted index. Choosing randomly. Weights given:");
            for (int i = 0; i < weights.Length; i++)
            {
                Debug.LogError($"{weights[i]},");
            }
            return UnityEngine.Random.Range(0, weights.Length);
        }

        bool HasReachedDestination()
        {
            if (!agent.pathPending) // Wait until the path is calculated
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

        bool HasReachedTargetNode()
        {
            if (targetNode == null) { return true; }
            return Vector3.Distance(transform.position, targetNode.position) < 1f;
        }

        public void Teleport(Vector3 position, bool outside)
        {
            logger.LogDebug($"Teleporting to position: {position.ToString()} Outside: {outside.ToString()}");
            position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit);
            agent.Warp(position);
            SetEnemyOutsideClientRpc(outside);
            agent.ResetPath();
        }

        public bool SetDestinationToPositionUseEntrance(Vector3 position, bool checkForPath = false)
        {
            if (agent == null || agent.enabled == false) { return false; }
            if (!SetDestinationToPosition(position, checkForPath: true))
            {
                Vector3 entranceOtherSide = isInsideFactory ? mainEntranceOutsidePosition : mainEntranceInsidePosition;

                return Utils.CalculatePath(entranceOtherSide, position) && SetDestinationToEntrance(checkForPath: checkForPath);
            }
            return true;
        }

        bool SetDestinationToEntrance(bool checkForPath = false)
        {
            if (agent == null || agent.enabled == false) { return false; }
            if (isInsideFactory)
            {
                if (!SetDestinationToPosition(mainEntranceInsidePosition, checkForPath: checkForPath)) { return false; }

                if (Vector3.Distance(transform.position, mainEntranceInsidePosition) < 1f)
                {
                    Teleport(mainEntranceOutsidePosition, true);
                }
            }
            else
            {
                if (!SetDestinationToPosition(mainEntranceOutsidePosition, checkForPath: checkForPath)) { return false; }

                if (Vector3.Distance(transform.position, mainEntranceOutsidePosition) < 1f)
                {
                    Teleport(mainEntranceInsidePosition, false);
                }
            }

            return true;
        }

        bool HasLineOfSightToPlayer(PlayerControllerB player, float width = 45f, int range = 20, int proximityAwareness = -1)
        {
            if (isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
            {
                range = Mathf.Clamp(range, 0, 30);
            }
            Vector3 position = player.gameplayCamera.transform.position;
            if (Vector3.Distance(position, eye.position) < (float)range && !Physics.Linecast(eye.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                Vector3 to = position - eye.position;
                if (Vector3.Angle(eye.forward, to) < width || (proximityAwareness != -1 && Vector3.Distance(eye.position, position) < (float)proximityAwareness))
                {
                    return true;
                }
            }
            return false;
        }

        public void StalkingChooseClosestNodeToPlayer()
        {
            if (targetNode == null)
            {
                targetNode = allAINodes[0].transform;
            }
            Transform? transform = ChooseClosestNodeToPosition(targetPlayer.transform.position, avoidLineOfSight: true);
            if (transform != null)
            {
                targetNode = transform;
            }
            if (targetPlayerDistance - mostOptimalDistance < 0.1f && (!PathIsIntersectedByLineOfSight(targetPlayer.transform.position, calculatePathDistance: true) || targetPlayerDistance < 3f))
            {
                if (pathDistance > 10f && !ignoredNodes.Contains(targetNode) && ignoredNodes.Count < 4)
                {
                    ignoredNodes.Add(targetNode);
                }
            }
            else
            {
                SetDestinationToPosition(targetNode.position);
            }
        }

        /* NoiseIDs
         * 6: player footsteps
         * 75: player voice chat
         */

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
            
            if (currentBehaviourStateIndex == (int)State.Reaction
                && targetPlayer != null
                && noiseLoudness >= playerScreamMinVolume
                && noiseID == 75
                && Vector3.Distance(targetPlayer.transform.position, noisePosition) < 1f)
            {
                TriggerReaction("Yell");
            }
        }

        void TrackCameraMovement()
        {
            if (triggeredActions.Contains("CameraTurn")) { return; }
            Vector2 currentAngles = new Vector2(targetPlayer.gameplayCamera.transform.eulerAngles.x, targetPlayer.gameplayCamera.transform.eulerAngles.y);

            // Calculate delta, account for angle wrapping (360 to 0)
            float deltaX = Mathf.DeltaAngle(lastCameraAngles.x, currentAngles.x);
            float deltaY = Mathf.DeltaAngle(lastCameraAngles.y, currentAngles.y);

            // Combine both axes into a single turn speed value
            float cameraTurnSpeed = new Vector2(deltaX, deltaY).magnitude / Time.deltaTime;
            lastCameraAngles = currentAngles;

            if (cameraTurnSpeed > maxTurnSpeed && timeSinceSwitchBehavior > 1f)
            {
                TriggerReaction("CameraTurn");
            }
        }

        public bool PlayerIsTargetable(PlayerControllerB playerScript)
        {
            if (playerScript.isPlayerControlled && !playerScript.isPlayerDead && playerScript.inAnimationWithEnemy == null && playerScript.sinkingValue < 0.73f)
            {
                return true;
            }
            return false;
        }

        void GetCurrentMaterialStandingOn()
        {
            Ray interactRay = new Ray(transform.position + Vector3.up, -Vector3.up);
            if (!Physics.Raycast(interactRay, out RaycastHit hit, 6f, StartOfRound.Instance.walkableSurfacesMask, QueryTriggerInteraction.Ignore) || hit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].surfaceTag))
            {
                return;
            }
            for (int i = 0; i < StartOfRound.Instance.footstepSurfaces.Length; i++)
            {
                if (hit.collider.CompareTag(StartOfRound.Instance.footstepSurfaces[i].surfaceTag))
                {
                    currentFootstepSurfaceIndex = i;
                    break;
                }
            }
        }

        public override void HitEnemy(int force = 0, PlayerControllerB? playerWhoHit = null, bool playHitSFX = true, int hitID = -1) // Synced
        {
            logger.LogDebug("In HitEnemy()");

            if (currentBehaviourStateIndex == (int)State.Reaction)
            {
                TriggerReaction("Attack");
                return;
            }

            if (timeSinceLastHit < 3f || currentBehaviourStateIndex == (int)State.Resting) { return; }
            timeSinceLastHit = 0f;

            inSpecialAnimation = true;
            logger.LogDebug("DoAnimation: spotted");
            creatureAnimator.SetTrigger("spotted");
            PlayRandomClip(AudioArray.Hit);

            if (!IsServer) { return; }
            targetPlayer = null;
            if (currentBehaviourStateIndex == (int)State.Spotted) { return; }
            SwitchToBehaviourClientRpc((int)State.Spotted);
        }

        public void PlayRandomClip(AudioArray audioArray, float volume = 1f)
        {
            timeSinceLastVoiceLine = 0f;
            creatureVoice.Stop(stopOneShots: true);
            creatureVoice.pitch = UnityEngine.Random.Range(0.94f, 1.06f);

            int index = -1;

            switch (audioArray)
            {
                case AudioArray.Bait:
                    index = audioRandom.Next(baitSFX.Length);
                    creatureVoice.PlayOneShot(baitSFX[index], volume);
                    break;
                case AudioArray.Scare:
                    index = audioRandom.Next(scareSFX.Length);
                    creatureVoice.PlayOneShot(scareSFX[index], volume);
                    break;
                case AudioArray.Hit:
                    index = audioRandom.Next(hitSFX.Length);
                    creatureVoice.PlayOneShot(hitSFX[index], volume);
                    break;
                case AudioArray.ReactionFailed:
                    index = audioRandom.Next(reactionFailedSFX.Length);
                    creatureVoice.PlayOneShot(reactionFailedSFX[index], volume);
                    break;
                case AudioArray.ReactionSuccess:
                    index = audioRandom.Next(reactionSuccessSFX.Length);
                    creatureVoice.PlayOneShot(reactionSuccessSFX[index], volume);
                    break;
                case AudioArray.Roaming:
                    index = audioRandom.Next(roamingSFX.Length);
                    creatureVoice.PlayOneShot(roamingSFX[index], volume);
                    break;
                case AudioArray.SeenSneaking:
                    index = audioRandom.Next(seenSneakingSFX.Length);
                    creatureVoice.PlayOneShot(seenSneakingSFX[index], volume);
                    break;
                case AudioArray.Sneaking:
                    index = audioRandom.Next(sneakingSFX.Length);
                    creatureVoice.PlayOneShot(sneakingSFX[index], volume);
                    break;
                case AudioArray.WatchingTape:
                    index = audioRandom.Next(watchingTapeSFX.Length);
                    creatureVoice.PlayOneShot(watchingTapeSFX[index], volume);
                    break;
                default:
                    break;
            }

            logger.LogDebug("Played audio at index: " + index);
        }

        // Animation Functions

        public void SetInSpecialAnimationFalse() // Animation
        {
            logger.LogDebug("inSpecialAnimation false");
            inSpecialAnimation = false;
        }

        public void PlayFootstepSFX() // Animation
        {
            if (currentBehaviourStateIndex == (int)State.Sneaking) { return; }
            GetCurrentMaterialStandingOn();
            int index = UnityEngine.Random.Range(0, StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].clips.Length);
            if (index == previousFootstepClip)
            {
                index = (index + 1) % StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].clips.Length;
            }
            creatureSFX.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
            creatureSFX.PlayOneShot(StartOfRound.Instance.footstepSurfaces[currentFootstepSurfaceIndex].clips[index], 0.6f);
            previousFootstepClip = index;
        }

        public void PlayLaughSFX() // Animation
        {
            logger.LogDebug("PlayLaughSFX");
            creatureVoice.PlayOneShot(laughSFX);
        }

        public void FinishLaughAnimation() // Animation
        {
            logger.LogDebug("FinishLaughAnimation called");
            inSpecialAnimation = false;
            PlayRandomClip(AudioArray.ReactionSuccess);
        }

        public void FinishWaveAnimation() // Animation
        {
            logger.LogDebug("FinishWaveAnimation called");
            inSpecialAnimation = false;
            facePlayer = false;
        }

        public void FinishThinkAnimation() // Animation
        {
            logger.LogDebug("FinishThinkAnimation called");
            inSpecialAnimation = false;
            faceEnemy = false;
        }

        public void GiveTape() // InteractTrigger
        {
            logger.LogDebug("Giving tape to SCP-2006");
            VHSTapeBehavior? tape = localPlayer.currentlyHeldObjectServer as VHSTapeBehavior;
            if (tape == null || tape is not VHSTapeBehavior || !tape.NetworkObject.IsSpawned) { return; }

            localPlayer.DiscardHeldObject();
            GiveTapeServerRpc(tape.NetworkObject);
        }

        public void SpawnMimicEnemy()
        {
            if (currentScareDef == null || currentScareDef.enemyTypeName == "")
            {
                SetDefaultScareClientRpc(UnityEngine.Random.Range(0, baitSFX.Length));
                return;
            }
            SpawnableEnemyWithRarity spawnableEnemy = Utils.GetEnemies().Where(x => x.enemyType.name == currentScareDef!.enemyTypeName).First();
            GameObject enemyPrefab = spawnableEnemy.enemyType.enemyPrefab;
            GameObject enemyObj = Instantiate(enemyPrefab, transform.position, transform.rotation, transform);
            enemyObj.GetComponent<NetworkObject>().Spawn(true);
            mimicEnemy = enemyObj.GetComponent<EnemyAI>();
            
            SpawnMimicEnemyClientRpc(mimicEnemy.NetworkObject, UnityEngine.Random.Range(0, currentScareDef.baitSFX.Length));
        }

        public void DespawnMimicEnemy()
        {
            if (mimicEnemy == null || !mimicEnemy.NetworkObject.IsSpawned) { return; }
            logger.LogDebug("Despawning mimic enemy " + mimicEnemy.enemyType.name);

            mimicEnemy.NetworkObject.Despawn(true);
            mimicEnemy = null;
            EnableEnemyMesh(true);
            DespawnMimicEnemyClientRpc();
        }

        public void DropTapeOnLocalClient(Vector3 position)
        {
            if (heldTape == null)
            {
                return;
            }
            GrabbableObject tape = heldTape;
            tape.parentObject = null;
            tape.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
            tape.EnablePhysics(enable: true);
            tape.fallTime = 0f;
            tape.startFallingPosition = tape.transform.parent.InverseTransformPoint(tape.transform.position);
            tape.targetFloorPosition = tape.transform.parent.InverseTransformPoint(position);
            tape.floorYRot = -1;
            tape.DiscardItemFromEnemy();
            tape.isHeldByEnemy = false;
            HoarderBugAI.grabbableObjectsInMap.Add(tape.gameObject);
            heldTape = null;
        }

        public void OnTapeEjected()
        {
            if (!resting || !IsServer) { return; }

            resting = false;
            agent.enabled = true;
            Teleport(transform.position, true);
            SwitchToBehaviourClientRpc((int)State.Spotted);
        }

        public new void SwitchToBehaviourStateOnLocalClient(int stateIndex)
        {
            if (currentBehaviourStateIndex != stateIndex)
            {
                logger.LogDebug("Switching behavior to " + (State)stateIndex);
                previousBehaviourStateIndex = currentBehaviourStateIndex;
                currentBehaviourStateIndex = stateIndex;
                currentBehaviourState = enemyBehaviourStates[stateIndex];
                PlayAudioOfCurrentState();
                PlayAnimationOfCurrentState();

                timeSinceSwitchBehavior = 0f;

                creatureAnimator.SetBool("sneaking", currentBehaviourStateIndex == (int)State.Sneaking);
                creatureAnimator.SetBool("wanting", currentBehaviourStateIndex == (int)State.Wanting);
                creatureAnimator.SetBool("running", currentBehaviourStateIndex == (int)State.Spotted);

                BehaviourSwitchCleanUp();
            }
        }

        public void BehaviourSwitchCleanUp()
        {
            facePlayer = false;
            faceEnemy = false;
            spottedByOtherPlayer = false;

            if (!IsServer) { return; } // Server
            agent.enabled = true;
            agent.ResetPath();
            agent.stoppingDistance = 0f;
            ignoredNodes.Clear();
            waved = false;
        }

        // RPC's

        [ClientRpc]
        public new void SwitchToBehaviourClientRpc(int stateIndex)
        {
            if (stateIndex != currentBehaviourStateIndex)
            {
                SwitchToBehaviourStateOnLocalClient(stateIndex);
            }
        }

        [ClientRpc]
        public void PlayRandomClipClientRpc(AudioArray audioArray, float volume = 1f)
        {
            PlayRandomClip(audioArray, volume);
        }

        [ClientRpc]
        public void PlayRandomClipClientRpc(AudioArray audioArray, ulong excludePlayerId, float volume = 1f)
        {
            if (localPlayer.actualClientId == excludePlayerId)
            {
                volume = 0f;
            }

            PlayRandomClip(audioArray, volume);
        }

        /*[ClientRpc]
        public void PlayClipClientRpc(AudioArray audioArray, int index, float volume = 1f)
        {
            creatureVoice.Stop(stopOneShots: true);
            creatureVoice.pitch = UnityEngine.Random.Range(0.94f, 1.06f);

            switch (audioArray)
            {
                case AudioArray.Bait:
                    creatureVoice.PlayOneShot(baitSFX[index], volume);
                    break;
                case AudioArray.Scare:
                    creatureVoice.PlayOneShot(scareSFX[index], volume);
                    break;
                case AudioArray.Hit:
                    creatureVoice.PlayOneShot(hitSFX[index], volume);
                    break;
                case AudioArray.ReactionFailed:
                    creatureVoice.PlayOneShot(reactionFailedSFX[index], volume);
                    break;
                case AudioArray.ReactionSuccess:
                    creatureVoice.PlayOneShot(reactionSuccessSFX[index], volume);
                    break;
                case AudioArray.Roaming:
                    creatureVoice.PlayOneShot(roamingSFX[index], volume);
                    break;
                case AudioArray.SeenSneaking:
                    creatureVoice.PlayOneShot(seenSneakingSFX[index], volume);
                    break;
                case AudioArray.Sneaking:
                    creatureVoice.PlayOneShot(sneakingSFX[index], volume);
                    break;
                case AudioArray.WatchingTape:
                    creatureVoice.PlayOneShot(watchingTapeSFX[index], volume);
                    break;
                default:
                    break;
            }
        }*/

        [ClientRpc]
        public void WatchTapeClientRpc()
        {
            if (heldTape == null) { return; }
            VHSTapeBehavior tape = heldTape;

            logger.LogDebug("DoAnimation: rest");
            creatureAnimator.SetTrigger("rest");
            PlayRandomClip(AudioArray.WatchingTape);

            if (TVSetUnlockable.Instance.tapeInVHS)
            {
                TVSetUnlockable.Instance.EjectVHS();
            }

            DropTapeOnLocalClient(TVSetUnlockable.Instance.insertTapePosition.position);
            TVSetUnlockable.Instance.InsertVHS(tape);
        }

        [ClientRpc]
        public void SpottedClientRpc()
        {
            inSpecialAnimation = true;
            PlayRandomClip(AudioArray.SeenSneaking);

            logger.LogDebug("DoAnimation: spotted");
            creatureAnimator.SetTrigger("spotted");

            SwitchToBehaviourStateOnLocalClient((int)State.Spotted);
        }

        [ClientRpc]
        public void SetDefaultScareClientRpc(int clipIndex)
        {
            currentScareDef = scareDefs.Where(x => x.enemyTypeName == "").First();
            creatureVoice.PlayOneShot(baitSFX[clipIndex]);
        }

        [ClientRpc]
        public void SpawnMimicEnemyClientRpc(NetworkObjectReference netRef, int baitSFXIndex)
        {
            if (!netRef.TryGet(out NetworkObject netObj)) { logger.LogError("Couldnt find network object in SpawnMimicEnemyClientRpc"); return; }
            if (!netObj.TryGetComponent(out mimicEnemy)) { logger.LogError("Couldnt find EnemyAI component in SpawnMimicEnemyClientRpc"); return; }
            if (mimicEnemy == null) { return; }

            //mimicEnemy.enabled = false; // TODO: Test this
            mimicEnemy.inSpecialAnimation = true; // TODO: Test this
            foreach (var collider in mimicEnemy.transform.root.gameObject.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            particleSystem.Play();
            EnableEnemyMesh(false);

            currentScareDef = scareDefs.Where(x => x.enemyTypeName == mimicEnemy.enemyType.name).First();

            if (baitSFXIndex < 0) { return; }
            creatureVoice.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            creatureVoice.PlayOneShot(currentScareDef.baitSFX[baitSFXIndex]);
        }

        [ClientRpc]
        public void DespawnMimicEnemyClientRpc()
        {
            mimicEnemy = null;
            particleSystem.Play();
            EnableEnemyMesh(true);
            creatureVoice.Stop(stopOneShots: true);
        }

        [ClientRpc]
        public void ScareClientRpc(int scareSFXIndex, ulong targetPlayerId)
        {
            targetPlayer = PlayerFromId(targetPlayerId);
            if (targetPlayer == null) { logger.LogError("Couldnt get target player in ScareClientRpc"); return; }
            if (currentScareDef == null || currentScareDef.enemyTypeName == "")
            {
                PlayRandomClip(AudioArray.Scare);
                logger.LogDebug("DoAnimation: scare");
                creatureAnimator.SetTrigger("scare");
            }
            else
            {
                creatureVoice.Stop(stopOneShots: true);

                if (currentScareDef.animState != "")
                    mimicEnemy.creatureAnimator.Play(currentScareDef.animState); // TODO: Test this

                if (currentScareDef.scareSFX.Length <= 0) { return; }
                creatureVoice.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                creatureVoice.PlayOneShot(currentScareDef.scareSFX[scareSFXIndex]);
            }

            triggeredActions.Clear();
            SwitchToBehaviourStateOnLocalClient((int)State.Reaction);
        }

        [ClientRpc]
        public void DoAnimationClientRpc(string animationName, bool value)
        {
            creatureAnimator.SetBool(animationName, value);
            logger.LogDebug("DoAnimation: " + animationName + ": " + value);
        }

        [ClientRpc]
        public void DoAnimationClientRpc(string animationName)
        {
            creatureAnimator.SetTrigger(animationName);
            logger.LogDebug("DoAnimation: " + animationName);
        }

        [ClientRpc]
        public void SetEnemyOutsideClientRpc(bool value)
        {
            isOutside = value;
        }

        [ServerRpc(RequireOwnership = false)]
        public void GiveTapeServerRpc(NetworkObjectReference netRef)
        {
            if (!IsServer) { return; }
            GiveTapeClientRpc(netRef);
        }

        [ClientRpc]
        public void GiveTapeClientRpc(NetworkObjectReference netRef)
        {
            if (!netRef.TryGet(out NetworkObject netObj)) { logger.LogError("Couldnt get network object from network object reference"); return; }
            if (!netObj.TryGetComponent(out VHSTapeBehavior tape)) { logger.LogError("Couldnt get VHSTapeBehavior component from network object"); return; }

            tape.parentObject = handTransform;
            tape.hasHitGround = false;
            tape.isHeldByEnemy = true;
            tape.GrabItemFromEnemy(this);
            tape.EnablePhysics(false);
            HoarderBugAI.grabbableObjectsInMap.Remove(tape.gameObject);
            heldTape = tape;
            creatureVoice.Stop(stopOneShots: true);
            creatureVoice.PlayOneShot(tapeGivenSFX);

            SwitchToBehaviourStateOnLocalClient((int)State.Resting);
        }

        [ClientRpc]
        public void DropTapeClientRpc(Vector3 position)
        {
            DropTapeOnLocalClient(position);
        }

        [ClientRpc]
        public void DEBUG_FillLearnedScaresClientRpc()
        {
            learnedScares.Clear();

            foreach(var scare in scareDefs)
            {
                learnedScares.Add(scare, 1);
            }

            HUDManager.Instance.DisplayTip("Server", $"Added {learnedScares.Count} scare defs to learned scares.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void TriggerReactionServerRpc(string actionName)
        {
            if (!IsServer) { return; }
            TriggerReaction(actionName);
        }
    }
}