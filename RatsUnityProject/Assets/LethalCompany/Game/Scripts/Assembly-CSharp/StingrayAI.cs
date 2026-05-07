using System;
using System.Collections;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;

public class StingrayAI : EnemyAI
{
	public bool cloaked;

	private float stuckTimer;

	private int offsetNodeAmount = 6;

	private bool pathToFirstKilledBodyIsClear;

	private bool choseTemporaryHidingSpot;

	private Vector3 firstKilledPlayerPosition = Vector3.zero;

	private Vector3 mainEntrancePosition;

	private int previousBehaviourState;

	private Vector3 agentLocalVelocity;

	public Transform animationContainer;

	public Vector3 previousPosition;

	private float velX;

	private float velZ;

	private float alphaValue = 1f;

	private float smoothnessValue = 0.771f;

	private float timeSinceAttacking;

	private Coroutine poisonCoroutine;

	public AudioClip attackSFX;

	public AudioClip poisonInjectSFX;

	public AudioSource poisonAudio;

	public AudioClip vocalSFX;

	public AudioClip floppingSFX;

	private float exitCloakLunge;

	public AudioSource floppingAudio;

	public AudioSource slidingAudio;

	public AudioClip spitSFX;

	public ParticleSystem spitParticle;

	private Collider[] playerColliders;

	public float spitRadius = 5f;

	private bool hasSpit;

	private int slimeDecalsIndex;

	private int maxSlimeDecals = 200;

	public bool debugSlime;

	public GameObject slimePrefab;

	private int checkSlimeCollisionIndex;

	private Vector3 previousSlimePosition;

	private bool flopping;

	private float timeSpentInState;

	private bool playedVocalization;

	private float timeSinceHitting = 15f;

	public AudioSource whiningAudio;

	public bool mainAI;

	private bool spawning;

	private float runAwayTimer;

	private bool isMoving;

	private float timeSinceMoving;

	public ParticleSystem movingParticle;

	private bool localPlayerSpitOn;

	private float timeSinceSpitOn;

	private int skipScrap;

	private System.Random stingrayRandom;

	private StingrayHidingSpot hidingSpot;

	private float seePlayerAndHideTimer;

	private float watchPlayerSpitTimer;

	private int stingrayNumber;

	private void MakePlayerSlipOnSlime()
	{
		if (!mainAI)
		{
			return;
		}
		PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
		if (localPlayerController.isPlayerDead || !localPlayerController.isInsideFactory || !localPlayerController.thisController.isGrounded || (localPlayerController.isUnderwater && localPlayerController.isCrouching))
		{
			return;
		}
		int num = Mathf.Min(30, StartOfRound.Instance.slimeDecals.Count);
		for (int i = 0; i < num; i++)
		{
			if (!(StartOfRound.Instance.slimeDecals[checkSlimeCollisionIndex] == null))
			{
				float num2 = Vector3.Distance(localPlayerController.transform.position, StartOfRound.Instance.slimeDecals[checkSlimeCollisionIndex].transform.position);
				if (num2 < 2.55f && localPlayerController.transform.position.y < StartOfRound.Instance.slimeDecals[checkSlimeCollisionIndex].transform.position.y + 0.2f)
				{
					localPlayerController.slipperyFloor = Mathf.Lerp(8f, 2f, Mathf.Clamp(num2 / 3.55f, 0f, 1f));
					break;
				}
				checkSlimeCollisionIndex = (checkSlimeCollisionIndex + 1) % StartOfRound.Instance.slimeDecals.Count;
			}
		}
	}

		[Rpc(SendTo.NotMe)]
		public void CreateSlimeRpc(Vector3 slimePos)
		{
			CreateSlime(slimePos);
		}

	private void CreateSlime(Vector3[] pos)
	{
		for (int i = 0; i < pos.Length; i++)
		{
			slimeDecalsIndex = (slimeDecalsIndex + 1) % maxSlimeDecals;
			GameObject item;
			if (StartOfRound.Instance.slimeDecals.Count <= slimeDecalsIndex)
			{
				if (debugSlime)
				{
					Debug.Log($"Adding slime decal at {pos[i]}");
				}
				for (int j = 0; j < 200; j++)
				{
					if (StartOfRound.Instance.slimeDecals.Count >= maxSlimeDecals)
					{
						break;
					}
					item = UnityEngine.Object.Instantiate(slimePrefab, RoundManager.Instance.mapPropsContainer.transform);
					StartOfRound.Instance.slimeDecals.Add(item);
				}
			}
			if (debugSlime)
			{
				Debug.Log($"Spraypaint B {StartOfRound.Instance.slimeDecals.Count}; index: {slimeDecalsIndex}");
			}
			if (StartOfRound.Instance.slimeDecals[slimeDecalsIndex] == null)
			{
				Debug.LogError($"ERROR: spray paint at index {slimeDecalsIndex} is null; creating new object in its place");
				item = UnityEngine.Object.Instantiate(slimePrefab, RoundManager.Instance.mapPropsContainer.transform);
				StartOfRound.Instance.slimeDecals[slimeDecalsIndex] = item;
			}
			else
			{
				if (!StartOfRound.Instance.slimeDecals[slimeDecalsIndex].activeSelf)
				{
					StartOfRound.Instance.slimeDecals[slimeDecalsIndex].SetActive(value: true);
				}
				item = StartOfRound.Instance.slimeDecals[slimeDecalsIndex];
			}
			item.transform.position = pos[i];
			previousSlimePosition = pos[i];
			if (StartOfRound.Instance.slimeDecalsFadingIn[StartOfRound.Instance.slimeFadingInDecalIndex] != null)
			{
				StartOfRound.Instance.slimeDecalsFadingIn[StartOfRound.Instance.slimeFadingInDecalIndex].fadeFactor = 1f;
			}
			DecalProjector component = item.GetComponent<DecalProjector>();
			StartOfRound.Instance.slimeDecalsFadingIn[StartOfRound.Instance.slimeFadingInDecalIndex] = component;
			component.fadeFactor = 0f;
			StartOfRound.Instance.slimeFadingInDecalIndex = (StartOfRound.Instance.slimeFadingInDecalIndex + 1) % StartOfRound.Instance.slimeDecalsFadingIn.Length;
		}
	}

	private void CreateSlime(Vector3 pos)
	{
		slimeDecalsIndex = (slimeDecalsIndex + 1) % maxSlimeDecals;
		GameObject item;
		if (StartOfRound.Instance.slimeDecals.Count <= slimeDecalsIndex)
		{
			if (debugSlime)
			{
				Debug.Log($"Adding slime decal at {pos}");
			}
			for (int i = 0; i < 200; i++)
			{
				if (StartOfRound.Instance.slimeDecals.Count >= maxSlimeDecals)
				{
					break;
				}
				item = UnityEngine.Object.Instantiate(slimePrefab, RoundManager.Instance.mapPropsContainer.transform);
				StartOfRound.Instance.slimeDecals.Add(item);
			}
		}
		if (debugSlime)
		{
			Debug.Log($"Spraypaint B {StartOfRound.Instance.slimeDecals.Count}; index: {slimeDecalsIndex}");
		}
		if (StartOfRound.Instance.slimeDecals[slimeDecalsIndex] == null)
		{
			Debug.LogError($"ERROR: spray paint at index {slimeDecalsIndex} is null; creating new object in its place");
			item = UnityEngine.Object.Instantiate(slimePrefab, RoundManager.Instance.mapPropsContainer.transform);
			StartOfRound.Instance.slimeDecals[slimeDecalsIndex] = item;
		}
		else
		{
			if (!StartOfRound.Instance.slimeDecals[slimeDecalsIndex].activeSelf)
			{
				StartOfRound.Instance.slimeDecals[slimeDecalsIndex].SetActive(value: true);
			}
			item = StartOfRound.Instance.slimeDecals[slimeDecalsIndex];
		}
		if (slimeDecalsIndex % 6 == 0)
		{
			CreateSlime(new Vector3[4]
			{
				pos + Vector3.right * 1.75f,
				pos + Vector3.right * -1.75f,
				pos + Vector3.forward * 1.75f,
				pos + Vector3.forward * 1.75f
			});
		}
		item.transform.position = pos;
		previousSlimePosition = pos;
		if (StartOfRound.Instance.slimeDecalsFadingIn[StartOfRound.Instance.slimeFadingInDecalIndex] != null)
		{
			StartOfRound.Instance.slimeDecalsFadingIn[StartOfRound.Instance.slimeFadingInDecalIndex].fadeFactor = 1f;
		}
		DecalProjector component = item.GetComponent<DecalProjector>();
		StartOfRound.Instance.slimeDecalsFadingIn[StartOfRound.Instance.slimeFadingInDecalIndex] = component;
		component.fadeFactor = 0f;
		StartOfRound.Instance.slimeFadingInDecalIndex = (StartOfRound.Instance.slimeFadingInDecalIndex + 1) % StartOfRound.Instance.slimeDecalsFadingIn.Length;
	}

	public override void Start()
	{
		base.Start();
		spawning = true;
		hidingSpot = new StingrayHidingSpot();
		StingrayAI[] array = UnityEngine.Object.FindObjectsByType<StingrayAI>(FindObjectsSortMode.None);
		Array.Sort(array, (StingrayAI a, StingrayAI b) => a.thisEnemyIndex.CompareTo(b.thisEnemyIndex));
		int num = 1000;
		int num2 = -1;
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			if (array[num3].thisEnemyIndex < num)
			{
				num = array[num3].thisEnemyIndex;
				num2 = num3;
			}
			array[num3].stingrayNumber = num3 + 1;
		}
		array[num2].mainAI = true;
		stingrayRandom = new System.Random(StartOfRound.Instance.randomMapSeed + stingrayNumber);
		mainEntrancePosition = RoundManager.FindMainEntrancePosition();
		playerColliders = new Collider[8];
		StartOfRound.Instance.slimeDecalsFadingIn = new DecalProjector[24];
		runAwayTimer = 11f;
	}

	private void IncreaseSpeedSlowly(float increaseSpeed = 1.5f, float speedCap = 5.5f, float baseSpeed = 0f)
	{
		if (stunNormalizedTimer > 0f)
		{
			creatureAnimator.SetBool("stunned", value: true);
			agent.speed = 0f;
		}
		else
		{
			creatureAnimator.SetBool("stunned", value: false);
			agent.speed = Mathf.Clamp(agent.speed + Time.deltaTime * increaseSpeed, baseSpeed, speedCap);
		}
	}

	private void MakeMovementNoises()
	{
		if (!creatureAnimator.GetBool("moving") || creatureAnimator.GetBool("cloaked"))
		{
			if (floppingAudio.isPlaying)
			{
				floppingAudio.volume -= Time.deltaTime * 2f;
				if (floppingAudio.volume <= 0f)
				{
					floppingAudio.Stop();
				}
			}
			if (slidingAudio.isPlaying)
			{
				slidingAudio.volume -= Time.deltaTime * 2f;
				if (slidingAudio.volume <= 0f)
				{
					slidingAudio.Stop();
				}
			}
			return;
		}
		creatureAnimator.SetBool("flopping", flopping);
		if (flopping)
		{
			if (!floppingAudio.isPlaying)
			{
				floppingAudio.Play();
			}
			floppingAudio.volume = Mathf.Lerp(floppingAudio.volume, 1f, Time.deltaTime * 3f);
			if (slidingAudio.isPlaying)
			{
				slidingAudio.volume -= Time.deltaTime * 2f;
				if (slidingAudio.volume <= 0f)
				{
					slidingAudio.Stop();
				}
			}
			return;
		}
		if (!slidingAudio.isPlaying)
		{
			slidingAudio.Play();
		}
		slidingAudio.volume = Mathf.Lerp(slidingAudio.volume, 1f, Time.deltaTime * 3f);
		if (floppingAudio.isPlaying)
		{
			floppingAudio.volume -= Time.deltaTime * 2f;
			if (floppingAudio.volume <= 0f)
			{
				floppingAudio.Stop();
			}
		}
	}

	private void CalculateAnimationDirection()
	{
		agentLocalVelocity = animationContainer.InverseTransformDirection(Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f) / (Time.deltaTime * 2f));
		creatureAnimator.SetBool("moving", agentLocalVelocity.sqrMagnitude > 0f);
		previousPosition = base.transform.position;
	}

	public override void KillEnemy(bool destroy = false)
	{
		base.KillEnemy(destroy);
		whiningAudio.Stop();
		floppingAudio.Stop();
		slidingAudio.Stop();
		creatureVoice.Stop();
		creatureSFX.PlayOneShot(dieSFX);
	}

	private void FadeInSlimeDecals()
	{
		if (!mainAI)
		{
			return;
		}
		for (int i = 0; i < StartOfRound.Instance.slimeDecalsFadingIn.Length; i++)
		{
			if (!(StartOfRound.Instance.slimeDecalsFadingIn[i] == null))
			{
				StartOfRound.Instance.slimeDecalsFadingIn[i].fadeFactor += Time.deltaTime * 1.5f;
				if (StartOfRound.Instance.slimeDecalsFadingIn[i].fadeFactor >= 1f)
				{
					StartOfRound.Instance.slimeDecalsFadingIn[i] = null;
				}
			}
		}
	}

	public bool PlayerHasHorizontalLOS(PlayerControllerB player)
	{
		Vector3 to = base.transform.position - player.transform.position;
		to.y = 0f;
		return Vector3.Angle(player.transform.forward, to) < 62f;
	}

	public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
		base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
		enemyHP -= force;
		if ((float)enemyHP < 0f)
		{
			KillEnemyOnOwnerClient();
			return;
		}
		spawning = false;
		hasSpit = false;
		timeSinceHitting = 0f;
		creatureAnimator.SetTrigger("hit");
		if (base.IsOwner)
		{
			SwitchToBehaviourState(0);
		}
	}

	public override void Update()
	{
		base.Update();
		MakePlayerSlipOnSlime();
		FadeInSlimeDecals();
		if (isEnemyDead)
		{
			agent.speed = 0f;
			agent.acceleration = 50f;
			return;
		}
		if (stunNormalizedTimer > 0f)
		{
			agent.speed = 0f;
			timeSinceHitting = 0f;
			creatureAnimator.SetBool("shocking", value: true);
			cloaked = false;
			return;
		}
		creatureAnimator.SetBool("shocking", value: false);
		timeSinceAttacking += Time.deltaTime;
		timeSinceHitting += Time.deltaTime;
		timeSinceSpitOn += Time.deltaTime;
		if (timeSinceSpitOn > 2f)
		{
			localPlayerSpitOn = false;
		}
		MakeMovementNoises();
		switch (currentBehaviourStateIndex)
		{
		case 0:
		{
			if (previousBehaviourState != currentBehaviourStateIndex)
			{
				if (base.IsOwner && GameNetworkManager.Instance.localPlayerController.playerClientId != 0L)
				{
					ChangeOwnershipOfEnemy(StartOfRound.Instance.allPlayerScripts[0].actualClientId);
					break;
				}
				timeSpentInState = 0f;
				agent.speed = 0f;
				agent.acceleration = 30f;
				creatureAnimator.SetBool("cloaked", value: false);
				hasSpit = false;
				hidingSpot.gotHidingSpot = false;
				hidingSpot.choseTemporarySpot = false;
				hidingSpot.type = HidingSpotType.Temporary;
				watchPlayerSpitTimer = 0f;
				previousBehaviourState = currentBehaviourStateIndex;
			}
			runAwayTimer += Time.deltaTime;
			bool flag3 = timeSinceHitting < 10f || runAwayTimer < 10f;
			if (flag3)
			{
				flopping = false;
				Debug.Log("Time since hitting < 8!");
				Debug.Log($"Whining audio playing?: {whiningAudio.isPlaying}");
				if (exitCloakLunge < 1f && !whiningAudio.isPlaying)
				{
					Debug.Log("Start whining audio!");
					whiningAudio.Play();
				}
				Debug.Log($"Whining audio playing? B: {whiningAudio.isPlaying}");
				IncreaseSpeedSlowly(5f, 11f);
			}
			else
			{
				flopping = true;
				if (whiningAudio.isPlaying)
				{
					whiningAudio.Stop();
				}
				IncreaseSpeedSlowly(1.5f, 6f);
			}
			CalculateAnimationDirection();
			timeSpentInState += Time.deltaTime;
			if (playedVocalization && timeSpentInState > 6f)
			{
				playedVocalization = true;
				creatureVoice.PlayOneShot(vocalSFX);
			}
			if (base.IsOwner)
			{
				if (flag3 && !spawning && Vector3.Distance(base.transform.position, previousSlimePosition) > 2.15f)
				{
					timeSinceMoving = 0f;
					if (!isMoving)
					{
						isMoving = true;
						movingParticle.Play();
					}
					CreateSlime(base.transform.position);
					CreateSlimeRpc(base.transform.position);
				}
				else
				{
					timeSinceMoving += Time.deltaTime;
					if (isMoving && timeSinceMoving > 1f)
					{
						isMoving = false;
						movingParticle.Stop();
					}
				}
			}
			if (exitCloakLunge > 0f)
			{
				Debug.Log($"cloak speed lunge: {exitCloakLunge}");
				exitCloakLunge -= Time.deltaTime * 12f;
				agent.speed = exitCloakLunge;
				agent.acceleration = 1250f;
				if (base.IsOwner && targetPlayer != null)
				{
					RoundManager.Instance.tempTransform.position = base.transform.position;
					RoundManager.Instance.tempTransform.LookAt(targetPlayer.transform.position);
					base.transform.eulerAngles = Vector3.Lerp(base.transform.eulerAngles, new Vector3(0f, RoundManager.Instance.tempTransform.eulerAngles.y, 0f), Time.deltaTime * 24f);
				}
				if (!hasSpit && exitCloakLunge < 9.75f)
				{
					hasSpit = true;
					SpitAtPlayers();
				}
				if (hasSpit && exitCloakLunge > 8f && !localPlayerSpitOn)
				{
					int num3 = Physics.OverlapCapsuleNonAlloc(base.transform.position + Vector3.up * 0.75f, base.transform.position + Vector3.up * 0.75f + base.transform.forward * spitRadius, 2.5f, playerColliders, StartOfRound.Instance.playersMask, QueryTriggerInteraction.Ignore);
					if (num3 > 0)
					{
						for (int j = 0; j < num3; j++)
						{
							PlayerControllerB component = playerColliders[j].gameObject.GetComponent<PlayerControllerB>();
							if (component != null && component == GameNetworkManager.Instance.localPlayerController)
							{
								if (Vector3.Distance(base.transform.position, component.transform.position) < 2.5f || !Physics.Linecast(base.transform.position + Vector3.up * 0.75f, component.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
								{
									SpitOnLocalPlayer();
								}
								ShowSpitOnPlayerServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
								break;
							}
						}
					}
				}
			}
			else
			{
				agent.acceleration = 31f;
				IncreaseSpeedSlowly(0.9f, 5.5f, 1.15f);
			}
			if (!base.IsOwner)
			{
				break;
			}
			movingTowardsTargetPlayer = false;
			if (timeSinceHitting > 10f && runAwayTimer > 10f && hidingSpot.gotHidingSpot)
			{
				if (Vector3.Distance(base.transform.position, hidingSpot.position) < 0.3f && !Physics.Linecast(base.transform.position, hidingSpot.position, 256, QueryTriggerInteraction.Ignore))
				{
					SyncPositionToClients();
					agent.speed = 0f;
					agent.acceleration = 1000f;
					SwitchToBehaviourState(1);
					break;
				}
				if ((bool)CheckLineOfSightForPlayer(110f, 25, 7))
				{
					seePlayerAndHideTimer += Time.deltaTime;
					if (seePlayerAndHideTimer > 0.35f)
					{
						SyncPositionToClients();
						agent.speed = 0f;
						agent.acceleration = 1000f;
						SwitchToBehaviourState(1);
					}
				}
				else
				{
					seePlayerAndHideTimer = 0f;
				}
			}
			if (agent.velocity.sqrMagnitude < 0.002f)
			{
				stuckTimer += Time.deltaTime;
				if (stuckTimer > 4f)
				{
					stuckTimer = 0f;
					offsetNodeAmount++;
					targetNode = null;
				}
			}
			else
			{
				stuckTimer = Mathf.Max(stuckTimer - Time.deltaTime, 0f);
			}
			break;
		}
		case 1:
			if (previousBehaviourState != currentBehaviourStateIndex)
			{
				agent.speed = 0f;
				creatureAnimator.SetBool("moving", value: false);
				creatureAnimator.SetBool("cloaked", value: true);
				agent.acceleration = 350f;
				hasSpit = false;
				playedVocalization = false;
				creatureVoice.Stop();
				timeSpentInState = 0f;
				exitCloakLunge = 11f;
				runAwayTimer = 0f;
				spawning = false;
				isMoving = false;
				movingParticle.Stop();
				hidingSpot.gotHidingSpot = false;
				hidingSpot.choseTemporarySpot = false;
				hidingSpot.type = HidingSpotType.Temporary;
				if (whiningAudio.isPlaying)
				{
					whiningAudio.Stop();
				}
				previousBehaviourState = currentBehaviourStateIndex;
			}
			timeSpentInState += Time.deltaTime;
			if (base.IsServer)
			{
				bool flag = false;
				if (timeSpentInState > 30f)
				{
					if ((bool)GetClosestPlayer() && mostOptimalDistance > 30f)
					{
						flag = true;
					}
					else
					{
						timeSpentInState = 15f;
					}
				}
				if ((timeSpentInState > 45f && flag) || timeSinceHitting < 10f)
				{
					runAwayTimer = 9f;
					exitCloakLunge = 0f;
					skipScrap++;
					SwitchToBehaviourState(0);
					break;
				}
			}
			if (GameNetworkManager.Instance.localPlayerController.thisController.isGrounded && Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, base.transform.position) < 1.5f && !Physics.Linecast(base.transform.position + Vector3.up * 1.2f, GameNetworkManager.Instance.localPlayerController.playerEye.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
			{
				SwitchToBehaviourState(0);
			}
			else if ((bool)CheckLineOfSightForPlayer(110f, 9, 9))
			{
				int num = 0;
				bool flag2 = false;
				for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
				{
					if (!StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled || !StartOfRound.Instance.allPlayerScripts[i].isInsideFactory)
					{
						continue;
					}
					float num2 = Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].transform.position, base.transform.position);
					if (num2 < 9f)
					{
						num++;
						if (num2 < 5f)
						{
							flag2 = true;
						}
					}
				}
				if (flag2)
				{
					watchPlayerSpitTimer += Time.deltaTime * (float)num;
					if (watchPlayerSpitTimer > 10f)
					{
						watchPlayerSpitTimer = 0f;
						SwitchToBehaviourState(0);
					}
				}
			}
			else
			{
				watchPlayerSpitTimer = Mathf.Max(0f, watchPlayerSpitTimer - Time.deltaTime);
			}
			break;
		}
	}

	private void SpitAtPlayers()
	{
		creatureSFX.PlayOneShot(spitSFX);
		WalkieTalkie.TransmitOneShotAudio(creatureSFX, spitSFX);
		spitParticle.Play(withChildren: true);
	}

	private void SpitOnLocalPlayer()
	{
		poisonAudio.Play();
		localPlayerSpitOn = true;
		timeSinceSpitOn = 0f;
		StartCoroutine(spitDelay());
	}

		[ServerRpc(RequireOwnership = false)]
		public void ShowSpitOnPlayerServerRpc(int playerId)
		{
			ShowSpitOnPlayerClientRpc(playerId);
		}

		[ClientRpc]
		public void ShowSpitOnPlayerClientRpc(int playerId)
		{
			if (playerId != (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
			{
				StartOfRound.Instance.allPlayerScripts[playerId].slimeOnFace = 10f;
				StartOfRound.Instance.allPlayerScripts[playerId].slimeOnFaceDecals[0].gameObject.SetActive(value: true);
				StartOfRound.Instance.allPlayerScripts[playerId].slimeOnFaceDecals[1].gameObject.SetActive(value: true);
			}
		}

	private IEnumerator spitDelay()
	{
		yield return new WaitForSeconds(0.2f);
		HUDManager.Instance.DisplaySpitOnHelmet();
		Ray ray = new Ray(eye.transform.position, eye.forward);
		Debug.DrawRay(eye.transform.position, eye.forward * 4.5f, Color.magenta, 5f);
		Vector3 point;
		if (Physics.Raycast(ray, out var hitInfo, 4.5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			Debug.DrawRay(hitInfo.point, Vector3.down * 6f, Color.red, 5f);
			point = hitInfo.point;
		}
		else
		{
			Debug.DrawRay(ray.GetPoint(4.5f), Vector3.down * 6f, Color.green, 5f);
			point = ray.GetPoint(4.5f);
		}
		if (Physics.Raycast(point, Vector3.down, out hitInfo, 6f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			Vector3 point2 = hitInfo.point;
			CreateSlime(point2);
			CreateSlime(point2 + base.transform.right * 2f);
			CreateSlime(point2 + base.transform.right * -2f);
			CreateSlime(point2 + base.transform.forward * -2f);
		}
	}

	public override void DoAIInterval()
	{
		base.DoAIInterval();
		if (StartOfRound.Instance.livingPlayers == 0 || isEnemyDead || stunNormalizedTimer > 0f)
		{
			return;
		}
		if (currentBehaviourStateIndex == 1)
		{
			for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
			{
				if (StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && StartOfRound.Instance.allPlayerScripts[i].isInsideFactory && Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].transform.position, base.transform.position) < 12f)
				{
					timeSpentInState = 0f;
				}
			}
			return;
		}
		if (hidingSpot.choseTemporarySpot)
		{
			if (Vector3.Distance(base.transform.position, hidingSpot.position) < 32f && !SetDestinationToPosition(hidingSpot.position, checkForPath: true))
			{
				Debug.DrawRay(hidingSpot.position, Vector3.up * 1.5f, Color.red, 15f);
				hidingSpot.gotHidingSpot = false;
				hidingSpot.choseTemporarySpot = false;
			}
			else if (hidingSpot.type != HidingSpotType.Temporary && hidingSpot.type != HidingSpotType.NearScrap)
			{
				if (TargetClosestPlayer(4f))
				{
					float num = Vector3.Distance(hidingSpot.position, targetPlayer.transform.position);
					bool flag = timeSinceHitting < 10f || runAwayTimer < 10f;
					if ((Vector3.Distance(base.transform.position, hidingSpot.position) < 0.75f && flag) || (hidingSpot.type == HidingSpotType.AvoidPlayer && num < 8f) || (hidingSpot.type == HidingSpotType.NearPlayer && num > 35f))
					{
						hidingSpot.gotHidingSpot = false;
						hidingSpot.choseTemporarySpot = false;
					}
				}
				if (hidingSpot.gotHidingSpot)
				{
					return;
				}
			}
		}
		if (!hidingSpot.choseTemporarySpot)
		{
			hidingSpot.choseTemporarySpot = true;
			SetDestinationToNode(ChooseFarthestNodeFromPosition(mainEntrancePosition, avoidLineOfSight: false, (allAINodes.Length / 3 + 3 * stingrayNumber) % allAINodes.Length));
			hidingSpot.position = destination;
			hidingSpot.type = HidingSpotType.Temporary;
		}
		if (TargetClosestPlayer(4f))
		{
			if (hasSpit)
			{
				if (hidingSpot.type != HidingSpotType.AvoidPlayer)
				{
					Transform transform = ChooseFarthestNodeFromPosition(targetPlayer.transform.position, avoidLineOfSight: false, 0, doAsync: false, 50, 40);
					if (transform != null)
					{
						hidingSpot.position = transform.transform.position;
						hidingSpot.gotHidingSpot = true;
						hidingSpot.type = HidingSpotType.AvoidPlayer;
					}
				}
			}
			else if (hidingSpot.type != HidingSpotType.NearPlayer)
			{
				Transform transform2 = ChooseClosestNodeToPositionStingray(targetPlayer.transform.position);
				if (transform2 != null)
				{
					hidingSpot.position = transform2.transform.position;
					hidingSpot.gotHidingSpot = true;
					hidingSpot.type = HidingSpotType.NearPlayer;
				}
			}
		}
		else if (hidingSpot.type != HidingSpotType.NearScrap)
		{
			Vector3 vector = LookForScrapToHideNear();
			if (vector != Vector3.zero)
			{
				hidingSpot.position = vector;
				hidingSpot.gotHidingSpot = true;
				hidingSpot.type = HidingSpotType.NearScrap;
			}
		}
	}

	public Transform ChooseClosestNodeToPositionStingray(Vector3 pos)
	{
		Array.Sort(allAINodes, (GameObject a, GameObject b) => (pos - a.transform.position).sqrMagnitude.CompareTo((pos - b.transform.position).sqrMagnitude));
		bool flag = false;
		Transform transform = allAINodes[0].transform;
		float num = 100f;
		mostOptimalDistance = 1000f;
		int num2 = 5 * stingrayNumber % allAINodes.Length;
		int num3 = 0;
		for (int num4 = 14; num4 < allAINodes.Length; num4++)
		{
			if (!flag && num3 > 24)
			{
				break;
			}
			if ((allAINodes[num4].transform.position - pos).sqrMagnitude < 64f)
			{
				continue;
			}
			if (num2 > 0 && flag)
			{
				num2--;
			}
			else if ((RoundManager.Instance.currentDungeonType < 0 || !(Mathf.Abs(nodesTempArray[num4].transform.position.y - base.transform.position.y) > 12.5f * RoundManager.Instance.dungeonFlowTypes[RoundManager.Instance.currentDungeonType].MapTileSize)) && (!(Vector3.Distance(base.transform.position, allAINodes[num4].transform.position) < 32f) || !PathIsIntersectedByLineOfSight(allAINodes[num4].transform.position, calculatePathDistance: true)))
			{
				num3++;
				num = pathDistance;
				if (num < mostOptimalDistance)
				{
					mostOptimalDistance = num;
					transform = allAINodes[num4].transform;
					flag = true;
				}
				if (num2 == 0 || num4 >= allAINodes.Length - 1)
				{
					break;
				}
				num2--;
			}
		}
		if (flag && debugEnemyAI)
		{
			Debug.Log($"Stingray #{stingrayNumber} node chosen: {transform.gameObject.name}; offset {num2}", transform.gameObject);
		}
		return transform;
	}

	private Vector3 LookForScrapToHideNear()
	{
		int num = Physics.OverlapSphereNonAlloc(base.transform.position, 28f, playerColliders, 64, QueryTriggerInteraction.Ignore);
		if (num == 0)
		{
			return Vector3.zero;
		}
		int num2 = 0;
		skipScrap %= num;
		for (int i = skipScrap; i < num; i++)
		{
			if (num2 > 3)
			{
				return Vector3.zero;
			}
			if ((bool)playerColliders[i].gameObject.GetComponent<GrabbableObject>() && !PathIsIntersectedByLineOfSight(playerColliders[i].transform.position, calculatePathDistance: true, avoidLineOfSight: false) && pathDistance < 30f)
			{
				float y = stingrayRandom.Next(-180, 180);
				float num3 = stingrayRandom.Next(2, 6);
				RoundManager.Instance.tempTransform.position = playerColliders[i].transform.position + Vector3.up * 0.1f;
				RoundManager.Instance.tempTransform.eulerAngles = new Vector3(0f, y, 0f);
				Ray ray = new Ray(playerColliders[i].transform.position + Vector3.up * 0.1f, RoundManager.Instance.tempTransform.forward);
				Vector3 navMeshPosition;
				if (Physics.Raycast(ray, out var hitInfo, num3, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					navMeshPosition = RoundManager.Instance.GetNavMeshPosition(ray.GetPoint(hitInfo.distance / 1.3f), default(NavMeshHit), 6f, agentMask);
				}
				else
				{
					navMeshPosition = RoundManager.Instance.GetNavMeshPosition(ray.GetPoint(num3 - 0.35f));
					Debug.DrawRay(ray.GetPoint(num3 - 0.35f), Vector3.up * 0.6f, Color.green);
				}
				if (RoundManager.Instance.GotNavMeshPositionResult)
				{
					return navMeshPosition;
				}
				num2++;
			}
		}
		return Vector3.zero;
	}

	public Transform ChooseClosestNodeToPositionNoPathCheck(Vector3 pos, bool avoidLineOfSight = false, int offset = 0)
	{
		nodesTempArray = allAINodes.OrderBy((GameObject x) => Vector3.Distance(pos, x.transform.position)).ToArray();
		Transform result = nodesTempArray[0].transform;
		for (int num = 0; num < nodesTempArray.Length; num++)
		{
			mostOptimalDistance = Vector3.Distance(pos, nodesTempArray[num].transform.position);
			result = nodesTempArray[num].transform;
			if (offset == 0 || num >= nodesTempArray.Length - 1)
			{
				break;
			}
			offset--;
		}
		return result;
	}

	private void SetDestinationToNode(Transform moveTowardsNode)
	{
		targetNode = moveTowardsNode;
		SetDestinationToPosition(targetNode.position);
	}
}
