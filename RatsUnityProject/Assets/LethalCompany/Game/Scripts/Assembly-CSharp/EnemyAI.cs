using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public abstract class EnemyAI : NetworkBehaviour
{
	public EnemyType enemyType;

	[Space(5f)]
	public SkinnedMeshRenderer[] skinnedMeshRenderers;

	public MeshRenderer[] meshRenderers;

	public Animator creatureAnimator;

	public AudioSource creatureVoice;

	public AudioSource creatureSFX;

	public Transform eye;

	public AudioClip dieSFX;

	[Space(3f)]
	[Space(3f)]
	public EnemyBehaviourState[] enemyBehaviourStates;

	public EnemyBehaviourState currentBehaviourState;

	public int currentBehaviourStateIndex;

	public int previousBehaviourStateIndex;

	public int currentOwnershipOnThisClient = -1;

	public bool isInsidePlayerShip;

	[Header("AI Calculation / Netcode")]
	public float AIIntervalTime = 0.2f;

	public bool inSpecialAnimation;

	public PlayerControllerB inSpecialAnimationWithPlayer;

	[HideInInspector]
	public Vector3 serverPosition;

	[HideInInspector]
	public Vector3 serverRotation;

	private float previousYRotation;

	private float targetYRotation;

	public NavMeshAgent agent;

	[HideInInspector]
	public NavMeshPath path1;

	public GameObject[] allAINodes;

	public int agentMask;

	public Transform targetNode;

	public Transform favoriteSpot;

	public float tempDist;

	public float mostOptimalDistance;

	public float pathDistance;

	[HideInInspector]
	public NetworkObject thisNetworkObject;

	public int thisEnemyIndex;

	public bool isClientCalculatingAI;

	public float updatePositionThreshold = 1f;

	private Vector3 tempVelocity;

	public PlayerControllerB targetPlayer;

	public bool movingTowardsTargetPlayer;

	public bool moveTowardsDestination = true;

	public Vector3 destination;

	private Vector3 prevDestination;

	public float addPlayerVelocityToDestination;

	private float updateDestinationInterval;

	public float syncMovementSpeed = 0.22f;

	public float timeSinceSpawn;

	public float exitVentAnimationTime = 1f;

	public bool ventAnimationFinished;

	[Space(5f)]
	public bool isEnemyDead;

	public bool daytimeEnemyLeaving;

	public int enemyHP = 3;

	public GameObject[] nodesTempArray;

	public float openDoorSpeedMultiplier;

	public bool useSecondaryAudiosOnAnimatedObjects;

	public AISearchRoutine currentSearch;

	public Coroutine searchCoroutine;

	public Coroutine chooseTargetNodeCoroutine;

	private RaycastHit raycastHit;

	private Ray LOSRay;

	public bool DebugEnemy;

	public int stunnedIndefinitely;

	public float stunNormalizedTimer;

	public float postStunInvincibilityTimer;

	public PlayerControllerB stunnedByPlayer;

	protected float setDestinationToPlayerInterval;

	public bool debugEnemyAI;

	[HideInInspector]
	public bool removedPowerLevel;

	public bool isOutside;

	public bool hitsPhysicsObjects;

	private System.Random searchRoutineRandom;

	private int getFarthestNodeAsyncBookmark;

	public bool gotFarthestNodeAsync;

	private Collider[] overlapColliders;

	private float enemySwimTimer;

	private bool swimming;

	protected GameObject nestObject;

	private bool disabledMeshes;

	[HideInInspector]
	public bool overrideSettingEnemyMeshes;

	private bool targetPlayerPositionHorizontallyOffset;

	private float refreshDestinationInterval;

	public virtual void SetEnemyStunned(bool setToStunned, float setToStunTime = 1f, PlayerControllerB setStunnedByPlayer = null)
	{
		Debug.Log("Set enemy stunned called!");
		if (isEnemyDead || !enemyType.canBeStunned)
		{
			return;
		}
		if (setToStunned)
		{
			if (!(postStunInvincibilityTimer >= 0f))
			{
				if (stunNormalizedTimer <= 0f && creatureVoice != null)
				{
					creatureVoice.PlayOneShot(enemyType.stunSFX);
				}
				stunnedByPlayer = setStunnedByPlayer;
				postStunInvincibilityTimer = 0.5f;
				stunNormalizedTimer = setToStunTime;
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

	public virtual void UseNestSpawnObject(EnemyAINestSpawnObject nestSpawnObject)
	{
		agent.enabled = false;
		base.transform.position = nestSpawnObject.transform.position;
		base.transform.rotation = nestSpawnObject.transform.rotation;
		agent.enabled = true;
		if (RoundManager.Instance.enemyNestSpawnObjects.Contains(nestSpawnObject))
		{
			RoundManager.Instance.enemyNestSpawnObjects.Remove(nestSpawnObject);
		}
		nestObject = nestSpawnObject.gameObject;
		if (!enemyType.useMinEnemyThresholdForNest)
		{
			Debug.Log($"Enemy {base.gameObject.name} #{thisEnemyIndex} destroying nest object '{nestSpawnObject.gameObject}'");
			UnityEngine.Object.Destroy(nestSpawnObject.gameObject);
		}
	}

	public void GetAINodes()
	{
		RoundManager.Instance.GetOutsideAINodes();
		if (agent == null)
		{
			agent = base.gameObject.GetComponent<NavMeshAgent>();
		}
		agentMask = RoundManager.Instance.GetLayermaskForEnemySizeLimit(enemyType, supplyExistingMask: true, agent.areaMask);
		agent.areaMask = agentMask;
		if (!isOutside)
		{
			allAINodes = GameObject.FindGameObjectsWithTag("AINode");
			return;
		}
		RoundManager.Instance.GetOutsideAINodes();
		if (enemyType.WaterType == EnemyWaterType.LandOnly)
		{
			allAINodes = RoundManager.Instance.outsideAIDryNodesUnordered;
		}
		else if (enemyType.WaterType == EnemyWaterType.WaterOnly)
		{
			allAINodes = RoundManager.Instance.outsideAIWaterNodesUnordered;
		}
		else
		{
			allAINodes = RoundManager.Instance.outsideAINodesUnordered;
		}
	}

	public virtual void Awake()
	{
		thisEnemyIndex = RoundManager.Instance.numberOfEnemiesInScene;
	}

	public virtual void Start()
	{
		try
		{
			overlapColliders = new Collider[1];
			agent = base.gameObject.GetComponentInChildren<NavMeshAgent>();
			skinnedMeshRenderers = (from x in base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>()
				where x.GetComponentInParent<PlayerControllerB>() == null && x.GetComponentInParent<GrabbableObject>() == null
				select x).ToArray();
			meshRenderers = (from x in base.gameObject.GetComponentsInChildren<MeshRenderer>()
				where x.GetComponentInParent<PlayerControllerB>() == null && x.GetComponentInParent<GrabbableObject>() == null
				select x).ToArray();
			if (creatureAnimator == null)
			{
				creatureAnimator = base.gameObject.GetComponentInChildren<Animator>();
			}
			thisNetworkObject = base.gameObject.GetComponentInChildren<NetworkObject>();
			RoundManager.Instance.numberOfEnemiesInScene++;
			isOutside = enemyType.isOutsideEnemy;
			GetAINodes();
			if (enemyType.isOutsideEnemy)
			{
				if (enemyType.nestSpawnPrefab != null)
				{
					bool flag = false;
					for (int num = 0; num < RoundManager.Instance.enemyNestSpawnObjects.Count; num++)
					{
						if (RoundManager.Instance.enemyNestSpawnObjects[num] == null)
						{
							RoundManager.Instance.enemyNestSpawnObjects.RemoveAt(num);
						}
						else if (RoundManager.Instance.enemyNestSpawnObjects[num].enemyType == enemyType)
						{
							flag = true;
							UseNestSpawnObject(RoundManager.Instance.enemyNestSpawnObjects[num]);
							break;
						}
					}
					if (!flag && enemyType.requireNestObjectsToSpawn)
					{
						isEnemyDead = true;
						inSpecialAnimation = true;
						if (base.IsServer)
						{
							UnityEngine.Object.Destroy(base.gameObject);
						}
						else
						{
							EnableEnemyMesh(enable: false);
						}
						return;
					}
				}
				if (GameNetworkManager.Instance.localPlayerController != null)
				{
					EnableEnemyMesh(!StartOfRound.Instance.hangarDoorsClosed || !GameNetworkManager.Instance.localPlayerController.isInHangarShipRoom);
				}
			}
			if (!base.IsServer)
			{
				RoundManager.Instance.SpawnedEnemies.Add(this);
			}
			path1 = new NavMeshPath();
			openDoorSpeedMultiplier = enemyType.doorSpeedMultiplier;
			serverPosition = base.transform.position;
			if (base.IsOwner)
			{
				SyncPositionToClients();
			}
			else
			{
				SetClientCalculatingAI(enable: false);
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"Error when initializing enemy variables for {base.gameObject.name} : {arg}");
		}
	}

	public PlayerControllerB MeetsStandardPlayerCollisionConditions(Collider other, bool inKillAnimation = false, bool overrideIsInsideFactoryCheck = false)
	{
		if (isEnemyDead)
		{
			return null;
		}
		if (!ventAnimationFinished)
		{
			return null;
		}
		if (inKillAnimation)
		{
			return null;
		}
		if (stunNormalizedTimer >= 0f)
		{
			return null;
		}
		PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
		if (component == null || component != GameNetworkManager.Instance.localPlayerController)
		{
			return null;
		}
		if (!PlayerIsTargetable(component, cannotBeInShip: false, overrideIsInsideFactoryCheck))
		{
			Debug.Log("Player is not targetable");
			return null;
		}
		if (IsSeparatedByMineshaftElevator(component.transform.position))
		{
			return null;
		}
		return component;
	}

	public virtual void OnCollideWithPlayer(Collider other)
	{
		if (debugEnemyAI)
		{
			Debug.Log(base.gameObject.name + ": Collided with player!");
		}
	}

	public virtual void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
	{
		if (base.IsServer && debugEnemyAI)
		{
			Debug.Log(base.gameObject.name + " collided with enemy!: " + other.gameObject.name);
		}
	}

	public void SwitchToBehaviourState(int stateIndex)
	{
		SwitchToBehaviourStateOnLocalClient(stateIndex);
		SwitchToBehaviourServerRpc(stateIndex);
	}

		[ServerRpc(RequireOwnership = false)]
		public void SwitchToBehaviourServerRpc(int stateIndex)
		{
			if (base.NetworkObject.IsSpawned)
			{
				SwitchToBehaviourClientRpc(stateIndex);
			}
		}

		[ClientRpc]
		public void SwitchToBehaviourClientRpc(int stateIndex)
		{
			if (stateIndex != currentBehaviourStateIndex)
			{
				SwitchToBehaviourStateOnLocalClient(stateIndex);
			}
		}

	public void SwitchToBehaviourStateOnLocalClient(int stateIndex)
	{
		if (currentBehaviourStateIndex != stateIndex)
		{
			previousBehaviourStateIndex = currentBehaviourStateIndex;
			currentBehaviourStateIndex = stateIndex;
			currentBehaviourState = enemyBehaviourStates[stateIndex];
			PlayAudioOfCurrentState();
			PlayAnimationOfCurrentState();
		}
	}

	public void PlayAnimationOfCurrentState()
	{
		if (!(creatureAnimator == null))
		{
			if (currentBehaviourState.IsAnimTrigger)
			{
				creatureAnimator.SetTrigger(currentBehaviourState.parameterString);
			}
			else
			{
				creatureAnimator.SetBool(currentBehaviourState.parameterString, currentBehaviourState.boolValue);
			}
		}
	}

	public void PlayAudioOfCurrentState()
	{
		if ((bool)creatureVoice)
		{
			if (currentBehaviourState.playOneShotVoice)
			{
				creatureVoice.PlayOneShot(currentBehaviourState.VoiceClip);
				WalkieTalkie.TransmitOneShotAudio(creatureVoice, currentBehaviourState.VoiceClip, creatureVoice.volume);
			}
			else if (currentBehaviourState.VoiceClip != null)
			{
				creatureVoice.clip = currentBehaviourState.VoiceClip;
				creatureVoice.Play();
			}
		}
		if ((bool)creatureSFX)
		{
			if (currentBehaviourState.playOneShotSFX)
			{
				creatureSFX.PlayOneShot(currentBehaviourState.SFXClip);
				WalkieTalkie.TransmitOneShotAudio(creatureSFX, currentBehaviourState.SFXClip, creatureSFX.volume);
			}
			else if (currentBehaviourState.SFXClip != null)
			{
				creatureSFX.clip = currentBehaviourState.SFXClip;
				creatureSFX.Play();
			}
		}
	}

	public void SetMovingTowardsTargetPlayer(PlayerControllerB playerScript)
	{
		movingTowardsTargetPlayer = true;
		if (targetPlayer != playerScript)
		{
			targetPlayerPositionHorizontallyOffset = false;
		}
		targetPlayer = playerScript;
	}

	public bool SetDestinationToPosition(Vector3 position, bool checkForPath = false)
	{
		if (checkForPath)
		{
			position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f, agentMask);
			if (path1 == null)
			{
				path1 = new NavMeshPath();
			}
			if (!agent.CalculatePath(position, path1))
			{
				return false;
			}
			int cornersNonAlloc = path1.GetCornersNonAlloc(RoundManager.Instance.storedPathCorners);
			if (Vector3.Distance(RoundManager.Instance.storedPathCorners[cornersNonAlloc - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f, agentMask)) > 1.55f)
			{
				return false;
			}
		}
		moveTowardsDestination = true;
		movingTowardsTargetPlayer = false;
		destination = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, -1f);
		return true;
	}

	public virtual void DoAIInterval()
	{
		if (inSpecialAnimation)
		{
			return;
		}
		if (moveTowardsDestination && agent.enabled)
		{
			if (agent.isOnNavMesh)
			{
				if (destination != prevDestination && agent.SetDestination(destination))
				{
					prevDestination = destination;
				}
			}
			else
			{
				Debug.LogError($"Agent '{enemyType.enemyName}' #{thisEnemyIndex}: Agent not on nav mesh when trying to set destination", base.gameObject);
			}
		}
		SyncPositionToClients();
	}

	private void DisableAnimatorWhenNotVisible()
	{
		if (enemyType.disableAnimatorWhenFar)
		{
			PlayerControllerB playerControllerB = GameNetworkManager.Instance.localPlayerController;
			if (playerControllerB.isPlayerDead && playerControllerB.spectatedPlayerScript != null)
			{
				playerControllerB = playerControllerB.spectatedPlayerScript;
			}
			bool flag = false;
			if (playerControllerB.isInHangarShipRoom && StartOfRound.Instance.mapScreen.headMountedCam.enabled && StartOfRound.Instance.mapScreen.targetedPlayer != null && !StartOfRound.Instance.mapScreen.targetedPlayer.isInsideFactory)
			{
				flag = true;
			}
			creatureAnimator.keepAnimatorStateOnDisable = true;
			creatureAnimator.enabled = EnemyMeetsConditionsToBeRendered(StartOfRound.Instance.activeCamera.transform.position) || (flag && EnemyMeetsConditionsToBeRendered(StartOfRound.Instance.mapScreen.targetedPlayer.gameplayCamera.transform.position));
		}
	}

	private bool EnemyMeetsConditionsToBeRendered(Vector3 cameraPosition)
	{
		float num = Vector3.Distance(StartOfRound.Instance.activeCamera.transform.position, base.transform.position);
		if (creatureAnimator.enabled)
		{
			num -= 8f;
		}
		if (num > 80f || (num > 35f && Physics.Linecast(StartOfRound.Instance.activeCamera.transform.position, base.transform.position + Vector3.up * 3.8f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
		{
			return false;
		}
		return true;
	}

	public void SyncPositionToClients()
	{
		if (Vector3.Distance(serverPosition, base.transform.position) > updatePositionThreshold)
		{
			if (!swimming)
			{
				enemySwimTimer = 0f;
			}
			serverPosition = base.transform.position;
			UpdateEnemyPositionRpc(serverPosition);
		}
		else if (enemyType.isOutsideEnemy && !swimming && enemyType.WaterType == EnemyWaterType.LandOnly)
		{
			enemySwimTimer += AIIntervalTime;
			if (enemySwimTimer > 15f)
			{
				enemySwimTimer = 0f;
				swimming = true;
				int areaFromName = NavMesh.GetAreaFromName("Water");
				agent.areaMask |= 1 << areaFromName;
				SyncAgentMaskSwimmingRpc(swimming);
			}
		}
	}

	public static void SetItemInElevatorNonPlayer(bool droppedInShipRoom, bool droppedInElevator, GrabbableObject gObject)
	{
		gObject.isInElevator = droppedInElevator;
		if (gObject.isInShipRoom == droppedInShipRoom)
		{
			return;
		}
		gObject.isInShipRoom = droppedInShipRoom;
		if (!gObject.scrapPersistedThroughRounds)
		{
			if (droppedInShipRoom)
			{
				RoundManager.Instance.scrapCollectedInLevel += gObject.scrapValue;
				RoundManager.Instance.CollectNewScrapForThisRound(gObject);
				gObject.OnBroughtToShip();
				if (gObject.itemProperties.isScrap && Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, gObject.transform.position) < 12f)
				{
					HUDManager.Instance.DisplayTip("Got scrap!", "To sell, use the terminal to route the ship to the company building.", isWarning: false, useSave: true, "LCTip_SellScrap");
				}
			}
			else
			{
				if (gObject.scrapPersistedThroughRounds)
				{
					return;
				}
				RoundManager.Instance.scrapCollectedInLevel -= gObject.scrapValue;
			}
			HUDManager.Instance.SetQuota(RoundManager.Instance.scrapCollectedInLevel);
		}
		if (droppedInShipRoom)
		{
			StartOfRound.Instance.currentShipItemCount++;
		}
		else
		{
			StartOfRound.Instance.currentShipItemCount--;
		}
	}

	public PlayerControllerB CheckLineOfSightForPlayer(float width = 45f, int range = 60, int proximityAwareness = -1)
	{
		if (isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
		{
			range = Mathf.Clamp(range, 0, 30);
		}
		float num = 1000f;
		for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
		{
			Vector3 position = StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position;
			num = Vector3.Distance(position, eye.position);
			Vector3 to = position - eye.position;
			if (num < (float)range && (Vector3.Angle(eye.forward, to) < width || (proximityAwareness != -1 && num < (float)proximityAwareness)) && !Physics.Linecast(eye.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) && !Physics.Linecast(position, eye.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				return StartOfRound.Instance.allPlayerScripts[i];
			}
		}
		return null;
	}

	public PlayerControllerB CheckLineOfSightForClosestPlayer(float width = 45f, int range = 60, int proximityAwareness = -1, float bufferDistance = 0f)
	{
		if (isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
		{
			range = Mathf.Clamp(range, 0, 30);
		}
		float num = 1000f;
		float num2 = 1000f;
		int num3 = -1;
		for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
		{
			num = 1000f;
			Vector3 position = StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position;
			if (DebugEnemy)
			{
				Debug.DrawLine(eye.position, position, Color.green, AIIntervalTime);
			}
			Vector3 to = position - eye.position;
			bool flag = false;
			if (Vector3.Angle(eye.forward, to) < width)
			{
				flag = true;
			}
			else
			{
				num = Vector3.Distance(eye.position, position);
				if (proximityAwareness != -1 && num < (float)proximityAwareness)
				{
					flag = true;
				}
				else if (DebugEnemy)
				{
					Debug.Log($"{enemyType.enemyName} #{thisEnemyIndex}: LOS angle not in range. LOS: {Vector3.Angle(eye.forward, to)} ; dist: {num}");
				}
			}
			if (!flag)
			{
				continue;
			}
			if (!Physics.Linecast(eye.position, position, out raycastHit, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) && !Physics.Linecast(position, eye.position, out raycastHit, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				if (num == 1000f)
				{
					num = Vector3.Distance(eye.position, position);
				}
				if (num < num2)
				{
					num2 = num;
					num3 = i;
				}
			}
			else if (debugEnemyAI)
			{
				Debug.Log($"{enemyType.enemyName} #{thisEnemyIndex}: LOS check for player #{i} hit an object: {raycastHit.point}, {raycastHit.collider.gameObject.name}, {raycastHit.collider.transform.gameObject.name}; {raycastHit.collider.name}", raycastHit.collider.gameObject);
			}
		}
		if (targetPlayer != null && num3 != -1 && targetPlayer != StartOfRound.Instance.allPlayerScripts[num3] && bufferDistance > 0f && Mathf.Abs(num2 - Vector3.Distance(base.transform.position, targetPlayer.transform.position)) < bufferDistance)
		{
			return null;
		}
		if (num3 < 0)
		{
			return null;
		}
		mostOptimalDistance = num2;
		return StartOfRound.Instance.allPlayerScripts[num3];
	}

	public int GetAllPlayersInLineOfSightNonAlloc(PlayerControllerB[] playersArray, float width = 45f, int range = 60, Transform eyeObject = null, float proximityCheck = -1f, int layerMask = -1)
	{
		if (layerMask == -1)
		{
			layerMask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
		}
		if (eyeObject == null)
		{
			eyeObject = eye;
		}
		if (isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
		{
			range = Mathf.Clamp(range, 0, 30);
		}
		for (int i = 0; i < playersArray.Length; i++)
		{
			playersArray[i] = null;
		}
		int num = 0;
		for (int j = 0; j < StartOfRound.Instance.allPlayerScripts.Length; j++)
		{
			if (!PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[j]))
			{
				continue;
			}
			Vector3 position = StartOfRound.Instance.allPlayerScripts[j].gameplayCamera.transform.position;
			if (Vector3.Distance(eye.position, position) < (float)range)
			{
				Vector3 to = position - eyeObject.position;
				if ((Vector3.Angle(eyeObject.forward, to) < width || (proximityCheck != -1f && Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[j].transform.position) < proximityCheck)) && !Physics.Linecast(eyeObject.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) && !Physics.Linecast(position, eyeObject.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					playersArray[num] = StartOfRound.Instance.allPlayerScripts[j];
					num++;
				}
			}
		}
		return num;
	}

	public int GetAllPlayersInLineOfSightNonAlloc(float width = 45f, int range = 60, Transform eyeObject = null, float proximityCheck = -1f, int layerMask = -1)
	{
		if (layerMask == -1)
		{
			layerMask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
		}
		if (eyeObject == null)
		{
			eyeObject = eye;
		}
		if (isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
		{
			range = Mathf.Clamp(range, 0, 30);
		}
		for (int i = 0; i < RoundManager.Instance.tempPlayersArray.Length; i++)
		{
			RoundManager.Instance.tempPlayersArray[i] = null;
		}
		int num = 0;
		for (int j = 0; j < StartOfRound.Instance.allPlayerScripts.Length; j++)
		{
			if (!PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[j]))
			{
				continue;
			}
			Vector3 position = StartOfRound.Instance.allPlayerScripts[j].gameplayCamera.transform.position;
			if (Vector3.Distance(eye.position, position) < (float)range)
			{
				Vector3 to = position - eyeObject.position;
				if ((Vector3.Angle(eyeObject.forward, to) < width || (proximityCheck != -1f && Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[j].transform.position) < proximityCheck)) && !Physics.Linecast(eyeObject.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) && !Physics.Linecast(position, eyeObject.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					RoundManager.Instance.tempPlayersArray[num] = StartOfRound.Instance.allPlayerScripts[j];
					num++;
				}
			}
		}
		return num;
	}

	public PlayerControllerB[] GetAllPlayersInLineOfSight(float width = 45f, int range = 60, Transform eyeObject = null, float proximityCheck = -1f, int layerMask = -1)
	{
		if (layerMask == -1)
		{
			layerMask = StartOfRound.Instance.collidersAndRoomMaskAndDefault;
		}
		if (eyeObject == null)
		{
			eyeObject = eye;
		}
		if (isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
		{
			range = Mathf.Clamp(range, 0, 30);
		}
		List<PlayerControllerB> list = new List<PlayerControllerB>(4);
		for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
		{
			if (!PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]))
			{
				continue;
			}
			Vector3 position = StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position;
			if (Vector3.Distance(eye.position, position) < (float)range)
			{
				Vector3 to = position - eyeObject.position;
				if ((Vector3.Angle(eyeObject.forward, to) < width || (proximityCheck != -1f && Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position) < proximityCheck)) && !Physics.Linecast(eyeObject.position, position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) && !Physics.Linecast(position, eyeObject.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					list.Add(StartOfRound.Instance.allPlayerScripts[i]);
				}
			}
		}
		if (list.Count == 4)
		{
			return StartOfRound.Instance.allPlayerScripts;
		}
		if (list.Count > 0)
		{
			return list.ToArray();
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
		if (Vector3.Distance(transform.position, objectPosition) < (float)range)
		{
			Vector3 to = objectPosition - transform.position;
			if ((Vector3.Angle(transform.forward, to) < width || (proximityAwareness != -1f && Vector3.Distance(base.transform.position, objectPosition) < proximityAwareness)) && !Physics.Linecast(transform.position, objectPosition, out var hitInfo, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) && !Physics.Linecast(objectPosition, transform.position, out hitInfo, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				if (debugEnemyAI)
				{
					Debug.DrawRay(transform.position, objectPosition - transform.position, Color.green, 2f);
				}
				return true;
			}
			if (debugEnemyAI)
			{
				Debug.DrawRay(transform.position, objectPosition - transform.position, Color.red, 2f);
			}
		}
		return false;
	}

	public GameObject CheckLineOfSight(List<GameObject> objectsToLookFor, float width = 45f, int range = 60, float proximityAwareness = -1f, Transform useEye = null, int[] itemIdExceptions = null)
	{
		if (useEye == null)
		{
			useEye = eye;
		}
		GrabbableObject component = base.transform.GetComponent<GrabbableObject>();
		for (int i = 0; i < objectsToLookFor.Count; i++)
		{
			if (objectsToLookFor[i] == null)
			{
				objectsToLookFor.TrimExcess();
				continue;
			}
			Vector3 position = objectsToLookFor[i].transform.position;
			if (!isOutside)
			{
				if (position.y > -80f)
				{
					continue;
				}
			}
			else if (position.y < -100f)
			{
				continue;
			}
			if (!(Vector3.Distance(useEye.position, objectsToLookFor[i].transform.position) < (float)range) || Physics.Linecast(useEye.position, position + Vector3.up * 0.05f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				continue;
			}
			Vector3 to = position - useEye.position;
			if (!(Vector3.Angle(useEye.forward, to) < width) && !(Vector3.Distance(base.transform.position, position) < proximityAwareness))
			{
				continue;
			}
			if (itemIdExceptions != null)
			{
				GrabbableObject component2 = objectsToLookFor[i].GetComponent<GrabbableObject>();
				if (!(component2 != null) || !(component2 != component) || component2.isHeld || component2.deactivated)
				{
					continue;
				}
				for (int j = 0; j < itemIdExceptions.Length; j++)
				{
					_ = component2.itemProperties.itemId;
					_ = itemIdExceptions[j];
				}
			}
			return objectsToLookFor[i];
		}
		return null;
	}

	public void StartSearch(Vector3 startOfSearch, AISearchRoutine newSearch = null)
	{
		if (DebugEnemy)
		{
			Debug.Log($"{base.gameObject.name} #{thisEnemyIndex} Starting search", base.gameObject);
		}
		StopSearch(currentSearch, clear: false);
		movingTowardsTargetPlayer = false;
		if (newSearch == null)
		{
			currentSearch = new AISearchRoutine();
			newSearch = currentSearch;
		}
		else
		{
			currentSearch = newSearch;
		}
		currentSearch.currentSearchStartPosition = startOfSearch;
		currentSearch.startedSearchAtSelf = Vector3.Distance(startOfSearch, base.transform.position) < 2f;
		searchRoutineRandom = new System.Random(RoundUpToNearestFive(startOfSearch.x) + RoundUpToNearestFive(startOfSearch.z));
		searchCoroutine = StartCoroutine(CurrentSearchCoroutine());
		currentSearch.inProgress = true;
	}

	private int RoundUpToNearestFive(float x)
	{
		return (int)(x / 5f) * 5;
	}

	public void StopSearch(AISearchRoutine search, bool clear = true)
	{
		if (DebugEnemy)
		{
			Debug.Log($"{base.gameObject.name} #{thisEnemyIndex} Stop search called; search == null: {search == null} ", base.gameObject);
		}
		if (search != null)
		{
			if (searchCoroutine != null)
			{
				StopCoroutine(searchCoroutine);
				searchCoroutine = null;
			}
			if (chooseTargetNodeCoroutine != null)
			{
				StopCoroutine(chooseTargetNodeCoroutine);
				chooseTargetNodeCoroutine = null;
			}
			search.calculatingNodeInSearch = false;
			search.waitingForTargetNode = false;
			search.inProgress = false;
			if (clear)
			{
				RoundManager.Instance.GetOutsideAINodes();
				search.unsearchedNodes = allAINodes.ToList();
				search.timesFinishingSearch = 0;
				search.nodesEliminatedInCurrentSearch = 0;
				search.currentTargetNode = null;
				search.currentSearchStartPosition = Vector3.zero;
				search.nextTargetNode = null;
				search.choseTargetNode = false;
				search.startedSearchAtSelf = false;
			}
		}
	}

	private IEnumerator CurrentSearchCoroutine()
	{
		yield return null;
		if (debugEnemyAI)
		{
			Debug.Log("Start coroutine A!!!");
		}
		WaitUntil waitUntilChoseTargetNode = new WaitUntil(() => currentSearch.choseTargetNode);
		while (searchCoroutine != null && base.IsOwner)
		{
			yield return null;
			if (currentSearch.unsearchedNodes.Count <= 0)
			{
				if (currentSearch.timesFinishingSearch > 0)
				{
					FinishedCurrentSearchRoutine();
				}
				if (!currentSearch.loopSearch)
				{
					Debug.Log("Start coroutine B!!!");
					currentSearch.inProgress = false;
					searchCoroutine = null;
					yield break;
				}
				currentSearch.unsearchedNodes = allAINodes.ToList();
				currentSearch.timesFinishingSearch++;
				currentSearch.nodesEliminatedInCurrentSearch = 0;
				if (debugEnemyAI)
				{
					Debug.Log("Start coroutine C!!!");
				}
				float timeAtStart = Time.realtimeSinceStartup;
				if (DebugEnemy)
				{
					Debug.Log($"{enemyType.enemyName} #{thisEnemyIndex}: Eliminating nodes from search which are out of search width!");
				}
				for (int i = currentSearch.unsearchedNodes.Count - 1; i >= 0; i--)
				{
					if (i % 30 == 0)
					{
						yield return null;
					}
					if (Vector3.Distance(currentSearch.unsearchedNodes[i].transform.position, currentSearch.currentSearchStartPosition) > currentSearch.searchWidth)
					{
						EliminateNodeFromSearch(i);
					}
				}
				if (DebugEnemy)
				{
					Debug.Log($"{enemyType.enemyName} #{thisEnemyIndex}: Finished eliminating nodes out of search width. Time taken: {Time.realtimeSinceStartup - timeAtStart}");
				}
				if (currentSearch.unsearchedNodes.Count == 0)
				{
					Debug.LogError($"Error! '{enemyType.enemyName}' #{thisEnemyIndex} eliminated all possible nodes, so it cannot search.", base.gameObject);
				}
				yield return null;
				yield return new WaitForSeconds(Mathf.Max(1f - (Time.realtimeSinceStartup - timeAtStart), 0.05f));
			}
			if (currentSearch.choseTargetNode && currentSearch.unsearchedNodes.Contains(currentSearch.nextTargetNode))
			{
				if (debugEnemyAI)
				{
					Debug.Log($"finding next node: {currentSearch.choseTargetNode}; node already found ahead of time");
				}
				currentSearch.currentTargetNode = currentSearch.nextTargetNode;
			}
			else
			{
				if (debugEnemyAI)
				{
					Debug.Log("finding next node; calculation not finished ahead of time");
				}
				currentSearch.waitingForTargetNode = true;
				StartCalculatingNextTargetNode();
				yield return waitUntilChoseTargetNode;
			}
			currentSearch.waitingForTargetNode = false;
			if (currentSearch.unsearchedNodes.Count <= 0 || currentSearch.currentTargetNode == null)
			{
				continue;
			}
			if (debugEnemyAI)
			{
				int num = 0;
				for (int num2 = 0; num2 < currentSearch.unsearchedNodes.Count; num2++)
				{
					if (currentSearch.unsearchedNodes[num2] == currentSearch.currentTargetNode)
					{
						Debug.Log($"Found node {currentSearch.unsearchedNodes[num2]} within list of unsearched nodes at index {num2}");
						num++;
					}
				}
				Debug.Log($"Copies of the node {currentSearch.currentTargetNode} found in list: {num}");
				Debug.Log($"unsearched nodes contains {currentSearch.currentTargetNode}? : {currentSearch.unsearchedNodes.Contains(currentSearch.currentTargetNode)}");
				Debug.Log($"Removing {currentSearch.currentTargetNode} from unsearched nodes list with Remove()");
			}
			currentSearch.unsearchedNodes.Remove(currentSearch.currentTargetNode);
			SetDestinationToPosition(currentSearch.currentTargetNode.transform.position);
			for (int i = currentSearch.unsearchedNodes.Count - 1; i >= 0; i--)
			{
				if (Vector3.Distance(currentSearch.currentTargetNode.transform.position, currentSearch.unsearchedNodes[i].transform.position) < currentSearch.searchPrecision)
				{
					EliminateNodeFromSearch(i);
				}
				if (i % 10 == 0)
				{
					yield return null;
				}
			}
			if (debugEnemyAI)
			{
				Debug.Log($"Removed. Does list now contain {currentSearch.currentTargetNode}?: {currentSearch.unsearchedNodes.Contains(currentSearch.currentTargetNode)}");
			}
			StartCalculatingNextTargetNode();
			int timeSpent = 0;
			while (searchCoroutine != null)
			{
				if (debugEnemyAI)
				{
					Debug.Log("Current search not null");
				}
				timeSpent++;
				if (timeSpent >= 32 || (currentSearch.onlySearchNodesInLOS && Physics.Linecast(currentSearch.currentTargetNode.transform.position, currentSearch.currentSearchStartPosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)))
				{
					break;
				}
				yield return new WaitForSeconds(0.5f);
				if (Vector3.Distance(base.transform.position, currentSearch.currentTargetNode.transform.position) < currentSearch.searchPrecision)
				{
					if (debugEnemyAI)
					{
						Debug.Log("Enemy: Reached the target " + currentSearch.currentTargetNode.name);
					}
					ReachedNodeInSearch();
					break;
				}
				if (debugEnemyAI)
				{
					Debug.Log($"Enemy: We have not reached the target node {currentSearch.currentTargetNode.transform.name}, distance: {Vector3.Distance(base.transform.position, currentSearch.currentTargetNode.transform.position)} ; {currentSearch.searchPrecision}");
				}
			}
			if (debugEnemyAI)
			{
				Debug.Log("Reached destination node");
			}
		}
		if (!base.IsOwner)
		{
			StopSearch(currentSearch);
		}
	}

	private void StartCalculatingNextTargetNode()
	{
		if (debugEnemyAI)
		{
			Debug.Log("Calculating next target node");
			Debug.Log($"Is calculate node coroutine null? : {chooseTargetNodeCoroutine == null}; choseTargetNode: {currentSearch.choseTargetNode}");
		}
		if (!currentSearch.calculatingNodeInSearch)
		{
			currentSearch.choseTargetNode = false;
			currentSearch.calculatingNodeInSearch = true;
			if (chooseTargetNodeCoroutine == null)
			{
				chooseTargetNodeCoroutine = StartCoroutine(ChooseNextNodeInSearchRoutine());
			}
		}
	}

	private IEnumerator ChooseNextNodeInSearchRoutine()
	{
		while (searchCoroutine != null && base.IsOwner)
		{
			if (!currentSearch.calculatingNodeInSearch)
			{
				yield return null;
				continue;
			}
			float closestDist = 500f;
			bool gotNode = false;
			GameObject chosenNode = null;
			for (int i = currentSearch.unsearchedNodes.Count - 1; i >= 0; i--)
			{
				if (i % 5 == 0)
				{
					yield return null;
				}
				if (!base.IsOwner || searchCoroutine == null)
				{
					currentSearch.calculatingNodeInSearch = false;
					chooseTargetNodeCoroutine = null;
					yield break;
				}
				if (PathIsIntersectedByLineOfSight(currentSearch.unsearchedNodes[i].transform.position, currentSearch.startedSearchAtSelf, avoidLineOfSight: false))
				{
					EliminateNodeFromSearch(i);
				}
				else if (currentSearch.onlySearchNodesInLOS && Physics.Linecast(currentSearch.unsearchedNodes[i].transform.position, currentSearch.currentSearchStartPosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					EliminateNodeFromSearch(i);
				}
				else
				{
					if (!currentSearch.startedSearchAtSelf)
					{
						GetPathDistance(currentSearch.unsearchedNodes[i].transform.position, currentSearch.currentSearchStartPosition);
					}
					if (pathDistance < closestDist && (!currentSearch.randomized || !gotNode || searchRoutineRandom.Next(0, 100) < 65))
					{
						closestDist = pathDistance;
						chosenNode = currentSearch.unsearchedNodes[i];
						gotNode = true;
						if (closestDist <= 0f && !currentSearch.randomized)
						{
							break;
						}
					}
				}
			}
			if (debugEnemyAI)
			{
				Debug.Log($"NODE C; chosen node: {chosenNode}");
			}
			if (currentSearch.waitingForTargetNode)
			{
				currentSearch.currentTargetNode = chosenNode;
				if (debugEnemyAI)
				{
					Debug.Log("NODE C1");
				}
			}
			else
			{
				currentSearch.nextTargetNode = chosenNode;
				if (debugEnemyAI)
				{
					Debug.Log("NODE C2");
				}
			}
			currentSearch.calculatingNodeInSearch = false;
			currentSearch.choseTargetNode = true;
			if (debugEnemyAI)
			{
				Debug.Log($"Chose target node?: {currentSearch.choseTargetNode} ");
			}
		}
		chooseTargetNodeCoroutine = null;
	}

	public virtual void ReachedNodeInSearch()
	{
	}

	private void EliminateNodeFromSearch(GameObject node)
	{
		currentSearch.unsearchedNodes.Remove(node);
		currentSearch.nodesEliminatedInCurrentSearch++;
	}

	private void EliminateNodeFromSearch(int index)
	{
		currentSearch.unsearchedNodes.RemoveAt(index);
		currentSearch.nodesEliminatedInCurrentSearch++;
	}

	public virtual void FinishedCurrentSearchRoutine()
	{
	}

	public bool TargetClosestPlayer(float bufferDistance = 1.5f, bool requireLineOfSight = false, float viewWidth = 70f, bool doGroundCast = false, bool requirePath = false, bool checkForMineshaftStartTile = true)
	{
		mostOptimalDistance = 2000f;
		PlayerControllerB playerControllerB = targetPlayer;
		targetPlayer = null;
		for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
		{
			if (!PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i], cannotBeInShip: false, overrideInsideFactoryCheck: false, checkForMineshaftStartTile))
			{
				continue;
			}
			if (doGroundCast)
			{
				if (!Physics.Raycast(StartOfRound.Instance.allPlayerScripts[i].transform.position, Vector3.down, out raycastHit, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) || (requirePath && PathIsIntersectedByLineOfSight(raycastHit.point, calculatePathDistance: false, avoidLineOfSight: false)))
				{
					continue;
				}
			}
			else if (requirePath && PathIsIntersectedByLineOfSight(StartOfRound.Instance.allPlayerScripts[i].transform.position, calculatePathDistance: false, avoidLineOfSight: false))
			{
				continue;
			}
			if (!requireLineOfSight || CheckLineOfSightForPosition(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, viewWidth, 40))
			{
				tempDist = Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
				if (tempDist < mostOptimalDistance)
				{
					mostOptimalDistance = tempDist;
					targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
				}
			}
		}
		if (targetPlayer != null && bufferDistance > 0f && playerControllerB != null && Mathf.Abs(mostOptimalDistance - Vector3.Distance(base.transform.position, playerControllerB.transform.position)) < bufferDistance)
		{
			targetPlayer = playerControllerB;
		}
		return targetPlayer != null;
	}

	public PlayerControllerB GetClosestPlayer(bool requireLineOfSight = false, bool cannotBeInShip = false, bool cannotBeNearShip = false)
	{
		PlayerControllerB result = null;
		mostOptimalDistance = 2000f;
		for (int i = 0; i < 4; i++)
		{
			if (!PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i], cannotBeInShip))
			{
				continue;
			}
			if (cannotBeNearShip)
			{
				if (StartOfRound.Instance.allPlayerScripts[i].isInElevator)
				{
					continue;
				}
				bool flag = false;
				for (int j = 0; j < RoundManager.Instance.spawnDenialPoints.Length; j++)
				{
					if (Vector3.Distance(RoundManager.Instance.spawnDenialPoints[j].transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position) < 10f)
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					continue;
				}
			}
			if (!requireLineOfSight || !Physics.Linecast(base.transform.position + Vector3.up * 0.25f, StartOfRound.Instance.allPlayerScripts[i].transform.position, 256))
			{
				tempDist = Vector3.Distance(base.transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
				if (tempDist < mostOptimalDistance)
				{
					mostOptimalDistance = tempDist;
					result = StartOfRound.Instance.allPlayerScripts[i];
				}
			}
		}
		return result;
	}

	private bool IsInMineshaftStartRoomWithPlayer(PlayerControllerB player)
	{
		if (RoundManager.Instance.currentDungeonType != 4)
		{
			return false;
		}
		if (RoundManager.Instance.currentMineshaftElevator == null)
		{
			RoundManager.Instance.currentMineshaftElevator = UnityEngine.Object.FindObjectOfType<MineshaftElevatorController>();
		}
		if (RoundManager.Instance.currentMineshaftElevator == null)
		{
			return false;
		}
		float num = RoundManager.Instance.currentMineshaftElevator.elevatorTopPoint.position.y - 4f;
		if (base.transform.position.y > num != player.transform.position.y > num)
		{
			return false;
		}
		return true;
	}

	public virtual bool PlayerIsTargetable(PlayerControllerB playerScript, bool cannotBeInShip = false, bool overrideInsideFactoryCheck = false, bool checkForMineshaftStartTile = true)
	{
		if (cannotBeInShip && playerScript.isInHangarShipRoom)
		{
			Debug.Log("Targetable A");
			return false;
		}
		if (!isOutside && checkForMineshaftStartTile && RoundManager.Instance.currentDungeonType == 4 && !IsInMineshaftStartRoomWithPlayer(playerScript))
		{
			return false;
		}
		if (playerScript.isPlayerControlled && !playerScript.isPlayerDead && playerScript.inAnimationWithEnemy == null && (overrideInsideFactoryCheck || playerScript.isInsideFactory != isOutside) && playerScript.sinkingValue < 0.73f)
		{
			if (isOutside && StartOfRound.Instance.hangarDoorsClosed)
			{
				return playerScript.isInHangarShipRoom == isInsidePlayerShip;
			}
			return true;
		}
		return false;
	}

	public Transform ChooseFarthestNodeFromPosition(Vector3 pos, bool avoidLineOfSight = false, int offset = 0, bool doAsync = false, int maxAsyncIterations = 50, int capDistance = -1)
	{
		if (!doAsync)
		{
			Array.Sort(allAINodes, (GameObject a, GameObject b) => (pos - b.transform.position).sqrMagnitude.CompareTo((pos - a.transform.position).sqrMagnitude));
		}
		else if (gotFarthestNodeAsync || getFarthestNodeAsyncBookmark <= 0)
		{
			if (nodesTempArray == null || nodesTempArray.Length == 0)
			{
				nodesTempArray = allAINodes.ToArray();
			}
			Array.Sort(nodesTempArray, (GameObject a, GameObject b) => (pos - b.transform.position).sqrMagnitude.CompareTo((pos - a.transform.position).sqrMagnitude));
		}
		if (!doAsync)
		{
			nodesTempArray = allAINodes;
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
			if (DebugEnemy)
			{
				Debug.Log("Set got farthest node async to false");
			}
		}
		for (int num2 = num; num2 < nodesTempArray.Length; num2++)
		{
			if (doAsync && num2 - getFarthestNodeAsyncBookmark > maxAsyncIterations)
			{
				if (DebugEnemy)
				{
					Debug.Log(enemyType.enemyName + ": Set bookmark");
				}
				gotFarthestNodeAsync = false;
				getFarthestNodeAsyncBookmark = num2;
				return null;
			}
			if ((capDistance == -1 || (!(Vector3.Distance(base.transform.position, nodesTempArray[num2].transform.position) > 40f) && (RoundManager.Instance.currentDungeonType < 0 || !(Mathf.Abs(nodesTempArray[num2].transform.position.y - base.transform.position.y) > 12.5f * RoundManager.Instance.dungeonFlowTypes[RoundManager.Instance.currentDungeonType].MapTileSize)))) && !PathIsIntersectedByLineOfSight(nodesTempArray[num2].transform.position, calculatePathDistance: false, avoidLineOfSight))
			{
				mostOptimalDistance = Vector3.Distance(pos, nodesTempArray[num2].transform.position);
				result = nodesTempArray[num2].transform;
				if (offset == 0 || num2 >= nodesTempArray.Length - 1)
				{
					break;
				}
				offset--;
			}
		}
		getFarthestNodeAsyncBookmark = 0;
		gotFarthestNodeAsync = true;
		if (DebugEnemy)
		{
			Debug.Log(enemyType.enemyName + " Set got farthest node");
		}
		return result;
	}

	public Transform ChooseClosestNodeToPosition(Vector3 pos, bool avoidLineOfSight = false, int offset = 0)
	{
		Array.Sort(allAINodes, (GameObject a, GameObject b) => (pos - a.transform.position).sqrMagnitude.CompareTo((pos - b.transform.position).sqrMagnitude));
		Transform result = allAINodes[0].transform;
		for (int num = 0; num < allAINodes.Length; num++)
		{
			if (!PathIsIntersectedByLineOfSight(allAINodes[num].transform.position, calculatePathDistance: false, avoidLineOfSight))
			{
				mostOptimalDistance = Vector3.Distance(pos, allAINodes[num].transform.position);
				result = allAINodes[num].transform;
				if (offset == 0 || num >= allAINodes.Length - 1)
				{
					break;
				}
				offset--;
			}
		}
		return result;
	}

	public bool PathIsIntersectedByLineOfSight(Vector3 targetPos, bool calculatePathDistance = false, bool avoidLineOfSight = true, bool checkLOSToTargetPlayer = false)
	{
		pathDistance = 0f;
		if (agent.isOnNavMesh && !agent.CalculatePath(targetPos, path1))
		{
			if (DebugEnemy)
			{
				Debug.Log($"Path could not be calculated: {targetPos}");
				Debug.DrawLine(base.transform.position, targetPos, Color.yellow, 5f);
				Debug.Break();
			}
			return true;
		}
		if (DebugEnemy)
		{
			for (int i = 1; i < path1.corners.Length; i++)
			{
				Debug.DrawLine(path1.corners[i - 1], path1.corners[i], Color.red);
			}
		}
		int cornersNonAlloc = path1.GetCornersNonAlloc(RoundManager.Instance.storedPathCorners);
		Vector3[] storedPathCorners = RoundManager.Instance.storedPathCorners;
		if (path1 == null || cornersNonAlloc == 0)
		{
			return true;
		}
		bool flag = false;
		Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(targetPos, RoundManager.Instance.navHit, 0.75f, agentMask);
		if (!RoundManager.Instance.GotNavMeshPositionResult)
		{
			navMeshPosition = RoundManager.Instance.GetNavMeshPosition(targetPos, RoundManager.Instance.navHit, 2.7f, agentMask);
			flag = true;
		}
		if (Vector3.Distance(navMeshPosition, storedPathCorners[cornersNonAlloc - 1]) > 1.55f || !RoundManager.Instance.GotNavMeshPositionResult)
		{
			if (DebugEnemy)
			{
				Debug.Log($"Path is not complete; final waypoint of path was too far from target position: {targetPos}; gotnavmeshpos: {RoundManager.Instance.GotNavMeshPositionResult}; {flag}");
				Debug.DrawRay(storedPathCorners[cornersNonAlloc - 1], Vector3.up * 0.75f, Color.cyan, 4f);
				Debug.DrawRay(navMeshPosition, Vector3.up * 0.5f, Color.red, 4f);
			}
			return true;
		}
		Vector3 a = storedPathCorners[0];
		if (calculatePathDistance)
		{
			for (int j = 1; j < cornersNonAlloc; j++)
			{
				if (j >= cornersNonAlloc)
				{
					return false;
				}
				pathDistance += Vector3.Distance(storedPathCorners[j - 1], storedPathCorners[j]);
				if ((!avoidLineOfSight && !checkLOSToTargetPlayer) || j > 15)
				{
					continue;
				}
				if (j > 5 && Vector3.Distance(a, storedPathCorners[j]) < 1.7f)
				{
					if (DebugEnemy)
					{
						Debug.Log($"Distance between corners {j} and {j - 1} under 3 meters; skipping LOS check");
						Debug.DrawRay(storedPathCorners[j - 1] + Vector3.up * 0.2f, storedPathCorners[j] + Vector3.up * 0.2f, Color.magenta, 0.2f);
					}
					continue;
				}
				a = storedPathCorners[j];
				if (targetPlayer != null && checkLOSToTargetPlayer && !Physics.Linecast(storedPathCorners[j - 1], targetPlayer.transform.position + Vector3.up * 0.3f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					return true;
				}
				if (avoidLineOfSight && Physics.Linecast(storedPathCorners[j - 1], storedPathCorners[j], 262144))
				{
					if (DebugEnemy)
					{
						Debug.Log($"{enemyType.enemyName}: The path is blocked by line of sight at corner {j}");
					}
					return true;
				}
			}
		}
		else if (avoidLineOfSight)
		{
			for (int k = 1; k < cornersNonAlloc; k++)
			{
				if (k >= cornersNonAlloc)
				{
					return false;
				}
				if (DebugEnemy)
				{
					Debug.DrawLine(storedPathCorners[k - 1], storedPathCorners[k], Color.green);
				}
				if (k > 5 && Vector3.Distance(a, storedPathCorners[k]) < 1.7f)
				{
					if (DebugEnemy)
					{
						Debug.Log($"Distance between corners {k} and {k - 1} under 3 meters; skipping LOS check");
						Debug.DrawRay(storedPathCorners[k - 1] + Vector3.up * 0.2f, storedPathCorners[k] + Vector3.up * 0.2f, Color.magenta, 0.2f);
					}
					continue;
				}
				a = storedPathCorners[k];
				if (targetPlayer != null && checkLOSToTargetPlayer && !Physics.Linecast(Vector3.Lerp(storedPathCorners[k - 1], storedPathCorners[k], 0.5f) + Vector3.up * 0.25f, targetPlayer.transform.position + Vector3.up * 0.25f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					return true;
				}
				if (Physics.Linecast(storedPathCorners[k - 1], storedPathCorners[k], 262144))
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
	}

	public bool GetPathDistance(Vector3 targetPos, Vector3 sourcePos)
	{
		pathDistance = 0f;
		if (!NavMesh.CalculatePath(sourcePos, targetPos, agent.areaMask, path1))
		{
			if (DebugEnemy)
			{
				Debug.Log("GetPathDistance: Path could not be calculated");
			}
			return false;
		}
		int cornersNonAlloc = path1.GetCornersNonAlloc(RoundManager.Instance.storedPathCorners);
		Vector3[] storedPathCorners = RoundManager.Instance.storedPathCorners;
		if (path1 == null || cornersNonAlloc == 0)
		{
			return false;
		}
		if (DebugEnemy)
		{
			for (int i = 1; i < cornersNonAlloc; i++)
			{
				Debug.DrawLine(storedPathCorners[i - 1], storedPathCorners[i], Color.red);
			}
			Debug.DrawRay(storedPathCorners[cornersNonAlloc - 1], Vector3.up * 0.5f, Color.magenta, 1f);
		}
		Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(targetPos, RoundManager.Instance.navHit, 0.75f, agentMask);
		if (!RoundManager.Instance.GotNavMeshPositionResult)
		{
			navMeshPosition = RoundManager.Instance.GetNavMeshPosition(targetPos, RoundManager.Instance.navHit, 2.7f, agentMask);
		}
		if (Vector3.Distance(navMeshPosition, storedPathCorners[cornersNonAlloc - 1]) > 1.55f || !RoundManager.Instance.GotNavMeshPositionResult)
		{
			Debug.Log($"{storedPathCorners[cornersNonAlloc - 1]} ; {navMeshPosition}; dist: {Vector3.Distance(storedPathCorners[cornersNonAlloc - 1], navMeshPosition)}");
			Debug.DrawRay(storedPathCorners[cornersNonAlloc - 1], Vector3.up * 0.6f, Color.magenta, 1f);
			Debug.DrawRay(navMeshPosition, Vector3.up * 0.45f, Color.red, 1f);
			if (DebugEnemy)
			{
				Debug.Log("GetPathDistance: Path is not complete; final waypoint of path was too far from target position");
			}
			return false;
		}
		for (int j = 1; j < cornersNonAlloc; j++)
		{
			pathDistance += Vector3.Distance(storedPathCorners[j - 1], storedPathCorners[j]);
		}
		return true;
	}

	public virtual void Update()
	{
		if (!base.IsOwner || inSpecialAnimation || !agent.enabled || !agent.isOnNavMesh || Time.realtimeSinceStartup - refreshDestinationInterval > 15f)
		{
			refreshDestinationInterval = Time.realtimeSinceStartup;
			prevDestination = Vector3.zero;
		}
		if (enemyType.isDaytimeEnemy && !daytimeEnemyLeaving)
		{
			CheckTimeOfDayToLeave();
		}
		if (stunnedIndefinitely <= 0)
		{
			if (stunNormalizedTimer >= 0f)
			{
				stunNormalizedTimer -= Time.deltaTime / enemyType.stunTimeMultiplier;
			}
			else
			{
				stunnedByPlayer = null;
				if (postStunInvincibilityTimer >= 0f)
				{
					postStunInvincibilityTimer -= Time.deltaTime * 5f;
				}
			}
		}
		if (!ventAnimationFinished && timeSinceSpawn < exitVentAnimationTime + 0.005f * (float)RoundManager.Instance.numberOfEnemiesInScene)
		{
			timeSinceSpawn += Time.deltaTime;
			if (!base.IsOwner)
			{
				_ = serverPosition;
				if (serverPosition != Vector3.zero)
				{
					base.transform.position = serverPosition;
					base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, targetYRotation, base.transform.eulerAngles.z);
				}
			}
			else if (updateDestinationInterval >= 0f)
			{
				updateDestinationInterval -= Time.deltaTime;
			}
			else
			{
				SyncPositionToClients();
				updateDestinationInterval = 0.1f;
			}
			return;
		}
		if (!inSpecialAnimation && !ventAnimationFinished)
		{
			ventAnimationFinished = true;
			if (creatureAnimator != null)
			{
				creatureAnimator.SetBool("inSpawningAnimation", value: false);
			}
		}
		DisableAnimatorWhenNotVisible();
		if (!base.IsOwner)
		{
			if (currentSearch.inProgress)
			{
				StopSearch(currentSearch);
			}
			SetClientCalculatingAI(enable: false);
			if (!inSpecialAnimation)
			{
				if (IsInsideMineshaftElevator(base.transform.position))
				{
					serverPosition += RoundManager.Instance.currentMineshaftElevator.elevatorInsidePoint.position - RoundManager.Instance.currentMineshaftElevator.previousElevatorPosition;
				}
				base.transform.position = Vector3.SmoothDamp(base.transform.position, serverPosition, ref tempVelocity, syncMovementSpeed);
				base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, targetYRotation, 15f * Time.deltaTime), base.transform.eulerAngles.z);
			}
			timeSinceSpawn += Time.deltaTime;
			return;
		}
		if (isEnemyDead)
		{
			SetClientCalculatingAI(enable: false);
			return;
		}
		if (!inSpecialAnimation)
		{
			SetClientCalculatingAI(enable: true);
		}
		if (movingTowardsTargetPlayer && targetPlayer != null)
		{
			NavigateTowardsTargetPlayer();
		}
		if (inSpecialAnimation)
		{
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
		if (enemyType.isOutsideEnemy && swimming && enemyType.WaterType == EnemyWaterType.LandOnly)
		{
			enemySwimTimer += Time.deltaTime;
			if (enemySwimTimer > 10f)
			{
				enemySwimTimer = 0f;
				swimming = false;
				int areaFromName = NavMesh.GetAreaFromName("Water");
				agent.areaMask &= ~(1 << areaFromName);
				Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(base.transform.position, default(NavMeshHit), 8f, agentMask);
				if (navMeshPosition != Vector3.zero)
				{
					agent.enabled = false;
					base.transform.position = navMeshPosition;
					agent.enabled = true;
				}
				SyncAgentMaskSwimmingRpc(swimming);
			}
		}
		if (Mathf.Abs(previousYRotation - base.transform.eulerAngles.y) > 6f)
		{
			previousYRotation = base.transform.eulerAngles.y;
			targetYRotation = previousYRotation;
			if (base.IsServer)
			{
				UpdateEnemyRotationClientRpc((short)previousYRotation);
			}
			else
			{
				UpdateEnemyRotationServerRpc((short)previousYRotation);
			}
		}
	}

		[Rpc(SendTo.NotMe)]
		public void SyncAgentMaskSwimmingRpc(bool setSwimming)
		{
			swimming = setSwimming;
	int areaFromName = NavMesh.GetAreaFromName("Water");
			if (swimming)
			{
				agent.areaMask |= 1 << areaFromName;
			}
			else
			{
				agent.areaMask &= ~(1 << areaFromName);
			}
		}

	private bool IsSeparatedByMineshaftElevator(Vector3 target)
	{
		if (RoundManager.Instance.currentDungeonType == 4 && !isOutside && RoundManager.Instance.currentMineshaftElevator != null && !RoundManager.Instance.currentMineshaftElevator.elevatorDoorOpen && RoundManager.Instance.currentMineshaftElevator.elevatorBounds.bounds.Contains(destination) != RoundManager.Instance.currentMineshaftElevator.elevatorBounds.bounds.Contains(base.transform.position))
		{
			return true;
		}
		return false;
	}

	public bool IsInsideMineshaftElevator(Vector3 target)
	{
		if (RoundManager.Instance.currentDungeonType == 4 && !isOutside && RoundManager.Instance.currentMineshaftElevator != null && RoundManager.Instance.currentMineshaftElevator.elevatorBounds.bounds.Contains(target))
		{
			return true;
		}
		return false;
	}

	public void MoveWithMineshaftElevator()
	{
		if (IsInsideMineshaftElevator(base.transform.position))
		{
			base.transform.position += RoundManager.Instance.currentMineshaftElevator.elevatorInsidePoint.position - RoundManager.Instance.currentMineshaftElevator.previousElevatorPosition;
			serverPosition = base.transform.position;
		}
	}

	public virtual void NavigateTowardsTargetPlayer()
	{
		if (setDestinationToPlayerInterval <= 0f)
		{
			setDestinationToPlayerInterval = 0.25f;
			targetPlayerPositionHorizontallyOffset = false;
			destination = RoundManager.Instance.GetNavMeshPosition(targetPlayer.transform.position, RoundManager.Instance.navHit, 3f, agentMask);
			if (IsSeparatedByMineshaftElevator(destination))
			{
				if (RoundManager.Instance.currentMineshaftElevator.elevatorMovingDown)
				{
					destination = RoundManager.Instance.currentMineshaftElevator.elevatorBottomPointDoor.position;
				}
				else
				{
					destination = RoundManager.Instance.currentMineshaftElevator.elevatorTopPoint.position;
				}
				targetPlayerPositionHorizontallyOffset = true;
			}
			if (!targetPlayerPositionHorizontallyOffset && (Mathf.Abs(targetPlayer.transform.position.x - destination.x) > 0.2f || Mathf.Abs(targetPlayer.transform.position.z - destination.z) > 0.2f))
			{
				targetPlayerPositionHorizontallyOffset = true;
			}
		}
		else
		{
			if (!targetPlayerPositionHorizontallyOffset)
			{
				destination = new Vector3(targetPlayer.transform.position.x, destination.y, targetPlayer.transform.position.z);
			}
			setDestinationToPlayerInterval -= Time.deltaTime;
		}
		if (addPlayerVelocityToDestination > 0f)
		{
			if (targetPlayer == GameNetworkManager.Instance.localPlayerController)
			{
				destination += Vector3.Normalize(targetPlayer.thisController.velocity * 100f) * addPlayerVelocityToDestination;
			}
			else if (targetPlayer.timeSincePlayerMoving < 0.25f)
			{
				destination += Vector3.Normalize((targetPlayer.serverPlayerPosition - targetPlayer.oldPlayerPosition) * 100f) * addPlayerVelocityToDestination;
			}
		}
	}

	public void KillEnemyOnOwnerClient(bool overrideDestroy = false)
	{
		if (!base.IsOwner)
		{
			return;
		}
		bool flag = enemyType.destroyOnDeath;
		if (overrideDestroy)
		{
			flag = true;
		}
		if ((!enemyType.canDie && !flag) || isEnemyDead)
		{
			return;
		}
		Debug.Log($"Kill enemy called! destroy: {flag}");
		if (flag)
		{
			if (base.IsServer)
			{
				Debug.Log("Kill enemy called on server, destroy true");
				KillEnemy(destroy: true);
			}
			else
			{
				KillEnemyServerRpc(destroy: true);
			}
		}
		else
		{
			KillEnemy();
			if (base.NetworkObject.IsSpawned)
			{
				KillEnemyServerRpc(destroy: false);
			}
		}
	}

		[ServerRpc(RequireOwnership = false)]
		public void KillEnemyServerRpc(bool destroy)
		{
			Debug.Log($"Kill enemy server rpc called with destroy {destroy}");
			if (destroy)
			{
				KillEnemy(destroy);
			}
			else
			{
				KillEnemyClientRpc(destroy);
			}
		}

		[ClientRpc]
		public void KillEnemyClientRpc(bool destroy)
		{
			Debug.Log($"Kill enemy client rpc called; {destroy}");
			if (!isEnemyDead)
			{
				KillEnemy(destroy);
			}
		}

	public virtual void KillEnemy(bool destroy = false)
	{
		if (destroy && enemyType.canBeDestroyed)
		{
			if (base.IsServer && thisNetworkObject.IsSpawned)
			{
				thisNetworkObject.Despawn();
			}
		}
		else
		{
			if (!enemyType.canDie)
			{
				return;
			}
			ScanNodeProperties componentInChildren = base.gameObject.GetComponentInChildren<ScanNodeProperties>();
			if (componentInChildren != null && (bool)componentInChildren.gameObject.GetComponent<Collider>())
			{
				componentInChildren.gameObject.GetComponent<Collider>().enabled = false;
			}
			isEnemyDead = true;
			if (creatureVoice != null)
			{
				creatureVoice.PlayOneShot(dieSFX);
				Debug.Log("Playing death sound for enemy: " + enemyType.enemyName);
			}
			try
			{
				if (creatureAnimator != null)
				{
					creatureAnimator.SetBool("Stunned", value: false);
					creatureAnimator.SetBool("stunned", value: false);
					creatureAnimator.SetBool("stun", value: false);
					creatureAnimator.SetTrigger("KillEnemy");
					creatureAnimator.SetBool("Dead", value: true);
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"enemy did not have bool in animator in KillEnemy, error returned; {arg}");
			}
			CancelSpecialAnimationWithPlayer();
			SubtractFromPowerLevel();
			agent.enabled = false;
		}
	}

	public virtual void CancelSpecialAnimationWithPlayer()
	{
		if ((bool)inSpecialAnimationWithPlayer)
		{
			inSpecialAnimationWithPlayer.inSpecialInteractAnimation = false;
			inSpecialAnimationWithPlayer.snapToServerPosition = false;
			inSpecialAnimationWithPlayer.inAnimationWithEnemy = null;
			inSpecialAnimationWithPlayer = null;
		}
		inSpecialAnimation = false;
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (RoundManager.Instance.SpawnedEnemies.Contains(this))
		{
			RoundManager.Instance.SpawnedEnemies.Remove(this);
		}
		SubtractFromPowerLevel();
		CancelSpecialAnimationWithPlayer();
		if (searchCoroutine != null)
		{
			StopCoroutine(searchCoroutine);
		}
		if (chooseTargetNodeCoroutine != null)
		{
			StopCoroutine(chooseTargetNodeCoroutine);
		}
	}

	private void SubtractFromPowerLevel()
	{
		if (!removedPowerLevel)
		{
			removedPowerLevel = true;
			if (enemyType.isDaytimeEnemy)
			{
				RoundManager.Instance.currentDaytimeEnemyPower = Mathf.Max(RoundManager.Instance.currentDaytimeEnemyPower - enemyType.PowerLevel, 0f);
				return;
			}
			if (enemyType.isOutsideEnemy)
			{
				RoundManager.Instance.currentOutsideEnemyPower = Mathf.Max(RoundManager.Instance.currentOutsideEnemyPower - enemyType.PowerLevel, 0f);
				return;
			}
			RoundManager.Instance.cannotSpawnMoreInsideEnemies = false;
			RoundManager.Instance.currentEnemyPower = Mathf.Max(RoundManager.Instance.currentEnemyPower - enemyType.PowerLevel, 0f);
		}
	}

		[ServerRpc(RequireOwnership = false)]
		private void UpdateEnemyRotationServerRpc(short rotationY)
		{
			UpdateEnemyRotationClientRpc(rotationY);
		}

		[ClientRpc]
		private void UpdateEnemyRotationClientRpc(short rotationY)
		{
			previousYRotation = base.transform.eulerAngles.y;
			targetYRotation = rotationY;
		}

		[Rpc(SendTo.NotMe)]
		private void UpdateEnemyPositionRpc(Vector3 newPos)
		{
			serverPosition = newPos;
			OnSyncPositionFromServer(newPos);
		}

	public virtual void OnSyncPositionFromServer(Vector3 newPos)
	{
	}

	public virtual void OnDrawGizmos()
	{
		if (base.IsOwner && debugEnemyAI)
		{
			Gizmos.DrawSphere(destination, 0.5f);
			Gizmos.DrawLine(base.transform.position, destination);
		}
	}

	public void ChangeOwnershipOfEnemy(ulong newOwnerClientId)
	{
		if (!StartOfRound.Instance.ClientPlayerList.TryGetValue(newOwnerClientId, out var value))
		{
			Debug.LogError($"Attempted to switch ownership of enemy {base.gameObject.name} to a player which does not have a link between client id and player object. Attempted clientId: {newOwnerClientId}");
		}
		else if (currentOwnershipOnThisClient == value)
		{
			if (debugEnemyAI)
			{
				Debug.Log($"enemy {enemyType.enemyName} #{thisEnemyIndex}: unable to set owner of {enemyType.name} #{thisEnemyIndex} to player #{value}; reason B; {base.NetworkObject.OwnerClientId} Time of day: {TimeOfDay.Instance.normalizedTimeOfDay}");
			}
		}
		else if (base.gameObject.GetComponent<NetworkObject>().OwnerClientId != newOwnerClientId)
		{
			if (debugEnemyAI)
			{
				Debug.Log($"{enemyType.enemyName} #{thisEnemyIndex}: ChangeOwnership success. setting ownership to player #{value} from currentOwnershipOnThisClient {currentOwnershipOnThisClient}. Time of day: {TimeOfDay.Instance.normalizedTimeOfDay}");
				HUDManager.Instance.SetDebugText($"CHANGE OWNERSHIP\nFROM Player #{currentOwnershipOnThisClient} to #{value}");
			}
			currentOwnershipOnThisClient = value;
			if (!base.IsServer)
			{
				ChangeEnemyOwnerServerRpc(newOwnerClientId);
				return;
			}
			thisNetworkObject.ChangeOwnership(newOwnerClientId);
			ChangeEnemyOwnerServerRpc(newOwnerClientId);
		}
	}

		[ServerRpc(RequireOwnership = false)]
		public void ChangeEnemyOwnerServerRpc(ulong clientId)
		{
			if (debugEnemyAI)
			{
				Debug.Log($"ENEMYAI On Server: Set clientId to {clientId} from current owner clientId: {base.gameObject.GetComponent<NetworkObject>().OwnerClientId} Time of day: {TimeOfDay.Instance.normalizedTimeOfDay}");
			}

			if (base.gameObject.GetComponent<NetworkObject>().OwnerClientId != clientId)
			{
				if (debugEnemyAI)
				{
					Debug.Log($"ENEMYAI On Server: Setting changeownership Time of day: {TimeOfDay.Instance.normalizedTimeOfDay}");
				}

				thisNetworkObject.ChangeOwnership(clientId);
			}

			if (debugEnemyAI)
			{
				Debug.Log($"ENEMYAI On Server: Found player with clientId #{clientId} in ClientPlayerList?: {StartOfRound.Instance.ClientPlayerList.TryGetValue(clientId, out var _)} Time of day: {TimeOfDay.Instance.normalizedTimeOfDay}");
			}

			if (StartOfRound.Instance.ClientPlayerList.TryGetValue(clientId, out var value2))
			{
				currentOwnershipOnThisClient = value2;
				ChangeEnemyOwnerClientRpc(value2);
			}
		}

		[ClientRpc]
		public void ChangeEnemyOwnerClientRpc(int playerVal)
		{
			if (!base.IsServer)
			{
				if (debugEnemyAI)
				{
					Debug.Log($"ClientRPC Setting currentOwnershipOnThisClient from {currentOwnershipOnThisClient} to {playerVal} ; Time of day: {TimeOfDay.Instance.normalizedTimeOfDay}");
				}

				currentOwnershipOnThisClient = playerVal;
			}
		}

	public void SetClientCalculatingAI(bool enable)
	{
		isClientCalculatingAI = enable;
		agent.enabled = enable;
	}

	public virtual void EnableEnemyMesh(bool enable, bool overrideDoNotSet = false, bool tamperWithMeshes = false)
	{
		if (overrideSettingEnemyMeshes)
		{
			return;
		}
		int layer = ((!enable) ? 23 : 19);
		for (int i = 0; i < skinnedMeshRenderers.Length; i++)
		{
			if (!skinnedMeshRenderers[i].CompareTag("DoNotSet") || overrideDoNotSet)
			{
				skinnedMeshRenderers[i].gameObject.layer = layer;
				if (!enable && tamperWithMeshes)
				{
					skinnedMeshRenderers[i].enabled = false;
					disabledMeshes = true;
				}
				else if (disabledMeshes)
				{
					skinnedMeshRenderers[i].enabled = true;
				}
			}
		}
		for (int j = 0; j < meshRenderers.Length; j++)
		{
			if (!meshRenderers[j].CompareTag("DoNotSet") || overrideDoNotSet)
			{
				meshRenderers[j].gameObject.layer = layer;
				if (!enable && tamperWithMeshes)
				{
					meshRenderers[j].enabled = false;
					disabledMeshes = true;
				}
				else if (disabledMeshes)
				{
					meshRenderers[j].enabled = true;
				}
			}
		}
	}

	public virtual void SetEnemyOutside(bool outside = false)
	{
		isOutside = outside;
		GetAINodes();
	}

	public virtual void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
	{
	}

	public virtual void HitFromExplosion(float distance)
	{
		if (debugEnemyAI)
		{
			Debug.Log($"{enemyType.enemyName} #{thisEnemyIndex} hit by explosion");
		}
	}

	public void HitEnemyOnLocalClient(int force = 1, Vector3 hitDirection = default(Vector3), PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
		Debug.Log($"Local client hit enemy {agent.transform.name} #{thisEnemyIndex} with force of {force}.");
		int playerWhoHit2 = -1;
		if (playerWhoHit != null)
		{
			playerWhoHit2 = (int)playerWhoHit.playerClientId;
			HitEnemy(force, playerWhoHit, playHitSFX, hitID);
		}
		HitEnemyServerRpc(force, playerWhoHit2, playHitSFX, hitID);
	}

		[ServerRpc(RequireOwnership = false)]
		public void HitEnemyServerRpc(int force, int playerWhoHit, bool playHitSFX, int hitID = -1)
		{
			HitEnemyClientRpc(force, playerWhoHit, playHitSFX, hitID);
		}

		[ClientRpc]
		public void HitEnemyClientRpc(int force, int playerWhoHit, bool playHitSFX, int hitID = -1)
		{
			if (playerWhoHit != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
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
		}

	public virtual void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
		if (playHitSFX && enemyType.hitBodySFX != null && !isEnemyDead)
		{
			creatureSFX.PlayOneShot(enemyType.hitBodySFX);
			WalkieTalkie.TransmitOneShotAudio(creatureSFX, enemyType.hitBodySFX);
			if (creatureVoice != null)
			{
				creatureVoice.PlayOneShot(enemyType.hitEnemyVoiceSFX);
				WalkieTalkie.TransmitOneShotAudio(creatureVoice, enemyType.hitEnemyVoiceSFX);
			}
		}
		if (debugEnemyAI)
		{
			Debug.Log($"Enemy #{thisEnemyIndex} was hit with force of {force}");
		}
		if (playerWhoHit != null)
		{
			Debug.Log($"Client #{playerWhoHit.playerClientId} hit enemy {agent.transform.name} with force of {force}.");
		}
	}

	public virtual void ReceiveLoudNoiseBlast(Vector3 position, float angle)
	{
		float num = Vector3.Distance(position, base.transform.position);
		if (num < 60f)
		{
			float num2 = 0f;
			if (angle < 35f)
			{
				num2 = ((!(num < 25f)) ? 0.7f : 1f);
				DetectNoise(position, num2, 0, 41888);
			}
			else if (angle < 16f)
			{
				num2 = 1f;
				DetectNoise(position, num2, 0, 41888);
			}
		}
	}

	private void CheckTimeOfDayToLeave()
	{
		if (!(TimeOfDay.Instance == null) && TimeOfDay.Instance.timeHasStarted && TimeOfDay.Instance.normalizedTimeOfDay > enemyType.normalizedTimeInDayToLeave && isOutside)
		{
			daytimeEnemyLeaving = true;
			DaytimeEnemyLeave();
		}
	}

	public virtual void DaytimeEnemyLeave()
	{
		if (debugEnemyAI)
		{
			Debug.Log(base.gameObject.name + ": Daytime enemy leave function called");
		}
	}

	public void LogEnemyError(string error)
	{
		Debug.LogError($"{enemyType.name} #{thisEnemyIndex}: {error}");
	}

	public virtual void AnimationEventA()
	{
	}

	public virtual void AnimationEventB()
	{
	}

	public virtual void AnimationEventC()
	{
	}

	public virtual void AnimationEventD()
	{
	}

	public virtual Transform GetRadarHeadTransform()
	{
		return null;
	}

	public virtual void ShipTeleportEnemy()
	{
	}
}
