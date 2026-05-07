using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class CadaverBloomAI : EnemyAI
{
	private int previousBehaviourState = -1;

	private Vector3 fallToPosition;

	private RaycastHit rayHit;

	public bool hasBurst;

	private Coroutine fallToGroundCoroutine;

	public AnimationCurve fallCurve;

	public ParticleSystem burstParticle;

	public SkinnedMeshRenderer thisSkinnedRenderer;

	public AudioClip burstSFX;

	private Vector3 agentLocalVelocity;

	private float velX;

	private float velZ;

	public Transform animationContainer;

	private Vector3 previousPosition;

	public float seenPlayersTimer;

	public AISearchRoutine searchForPlayers;

	private Vector3 lastPositionOfSeenPlayer;

	private bool lostPlayerInChase;

	private float lostPlayerTimer;

	private float seePlayerMeter;

	private float jitterMoveInterval;

	private Vector3 jitterDest;

	private RaycastHit rayhit;

	public bool testingBloomEnemy;

	private bool chestPlantVisible;

	private float timeSinceBurst;

	public AudioSource breathingSFX;

	public AudioSource burstSource;

	public AudioClip plantFeetHit;

	public AudioClip chestBurst;

	private float timeAtLastChestBurst;

	public AudioClip[] chestPlantRoars;

	private float timeAtLastHeardNoise;

	private Vector3 pingAttentionPos;

	private float timeSinceHittingPlayer;

	private float timeAtLastHitStun;

	public GameObject killParticle;

	public PlayerControllerB burstPlayer;

	public AudioClip bitePlayer;

	private ScanNodeProperties playerScanNode;

	private float distanceToTarget;

	private RaycastHit hit;

	private bool isBehindObstacle;

	private float checkPathDistanceInterval;

	public Transform radarHeadTransform;

	public ParticleSystem teleportParticle;

	private IEnumerator teleportBloomCoroutine;

	public override Transform GetRadarHeadTransform()
	{
		return radarHeadTransform;
	}

	public override void ShipTeleportEnemy()
	{
		base.ShipTeleportEnemy();
		if (teleportBloomCoroutine != null)
		{
			StopCoroutine(teleportBloomCoroutine);
		}
		StartCoroutine(teleportBloom());
	}

	private IEnumerator teleportBloom()
	{
		teleportParticle.Play();
		creatureVoice.PlayOneShot(UnityEngine.Object.FindObjectOfType<ShipTeleporter>().beamUpPlayerBodySFX);
		yield return new WaitForSeconds(3f);
		if (StartOfRound.Instance.shipIsLeaving || !hasBurst)
		{
			yield break;
		}
		SetEnemyOutside(outside: true);
		isInsidePlayerShip = true;
		ShipTeleporter[] array = UnityEngine.Object.FindObjectsOfType<ShipTeleporter>();
		ShipTeleporter shipTeleporter = null;
		if (array != null)
		{
			for (int i = 0; i < array.Length; i++)
			{
				if (!array[i].isInverseTeleporter)
				{
					shipTeleporter = array[i];
				}
			}
		}
		if (shipTeleporter != null)
		{
			if (base.IsOwner)
			{
				agent.enabled = false;
				base.transform.position = shipTeleporter.teleporterPosition.position;
				agent.enabled = true;
				isInsidePlayerShip = true;
			}
			serverPosition = shipTeleporter.teleporterPosition.position;
			PlayerControllerB closestPlayer = GetClosestPlayer();
			if (closestPlayer != null)
			{
				PingAttention(closestPlayer.playerEye.transform.position);
			}
		}
		teleportBloomCoroutine = null;
	}

	public override void Start()
	{
		Debug.Log($"Cadaver bloom enemy #{thisEnemyIndex} running Start()!");
		base.Start();
		if (!testingBloomEnemy)
		{
			inSpecialAnimation = true;
			agent.enabled = false;
			if (!hasBurst)
			{
				EnableEnemyMesh(enable: false, overrideDoNotSet: false, tamperWithMeshes: true);
			}
		}
		else
		{
			BurstForth(StartOfRound.Instance.allPlayerScripts[0], kill: false, StartOfRound.Instance.allPlayerScripts[0].transform.position, StartOfRound.Instance.allPlayerScripts[0].transform.eulerAngles);
		}
		CadaverGrowthAI cadaverGrowthAI = UnityEngine.Object.FindObjectOfType<CadaverGrowthAI>();
		if (cadaverGrowthAI != null)
		{
			int num = -1;
			for (int i = 0; i < cadaverGrowthAI.bloomEnemies.Length; i++)
			{
				if (cadaverGrowthAI.bloomEnemies[i] == null)
				{
					num = i;
					break;
				}
			}
			cadaverGrowthAI.bloomEnemies[num] = this;
		}
		lostPlayerInChase = true;
	}

	public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
	{
		if (!base.IsOwner || !lostPlayerInChase || seePlayerMeter > 0.07f || inSpecialAnimation || isEnemyDead || Time.realtimeSinceStartup - timeAtLastHeardNoise < 1f || Vector3.Distance(noisePosition, base.transform.position + Vector3.up * 0.4f) < 0.5f || (currentBehaviourStateIndex == 2 && Vector3.Angle(base.transform.forward, noisePosition - base.transform.position) < 60f))
		{
			return;
		}
		float num = Vector3.Distance(noisePosition, base.transform.position);
		float num2 = noiseLoudness / num;
		if (!(num > 15f) && !(num2 <= 0.075f))
		{
			if (Physics.Linecast(base.transform.position, noisePosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				num2 *= 0.6f;
			}
			if (!(num2 <= 0.022f))
			{
				PingAttention(noisePosition);
			}
		}
	}

	public void PingAttention(Vector3 lookAtPos)
	{
		timeAtLastHeardNoise = Time.realtimeSinceStartup;
		pingAttentionPos = lookAtPos;
		PingAttentionRpc(lookAtPos);
	}

		[Rpc(SendTo.NotMe)]
		public void PingAttentionRpc(Vector3 lookAtPos)
		{
			timeAtLastHeardNoise = Time.realtimeSinceStartup;
			pingAttentionPos = lookAtPos;
		}

	public void BurstForth(PlayerControllerB player, bool kill, Vector3 burstPosition, Vector3 burstRotation)
	{
		burstPlayer = player;
		player.redirectToEnemy = this;
		EnableEnemyMesh(enable: true);
		inSpecialAnimation = true;
		agent.enabled = false;
		Debug.Log($"Setting bloom enemy to pos: {player.transform.position}; is agent enabled?: {agent.enabled}");
		base.transform.position = burstPosition + Vector3.up * 0.005f;
		base.transform.rotation = Quaternion.Euler(burstRotation);
		SetEnemyOutside(burstPosition.y > -90f);
		SetSuit(player.currentSuitID);
		player.DropBlood(Vector3.down);
		timeSinceBurst = Time.realtimeSinceStartup;
		fallToPosition = burstPosition;
		if (Physics.Raycast(burstPosition + Vector3.up * 0.75f, Vector3.down, out rayHit, 10f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			fallToPosition = rayHit.point;
		}
		fallToPosition = RoundManager.Instance.GetNavMeshPosition(fallToPosition, default(NavMeshHit), 1f, agentMask);
		if (!RoundManager.Instance.GotNavMeshPositionResult)
		{
			fallToPosition = RoundManager.Instance.GetNavMeshPosition(fallToPosition, default(NavMeshHit), 10f, agentMask);
		}
		serverPosition = fallToPosition;
		serverRotation = base.transform.eulerAngles;
		if (kill && player == GameNetworkManager.Instance.localPlayerController)
		{
			if (!player.isPlayerDead)
			{
				player.KillPlayer(Vector3.zero, spawnBody: false, CauseOfDeath.Suffocation, 0, default(Vector3), setOverrideDropItems: true);
			}
			else
			{
				player.overrideDontSpawnBody = true;
				player.overrideDropItems = true;
			}
			player.overrideGameOverSpectatePivot = eye;
		}
		playerScanNode = base.gameObject.GetComponentInChildren<ScanNodeProperties>();
		playerScanNode.headerText = "Body of " + player.playerUsername;
		playerScanNode.subText = "Cause of death: Suffocation";
		playerScanNode.maxRange = 0;
		hasBurst = true;
		if (fallToGroundCoroutine != null)
		{
			StopCoroutine(fallToGroundCoroutine);
		}
		fallToGroundCoroutine = StartCoroutine(DoBurstAnimation());
	}

	private IEnumerator DoBurstAnimation()
	{
		creatureAnimator.SetTrigger("Burst");
		burstParticle.Play(withChildren: true);
		burstSource.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
		burstSource.PlayOneShot(burstSFX);
		WalkieTalkie.TransmitOneShotAudio(burstSource, burstSFX);
		float num = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, base.transform.position);
		if (num < 5f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
		}
		else if (num < 14f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
		}
		float fallTimer = 0f;
		float fallTime = Mathf.Min(Vector3.Distance(base.transform.position, fallToPosition), 15f) * 0.1f;
		Vector3 startFallingPos = base.transform.position;
		System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 7);
		CadaverGrowthAI cadaverGrowthAI = UnityEngine.Object.FindObjectOfType<CadaverGrowthAI>();
		float animationTime = 1.7f;
		if (cadaverGrowthAI != null && burstPlayer != null && (bool)GetClosestPlayer() && mostOptimalDistance < 7f && cadaverGrowthAI.playerInfections[burstPlayer.playerClientId].emittingSpores && random.Next(0, 100) < 40)
		{
			animationTime = 0.35f;
		}
		if (!IsInsideMineshaftElevator(base.transform.position) || RoundManager.Instance.currentMineshaftElevator.elevatorFinishedMoving)
		{
			while (fallTimer < fallTime)
			{
				fallTimer += Time.deltaTime;
				animationTime -= Time.deltaTime;
				base.transform.position = Vector3.Lerp(startFallingPos, fallToPosition, Mathf.Clamp(fallCurve.Evaluate(fallTimer / (fallTime + 0.01f)), 0f, 1f));
				yield return null;
			}
			base.transform.position = fallToPosition;
		}
		if (animationTime > 0f)
		{
			yield return new WaitForSeconds(animationTime);
		}
		creatureAnimator.SetBool("SproutLegs", value: true);
		creatureSFX.PlayOneShot(plantFeetHit);
		yield return new WaitForSeconds(0.25f);
		breathingSFX.Play();
		creatureSFX.Play();
		inSpecialAnimation = false;
		creatureAnimator.SetBool("FinishBurst", value: true);
		exitVentAnimationTime = 0f;
		if (base.IsOwner)
		{
			CheckForVeryClosePlayer(seeInstantly: true);
		}
	}

	public void SetSuit(int suitId)
	{
		Material suitMaterial = StartOfRound.Instance.unlockablesList.unlockables[suitId].suitMaterial;
		Material[] sharedMaterials = thisSkinnedRenderer.sharedMaterials;
		sharedMaterials[1] = suitMaterial;
		thisSkinnedRenderer.sharedMaterials = sharedMaterials;
	}

	public override void DoAIInterval()
	{
		if (inSpecialAnimation)
		{
			return;
		}
		base.DoAIInterval();
		switch (currentBehaviourStateIndex)
		{
		case 0:
			if (!CheckForVeryClosePlayer())
			{
				if (!lostPlayerInChase)
				{
					Debug.Log("Switch to state 1 a");
					SwitchToBehaviourState(1);
				}
				else if (!searchForPlayers.inProgress)
				{
					searchForPlayers.searchWidth = 80f;
					Debug.Log($"Cadaver bloom burst: Started new search; is searching?: {searchForPlayers.inProgress}; transform position: {base.transform.position}");
					StartSearch(base.transform.position, searchForPlayers);
				}
			}
			break;
		case 1:
			if (CheckForVeryClosePlayer())
			{
				break;
			}
			if (targetPlayer != null)
			{
				checkPathDistanceInterval -= AIIntervalTime;
				if (checkPathDistanceInterval < 0f)
				{
					Vector3 targetPos = targetPlayer.transform.position;
					if (Physics.Raycast(targetPlayer.transform.position, Vector3.down, out hit, 15f, agentMask, QueryTriggerInteraction.Ignore))
					{
						targetPos = hit.point;
					}
					if (GetPathDistance(targetPos, base.transform.position))
					{
						distanceToTarget = pathDistance;
						isBehindObstacle = distanceToTarget - Vector3.Distance(base.transform.position, targetPlayer.transform.position) > distanceToTarget / 5f;
					}
					else
					{
						distanceToTarget = Vector3.Distance(targetPlayer.transform.position, base.transform.position);
						isBehindObstacle = false;
					}
					if (distanceToTarget > 8.7f)
					{
						checkPathDistanceInterval = 0.5f;
					}
					else
					{
						checkPathDistanceInterval = 0f;
					}
				}
			}
			if (lostPlayerInChase)
			{
				movingTowardsTargetPlayer = false;
				agent.speed = 9f;
				if (!searchForPlayers.inProgress)
				{
					searchForPlayers.searchWidth = 18f;
					if (lastPositionOfSeenPlayer == Vector3.zero)
					{
						lastPositionOfSeenPlayer = base.transform.position;
					}
					StartSearch(lastPositionOfSeenPlayer, searchForPlayers);
					Debug.Log("Cadaver bloom burst: Lost player in chase; beginning search where the player was last seen");
				}
				if (lostPlayerTimer > 9f)
				{
					SwitchToBehaviourState(0);
				}
			}
			else if (searchForPlayers.inProgress)
			{
				StopSearch(searchForPlayers);
				Debug.Log("Cadaver bloom burst: Found player during chase; stopping search coroutine and moving after target player");
			}
			break;
		case 2:
			break;
		}
	}

	private bool CheckForVeryClosePlayer(bool seeInstantly = false)
	{
		float proximityCheck = -1f;
		if (seeInstantly)
		{
			proximityCheck = 6f;
		}
		int allPlayersInLineOfSightNonAlloc = GetAllPlayersInLineOfSightNonAlloc(70f, 50, null, proximityCheck);
		float num = 2000f;
		int num2 = -1;
		for (int i = 0; i < allPlayersInLineOfSightNonAlloc; i++)
		{
			float sqrMagnitude = (eye.position - RoundManager.Instance.tempPlayersArray[i].transform.position).sqrMagnitude;
			if (sqrMagnitude < num)
			{
				num = sqrMagnitude;
				num2 = i;
			}
		}
		if (num2 != -1)
		{
			lostPlayerTimer = 0f;
			seePlayerMeter = Mathf.Min(seePlayerMeter + AIIntervalTime, 0.5f);
			if (seePlayerMeter > 0.25f || seeInstantly)
			{
				lastPositionOfSeenPlayer = RoundManager.Instance.tempPlayersArray[num2].transform.position;
				lostPlayerInChase = false;
				seePlayerMeter = 0f;
				if (targetPlayer == null || targetPlayer != RoundManager.Instance.tempPlayersArray[num2])
				{
					targetPlayer = RoundManager.Instance.tempPlayersArray[num2];
					SetNewTargetPlayerRpc((int)RoundManager.Instance.tempPlayersArray[num2].playerClientId);
					ChangeOwnershipOfEnemy(RoundManager.Instance.tempPlayersArray[num2].actualClientId);
					SwitchToBehaviourStateOnLocalClient(1);
				}
				return true;
			}
		}
		else
		{
			seePlayerMeter = Mathf.Max(seePlayerMeter - AIIntervalTime, 0f);
			lostPlayerTimer += AIIntervalTime;
			if (lostPlayerTimer > 6f)
			{
				lostPlayerInChase = true;
			}
		}
		return false;
	}

		[Rpc(SendTo.NotMe)]
		public void SetNewTargetPlayerRpc(int playerId)
		{
			targetPlayer = StartOfRound.Instance.allPlayerScripts[playerId];
			lostPlayerInChase = false;
			lostPlayerTimer = 0f;
			lastPositionOfSeenPlayer = targetPlayer.transform.position;
			SwitchToBehaviourStateOnLocalClient(1);
		}

	private void CalculateAnimationDirection(float maxSpeed = 1f)
	{
		if (agentLocalVelocity.sqrMagnitude > 0f)
		{
			creatureSFX.volume = Mathf.Lerp(creatureSFX.volume, 1f, Time.deltaTime * 10f);
		}
		else
		{
			creatureSFX.volume = Mathf.Lerp(creatureSFX.volume, 0f, Time.deltaTime * 10f);
		}
		agentLocalVelocity = animationContainer.InverseTransformDirection(Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f) / (Time.deltaTime * 2f));
		velX = Mathf.Lerp(velX, agentLocalVelocity.x, 10f * Time.deltaTime);
		creatureAnimator.SetFloat("MoveX", Mathf.Clamp(velX, 0f - maxSpeed, maxSpeed));
		velZ = Mathf.Lerp(velZ, agentLocalVelocity.z, 10f * Time.deltaTime);
		creatureAnimator.SetFloat("MoveZ", Mathf.Clamp(velZ, 0f - maxSpeed, maxSpeed));
		previousPosition = base.transform.position;
	}

	public override void OnCollideWithPlayer(Collider other)
	{
		base.OnCollideWithPlayer(other);
		if (!(Time.realtimeSinceStartup - timeSinceHittingPlayer < 0.65f) && !isEnemyDead)
		{
			PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
			if (playerControllerB != null)
			{
				timeSinceHittingPlayer = Time.realtimeSinceStartup;
				burstSource.PlayOneShot(bitePlayer);
				WalkieTalkie.TransmitOneShotAudio(burstSource, bitePlayer);
				playerControllerB.DamagePlayer(30, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling, 8);
				GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1f);
			}
		}
	}

		[Rpc(SendTo.NotMe)]
		public void SyncBitePlayerRpc()
		{
			burstSource.PlayOneShot(bitePlayer);
			WalkieTalkie.TransmitOneShotAudio(burstSource, bitePlayer);
		}

	public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
		base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
		if (!isEnemyDead)
		{
			TakeDamage(stun: true);
		}
	}

	public void TakeDamage(bool stun = false)
	{
		if (stun && Time.realtimeSinceStartup - timeAtLastHitStun > 1.15f && stunNormalizedTimer <= 0.2f)
		{
			timeAtLastHitStun = Time.realtimeSinceStartup;
		}
		enemyHP--;
		if (enemyHP <= 0)
		{
			KillEnemyOnOwnerClient();
		}
	}

	public override void KillEnemy(bool destroy = false)
	{
		removedPowerLevel = true;
		GameObject gameObject = UnityEngine.Object.Instantiate(killParticle, base.transform.position, base.transform.rotation, RoundManager.Instance.mapPropsContainer.transform);
		Transform physicsParentAtEnemyPosition = GetPhysicsParentAtEnemyPosition();
		burstPlayer.SpawnDeadBody((int)burstPlayer.playerClientId, Vector3.zero, 5, burstPlayer, 11, physicsParentAtEnemyPosition, gameObject.transform);
		if (base.IsServer)
		{
			GameObject obj = UnityEngine.Object.Instantiate(StartOfRound.Instance.ragdollGrabbableObjectPrefab, StartOfRound.Instance.propsContainer);
			obj.GetComponent<NetworkObject>().Spawn();
			obj.GetComponent<RagdollGrabbableObject>().bodyID = (int)burstPlayer.playerClientId;
		}
		if (Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, base.transform.position) < 8f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
		}
		if (searchForPlayers.inProgress)
		{
			StopSearch(searchForPlayers);
		}
		EnableEnemyMesh(enable: false, overrideDoNotSet: true, tamperWithMeshes: true);
		inSpecialAnimation = true;
		agent.enabled = false;
		agent.speed = 0f;
		overrideSettingEnemyMeshes = true;
		burstSource.Stop();
		creatureSFX.Stop();
		creatureVoice.Stop();
		breathingSFX.Stop();
		burstPlayer.redirectToEnemy = null;
		base.KillEnemy();
	}

	private Transform GetPhysicsParentAtEnemyPosition()
	{
		VehicleController vehicleController = UnityEngine.Object.FindObjectOfType<VehicleController>();
		if (vehicleController != null && !vehicleController.physicsRegion.disablePhysicsRegion && vehicleController.physicsRegion.physicsCollider.ClosestPoint(base.transform.position) == base.transform.position)
		{
			return vehicleController.physicsRegion.physicsTransform;
		}
		MineshaftElevatorController mineshaftElevatorController = UnityEngine.Object.FindObjectOfType<MineshaftElevatorController>();
		if (mineshaftElevatorController != null)
		{
			PlayerPhysicsRegion componentInChildren = mineshaftElevatorController.GetComponentInChildren<PlayerPhysicsRegion>();
			if (componentInChildren != null && componentInChildren.physicsCollider.ClosestPoint(base.transform.position + Vector3.up * 0.05f) == base.transform.position + Vector3.up * 0.05f)
			{
				return componentInChildren.physicsTransform;
			}
		}
		return null;
	}

		[Rpc(SendTo.NotMe)]
		public void SyncChompingAnimRpc(bool chomping)
		{
			chestPlantVisible = chomping;
			creatureAnimator.SetBool("ChestVisible", chomping);
			creatureAnimator.SetBool("ChestBite", chomping);
			if (chomping && !burstSource.isPlaying)
			{
				burstSource.Play();
			}
			else if (!chomping && burstSource.isPlaying)
			{
				burstSource.Stop();
			}
		}

	private void LateUpdate()
	{
		if (inSpecialAnimation)
		{
			MoveWithMineshaftElevator();
		}
	}

	public override void Update()
	{
		base.Update();
		if (isEnemyDead || inSpecialAnimation)
		{
			return;
		}
		bool flag = Time.realtimeSinceStartup - timeAtLastHeardNoise < 1.5f;
		bool flag2 = Time.realtimeSinceStartup - timeAtLastHitStun < 0.75f;
		if ((flag && (lostPlayerInChase || seePlayerMeter < 0.07f)) || flag2 || stunNormalizedTimer > 0f)
		{
			agent.speed = 0f;
			velX = Mathf.Lerp(velX, 0f, Time.deltaTime * 3f);
			velZ = Mathf.Lerp(velZ, 0f, Time.deltaTime * 3f);
			creatureAnimator.SetFloat("MoveX", velX);
			creatureAnimator.SetFloat("MoveZ", velZ);
			creatureAnimator.SetBool("stun", stunNormalizedTimer > 0f);
			creatureAnimator.SetBool("hitStunned", flag2 && stunNormalizedTimer <= 0f);
			if (stunnedByPlayer != null)
			{
				flag = true;
				pingAttentionPos = stunnedByPlayer.transform.position;
			}
			if (flag)
			{
				RoundManager.Instance.tempTransform.position = base.transform.position;
				RoundManager.Instance.tempTransform.LookAt(pingAttentionPos);
				base.transform.eulerAngles = new Vector3(0f, Mathf.LerpAngle(base.transform.eulerAngles.y, RoundManager.Instance.tempTransform.eulerAngles.y, Time.deltaTime * 22f), 0f);
			}
			return;
		}
		creatureAnimator.SetBool("hitStunned", value: false);
		creatureAnimator.SetBool("stun", value: false);
		CalculateAnimationDirection();
		if (breathingSFX.isPlaying && Time.realtimeSinceStartup - timeSinceBurst > 10f)
		{
			breathingSFX.Stop();
			playerScanNode.maxRange = 17;
		}
		int range = 18;
		if (TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
		{
			range = 8;
		}
		if (GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(base.transform.position + Vector3.up * 0.25f, 80f, range, 5f))
		{
			if (currentBehaviourStateIndex == 1 && !lostPlayerInChase)
			{
				GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.8f, 0.7f);
			}
			else
			{
				GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.8f, 0.3f);
			}
		}
		switch (currentBehaviourStateIndex)
		{
		case 0:
			if (previousBehaviourState != currentBehaviourStateIndex)
			{
				if (previousBehaviourStateIndex == 1 && searchForPlayers.inProgress)
				{
					StopSearch(searchForPlayers);
				}
				if (burstSource.isPlaying)
				{
					burstSource.Stop();
				}
				previousBehaviourState = currentBehaviourStateIndex;
			}
			if (base.IsOwner)
			{
				agent.speed = 5f;
				if (chestPlantVisible)
				{
					chestPlantVisible = false;
					creatureAnimator.SetBool("ChestVisible", value: false);
					ChestBurstRpc(burst: false);
				}
			}
			break;
		case 1:
		{
			if (previousBehaviourState != currentBehaviourStateIndex)
			{
				previousBehaviourState = currentBehaviourStateIndex;
			}
			if (!base.IsOwner || targetPlayer == null || lostPlayerInChase)
			{
				break;
			}
			RoundManager.Instance.tempTransform.position = base.transform.position;
			RoundManager.Instance.tempTransform.LookAt(targetPlayer.transform);
			base.transform.eulerAngles = new Vector3(0f, Mathf.LerpAngle(base.transform.eulerAngles.y, RoundManager.Instance.tempTransform.eulerAngles.y, Time.deltaTime * 35f), 0f);
			float num = distanceToTarget;
			if (num < 3.6f)
			{
				agent.speed = 7f;
				movingTowardsTargetPlayer = true;
				agent.acceleration = Mathf.Lerp(agent.acceleration, 50f, Time.deltaTime * 12f);
				chestPlantVisible = true;
				creatureAnimator.SetBool("ChestVisible", value: true);
				if (!creatureAnimator.GetBool("ChestBite"))
				{
					creatureAnimator.SetBool("ChestBite", value: true);
					SyncChompingAnimRpc(chomping: true);
				}
				if (!burstSource.isPlaying)
				{
					burstSource.Play();
				}
				break;
			}
			if (creatureAnimator.GetBool("ChestBite"))
			{
				creatureAnimator.SetBool("ChestBite", value: false);
				SyncChompingAnimRpc(chomping: false);
			}
			if (burstSource.isPlaying)
			{
				burstSource.Stop();
			}
			if (chestPlantVisible)
			{
				if (num > 8f && Time.realtimeSinceStartup - timeAtLastChestBurst > 3f)
				{
					chestPlantVisible = false;
					creatureAnimator.SetBool("ChestVisible", value: false);
					ChestBurstRpc(burst: false);
				}
			}
			else if (num < 6f)
			{
				timeAtLastChestBurst = Time.realtimeSinceStartup;
				chestPlantVisible = true;
				creatureAnimator.SetBool("ChestVisible", value: true);
				creatureVoice.PlayOneShot(chestBurst);
				WalkieTalkie.TransmitOneShotAudio(creatureVoice, chestBurst);
				creatureVoice.clip = chestPlantRoars[UnityEngine.Random.Range(0, chestPlantRoars.Length)];
				creatureVoice.Play();
				ChestBurstRpc(burst: true);
			}
			if (num > 18f || isBehindObstacle)
			{
				movingTowardsTargetPlayer = true;
				agent.speed = 10f;
				agent.acceleration = Mathf.Lerp(agent.acceleration, 120f, Time.deltaTime * 12f);
				break;
			}
			float num2 = 3f;
			if (pathDistance > 12f)
			{
				num2 = 6f;
			}
			agent.speed = UnityEngine.Random.Range(7f, 18f);
			jitterMoveInterval -= Time.deltaTime;
			if (jitterMoveInterval < 0f)
			{
				jitterMoveInterval = UnityEngine.Random.Range(0.1f, 0.3f);
				Ray ray = new Ray(base.transform.position + Vector3.up * 0.75f, targetPlayer.transform.position - base.transform.position);
				Vector3 point = ray.GetPoint(UnityEngine.Random.Range(0f, num / 2f));
				float num3 = UnityEngine.Random.Range(0f, Mathf.Clamp(num / num2, 4f, 15f));
				int num4 = 1;
				if (UnityEngine.Random.Range(0, 100) < 50)
				{
					num4 = -1;
				}
				if (Physics.Raycast(point, Vector3.Cross(ray.direction * num4, Vector3.up), out rayhit, num3, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
				{
					num3 = UnityEngine.Random.Range(0f, Mathf.Clamp(num / 3f, 4f, rayhit.distance));
				}
				jitterDest = new Ray(point, Vector3.Cross(ray.direction * num4, Vector3.up)).GetPoint(num3);
			}
			agent.acceleration = Mathf.Lerp(agent.acceleration, 180f, Time.deltaTime * 12f);
			movingTowardsTargetPlayer = false;
			SetDestinationToPosition(jitterDest);
			break;
		}
		case 2:
			if (previousBehaviourState != currentBehaviourStateIndex)
			{
				previousBehaviourState = currentBehaviourStateIndex;
			}
			break;
		}
	}

		[Rpc(SendTo.NotMe)]
		public void ChestBurstRpc(bool burst)
		{
			creatureAnimator.SetBool("ChestVisible", burst);
			chestPlantVisible = burst;
			if (burst)
			{
				timeAtLastChestBurst = Time.realtimeSinceStartup;
				creatureVoice.PlayOneShot(chestBurst);
				creatureVoice.clip = chestPlantRoars[UnityEngine.Random.Range(0, chestPlantRoars.Length)];
				creatureVoice.Play();
			}
		}
}
