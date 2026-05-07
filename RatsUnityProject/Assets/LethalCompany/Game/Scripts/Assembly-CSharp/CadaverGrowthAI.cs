using System;
using System.Collections.Generic;
using DunGen;
using DunGen.Tags;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;

public class CadaverGrowthAI : EnemyAI
{
	public List<TileWithGrowth> GrowthTiles = new List<TileWithGrowth>();

	public System.Random growthRandom;

	private int previousBehaviourState = -1;

	[Header("Growth Properties")]
	public float GrowthInterval = 3f;

	public AnimationCurve growthIntervalCurveOverDay;

	[Range(0f, 100f)]
	public float baseSpreadChance = 3f;

	public int TileCapacity = 40;

	[Range(0f, 100f)]
	public float GrowthChancePerInterval = 20f;

	[Space(5f)]
	[Header("Infect Properties")]
	public float InfectIntervalTime = 1f;

	[Range(0f, 2f)]
	public float ChanceToInfectMultiplier = 0.25f;

	[Range(0f, 2f)]
	public float InfectionSpeedMultiplier = 0.25f;

	[Range(0f, 2f)]
	public float BurstSpeedMultiplier = 0.25f;

	public GameObject[] plantPrefabs;

	private RaycastHit hit;

	public Tag RoomTileTag;

	public Tag TunnelTileTag;

	public Tag CaveTileTag;

	private float spreadInterval;

	public GameObject CadaverSporesParticle;

	public BatchAllMeshChildren[] plantBatchers;

	private int previousTileIndex;

	public int[,] moldPositions;

	public GameObject destroyPlantParticle;

	public AudioSource destroyAudio;

	public AudioSource plantAudio;

	private float cullIntervalTime = 0.75f;

	private float cullInterval;

	private float infectInterval;

	public PlayerInfection[] playerInfections;

	private int numberOfInfected;

	public GameObject backFlowersPrefab;

	private float showSignsMeter;

	public bool growingRecentPlant;

	public Vector3 recentPlantPosition;

	private float recentPlantSizeTarget = 1f;

	private (int batchNum, int positionIndex, int plantType) growingPlant;

	public GameObject bloomEnemyPrefab;

	public EnemyType bloomEnemyType;

	public CadaverBloomAI[] bloomEnemies;

	private float timeLastMusicPlayed;

	public AudioSource bloomMusic;

	public GameObject faceSporesPrefab;

	public AudioSource sporeAmbienceSource;

	private float updateSporeAudioInterval;

	private float distToPlants = 100f;

	private bool growingInTiles;

	public GameObject scanNodePrefab;

	private const string sporesWarningText = "HEALTH RISK!\n\nAir filter overwhelmed by particulates";

	private List<Transform> particlesTempArray;

	private System.Random playerDeathChanceRandom;

	public GameObject crackDecal;

	public AudioClip[] healPlayerSFX;

	public float totalTimeSpentInPlants;

	private float growthBurstTimer;

	private bool inGrowthBurst;

	private float timeAtLastHealing;

	public AudioSource vinesInHeadAudio;

	public AudioClip[] vinesInHeadSFX;

	private bool hinderingLocalPlayer;

	private float setPoison;

	private float localPlayerImmunityTimer;

	private bool stoodInWeedsLastCheck;

	public void OnEnable()
	{
		StartOfRound.Instance.LocalPlayerTalkEvent.AddListener(OnLocalPlayerTalk);
		StartOfRound.Instance.LocalPlayerDieEvent.AddListener(OnLocalPlayerDie);
	}

	public void OnDisable()
	{
		StartOfRound.Instance.LocalPlayerTalkEvent.RemoveListener(OnLocalPlayerTalk);
		StartOfRound.Instance.LocalPlayerDieEvent.RemoveListener(OnLocalPlayerDie);
		if (hinderingLocalPlayer)
		{
			hinderingLocalPlayer = false;
			GameNetworkManager.Instance.localPlayerController.isMovementHindered--;
			playerInfections[GameNetworkManager.Instance.localPlayerController.playerClientId].hinderingPlayerMovement = false;
		}
		setPoison = 0f;
		if (GameNetworkManager.Instance.localPlayerController.overridePoisonValue)
		{
			GameNetworkManager.Instance.localPlayerController.overridePoisonValue = false;
		}
	}

	public void OnLocalPlayerDie(PlayerControllerB player, int deathAnimation)
	{
		switch (deathAnimation)
		{
		case -1:
		case 7:
		case 9:
			Debug.Log($"Cadaver growth: Unable to burst from dying player '{player.playerUsername}'. Reason A (incompatible cause of death). Time:{TimeOfDay.Instance.normalizedTimeOfDay} ");
			return;
		default:
			if (player.enemyWaitingForBodyRagdoll != null && player.enemyWaitingForBodyRagdoll.enemyType.enemyName == "MouthDog")
			{
				Debug.Log($"Cadaver growth: Unable to burst from dying player '{player.playerUsername}'. Reason B (eyeless dog). Time:{TimeOfDay.Instance.normalizedTimeOfDay} ");
				return;
			}
			break;
		case 200:
			break;
		}
		if (!playerInfections[player.playerClientId].infected || (!playerInfections[player.playerClientId].bloomOnDeath && (double)(playerInfections[player.playerClientId].infectionMeter + 0.15f) < playerDeathChanceRandom.NextDouble()))
		{
			Debug.Log($"Cadaver growth: Unable to burst from dying player '{player.playerUsername}'. Reason C (infection not progressed enough). Time:{TimeOfDay.Instance.normalizedTimeOfDay} ");
			return;
		}
		Vector3 pos = player.transform.position;
		if (Physics.Raycast(player.transform.position, Vector3.down, out var hitInfo, 10f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			Debug.Log($"Cadaver growth: Burst on player death found raycast to ground. player position: {player.transform.position}; rayhit position: {hitInfo.point}; Time:{TimeOfDay.Instance.normalizedTimeOfDay} ");
			pos = hitInfo.point;
		}
		pos = RoundManager.Instance.GetNavMeshPosition(pos, default(NavMeshHit), 10f, agentMask);
		if (!RoundManager.Instance.GotNavMeshPositionResult)
		{
			Debug.Log($"Cadaver growth: Unable to burst from dying player '{player.playerUsername}' Reason D (no navmesh position result). player position: {player.transform.position}; Time:{TimeOfDay.Instance.normalizedTimeOfDay} ");
			return;
		}
		playerInfections[player.playerClientId].infected = false;
		numberOfInfected--;
		BurstFromPlayer(player, player.transform.position, player.transform.eulerAngles);
		SyncBurstFromPlayerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, player.transform.position, player.transform.eulerAngles);
	}

	public void OnLocalPlayerTalk(float loudness)
	{
		if (!GameNetworkManager.Instance.localPlayerController.isPlayerControlled)
		{
			return;
		}
		PlayerInfection playerInfection = playerInfections[GameNetworkManager.Instance.localPlayerController.playerClientId];
		Debug.Log("On local player talk!");
		if (!playerInfection.infected)
		{
			return;
		}
		Debug.Log($"On local player talk! B ; {playerInfection.infectionMeter} ; {playerInfection.emittingSpores}");
		if (playerInfection.infectionMeter > 0.6f && (playerInfection.emittingSpores || UnityEngine.Random.Range(0, 1000) < 10))
		{
			Debug.Log($"On local player talk! C {playerInfection.backFlowers == null}; {playerInfection.faceSpores == null}");
			if (!(playerInfection.backFlowers == null) && playerInfection.faceSpores != null)
			{
				playerInfection.faceSpores.SetFloat("BurstAmount", Mathf.Lerp(370f, 700f, loudness));
				playerInfection.faceSporesOutput = 0.16f;
				playerInfection.faceSpores.transform.position = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position + GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward * 0.25f;
				Debug.Log($"Face spore object position: {GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position}; {GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position + GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward * 0.25f}");
				playerInfection.faceSpores.transform.rotation = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.rotation;
				playerInfection.faceSpores.Play();
				CoughSporesRpc(loudness, (int)GameNetworkManager.Instance.localPlayerController.playerClientId);
			}
		}
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void CoughSporesRpc(float loudness, int playerId)
		{
	PlayerInfection obj = playerInfections[playerId];
			obj.faceSpores.SetFloat("BurstAmount", Mathf.Lerp(870f, 1300f, loudness));
			obj.faceSporesOutput = 0.45f;
			obj.faceSpores.transform.position = StartOfRound.Instance.allPlayerScripts[playerId].bodyParts[0].position + StartOfRound.Instance.allPlayerScripts[playerId].bodyParts[0].forward * 0.15f;
			obj.faceSpores.transform.rotation = StartOfRound.Instance.allPlayerScripts[playerId].bodyParts[0].transform.rotation;
			obj.faceSpores.Play();
			if (!(StartOfRound.Instance.allPlayerScripts[playerId].LineOfSightToPositionAngle(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, 12, 0f) < 30f))
			{
				return;
			}

	float num = Vector3.Distance(GameNetworkManager.Instance.localPlayerController.transform.position, StartOfRound.Instance.allPlayerScripts[playerId].transform.position);
			if (num < 6f)
			{
				HUDManager.Instance.DisplayStatusEffect("HEALTH RISK!\n\nAir filter overwhelmed by particulates");
			}

			if ((float)UnityEngine.Random.Range(0, 1000) < Mathf.Lerp(50f, 1f, Mathf.Clamp(num * num / 60f, 0f, 1f)))
			{
		bool flag = false;
				if (UnityEngine.Random.Range(0, 100) < 70)
				{
					flag = true;
				}

		bool flag2 = flag || UnityEngine.Random.Range(0, 100) < 40;
				InfectPlayer(GameNetworkManager.Instance.localPlayerController, flag, flag2);
				InfectPlayerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, flag, flag2);
			}
		}

	public bool GetAllPlantPositionsInRadius(Vector3 position, float radius, bool fillArray = false)
	{
		bool result = false;
		int[] array = new int[plantBatchers.Length];
		Debug.Log($"Checking nearby cadaver plant positions to position {position}");
		int length = moldPositions.GetLength(0);
		for (int i = 0; i < plantBatchers.Length; i++)
		{
			List<Vector3> batchedPositions = plantBatchers[i].batchedPositions;
			Debug.Log($"Checking plant type {i}");
			for (int j = 0; j < batchedPositions.Count; j++)
			{
				if (!((batchedPositions[j] - position).sqrMagnitude <= radius * radius))
				{
					continue;
				}
				Debug.Log($"Got nearby cadaver plant at index {j}, position {batchedPositions[j]}; addToArrayIndex: {array[i]}");
				result = true;
				if (fillArray)
				{
					moldPositions[array[i], i] = j;
					array[i]++;
					if (array[i] >= length)
					{
						return true;
					}
				}
				else
				{
					array[i]++;
				}
			}
		}
		if (fillArray)
		{
			for (int k = 0; k < plantBatchers.Length; k++)
			{
				Debug.Log($"batcher #{k}; addToArrayIndex: {array[k]}");
				for (int l = array[k]; l < length; l++)
				{
					Debug.Log($"setting to -1: ({l}, {k})");
					moldPositions[l, k] = -1;
				}
			}
		}
		return result;
	}

	public void DestroyPlantAtPosition(Vector3 pos, bool playEffect = false)
	{
		float radius = 1.5f;
		if (!GetAllPlantPositionsInRadius(pos, 1.5f, fillArray: true))
		{
			Debug.LogError($"Error: DestroyPlantAtPosition was called but could not find any plants at position: {pos}");
			return;
		}
		Debug.Log($"cadaver plants found at pos {pos}");
		int length = moldPositions.GetLength(0);
		for (int i = 0; i < plantBatchers.Length; i++)
		{
			for (int j = 0; j < length && moldPositions[j, i] != -1; j++)
			{
				Debug.Log($"moldPositions[{j},{i}]: {moldPositions[j, i]} -- corresponding to batchedPosition: {plantBatchers[i].batchedPositions[j]}");
			}
		}
		for (int k = 0; k < plantBatchers.Length; k++)
		{
			List<Vector3> batchedPositions = plantBatchers[k].batchedPositions;
			for (int l = 0; l < length && moldPositions[l, k] != -1; l++)
			{
				if (playEffect)
				{
					Vector3 position = batchedPositions[moldPositions[l, k]] + Vector3.up * 0.5f;
					UnityEngine.Object.Instantiate(destroyPlantParticle, position, Quaternion.identity, null);
					destroyAudio.Stop();
					destroyAudio.transform.position = position;
					destroyAudio.Play();
					RoundManager.Instance.PlayAudibleNoise(destroyAudio.transform.position, 6f, 0.5f, 0, noiseIsInsideClosedShip: false, 99611);
				}
				Debug.Log($"Destroying: planttype:{k}, index:{l}");
				RemoveWeedFromTile(plantBatchers[k].batchedPositionTiles[moldPositions[l, k]], pos, radius);
			}
		}
		int[] array = new int[40];
		for (int m = 0; m < plantBatchers.Length; m++)
		{
			List<Vector3> batchedPositions = plantBatchers[m].batchedPositions;
			for (int n = 0; n < length; n++)
			{
				array[n] = moldPositions[n, m];
			}
			Array.Sort(array, (int a, int b) => b.CompareTo(a));
			for (int num = 0; num < length; num++)
			{
				moldPositions[num, m] = array[num];
				if (moldPositions[num, m] < 0 || moldPositions[num, m] >= batchedPositions.Count)
				{
					break;
				}
				plantBatchers[m].DestroyWeedAtIndexInMatrixList(moldPositions[num, m]);
			}
		}
	}

	private void RemoveWeedFromTile(int tileIndex, Vector3 destroyAtPos, float radius)
	{
		GrowthTiles[tileIndex].plantsInTile--;
		GrowthTiles[tileIndex].particlesContainer.GetComponentsInChildren(includeInactive: true, particlesTempArray);
		for (int num = particlesTempArray.Count - 1; num >= 0; num--)
		{
			if ((destroyAtPos + Vector3.up * 0.75f - particlesTempArray[num].position).sqrMagnitude <= radius * radius)
			{
				UnityEngine.Object.Destroy(particlesTempArray[num].gameObject);
			}
		}
		GrowthTiles[tileIndex].scanNodesContainer.GetComponentsInChildren(includeInactive: true, particlesTempArray);
		for (int num2 = particlesTempArray.Count - 1; num2 >= 0; num2--)
		{
			if ((destroyAtPos + Vector3.up * 0.35f - particlesTempArray[num2].position).sqrMagnitude <= radius * radius)
			{
				UnityEngine.Object.Destroy(particlesTempArray[num2].gameObject);
			}
		}
		if (growingRecentPlant && growingPlant.positionIndex < plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum].Count && (plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum][growingPlant.positionIndex].GetPosition() - destroyAtPos).sqrMagnitude <= radius * radius)
		{
			growingPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
			growingRecentPlant = false;
		}
		GrowthTiles[tileIndex].plantPositions.Remove(destroyAtPos);
		for (int i = 0; i < GrowthTiles[tileIndex].plantPositions.Count; i++)
		{
			if ((GrowthTiles[tileIndex].plantPositions[i] - destroyAtPos).sqrMagnitude <= radius * radius)
			{
				GrowthTiles[tileIndex].plantPositions.RemoveAt(i);
			}
		}
		if (GrowthTiles[tileIndex].plantsInTile <= 0)
		{
			GrowthTiles[tileIndex].eradicated = true;
			GrowthTiles[tileIndex].eradicatedAtTime = Time.realtimeSinceStartup;
			IEnumerable<Tile> adjacentTiles = GrowthTiles[tileIndex].tile.GetAdjacentTiles();
			List<Tile> list = new List<Tile>();
			foreach (Tile item in adjacentTiles)
			{
				list.Add(item);
			}
			for (int j = 0; j < list.Count; j++)
			{
				for (int k = 0; k < GrowthTiles.Count; k++)
				{
					if (GrowthTiles[k].tile == list[j])
					{
						GrowthTiles[k].cannotSpread = false;
					}
				}
			}
		}
		spreadInterval = Mathf.Min(spreadInterval + 2f, 12f);
	}

	public override void Start()
	{
		if (UnityEngine.Object.FindObjectsByType<CadaverGrowthAI>(FindObjectsSortMode.None).Length > 1)
		{
			isEnemyDead = true;
			inSpecialAnimation = true;
			if (base.IsServer)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			return;
		}
		base.Start();
		particlesTempArray = new List<Transform>(TileCapacity);
		bloomEnemies = new CadaverBloomAI[StartOfRound.Instance.allPlayerScripts.Length];
		Debug.Log($"Cadaver bloom growth ai spawning bloomEnemy array; Players length: {StartOfRound.Instance.allPlayerScripts.Length}; {bloomEnemies.Length}");
		plantBatchers = base.gameObject.GetComponents<BatchAllMeshChildren>();
		moldPositions = new int[40, plantBatchers.Length];
		playerInfections = new PlayerInfection[StartOfRound.Instance.allPlayerScripts.Length];
		for (int i = 0; i < playerInfections.Length; i++)
		{
			playerInfections[i] = new PlayerInfection();
		}
		for (int j = 0; j < plantBatchers.Length; j++)
		{
			int length = moldPositions.GetLength(0);
			for (int k = 0; k < length; k++)
			{
				moldPositions[k, j] = -1;
			}
		}
		growthRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 11);
		playerDeathChanceRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 12);
		if (base.IsOwner)
		{
			GetRandomTileToStart();
		}
	}

	private void GetRandomTileToStart()
	{
		RuntimeDungeon runtimeDungeon = UnityEngine.Object.FindObjectOfType<RuntimeDungeon>();
		if (runtimeDungeon == null || runtimeDungeon.Generator.CurrentDungeon == null || runtimeDungeon.Generator.CurrentDungeon.AllTiles.Count <= 0)
		{
			Debug.LogError("Cadaver plant growth AI: Error! Found no dungeon");
			if (base.IsServer)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			return;
		}
		List<Tile> list = new List<Tile>();
		List<Tile> list2 = new List<Tile>();
		for (int i = 0; i < runtimeDungeon.Generator.CurrentDungeon.MainPathTiles.Count; i++)
		{
			if ((RoundManager.Instance.currentDungeonType == 4 && runtimeDungeon.Generator.CurrentDungeon.AllTiles[i].Tags.Tags.Contains(CaveTileTag)) || runtimeDungeon.Generator.CurrentDungeon.AllTiles[i].Tags.Tags.Contains(RoomTileTag))
			{
				list.Add(runtimeDungeon.Generator.CurrentDungeon.AllTiles[i]);
			}
			else
			{
				list2.Add(runtimeDungeon.Generator.CurrentDungeon.AllTiles[i]);
			}
		}
		bool flag = true;
		List<Tile> list3;
		if (growthRandom.Next(0, 100) < 30 || list.Count == 0)
		{
			list3 = list2;
			flag = false;
			if (RoundManager.Instance.currentDungeonType == 0 || (RoundManager.Instance.currentDungeonType == 3 && list3[0] == RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.MainPathTiles[0]))
			{
				list3.RemoveAt(0);
			}
		}
		else
		{
			if (list[list.Count - 1] == runtimeDungeon.Generator.CurrentDungeon.MainPathTiles[runtimeDungeon.Generator.CurrentDungeon.MainPathTiles.Count - 1])
			{
				list.RemoveAt(list.Count - 1);
				if (list.Count == 0)
				{
					list3 = list2;
					flag = false;
				}
			}
			list3 = list;
		}
		if (list3.Count == 0)
		{
			Debug.LogError("Cadaver plant growth AI: Could not find any tile in AllTiles list!");
			if (base.IsServer)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
		}
		else
		{
			int num = (flag ? growthRandom.Next(0, list3.Count) : growthRandom.Next(0, list3.Count / 2));
			GrowthTiles.Add(new TileWithGrowth(list3[num], TileCapacity, CaveTileTag, RoomTileTag, TunnelTileTag, Vector3.zero + Vector3.down * 30f));
			SyncAddInitialGrowthTileRpc(num, Vector3.zero + Vector3.down * 30f, flag);
		}
	}

	private Transform FindChildWithTag(string tag, Transform inTransform)
	{
		Transform[] componentsInChildren = inTransform.GetComponentsInChildren<Transform>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i].CompareTag(tag))
			{
				return componentsInChildren[i];
			}
		}
		return null;
	}

	public void CullParticlesForTiles()
	{
		bool flag = StartOfRound.Instance.activeCamera.transform.position.y < -90f;
		for (int i = 0; i < GrowthTiles.Count; i++)
		{
			if (GrowthTiles[i].particlesContainer == null || GrowthTiles[i].plantsContainer == null)
			{
				continue;
			}
			Vector3 a = new Vector3(GrowthTiles[i].tile.Bounds.center.x, StartOfRound.Instance.activeCamera.transform.position.y, GrowthTiles[i].tile.Bounds.center.z);
			bool flag2 = false;
			if (StartOfRound.Instance.activeCamera.transform.position.y < GrowthTiles[i].tile.Bounds.min.y || StartOfRound.Instance.activeCamera.transform.position.y > GrowthTiles[i].tile.Bounds.max.y)
			{
				flag2 = true;
			}
			if (!flag || flag2)
			{
				if (GrowthTiles[i].plantsContainer.activeSelf)
				{
					GrowthTiles[i].plantsContainer.SetActive(value: false);
				}
				if (GrowthTiles[i].particlesContainer.activeSelf)
				{
					GrowthTiles[i].particlesContainer.SetActive(value: false);
				}
				continue;
			}
			float num = Vector3.Distance(a, StartOfRound.Instance.activeCamera.transform.position);
			float num2 = Mathf.Max(GrowthTiles[i].tile.Bounds.size.x, GrowthTiles[i].tile.Bounds.size.z);
			if (num > num2 + 16f)
			{
				if (GrowthTiles[i].plantsContainer.activeSelf)
				{
					GrowthTiles[i].plantsContainer.SetActive(value: false);
				}
				if (GrowthTiles[i].particlesContainer.activeSelf)
				{
					GrowthTiles[i].particlesContainer.SetActive(value: false);
				}
				continue;
			}
			if (!GrowthTiles[i].plantsContainer.activeSelf)
			{
				GrowthTiles[i].plantsContainer.SetActive(value: true);
			}
			if (!GrowthTiles[i].particlesContainer.activeSelf)
			{
				if (num < num2 + 6f)
				{
					GrowthTiles[i].particlesContainer.SetActive(value: true);
				}
			}
			else if (num > num2 + 8f)
			{
				GrowthTiles[i].particlesContainer.SetActive(value: false);
			}
		}
	}

		[Rpc(SendTo.NotMe)]
		public void SyncSpawnPlantRpc(int plantId, int tileIndex, Vector3 spawnPosition, Quaternion spawnRotation, float randomSize, bool spawnScanNode, bool spawnDecal)
		{
			if (GrowthTiles.Count <= tileIndex)
			{
				Debug.LogError($"Error: Client does not have tile at index of {tileIndex}");
				return;
			}

			if (GrowthTiles[tileIndex].plantsContainer == null)
			{
		GameObject gameObject = new GameObject("PlantsContainer");
				gameObject.transform.position = GrowthTiles[tileIndex].tile.Bounds.center;
				gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
				GrowthTiles[tileIndex].plantsContainer = gameObject;
			}
			else
			{
		GameObject gameObject = GrowthTiles[tileIndex].plantsContainer;
			}

	GameObject gameObject2 = UnityEngine.Object.Instantiate(plantPrefabs[plantId], spawnPosition, spawnRotation, null);
			GrowthTiles[tileIndex].plantPositions.Add(spawnPosition);
			gameObject2.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
			if (GrowthTiles[tileIndex].particlesContainer == null)
			{
		GameObject gameObject = new GameObject("SporesContainer");
				gameObject.transform.position = GrowthTiles[tileIndex].tile.Bounds.center;
				gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
				GrowthTiles[tileIndex].particlesContainer = gameObject;
			}

	GameObject gameObject3 = UnityEngine.Object.Instantiate(CadaverSporesParticle, gameObject2.transform.position + Vector3.up * 0.75f, Quaternion.identity, null);
			gameObject3.transform.localScale = Vector3.one;
			gameObject3.transform.SetParent(GrowthTiles[tileIndex].particlesContainer.transform, worldPositionStays: true);
			gameObject3.GetComponent<VisualEffect>().SetVector3("RoomBoundsCenter", GrowthTiles[tileIndex].tile.Bounds.center);
			gameObject3.GetComponent<VisualEffect>().SetVector3("RoomBoundsSize", GrowthTiles[tileIndex].tile.Bounds.size);
			if (GrowthTiles[tileIndex].scanNodesContainer == null)
			{
		GameObject gameObject = new GameObject("ScanNodesContainer");
				gameObject.transform.position = GrowthTiles[tileIndex].tile.Bounds.center;
				gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
				GrowthTiles[tileIndex].scanNodesContainer = gameObject;
			}

			if (spawnScanNode)
			{
				UnityEngine.Object.Instantiate(scanNodePrefab, gameObject3.transform.position + Vector3.up * 0.35f, Quaternion.identity, null).transform.SetParent(GrowthTiles[tileIndex].scanNodesContainer.transform, worldPositionStays: true);
			}

			if (GrowthTiles[tileIndex].decalsContainer == null)
			{
		GameObject gameObject = new GameObject("DecalsContainer");
				gameObject.transform.position = GrowthTiles[tileIndex].tile.Bounds.center;
				gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
				GrowthTiles[tileIndex].decalsContainer = gameObject;
			}

			if (spawnDecal)
			{
				UnityEngine.Object.Instantiate(crackDecal, gameObject3.transform.position + Vector3.up * 0.35f, Quaternion.Euler(0f, UnityEngine.Random.Range(-360f, 360f), 0f), null).transform.SetParent(GrowthTiles[tileIndex].scanNodesContainer.transform, worldPositionStays: true);
			}

			QuickFinishGrowingPlant();
	(int, int) tuple = plantBatchers[plantId].AddToBatchedChildren(gameObject2.GetComponent<MeshFilter>(), savePositionArray: true, tileIndex);
	int item = tuple.Item1;
	int item2 = tuple.Item2;
			growingPlant = (batchNum: item, positionIndex: item2, plantType: plantId);
			recentPlantSizeTarget = randomSize * 7.75f;
			growingRecentPlant = true;
			recentPlantPosition = spawnPosition;
			plantAudio.transform.position = spawnPosition;
			plantAudio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
			plantAudio.Play();
			GrowthTiles[tileIndex].plantsInTile++;
		}

	public void GrowInTiles()
	{
		bool flag = false;
		bool flag2 = false;
		List<Tile> list = new List<Tile>();
		int num = 0;
		for (int i = previousTileIndex; i < GrowthTiles.Count; i++)
		{
			num = i;
			if (flag)
			{
				growingInTiles = false;
				previousTileIndex = i;
				return;
			}
			if (flag2)
			{
				previousTileIndex = i;
				return;
			}
			int num2 = GrowthTiles[i].tileCapacity;
			if (RoundManager.Instance.currentDungeonType == 4)
			{
				if (GrowthTiles[i].tile.Tags.Tags.Contains(CaveTileTag))
				{
					num2 = TileCapacity / 4;
				}
			}
			else if (!GrowthTiles[i].tile.Tags.Tags.Contains(RoomTileTag) && (RoundManager.Instance.currentDungeonType != 1 || !GrowthTiles[i].tile.Tags.Tags.Contains(TunnelTileTag)))
			{
				num2 = TileCapacity / 4;
			}
			float num3 = growthRandom.Next(0, 100);
			if (GrowthTiles[i].plantsInTile < num2 && num3 < GrowthChancePerInterval && !GrowthTiles[i].eradicated)
			{
				Vector3 pos;
				if (GrowthTiles[i].lastPlantGrownPosition == Vector3.zero + Vector3.down * 30f)
				{
					Transform transform = FindChildWithTag("AINode", GrowthTiles[i].tile.gameObject.transform);
					pos = ((!(transform != null)) ? RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(GrowthTiles[i].tile.Bounds.center, GrowthTiles[i].tile.Bounds.extents.x * 2f, default(NavMeshHit), growthRandom) : transform.position);
				}
				else
				{
					pos = GrowthTiles[i].lastPlantGrownPosition;
				}
				float radius = ((growthRandom.Next(0, 100) >= 25) ? 3f : 5f);
				Vector3 randomNavMeshPositionInBoxWithLimits = RoundManager.Instance.GetRandomNavMeshPositionInBoxWithLimits(pos, GrowthTiles[i].tile.Bounds.center.x - GrowthTiles[i].tile.Bounds.extents.x, GrowthTiles[i].tile.Bounds.center.x + GrowthTiles[i].tile.Bounds.extents.x, GrowthTiles[i].tile.Bounds.center.z - GrowthTiles[i].tile.Bounds.extents.z, GrowthTiles[i].tile.Bounds.center.z + GrowthTiles[i].tile.Bounds.extents.z, GrowthTiles[i].tile.Bounds.center.y - GrowthTiles[i].tile.Bounds.extents.y, GrowthTiles[i].tile.Bounds.center.y + GrowthTiles[i].tile.Bounds.extents.y, radius, default(NavMeshHit), growthRandom, StartOfRound.Instance.collidersAndRoomMaskAndDefault);
				if (randomNavMeshPositionInBoxWithLimits == Vector3.zero)
				{
					continue;
				}
				Vector3 vector = Vector3.zero;
				if (Physics.Raycast(randomNavMeshPositionInBoxWithLimits + Vector3.up * 0.2f, Vector3.down, out hit, 3f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					float num4 = Vector3.Angle(Vector3.up, hit.normal);
					if (num4 > 75f)
					{
						continue;
					}
					if (num4 > 30f)
					{
						vector = hit.normal;
					}
				}
				int num5 = growthRandom.Next(0, plantPrefabs.Length);
				if (GrowthTiles[i].plantsContainer == null)
				{
					GameObject gameObject = new GameObject("PlantsContainer");
					gameObject.transform.position = GrowthTiles[i].tile.Bounds.center;
					gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
					GrowthTiles[i].plantsContainer = gameObject;
				}
				else
				{
					GameObject gameObject = GrowthTiles[i].plantsContainer;
				}
				GameObject gameObject2 = UnityEngine.Object.Instantiate(plantPrefabs[num5], randomNavMeshPositionInBoxWithLimits, Quaternion.Euler(vector.x, growthRandom.Next(-180, 180), vector.z), null);
				float num6 = Mathf.Clamp((float)(growthRandom.NextDouble() * 0.6000000238418579), 0.35f, 1f) + 0.75f;
				GrowthTiles[i].plantPositions.Add(randomNavMeshPositionInBoxWithLimits);
				gameObject2.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
				GrowthTiles[i].plantsInTile++;
				GrowthTiles[i].lastPlantGrownPosition = randomNavMeshPositionInBoxWithLimits;
				if (GrowthTiles[i].particlesContainer == null)
				{
					GameObject gameObject = new GameObject("SporesContainer");
					gameObject.transform.position = GrowthTiles[i].tile.Bounds.center;
					gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
					GrowthTiles[i].particlesContainer = gameObject;
				}
				GameObject gameObject3 = UnityEngine.Object.Instantiate(CadaverSporesParticle, gameObject2.transform.position + Vector3.up * 0.75f, Quaternion.identity, null);
				gameObject3.transform.localScale = Vector3.one;
				gameObject3.transform.SetParent(GrowthTiles[i].particlesContainer.transform, worldPositionStays: true);
				gameObject3.GetComponent<VisualEffect>().SetVector3("RoomBoundsCenter", GrowthTiles[i].tile.Bounds.center);
				gameObject3.GetComponent<VisualEffect>().SetVector3("RoomBoundsSize", GrowthTiles[i].tile.Bounds.size);
				if (GrowthTiles[i].scanNodesContainer == null)
				{
					GameObject gameObject = new GameObject("ScanNodesContainer");
					gameObject.transform.position = GrowthTiles[i].tile.Bounds.center;
					gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
					GrowthTiles[i].scanNodesContainer = gameObject;
				}
				if (GrowthTiles[i].decalsContainer == null)
				{
					GameObject gameObject = new GameObject("DecalsContainer");
					gameObject.transform.position = GrowthTiles[i].tile.Bounds.center;
					gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform, worldPositionStays: true);
					GrowthTiles[i].decalsContainer = gameObject;
				}
				GrowthTiles[i].scanNodesContainer.GetComponentsInChildren(includeInactive: true, particlesTempArray);
				bool flag3 = false;
				bool flag4 = false;
				for (int num7 = particlesTempArray.Count - 1; num7 >= 0; num7--)
				{
					if ((gameObject3.transform.position - particlesTempArray[num7].position).sqrMagnitude <= 9f)
					{
						flag3 = true;
						break;
					}
				}
				GrowthTiles[i].decalsContainer.GetComponentsInChildren(includeInactive: true, particlesTempArray);
				for (int num8 = particlesTempArray.Count - 1; num8 >= 0; num8--)
				{
					if ((gameObject3.transform.position - particlesTempArray[num8].position).sqrMagnitude <= 36f)
					{
						flag4 = true;
						break;
					}
				}
				if (!flag3)
				{
					UnityEngine.Object.Instantiate(scanNodePrefab, gameObject3.transform.position + Vector3.up * 0.35f, Quaternion.identity, null).transform.SetParent(GrowthTiles[i].scanNodesContainer.transform, worldPositionStays: true);
				}
				if (!flag4)
				{
					GameObject obj = UnityEngine.Object.Instantiate(crackDecal, gameObject3.transform.position + Vector3.up * 0.35f, Quaternion.Euler(90f, UnityEngine.Random.Range(-360f, 360f), 0f), null);
					obj.transform.localScale *= UnityEngine.Random.Range(0.7f, 1f);
					obj.transform.SetParent(GrowthTiles[i].scanNodesContainer.transform, worldPositionStays: true);
				}
				SyncSpawnPlantRpc(num5, i, randomNavMeshPositionInBoxWithLimits, gameObject2.transform.rotation, num6, !flag3, !flag4);
				QuickFinishGrowingPlant();
				(int, int) tuple = plantBatchers[num5].AddToBatchedChildren(gameObject2.GetComponent<MeshFilter>(), savePositionArray: true, i);
				int item = tuple.Item1;
				int item2 = tuple.Item2;
				growingPlant = (batchNum: item, positionIndex: item2, plantType: num5);
				recentPlantSizeTarget = num6 * 7.75f;
				growingRecentPlant = true;
				recentPlantPosition = randomNavMeshPositionInBoxWithLimits;
				plantAudio.transform.position = randomNavMeshPositionInBoxWithLimits;
				plantAudio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
				plantAudio.Play();
				flag = true;
			}
			if (GrowthTiles[i].cannotSpread || GrowthTiles[i].plantsInTile < 7)
			{
				continue;
			}
			float num9 = Mathf.Clamp(Mathf.Lerp(-12f, 14f, (float)GrowthTiles[i].plantsInTile / (float)num2), baseSpreadChance, 100f);
			if (GrowthTiles.Count >= 6)
			{
				num9 *= 0.66f;
			}
			if ((float)growthRandom.NextDouble() * 100f > num9)
			{
				continue;
			}
			flag2 = true;
			IEnumerable<Tile> adjacentTiles = GrowthTiles[i].tile.GetAdjacentTiles();
			list?.Clear();
			foreach (Tile item3 in adjacentTiles)
			{
				list.Add(item3);
			}
			bool flag5 = false;
			bool flag6 = false;
			for (int num10 = list.Count - 1; num10 >= 0; num10--)
			{
				for (int j = 0; j < GrowthTiles.Count; j++)
				{
					if (!(GrowthTiles[j].tile == list[num10]))
					{
						continue;
					}
					if (GrowthTiles[j].eradicated)
					{
						if (Time.realtimeSinceStartup - GrowthTiles[j].eradicatedAtTime < 45f)
						{
							list.RemoveAt(num10);
							flag6 = true;
						}
						else
						{
							flag5 = true;
						}
					}
					else
					{
						list.RemoveAt(num10);
					}
					break;
				}
			}
			if (list.Count == 0)
			{
				if (!flag6)
				{
					GrowthTiles[i].cannotSpread = true;
				}
				continue;
			}
			int index = growthRandom.Next(0, list.Count);
			bool flag7 = false;
			if (flag5)
			{
				for (int k = 0; k < GrowthTiles.Count; k++)
				{
					if (GrowthTiles[k].tile == list[index])
					{
						GrowthTiles[k].eradicated = false;
						flag7 = true;
						break;
					}
				}
			}
			Vector3 vector2 = Vector3.zero + Vector3.down * 30f;
			for (int l = 0; l < list[index].UsedDoorways.Count; l++)
			{
				if (list[index].UsedDoorways[l].ConnectedDoorway.Tile == GrowthTiles[i].tile)
				{
					vector2 = list[index].UsedDoorways[l].transform.position;
					break;
				}
			}
			if (!flag7)
			{
				GrowthTiles.Add(new TileWithGrowth(list[index], TileCapacity, CaveTileTag, RoomTileTag, TunnelTileTag, vector2));
				RuntimeDungeon runtimeDungeon = UnityEngine.Object.FindObjectOfType<RuntimeDungeon>();
				if (runtimeDungeon == null || runtimeDungeon.Generator.CurrentDungeon == null || runtimeDungeon.Generator.CurrentDungeon.AllTiles.Count <= 0)
				{
					Debug.LogError("Cadaver plant growth AI: Error! Found no dungeon when adding new tile");
					return;
				}
				int num11 = -1;
				for (int m = 0; m < runtimeDungeon.Generator.CurrentDungeon.AllTiles.Count; m++)
				{
					if (runtimeDungeon.Generator.CurrentDungeon.AllTiles[m] == list[index])
					{
						num11 = m;
						break;
					}
				}
				if (num11 == -1)
				{
					Debug.LogError("Cadaver plant growth AI: Could not find adjacent tile in AllTiles list! Unable to sync to clients.");
					return;
				}
				SyncAddGrowthTileRpc(num11, vector2);
				continue;
			}
			int num12 = -1;
			for (int n = 0; n < GrowthTiles.Count; n++)
			{
				if (GrowthTiles[n].tile == list[index])
				{
					num12 = n;
					break;
				}
			}
			if (num12 == -1)
			{
				Debug.LogError("Cadaver plant growth AI: Could not find growth tile in alltiles list! Unable to sync un-eradicated tile to clients");
				return;
			}
			SyncRemoveEradicationFromTileRpc(num12);
		}
		if (num == GrowthTiles.Count - 1)
		{
			growingInTiles = false;
			previousTileIndex = 0;
		}
	}

		[Rpc(SendTo.NotMe)]
		public void SyncRemoveEradicationFromTileRpc(int tileIndex)
		{
			if (tileIndex >= GrowthTiles.Count)
			{
				Debug.LogError($"Client sent tile index greater than GrowthTiles count! tile index: {tileIndex}");
			}
			else
			{
				GrowthTiles[tileIndex].eradicated = false;
			}
		}

		[Rpc(SendTo.NotMe)]
		public void SyncAddGrowthTileRpc(int tileAtIndex, Vector3 doorPos)
		{
	RuntimeDungeon runtimeDungeon = UnityEngine.Object.FindObjectOfType<RuntimeDungeon>();
			if (runtimeDungeon == null || runtimeDungeon.Generator.CurrentDungeon == null || runtimeDungeon.Generator.CurrentDungeon.AllTiles.Count <= 0)
			{
				Debug.LogError("Cadaver plant growth AI: Error! Found no dungeon when syncing from host");
			}
			else
			{
				GrowthTiles.Add(new TileWithGrowth(runtimeDungeon.Generator.CurrentDungeon.AllTiles[tileAtIndex], TileCapacity, CaveTileTag, RoomTileTag, TunnelTileTag, doorPos));
			}
		}

		[Rpc(SendTo.NotMe)]
		public void SyncAddInitialGrowthTileRpc(int tileAtIndex, Vector3 doorPos, bool gotRoomTile)
		{
	RuntimeDungeon runtimeDungeon = UnityEngine.Object.FindObjectOfType<RuntimeDungeon>();
			if (runtimeDungeon == null || runtimeDungeon.Generator.CurrentDungeon == null || runtimeDungeon.Generator.CurrentDungeon.AllTiles.Count <= 0)
			{
				Debug.LogError("Cadaver growth AI: Error! Found no dungeon when syncing from host");
				return;
			}

	List<Tile> list = new List<Tile>();
	List<Tile> list2 = new List<Tile>();
			for (int i = 0; i < runtimeDungeon.Generator.CurrentDungeon.MainPathTiles.Count; i++)
			{
				if ((RoundManager.Instance.currentDungeonType == 4 && runtimeDungeon.Generator.CurrentDungeon.AllTiles[i].Tags.Tags.Contains(CaveTileTag)) || runtimeDungeon.Generator.CurrentDungeon.AllTiles[i].Tags.Tags.Contains(RoomTileTag))
				{
					list.Add(runtimeDungeon.Generator.CurrentDungeon.AllTiles[i]);
				}
				else
				{
					list2.Add(runtimeDungeon.Generator.CurrentDungeon.AllTiles[i]);
				}
			}

			if (RoundManager.Instance.currentDungeonType == 0 || (RoundManager.Instance.currentDungeonType == 3 && list2[0] == RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.MainPathTiles[0]))
			{
				list2.RemoveAt(0);
			}

			if (gotRoomTile && list[list.Count - 1] == runtimeDungeon.Generator.CurrentDungeon.MainPathTiles[runtimeDungeon.Generator.CurrentDungeon.MainPathTiles.Count - 1])
			{
				list.RemoveAt(list.Count - 1);
				if (list.Count == 0)
				{
					Debug.LogError("Cadaver growth AI: Client found no room tiles after removing last room tile! Unable to sync");
					return;
				}
			}

			if (gotRoomTile)
			{
				if (tileAtIndex >= list.Count)
				{
					Debug.LogError($"Cadaver growth AI: Tile index synced from host ({tileAtIndex}) is greater than the amount of rooms on the main path! {list.Count} Cannot sync!");
				}
				else
				{
					GrowthTiles.Add(new TileWithGrowth(list[tileAtIndex], TileCapacity, CaveTileTag, RoomTileTag, TunnelTileTag, doorPos));
				}
			}
			else if (tileAtIndex >= list2.Count)
			{
				Debug.LogError($"Cadaver growth AI: Non-room tile index synced from host ({tileAtIndex}) is greater than the amount of tiles on the main path! {list.Count} Cannot sync!");
			}
			else
			{
				GrowthTiles.Add(new TileWithGrowth(list2[tileAtIndex], TileCapacity, CaveTileTag, RoomTileTag, TunnelTileTag, doorPos));
			}
		}

	public static void Shuffle(MeshRenderer[] array)
	{
		for (int num = array.Length - 1; num > 0; num--)
		{
			int num2 = UnityEngine.Random.Range(0, num + 1);
			MeshRenderer meshRenderer = array[num];
			array[num] = array[num2];
			array[num2] = meshRenderer;
		}
	}

	private void IncreaseBackFlowers(int playerId)
	{
		PlayerControllerB obj = StartOfRound.Instance.allPlayerScripts[playerId];
		Debug.Log($"Increase back flowers called for player #{playerId}");
		if (obj == GameNetworkManager.Instance.localPlayerController)
		{
			return;
		}
		if (playerInfections[playerId].backFlowers == null)
		{
			playerInfections[playerId].backFlowers = UnityEngine.Object.Instantiate(backFlowersPrefab, null, worldPositionStays: true);
			playerInfections[playerId].backFlowers.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform);
			playerInfections[playerId].backFlowerRenderers = playerInfections[playerId].backFlowers.GetComponentsInChildren<MeshRenderer>();
			Shuffle(playerInfections[playerId].backFlowerRenderers);
			if (playerInfections[playerId].backFlowersScanNode == null)
			{
				playerInfections[playerId].backFlowersScanNode = UnityEngine.Object.Instantiate(scanNodePrefab, playerInfections[playerId].backFlowers.transform, worldPositionStays: true);
				playerInfections[playerId].backFlowersScanNode.transform.localPosition = new Vector3(0f, 0.18f, -0.3f);
			}
			playerInfections[playerId].backFlowersScanNode.SetActive(value: true);
		}
		int num = 0;
		int num2 = UnityEngine.Random.Range(0, 4);
		for (int i = 0; i < playerInfections[playerId].backFlowerRenderers.Length; i++)
		{
			if (!playerInfections[playerId].backFlowerRenderers[i].enabled)
			{
				playerInfections[playerId].backFlowerRenderers[i].enabled = true;
				num++;
				if (num >= num2)
				{
					break;
				}
			}
		}
	}

		[Rpc(SendTo.NotMe)]
		public void IncreaseBackFlowersRpc(int playerId, float infectionMeter)
		{
			playerInfections[playerId].infectionMeter = infectionMeter;
			if (playerInfections[playerId].infectionMeter > 0.55f)
			{
				playerInfections[playerId].bloomOnDeath = true;
			}

			IncreaseBackFlowers(playerId);
		}

	public void InfectPlayers()
	{
		if (GameNetworkManager.Instance.localPlayerController.isPlayerDead || !GameNetworkManager.Instance.localPlayerController.isInsideFactory || StartOfRound.Instance.occlusionCuller.currentTile == null)
		{
			return;
		}
		Tile currentTile = StartOfRound.Instance.occlusionCuller.currentTile;
		bool infected = playerInfections[GameNetworkManager.Instance.localPlayerController.playerClientId].infected;
		bool flag = false;
		bool flag2 = false;
		int index = -1;
		for (int i = 0; i < GrowthTiles.Count; i++)
		{
			if (GrowthTiles[i].tile == currentTile && !GrowthTiles[i].eradicated && GrowthTiles[i].plantsInTile > 0)
			{
				flag = true;
				flag2 = true;
				index = i;
				break;
			}
		}
		float num = 0f;
		if (flag)
		{
			float num2 = 1000f;
			int num3 = -1;
			float num4 = 0f;
			for (int j = 0; j < GrowthTiles[index].plantPositions.Count; j++)
			{
				float sqrMagnitude = (GameNetworkManager.Instance.localPlayerController.transform.position - GrowthTiles[index].plantPositions[j]).sqrMagnitude;
				if (sqrMagnitude < num2)
				{
					num2 = sqrMagnitude;
					num3 = j;
				}
				if (sqrMagnitude < 100f)
				{
					num4 += 1f;
				}
			}
			num = Mathf.Clamp(Mathf.Lerp(2f, 16f, num4 / (float)TileCapacity), 0f, 100f);
			num *= ChanceToInfectMultiplier;
			if (!infected)
			{
				num *= Mathf.Lerp(1f, 0.75f, (float)numberOfInfected / (float)StartOfRound.Instance.livingPlayers);
			}
			if (num3 != -1)
			{
				float sqrMagnitude = Vector3.Distance(GrowthTiles[index].plantPositions[num3], GameNetworkManager.Instance.localPlayerController.transform.position);
				num *= Mathf.Lerp(3f, 0.015f, Mathf.Clamp(sqrMagnitude / 10f, 0f, 1f));
				if (sqrMagnitude < 8f && sporeAmbienceSource.isPlaying)
				{
					sporeAmbienceSource.volume = Mathf.MoveTowards(sporeAmbienceSource.volume, 0f, Time.deltaTime);
				}
			}
		}
		bool flag3 = false;
		for (int k = 0; k < playerInfections.Length; k++)
		{
			if (k == (int)GameNetworkManager.Instance.localPlayerController.playerClientId || !playerInfections[k].infected)
			{
				continue;
			}
			float sqrMagnitude = Vector3.Distance(StartOfRound.Instance.allPlayerScripts[k].transform.position, GameNetworkManager.Instance.localPlayerController.transform.position);
			if (sqrMagnitude < 7f)
			{
				if (!flag && !flag3)
				{
					flag3 = true;
					num = 1.25f;
				}
				float min = 0.35f;
				if (flag2)
				{
					min = 1f;
				}
				num *= Mathf.Clamp(Mathf.Lerp(2f, 0.35f, Mathf.Clamp(sqrMagnitude / 7f, 0f, 1f)), min, 100f);
			}
		}
		if (num >= 1.5f && flag)
		{
			bool flag4 = false;
			if (num >= 1.9f)
			{
				if (stoodInWeedsLastCheck)
				{
					localPlayerImmunityTimer += InfectIntervalTime;
					totalTimeSpentInPlants += InfectIntervalTime;
					if (StartOfRound.Instance.connectedPlayersAmount == 0)
					{
						flag4 = true;
						if (localPlayerImmunityTimer >= 7f)
						{
							HUDManager.Instance.DisplayStatusEffect("HEALTH RISK!\nAir filter overwhelmed by particulates;\n\nFilter inoperative!");
						}
						else
						{
							HUDManager.Instance.DisplayStatusEffect($"HEALTH RISK!\nAir filter overwhelmed by particulates;\n\nFilter quality: {Mathf.RoundToInt(Mathf.Lerp(0f, 100f, Mathf.Abs(localPlayerImmunityTimer - 7f) / 7f))}%");
						}
					}
				}
				stoodInWeedsLastCheck = true;
			}
			if (!flag4)
			{
				HUDManager.Instance.DisplayStatusEffect("HEALTH RISK!\n\nAir filter overwhelmed by particulates");
			}
		}
		else if (stoodInWeedsLastCheck)
		{
			stoodInWeedsLastCheck = false;
			if (playerInfections[GameNetworkManager.Instance.localPlayerController.playerClientId].infected)
			{
				totalTimeSpentInPlants = Mathf.Max(0f, totalTimeSpentInPlants - InfectIntervalTime * 0.25f);
			}
		}
		if (!infected && flag)
		{
			if (GameNetworkManager.Instance.localPlayerController.health == 100)
			{
				num *= 0.75f;
			}
			else if (GameNetworkManager.Instance.localPlayerController.health <= 60)
			{
				num *= 1.2f;
			}
			if (GameNetworkManager.Instance.localPlayerController.criticallyInjured && stoodInWeedsLastCheck && num >= 1.9f)
			{
				num *= 1.5f;
			}
			if ((StartOfRound.Instance.connectedPlayersAmount != 0 || !(localPlayerImmunityTimer < 7f)) && (StartOfRound.Instance.connectedPlayersAmount <= 0 || !(localPlayerImmunityTimer <= 4f)) && UnityEngine.Random.Range(0f, 100f) < num)
			{
				bool flag5 = UnityEngine.Random.Range(0, 100) < 60;
				bool flag6 = flag5 || UnityEngine.Random.Range(0, 100) < 40;
				InfectPlayer(GameNetworkManager.Instance.localPlayerController, flag5, flag6);
				InfectPlayerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, flag5, flag6);
			}
		}
	}

		[Rpc(SendTo.NotMe)]
		public void AddToTimeSpentInPlantsRpc()
		{
			totalTimeSpentInPlants += infectInterval;
		}

	public void InfectPlayer(PlayerControllerB playerScript, bool severe, bool emittingSpores = false)
	{
		if (playerInfections[playerScript.playerClientId].infected)
		{
			Debug.Log($"Growth AI: Attempted to infect player #{playerScript.playerClientId} but they are already infected");
			return;
		}
		if (!playerScript.isPlayerControlled)
		{
			Debug.Log($"Growth AI: Attempted to infect player #{playerScript.playerClientId} but they are dead");
			return;
		}
		if (playerScript == GameNetworkManager.Instance.localPlayerController)
		{
			totalTimeSpentInPlants = 0f;
		}
		if (severe)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(faceSporesPrefab, null, worldPositionStays: true);
			gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform);
			playerInfections[playerScript.playerClientId].faceSpores = gameObject.GetComponent<VisualEffect>();
		}
		totalTimeSpentInPlants = 0f;
		numberOfInfected++;
		playerInfections[playerScript.playerClientId].infected = true;
		playerInfections[playerScript.playerClientId].severe = severe;
		playerInfections[playerScript.playerClientId].emittingSpores = emittingSpores;
		if (base.IsServer)
		{
			CreateBloomEnemyOnStandby();
		}
	}

	private void CreateBloomEnemyOnStandby()
	{
		int num = -1;
		for (int i = 0; i < bloomEnemies.Length; i++)
		{
			if (bloomEnemies[i] == null)
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			Debug.LogError("Attempted to spawn standby Bloom enemy, but the array is full of standby enemies!");
			return;
		}
		Debug.Log("Player infected; spawning bloom enemy on standby");
		RoundManager.Instance.SpawnEnemyGameObject(new Vector3(1200f, -300f, 0f), 0f, -1, bloomEnemyType);
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void InfectPlayerRpc(int playerId, bool severe, bool emitSpores = false)
		{
	PlayerControllerB playerScript = StartOfRound.Instance.allPlayerScripts[playerId];
			InfectPlayer(playerScript, severe, emitSpores);
		}

	private void CheckForLivingPlayers()
	{
		for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
		{
			if (!StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && playerInfections[i].infected)
			{
				playerInfections[i].infected = false;
				numberOfInfected--;
			}
		}
	}

	public void BurstFromPlayer(PlayerControllerB playerScript, Vector3 burstPosition, Vector3 burstRotation)
	{
		Debug.Log($"Burst out of player! #{playerScript.playerClientId}");
		int num = -1;
		Vector3 position = playerScript.transform.position;
		for (int i = 0; i < bloomEnemies.Length; i++)
		{
			if (!(bloomEnemies[i] == null) && !bloomEnemies[i].hasBurst)
			{
				bloomEnemies[i].BurstForth(playerScript, kill: true, burstPosition, burstRotation);
				if (playerInfections[playerScript.playerClientId].backFlowers != null)
				{
					UnityEngine.Object.Destroy(playerInfections[playerScript.playerClientId].backFlowers);
				}
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			Debug.LogError("Cadaver growth AI: Tried to burst from player, but there are no bloom enemies on standby? B");
			return;
		}
		for (int j = 0; j < StartOfRound.Instance.allPlayerScripts.Length; j++)
		{
			if (StartOfRound.Instance.allPlayerScripts[j] == playerScript)
			{
				playerInfections[j].infected = false;
				numberOfInfected--;
			}
		}
		if (Time.realtimeSinceStartup - timeLastMusicPlayed > 10f && Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, position) < 10f)
		{
			timeLastMusicPlayed = Time.realtimeSinceStartup;
			bloomMusic.Play();
		}
		if (StartOfRound.Instance.livingPlayers > 1)
		{
			PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
			PlayerInfection playerInfection = playerInfections[localPlayerController.playerClientId];
			if (localPlayerController != playerScript && localPlayerController.isPlayerControlled && playerInfection.infected && playerInfection.infectionMeter > 0.85f && Vector3.Distance(localPlayerController.transform.position, bloomEnemies[num].transform.position) < 14f)
			{
				BurstFromPlayer(localPlayerController, localPlayerController.transform.position, localPlayerController.transform.eulerAngles);
				SyncBurstFromPlayerRpc((int)localPlayerController.playerClientId, localPlayerController.transform.position, localPlayerController.transform.eulerAngles);
			}
		}
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void SyncBurstFromPlayerRpc(int playerId, Vector3 playerPosition, Vector3 playerRotation)
		{
			BurstFromPlayer(StartOfRound.Instance.allPlayerScripts[playerId], playerPosition, playerRotation);
		}

	private void ProgressPlayerInfections()
	{
		for (int i = 0; i < playerInfections.Length; i++)
		{
			if (!playerInfections[i].infected)
			{
				continue;
			}
			if (!StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled)
			{
				playerInfections[i].infected = false;
				numberOfInfected--;
			}
			else
			{
				if (StartOfRound.Instance.allPlayerScripts[i] != GameNetworkManager.Instance.localPlayerController)
				{
					continue;
				}
				PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
				if (playerInfections[i].infectionMeter >= 1f)
				{
					if (playerInfections[i].burstMeter >= 1f)
					{
						playerInfections[i].infected = false;
						numberOfInfected--;
						BurstFromPlayer(localPlayerController, localPlayerController.transform.position, localPlayerController.transform.eulerAngles);
						SyncBurstFromPlayerRpc((int)localPlayerController.playerClientId, localPlayerController.transform.position, localPlayerController.transform.eulerAngles);
						continue;
					}
					localPlayerController.poison = Mathf.Lerp(localPlayerController.poison, 0f, Time.deltaTime * 0.3f);
					if (playerInfections[i].burstMeter >= 0.9f)
					{
						if (!StartOfRound.Instance.shipIsLeaving)
						{
							if (StartOfRound.Instance.livingPlayers <= 1)
							{
								HUDManager.Instance.DisplayStatusEffect("HIGH FEVER DETECTED!!!\nFOREIGN BODIES DETECTED!!!\nIRREGULAR BRAINWAVE DETECTED!!!");
								playerInfections[i].burstMeter += Time.deltaTime * 0.0055f;
							}
							else
							{
								playerInfections[i].burstMeter += Time.deltaTime * 0.058f;
							}
							if (StartOfRound.Instance.connectedPlayersAmount > 0 && !hinderingLocalPlayer)
							{
								hinderingLocalPlayer = true;
								StartOfRound.Instance.allPlayerScripts[i].isMovementHindered++;
								playerInfections[i].hinderingPlayerMovement = true;
							}
							HUDManager.Instance.cadaverFilter = Mathf.Lerp(0f, 1f, (playerInfections[i].burstMeter - 0.9f) / 0.1f);
							SoundManager.Instance.alternateEarsRinging = true;
							SoundManager.Instance.earsRingingTimer = 1f;
						}
						continue;
					}
					float num;
					if (StartOfRound.Instance.connectedPlayersAmount > 0)
					{
						if (StartOfRound.Instance.livingPlayers == 1)
						{
							num = 0.75f;
						}
						else
						{
							float num2 = 2000f;
							for (int j = 0; j < StartOfRound.Instance.allPlayerScripts.Length; j++)
							{
								if (!(StartOfRound.Instance.allPlayerScripts[j] == GameNetworkManager.Instance.localPlayerController))
								{
									float num3 = Vector3.Distance(StartOfRound.Instance.allPlayerScripts[j].transform.position, GameNetworkManager.Instance.localPlayerController.transform.position);
									if (num3 < num2)
									{
										num2 = num3;
									}
								}
							}
							num = 1f;
							num = Mathf.Lerp(3f, 0.015f, Mathf.Clamp(num2 / 30f, 0f, 1f));
						}
					}
					else
					{
						num = 1.25f;
					}
					playerInfections[i].burstMeter += Time.deltaTime * num * BurstSpeedMultiplier;
					continue;
				}
				if (playerInfections[i].healing > 0)
				{
					playerInfections[i].infectionMeter -= Time.deltaTime * 0.08f * (float)playerInfections[i].healing;
					HUDManager.Instance.DisplayStatusEffect("FIGHTING INFECTION!\nReduction in fever");
					if (playerInfections[i].infectionMeter <= 0f)
					{
						playerInfections[i].infectionMeter = 0f;
						CurePlayer(i);
						CurePlayerRpc(i);
					}
					continue;
				}
				float num4 = 1f;
				bool flag = false;
				if (!StartOfRound.Instance.allPlayerScripts[i].isInsideFactory && TimeOfDay.Instance.normalizedTimeOfDay < 0.6f && !Physics.Linecast(TimeOfDay.Instance.sunDirect.transform.position, StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, StartOfRound.Instance.collidersRoomDefaultAndFoliage, QueryTriggerInteraction.Ignore))
				{
					flag = true;
				}
				if (flag)
				{
					num4 *= 1.15f;
				}
				if (GameNetworkManager.Instance.localPlayerController.NearOtherPlayers(15f))
				{
					num4 = ((!(playerInfections[i].infectionMeter > 0.925f)) ? (num4 * 0.7f) : (num4 * 1.5f));
				}
				num4 *= Mathf.Lerp(1f, 0.85f, (float)numberOfInfected / (float)StartOfRound.Instance.livingPlayers);
				num4 *= 1f + totalTimeSpentInPlants / 18f;
				if (localPlayerController.health <= 40)
				{
					num4 *= 1.15f;
				}
				else if (localPlayerController.health == 100)
				{
					num4 *= 0.85f;
				}
				bool flag2 = StartOfRound.Instance.connectedPlayersAmount == 0;
				float num5 = Time.deltaTime * InfectionSpeedMultiplier * num4 * playerInfections[i].multiplier;
				if (flag2)
				{
					num5 *= 0.45f;
				}
				if (Time.realtimeSinceStartup - timeAtLastHealing < 0.7f)
				{
					continue;
				}
				playerInfections[i].infectionMeter = Mathf.Clamp(playerInfections[i].infectionMeter + num5, 0f, 1f);
				showSignsMeter += num5;
				if (playerInfections[i].infectionMeter > 0.35f && !flag2)
				{
					playerInfections[i].bloomOnDeath = true;
				}
				if (localPlayerController.overridePoisonValue)
				{
					localPlayerController.poison = Mathf.Lerp(localPlayerController.poison, setPoison, Time.deltaTime * 0.7f);
				}
				if (StartOfRound.Instance.connectedPlayersAmount == 0)
				{
					if (showSignsMeter > 0.05f)
					{
						showSignsMeter = 0f;
						DisplayFeverStatusEffect(i);
						if (playerInfections[i].severe && playerInfections[i].infectionMeter > 0.6f && UnityEngine.Random.Range(0, 100) < 50)
						{
							vinesInHeadAudio.pitch = UnityEngine.Random.Range(0.8f, 1.2f);
							vinesInHeadAudio.PlayOneShot(vinesInHeadSFX[UnityEngine.Random.Range(0, vinesInHeadSFX.Length)], UnityEngine.Random.Range(0.3f, 1f));
						}
					}
				}
				else if (showSignsMeter > 0.05f)
				{
					if (playerInfections[i].severe && playerInfections[i].infectionMeter > 0.8f && UnityEngine.Random.Range(0, 100) < 50)
					{
						vinesInHeadAudio.PlayOneShot(vinesInHeadSFX[UnityEngine.Random.Range(0, vinesInHeadSFX.Length)], UnityEngine.Random.Range(0.3f, 1f));
					}
					showSignsMeter = 0f;
					float num6 = Mathf.Lerp(5f, 50f, Mathf.Clamp(playerInfections[i].infectionMeter / 0.9f, 0f, 1f));
					if (UnityEngine.Random.Range(0f, 100f) < num6)
					{
						IncreaseBackFlowers(i);
						IncreaseBackFlowersRpc(i, playerInfections[i].infectionMeter);
					}
					else
					{
						SyncInfectionMeterRpc((int)localPlayerController.playerClientId, playerInfections[i].infectionMeter);
					}
				}
			}
		}
	}

	private void DisplayFeverStatusEffect(int infectionId, float infectionThreshold = 0.8f)
	{
		if (playerInfections[infectionId].infectionMeter > infectionThreshold)
		{
			float t = (playerInfections[infectionId].infectionMeter - infectionThreshold) / (1f - infectionThreshold);
			HUDManager.Instance.DisplayStatusEffect($"HIGH FEVER DETECTED!\nREACHING {Mathf.RoundToInt(Mathf.Lerp(101f, 112f, t))}°F");
			GameNetworkManager.Instance.localPlayerController.overridePoisonValue = true;
			setPoison = Mathf.Lerp(0f, 0.4f, t);
		}
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void SyncInfectionMeterRpc(int playerId, float infectionMeter)
		{
			playerInfections[playerId].infectionMeter = infectionMeter;
			if (playerInfections[playerId].infectionMeter > 0.55f)
			{
				playerInfections[playerId].bloomOnDeath = true;
			}
		}

	public void HealInfection(int infectionId, float healAmount)
	{
		PlayerInfection obj = playerInfections[infectionId];
		int clipIndex = UnityEngine.Random.Range(0, healPlayerSFX.Length);
		HealPlayerSporeEffect(infectionId, clipIndex);
		obj.infectionMeter -= healAmount;
		timeAtLastHealing = Time.realtimeSinceStartup;
		totalTimeSpentInPlants = Mathf.Clamp(totalTimeSpentInPlants - totalTimeSpentInPlants / 4f, 0f, 100f);
		if (obj.infectionMeter <= 0f)
		{
			CurePlayer(infectionId);
			CurePlayerRpc(infectionId);
		}
		else
		{
			HealInfectionSyncRpc(infectionId, healAmount, clipIndex);
		}
	}

	private void HealPlayerSporeEffect(int infectionId, int clipIndex)
	{
		PlayerInfection playerInfection = playerInfections[infectionId];
		if (clipIndex == 0 || clipIndex == 2)
		{
			if (playerInfection.faceSpores == null)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(faceSporesPrefab, null, worldPositionStays: true);
				gameObject.transform.SetParent(RoundManager.Instance.mapPropsContainer.transform);
				playerInfection.faceSpores = gameObject.GetComponent<VisualEffect>();
				playerInfection.faceSpores.transform.position = StartOfRound.Instance.allPlayerScripts[infectionId].gameplayCamera.transform.position;
				playerInfection.faceSpores.transform.rotation = StartOfRound.Instance.allPlayerScripts[infectionId].gameplayCamera.transform.rotation;
			}
			playerInfection.faceSpores.SetFloat("BurstAmount", Mathf.Lerp(420f, 1400f, playerInfection.infectionMeter / 1f));
			playerInfection.faceSporesOutput = 0.6f;
			playerInfection.faceSpores.transform.position = StartOfRound.Instance.allPlayerScripts[infectionId].bodyParts[0].position + StartOfRound.Instance.allPlayerScripts[infectionId].bodyParts[0].forward * 0.15f;
			playerInfection.faceSpores.transform.rotation = StartOfRound.Instance.allPlayerScripts[infectionId].bodyParts[0].transform.rotation;
		}
		AudioClip clip = healPlayerSFX[clipIndex];
		StartOfRound.Instance.allPlayerScripts[infectionId].itemAudio.PlayOneShot(clip, 1f);
		WalkieTalkie.TransmitOneShotAudio(StartOfRound.Instance.allPlayerScripts[infectionId].itemAudio, clip);
		playerInfection.faceSpores.Play();
		if (infectionId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
		}
	}

		[Rpc(SendTo.NotMe, RequireOwnership = false)]
		public void HealInfectionSyncRpc(int infectionId, float healAmount, int clipIndex)
		{
	PlayerInfection obj = playerInfections[infectionId];
			HealPlayerSporeEffect(infectionId, clipIndex);
			obj.infectionMeter -= healAmount;
		}

	public void CurePlayer(int playerId)
	{
		playerInfections[playerId].infected = false;
		playerInfections[playerId].infectionMeter = 0f;
		playerInfections[playerId].burstMeter = 0f;
		playerInfections[playerId].severe = false;
		playerInfections[playerId].emittingSpores = false;
		StartOfRound.Instance.allPlayerScripts[playerId].overridePoisonValue = false;
		if (playerInfections[playerId].backFlowerRenderers != null)
		{
			for (int i = 0; i < playerInfections[playerId].backFlowerRenderers.Length; i++)
			{
				playerInfections[playerId].backFlowerRenderers[i].enabled = false;
			}
			playerInfections[playerId].backFlowersScanNode.SetActive(value: false);
			Vector3 position = playerInfections[playerId].backFlowerRenderers[0].transform.position;
			UnityEngine.Object.Instantiate(destroyPlantParticle, position, Quaternion.identity, null);
			destroyAudio.Stop();
			destroyAudio.transform.position = position;
			destroyAudio.Play();
		}
		if (playerInfections[playerId].faceSpores != null)
		{
			playerInfections[playerId].faceSpores.Stop();
		}
		if (playerInfections[playerId].hinderingPlayerMovement)
		{
			StartOfRound.Instance.allPlayerScripts[playerId].isMovementHindered = Mathf.Max(StartOfRound.Instance.allPlayerScripts[playerId].isMovementHindered - 1, 0);
			playerInfections[playerId].hinderingPlayerMovement = false;
		}
	}

		[Rpc(SendTo.NotMe)]
		public void CurePlayerRpc(int playerId)
		{
			CurePlayer(playerId);
		}

	public override void DoAIInterval()
	{
		base.DoAIInterval();
		if (isEnemyDead)
		{
			return;
		}
		switch (currentBehaviourStateIndex)
		{
		case 0:
		{
			if (inGrowthBurst)
			{
				if (growthBurstTimer > 4f)
				{
					inGrowthBurst = false;
				}
				else
				{
					growthBurstTimer += AIIntervalTime;
				}
			}
			else if (growthBurstTimer > 25f)
			{
				growthBurstTimer = 0f;
				inGrowthBurst = true;
			}
			else if (TimeOfDay.Instance.normalizedTimeOfDay < 0.2f)
			{
				growthBurstTimer += AIIntervalTime * 0.66f;
			}
			else
			{
				growthBurstTimer += AIIntervalTime;
			}
			float num = Mathf.Clamp(growthIntervalCurveOverDay.Evaluate(TimeOfDay.Instance.normalizedTimeOfDay) * GrowthInterval, 0.25f, 12f);
			if (inGrowthBurst)
			{
				num = 0.15f;
			}
			if (growingInTiles || spreadInterval > num)
			{
				growingInTiles = true;
				spreadInterval = 0f;
				GrowInTiles();
			}
			else
			{
				spreadInterval += AIIntervalTime;
			}
			break;
		}
		case 1:
		case 2:
			break;
		}
	}

	public override void Update()
	{
		base.Update();
		if (!isEnemyDead)
		{
			if (cullInterval > cullIntervalTime)
			{
				cullInterval = 0f;
				CullParticlesForTiles();
			}
			else
			{
				cullInterval += Time.deltaTime;
			}
			if (infectInterval > InfectIntervalTime)
			{
				infectInterval = 0f;
				InfectPlayers();
			}
			else
			{
				infectInterval += Time.deltaTime;
			}
			ProgressPlayerInfections();
			destination = base.transform.position;
		}
	}

	public void UpdateInfectionMeshes()
	{
		for (int i = 0; i < playerInfections.Length; i++)
		{
			if (!playerInfections[i].infected)
			{
				if (!StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && playerInfections[i].backFlowers != null)
				{
					UnityEngine.Object.Destroy(playerInfections[i].backFlowers);
				}
				continue;
			}
			if (playerInfections[i].backFlowers != null)
			{
				playerInfections[i].backFlowers.transform.position = StartOfRound.Instance.allPlayerScripts[i].bodyParts[5].position;
				playerInfections[i].backFlowers.transform.rotation = StartOfRound.Instance.allPlayerScripts[i].bodyParts[5].rotation;
			}
			if (playerInfections[i].faceSporesOutput > 0f && playerInfections[i].faceSpores != null)
			{
				playerInfections[i].faceSpores.transform.position = StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position;
				playerInfections[i].faceSpores.transform.rotation = StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.rotation;
				playerInfections[i].faceSporesOutput = Mathf.Max(0f, playerInfections[i].faceSporesOutput - Time.deltaTime);
				playerInfections[i].faceSpores.SetFloat("TimeSinceBurst", Mathf.Abs(1f - playerInfections[i].faceSporesOutput));
				if (playerInfections[i].faceSporesOutput <= 0f)
				{
					playerInfections[i].faceSpores.Stop();
				}
			}
		}
	}

	private void UpdateSporeAmbience()
	{
		if (!GameNetworkManager.Instance.localPlayerController.isInsideFactory)
		{
			sporeAmbienceSource.volume = 0f;
		}
		if (updateSporeAudioInterval > 1f)
		{
			updateSporeAudioInterval = 0f;
			if (StartOfRound.Instance.occlusionCuller.currentTile == null)
			{
				return;
			}
			if (StartOfRound.Instance.audioListener.transform.position.y > -90f)
			{
				distToPlants = 100f;
				return;
			}
			Tile currentTile = StartOfRound.Instance.occlusionCuller.currentTile;
			int num = -1;
			for (int i = 0; i < GrowthTiles.Count; i++)
			{
				if (GrowthTiles[i].tile == currentTile && !GrowthTiles[i].eradicated && GrowthTiles[i].plantsInTile > 0)
				{
					num = i;
					break;
				}
			}
			if (num == -1)
			{
				distToPlants = 100f;
				return;
			}
			float num2 = 1000f;
			int num3 = -1;
			for (int j = 0; j < GrowthTiles[num].plantPositions.Count; j++)
			{
				float sqrMagnitude = (GameNetworkManager.Instance.localPlayerController.transform.position - GrowthTiles[num].plantPositions[j]).sqrMagnitude;
				if (sqrMagnitude < num2)
				{
					num2 = sqrMagnitude;
					num3 = j;
				}
			}
			if (num3 != -1)
			{
				distToPlants = Vector3.Distance(GrowthTiles[num].plantPositions[num3], GameNetworkManager.Instance.localPlayerController.transform.position);
			}
		}
		else
		{
			updateSporeAudioInterval += Time.deltaTime;
		}
		if (distToPlants < 8f)
		{
			if (!sporeAmbienceSource.isPlaying)
			{
				sporeAmbienceSource.Play();
			}
			sporeAmbienceSource.volume = Mathf.Lerp(sporeAmbienceSource.volume, Mathf.Lerp(1f, 0f, distToPlants / 7f), Time.deltaTime * 0.25f);
		}
		else
		{
			sporeAmbienceSource.volume = Mathf.MoveTowards(sporeAmbienceSource.volume, 0f, Time.deltaTime * 0.5f);
			if (sporeAmbienceSource.isPlaying && sporeAmbienceSource.volume <= 0f)
			{
				sporeAmbienceSource.Stop();
			}
		}
	}

	public void LateUpdate()
	{
		UpdateSporeAmbience();
		UpdateInfectionMeshes();
		if (!growingRecentPlant)
		{
			return;
		}
		if (growingPlant.positionIndex >= plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum].Count)
		{
			growingRecentPlant = false;
			return;
		}
		float shrinkSpeed = Mathf.Lerp(12f, 4f, growthIntervalCurveOverDay.Evaluate(TimeOfDay.Instance.normalizedTimeOfDay) * GrowthInterval / 8f);
		plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum][growingPlant.positionIndex] = SprayPaintItem.ResizeMatrix(plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum][growingPlant.positionIndex], shrinkSpeed, shrink: false);
		if (SprayPaintItem.ExtractScaleFromMatrix(plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum][growingPlant.positionIndex]).x > recentPlantSizeTarget)
		{
			growingRecentPlant = false;
		}
	}

	private void QuickFinishGrowingPlant()
	{
		if (growingRecentPlant && growingPlant.plantType != -1)
		{
			plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum][growingPlant.positionIndex] = SprayPaintItem.SetMatrixScale(plantBatchers[growingPlant.plantType].Batches[growingPlant.batchNum][growingPlant.positionIndex], recentPlantSizeTarget);
			growingRecentPlant = false;
		}
	}
}
