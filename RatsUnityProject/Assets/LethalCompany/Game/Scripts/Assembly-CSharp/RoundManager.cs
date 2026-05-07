using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DunGen;
using DunGen.Tags;
using GameNetcodeStuff;
using TMPro;
using Unity.AI.Navigation;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public class RoundManager : NetworkBehaviour
{
	public StartOfRound playersManager;

	public Transform itemPooledObjectsContainer;

	[Header("Global Game Variables / Balancing")]
	public float scrapValueMultiplier = 1f;

	public float scrapAmountMultiplier = 1f;

	public float mapSizeMultiplier = 1f;

	[Space(3f)]
	public int increasedInsideEnemySpawnRateIndex = -1;

	public int increasedOutsideEnemySpawnRateIndex = -1;

	public int increasedMapPropSpawnRateIndex = -1;

	public int increasedScrapSpawnRateIndex = -1;

	public int increasedMapHazardSpawnRateIndex = -1;

	[Space(5f)]
	[Space(5f)]
	public float currentMaxInsidePower;

	public float currentMaxOutsidePower;

	public float currentEnemyPower;

	public float currentEnemyPowerNoDeaths;

	public float currentOutsideEnemyPower;

	public float currentOutsideEnemyPowerNoDeaths;

	public float currentWeedEnemyPower;

	public float currentDaytimeEnemyPower;

	public float currentDaytimeEnemyPowerNoDeaths;

	public int currentMaxInsideDiversityLevel;

	public int currentMaxOutsideDiversityLevel;

	public int currentInsideEnemyDiversityLevel;

	public int currentOutsideEnemyDiversityLevel;

	public TimeOfDay timeScript;

	private int currentHour;

	public float currentHourTime;

	[Header("Gameplay events")]
	public List<int> enemySpawnTimes = new List<int>();

	public int currentEnemySpawnIndex;

	public bool isSpawningEnemies;

	public bool begunSpawningEnemies;

	[Header("Elevator Properties")]
	public bool ElevatorCharging;

	public float elevatorCharge;

	public bool ElevatorPowered;

	public bool elevatorUp;

	public bool ElevatorLowering;

	public bool ElevatorRunning;

	public bool ReturnToSurface;

	[Header("Elevator Variables")]
	public Animator ElevatorAnimator;

	public Animator ElevatorLightAnimator;

	public AudioSource elevatorMotorAudio;

	public AudioClip startMotor;

	public Animator PanelButtons;

	public Animator PanelLights;

	public AudioSource elevatorButtonsAudio;

	public AudioClip PressButtonSFX1;

	public AudioClip PressButtonSFX2;

	public TextMeshProUGUI PanelScreenText;

	public Canvas PanelScreen;

	public NetworkObject lungPlacePosition;

	public InteractTrigger elevatorSocketTrigger;

	private Coroutine loadLevelCoroutine;

	private Coroutine flickerLightsCoroutine;

	private Coroutine powerLightsCoroutine;

	[Header("Enemies")]
	public List<SpawnableEnemyWithRarity> WeedEnemies = new List<SpawnableEnemyWithRarity>();

	public EnemyVent[] allEnemyVents;

	public List<Anomaly> SpawnedAnomalies = new List<Anomaly>();

	public List<EnemyAI> SpawnedEnemies = new List<EnemyAI>();

	private List<int> SpawnProbabilities = new List<int>();

	public int hourTimeBetweenEnemySpawnBatches = 2;

	public int numberOfEnemiesInScene;

	public int minEnemiesToSpawn;

	public int minOutsideEnemiesToSpawn;

	[Header("Hazards")]
	public SpawnableMapObject[] spawnableMapObjects;

	public GameObject mapPropsContainer;

	public Transform VehiclesContainer;

	public Transform[] shipSpawnPathPoints;

	public GameObject[] spawnDenialPoints;

	public string[] possibleCodesForBigDoors;

	public GameObject quicksandPrefab;

	public GameObject keyPrefab;

	[Space(3f)]
	public GameObject breakTreePrefab;

	public GameObject breakSnowmanPrefab;

	public AudioClip breakTreeAudio1;

	public AudioClip breakTreeAudio2;

	public AudioClip breakSnowmanAudio1;

	[Space(5f)]
	public GameObject[] outsideAINodesUnordered;

	public GameObject[] outsideAINodes;

	public GameObject[] outsideAIWaterNodesUnordered;

	public GameObject[] outsideAIWaterNodes;

	public GameObject[] outsideAIDryNodesUnordered;

	public GameObject[] outsideAIDryNodes;

	public GameObject[] allCaveNodes;

	public Bounds[] underwaterBounds;

	public GameObject[] insideAINodes;

	[Header("Dungeon generation")]
	public IndoorMapType[] dungeonFlowTypes;

	public RuntimeDungeon dungeonGenerator;

	public bool dungeonCompletedGenerating;

	public bool dungeonIsGenerating;

	public bool bakedNavMesh;

	public bool dungeonFinishedGeneratingForAllPlayers;

	public AudioClip[] firstTimeDungeonAudios;

	public int currentDungeonType = -1;

	[Space(3f)]
	public GameObject caveEntranceProp;

	public Tag CaveDoorwayTag;

	public Tag MineshaftTunnelTag;

	public MineshaftElevatorController currentMineshaftElevator;

	[Header("Scrap-collection")]
	public Transform spawnedScrapContainer;

	public int scrapCollectedInLevel;

	public float totalScrapValueInLevel;

	public int valueOfFoundScrapItems;

	public List<GrabbableObject> scrapDroppedInShip = new List<GrabbableObject>();

	public SelectableLevel currentLevel;

	public System.Random LevelRandom;

	public System.Random AnomalyRandom;

	public System.Random EnemySpawnRandom;

	public System.Random IndoorEnemySpawnPlacementRandom;

	public System.Random DaytimeEnemySpawnRandom;

	public System.Random DaytimeEnemySpawnPlacementRandom;

	public System.Random OutsideEnemySpawnRandom;

	public System.Random OutsideEnemySpawnPlacementRandom;

	public System.Random WeedEnemySpawnRandom;

	public System.Random WeedEnemySpawnPlacementRandom;

	public System.Random BreakerBoxRandom;

	public System.Random ScrapValuesRandom;

	public System.Random ChallengeMoonRandom;

	public bool powerOffPermanently;

	public bool hasInitializedLevelRandomSeed;

	public List<ulong> playersFinishedGeneratingFloor = new List<ulong>(4);

	public PowerSwitchEvent onPowerSwitch = new PowerSwitchEvent();

	public List<Animator> allPoweredLightsAnimators = new List<Animator>();

	public List<Light> allPoweredLights = new List<Light>();

	public List<GameObject> spawnedSyncedObjects = new List<GameObject>();

	public float stabilityMeter;

	private Coroutine elevatorRunningCoroutine;

	public int collisionsMask = 2305;

	public bool cannotSpawnMoreInsideEnemies;

	public Collider[] tempColliderResults = new Collider[20];

	public Transform tempTransform;

	public bool GotNavMeshPositionResult;

	public NavMeshHit navHit;

	private bool firstTimeSpawningEnemies;

	private bool firstTimeSpawningOutsideEnemies;

	private bool firstTimeSpawningWeedEnemies;

	private bool firstTimeSpawningDaytimeEnemies;

	private int enemyRushIndex;

	public LocalVolumetricFog indoorFog;

	public List<EnemyAINestSpawnObject> enemyNestSpawnObjects = new List<EnemyAINestSpawnObject>();

	public AudioClip[] snowmanLaughSFX;

	public Collider startRoomSpecialBounds;

	public Vector3[] storedPathCorners;

	public PlayerControllerB[] tempPlayersArray;

	private List<NavMeshSurface> fullBakeSurfaces = new List<NavMeshSurface>();

	private int dungeonLayermask = 35072;

	public Vector3 randomPositionInBoxRadius;

	public static RoundManager Instance { get; private set; }

	public event Action OnFinishedGeneratingDungeon;

	public void GetOutsideAINodes(bool getUnderwaterNodes = true)
	{
		if (outsideAINodes == null || outsideAINodes.Length == 0 || outsideAINodes[0] == null)
		{
			outsideAINodes = (from x in GameObject.FindGameObjectsWithTag("OutsideAINode")
				orderby Vector3.Distance(x.transform.position, StartOfRound.Instance.shipLandingPosition.position)
				select x).ToArray();
			outsideAINodesUnordered = outsideAINodes.ToArray();
			Debug.Log($"Roundmanager: Refreshed OutsideAINodes list!: {outsideAINodes.Length}");
			Debug.Log($"Roundmanager: Refreshed OutsideAINodes list!: {outsideAINodesUnordered.Length}");
		}
		if (getUnderwaterNodes && (outsideAIWaterNodes == null || outsideAIWaterNodes.Length == 0 || outsideAIWaterNodes[0] == null))
		{
			NavMeshModifierVolume[] array = (from x in UnityEngine.Object.FindObjectsByType<NavMeshModifierVolume>(FindObjectsSortMode.None)
				where x.area == 12
				select x).ToArray();
			if (array.Length == 0)
			{
				outsideAIDryNodes = outsideAINodes;
				outsideAIDryNodesUnordered = outsideAIDryNodes.ToArray();
				return;
			}
			List<Bounds> list = new List<Bounds>();
			List<GameObject> list2 = new List<GameObject>(150);
			List<GameObject> list3 = outsideAINodes.ToList();
			for (int num = 0; num < array.Length; num++)
			{
				Bounds item = new Bounds(array[num].transform.position + array[num].center, Vector3.Scale(array[num].size, array[num].transform.localScale));
				list.Add(item);
			}
			for (int num2 = 0; num2 < outsideAINodes.Length; num2++)
			{
				for (int num3 = 0; num3 < list.Count; num3++)
				{
					if (list[num3].Contains(outsideAINodes[num2].transform.position))
					{
						list2.Add(outsideAINodes[num2]);
						list3.Remove(outsideAINodes[num2]);
						break;
					}
				}
			}
			underwaterBounds = list.ToArray();
			outsideAIWaterNodes = list2.OrderBy((GameObject x) => Vector3.Distance(x.transform.position, StartOfRound.Instance.shipLandingPosition.position)).ToArray();
			outsideAIWaterNodesUnordered = outsideAIWaterNodes.ToArray();
			if (list2.Count == 0)
			{
				outsideAIDryNodes = outsideAINodes;
			}
			else
			{
				outsideAIDryNodes = list3.OrderBy((GameObject x) => Vector3.Distance(x.transform.position, StartOfRound.Instance.shipLandingPosition.position)).ToArray();
			}
			outsideAIDryNodesUnordered = outsideAIDryNodes.ToArray();
		}
		else if (outsideAIDryNodes == null || outsideAIDryNodes.Length == 0 || outsideAIDryNodes[0] == null)
		{
			outsideAIDryNodes = outsideAINodes;
			outsideAIDryNodesUnordered = outsideAIDryNodes.ToArray();
		}
	}

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			UnityEngine.Object.Destroy(Instance.gameObject);
		}
	}

	public void SpawnScrapInLevel()
	{
		int num = (int)((float)AnomalyRandom.Next(currentLevel.minScrap, currentLevel.maxScrap) * scrapAmountMultiplier);
		if (currentDungeonType == 4)
		{
			num += 6;
		}
		if (StartOfRound.Instance.isChallengeFile)
		{
			int num2 = AnomalyRandom.Next(10, 30);
			num += num2;
			Debug.Log($"Anomaly random 0b: {num2}");
		}
		int num3 = -1;
		if (AnomalyRandom.Next(0, 500) <= 20)
		{
			num3 = AnomalyRandom.Next(0, currentLevel.spawnableScrap.Count);
			bool flag = false;
			for (int i = 0; i < 2; i++)
			{
				if (currentLevel.spawnableScrap[num3].rarity < 5 || currentLevel.spawnableScrap[num3].spawnableItem.twoHanded)
				{
					num3 = AnomalyRandom.Next(0, currentLevel.spawnableScrap.Count);
					continue;
				}
				flag = true;
				break;
			}
			if (!flag && AnomalyRandom.Next(0, 100) < 60)
			{
				num3 = -1;
			}
		}
		List<Item> ScrapToSpawn = new List<Item>();
		List<int> list = new List<int>();
		int num4 = 0;
		List<int> list2 = new List<int>(currentLevel.spawnableScrap.Count);
		for (int j = 0; j < currentLevel.spawnableScrap.Count; j++)
		{
			if (j == increasedScrapSpawnRateIndex)
			{
				list2.Add(100);
			}
			else
			{
				list2.Add(currentLevel.spawnableScrap[j].rarity);
			}
		}
		int[] weights = list2.ToArray();
		for (int k = 0; k < num; k++)
		{
			if (num3 != -1)
			{
				ScrapToSpawn.Add(currentLevel.spawnableScrap[num3].spawnableItem);
			}
			else
			{
				ScrapToSpawn.Add(currentLevel.spawnableScrap[GetRandomWeightedIndex(weights)].spawnableItem);
			}
		}
		Debug.Log($"Number of scrap to spawn: {ScrapToSpawn.Count}. minTotalScrapValue: {currentLevel.minTotalScrapValue}. Total value of items: {num4}.");
		RandomScrapSpawn randomScrapSpawn = null;
		RandomScrapSpawn[] source = UnityEngine.Object.FindObjectsOfType<RandomScrapSpawn>();
		List<NetworkObjectReference> list3 = new List<NetworkObjectReference>();
		List<RandomScrapSpawn> usedSpawns = new List<RandomScrapSpawn>();
		int l;
		for (l = 0; l < ScrapToSpawn.Count; l++)
		{
			if (ScrapToSpawn[l] == null)
			{
				Debug.Log("Error!!!!! Found null element in list ScrapToSpawn. Skipping it.");
				continue;
			}
			List<RandomScrapSpawn> list4 = ((ScrapToSpawn[l].spawnPositionTypes != null && ScrapToSpawn[l].spawnPositionTypes.Count != 0 && num3 == -1) ? source.Where((RandomScrapSpawn x) => ScrapToSpawn[l].spawnPositionTypes.Contains(x.spawnableItems) && !x.spawnUsed).ToList() : source.ToList());
			if (list4.Count <= 0)
			{
				Debug.Log("No tiles containing a scrap spawn with item type: " + ScrapToSpawn[l].itemName);
				continue;
			}
			if (usedSpawns.Count > 0 && list4.Contains(randomScrapSpawn))
			{
				list4.RemoveAll((RandomScrapSpawn x) => usedSpawns.Contains(x));
				if (list4.Count <= 0)
				{
					usedSpawns.Clear();
					l--;
					continue;
				}
			}
			randomScrapSpawn = list4[AnomalyRandom.Next(0, list4.Count)];
			usedSpawns.Add(randomScrapSpawn);
			Vector3 position;
			if (randomScrapSpawn.spawnedItemsCopyPosition)
			{
				randomScrapSpawn.spawnUsed = true;
				position = ((!randomScrapSpawn.spawnWithParent) ? randomScrapSpawn.transform.position : randomScrapSpawn.spawnWithParent.transform.position);
			}
			else
			{
				position = GetRandomNavMeshPositionInBoxPredictable(randomScrapSpawn.transform.position, randomScrapSpawn.itemSpawnRange, navHit, AnomalyRandom, -8193) + Vector3.up * ScrapToSpawn[l].verticalOffset;
			}
			GameObject obj = UnityEngine.Object.Instantiate(ScrapToSpawn[l].spawnPrefab, position, Quaternion.identity, null);
			GrabbableObject component = obj.GetComponent<GrabbableObject>();
			component.transform.rotation = Quaternion.Euler(component.itemProperties.restingRotation);
			component.fallTime = 0f;
			if (num3 != -1)
			{
				list.Add(Mathf.Clamp((int)((float)AnomalyRandom.Next(ScrapToSpawn[l].minValue, ScrapToSpawn[l].maxValue) * scrapValueMultiplier), 50, 170));
			}
			else
			{
				list.Add((int)((float)AnomalyRandom.Next(ScrapToSpawn[l].minValue, ScrapToSpawn[l].maxValue) * scrapValueMultiplier));
			}
			num4 += list[list.Count - 1];
			component.scrapValue = list[list.Count - 1];
			NetworkObject component2 = obj.GetComponent<NetworkObject>();
			component2.Spawn();
			list3.Add(component2);
		}
		if (num3 != -1)
		{
			float num5 = 600f;
			if (currentLevel.spawnableScrap[num3].spawnableItem.twoHanded)
			{
				num5 = 1500f;
			}
			if (num4 > 4500)
			{
				num4 = 0;
				for (int num6 = 0; num6 < list.Count; num6++)
				{
					list[num6] = (int)((float)list[num6] * 0.7f);
					num4 += list[num6];
				}
			}
			else if ((float)num4 < num5)
			{
				num4 = 0;
				for (int num7 = 0; num7 < list.Count; num7++)
				{
					list[num7] = (int)((float)list[num7] * 1.4f);
					num4 += list[num7];
				}
			}
		}
		StartCoroutine(waitForScrapToSpawnToSync(list3.ToArray(), list.ToArray()));
	}

	private IEnumerator waitForScrapToSpawnToSync(NetworkObjectReference[] spawnedScrap, int[] scrapValues)
	{
		yield return new WaitForSeconds(11f);
		SyncScrapValuesClientRpc(spawnedScrap, scrapValues);
	}

		[ClientRpc]
		public void SyncScrapValuesClientRpc(NetworkObjectReference[] spawnedScrap, int[] allScrapValue)
		{
			Debug.Log($"clientRPC scrap values length: {allScrapValue.Length}");
			ScrapValuesRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 210);
	int num = 0;
			for (int i = 0; i < spawnedScrap.Length; i++)
			{
				if (spawnedScrap[i].TryGet(out var networkObject))
				{
			GrabbableObject component = networkObject.GetComponent<GrabbableObject>();
					if (component != null)
					{
						if (i >= allScrapValue.Length)
						{
							Debug.LogError($"spawnedScrap amount exceeded allScrapValue!: {spawnedScrap.Length}");
							break;
						}

						component.SetScrapValue(allScrapValue[i]);
						num += allScrapValue[i];
						try
						{
							if (component.itemProperties.meshVariants.Length != 0)
							{
								component.gameObject.GetComponent<MeshFilter>().mesh = component.itemProperties.meshVariants[ScrapValuesRandom.Next(0, component.itemProperties.meshVariants.Length)];
							}

							if (component.itemProperties.materialVariants.Length != 0)
							{
								component.gameObject.GetComponent<MeshRenderer>().sharedMaterial = component.itemProperties.materialVariants[ScrapValuesRandom.Next(0, component.itemProperties.materialVariants.Length)];
							}
						}
						catch (Exception arg)
						{
							Debug.Log($"Item name: {component.gameObject.name}; {arg}");
						}
					}
					else
					{
						Debug.LogError("Scrap networkobject object did not contain grabbable object!: " + networkObject.gameObject.name);
					}
				}
				else
				{
					Debug.LogError($"Failed to get networkobject reference for scrap. id: {spawnedScrap[i].NetworkObjectId}");
				}
			}

			totalScrapValueInLevel = num;
			scrapCollectedInLevel = 0;
			valueOfFoundScrapItems = 0;
		}

	public void SpawnSyncedProps()
	{
		try
		{
			spawnedSyncedObjects.Clear();
			SpawnSyncedObject[] array = UnityEngine.Object.FindObjectsOfType<SpawnSyncedObject>();
			if (array == null)
			{
				return;
			}
			mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer");
			GameObject gameObject = GameObject.FindGameObjectWithTag("SpecialStartRoomBounds");
			if (gameObject != null)
			{
				startRoomSpecialBounds = gameObject.GetComponent<Collider>();
			}
			Debug.Log($"Spawning synced props on server. Length: {array.Length}");
			for (int i = 0; i < array.Length; i++)
			{
				GameObject gameObject2 = UnityEngine.Object.Instantiate(array[i].spawnPrefab, array[i].transform.position, array[i].transform.rotation, mapPropsContainer.transform);
				if (gameObject2 != null)
				{
					gameObject2.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
					spawnedSyncedObjects.Add(gameObject2);
				}
			}
		}
		catch (Exception arg)
		{
			Debug.Log($"Exception! Unable to sync spawned objects on host; {arg}");
		}
	}

	public void SpawnMapObjects()
	{
		if (currentLevel.indoorMapHazards.Length == 0)
		{
			return;
		}
		System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 587);
		mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer");
		GameObject gameObject = GameObject.FindGameObjectWithTag("SpecialStartRoomBounds");
		if (gameObject != null)
		{
			startRoomSpecialBounds = gameObject.GetComponent<Collider>();
		}
		RandomMapObject[] array = UnityEngine.Object.FindObjectsOfType<RandomMapObject>();
		EntranceTeleport[] array2 = UnityEngine.Object.FindObjectsByType<EntranceTeleport>(FindObjectsSortMode.None);
		List<Vector3> list = new List<Vector3>();
		List<RandomMapObject> list2 = new List<RandomMapObject>();
		for (int i = 0; i < currentLevel.indoorMapHazards.Length; i++)
		{
			if (currentDungeonType == 4 && !currentLevel.indoorMapHazards[i].hazardType.allowInMineshaft)
			{
				continue;
			}
			list2.Clear();
			int num = (int)currentLevel.indoorMapHazards[i].numberToSpawn.Evaluate((float)random.NextDouble());
			if (increasedMapHazardSpawnRateIndex == i)
			{
				num = Mathf.Min(num * 2, 150);
			}
			if (num <= 0)
			{
				continue;
			}
			for (int j = 0; j < array.Length; j++)
			{
				if (array[j].spawnablePrefabs.Contains(currentLevel.indoorMapHazards[i].hazardType.prefabToSpawn))
				{
					list2.Add(array[j]);
				}
			}
			if (list2.Count == 0)
			{
				Debug.Log("NO SPAWNERS WERE COMPATIBLE WITH THE SPAWNABLE MAP OBJECT: '" + currentLevel.indoorMapHazards[i].hazardType.prefabToSpawn.gameObject.name + "'");
				continue;
			}
			list.Clear();
			for (int k = 0; k < num; k++)
			{
				RandomMapObject randomMapObject = list2[random.Next(0, list2.Count)];
				Vector3 position = randomMapObject.transform.position;
				position = GetRandomNavMeshPositionInBoxPredictable(position, randomMapObject.spawnRange, default(NavMeshHit), random, -8193);
				if (currentLevel.indoorMapHazards[i].hazardType.disallowSpawningNearEntrances)
				{
					for (int l = 0; l < array2.Length; l++)
					{
						if (!array2[l].isEntranceToBuilding)
						{
							Vector3.Distance(array2[l].entrancePoint.transform.position, position);
							_ = 5.5f;
						}
					}
				}
				if (currentLevel.indoorMapHazards[i].hazardType.requireDistanceBetweenSpawns)
				{
					bool flag = false;
					for (int m = 0; m < list.Count; m++)
					{
						if (Vector3.Distance(position, list[m]) < 5f)
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
				GameObject gameObject2 = UnityEngine.Object.Instantiate(currentLevel.indoorMapHazards[i].hazardType.prefabToSpawn, position, Quaternion.identity, mapPropsContainer.transform);
				if (currentLevel.indoorMapHazards[i].hazardType.spawnFacingAwayFromWall)
				{
					gameObject2.transform.eulerAngles = new Vector3(0f, YRotationThatFacesTheFarthestFromPosition(position + Vector3.up * 0.2f), 0f);
				}
				else if (currentLevel.indoorMapHazards[i].hazardType.spawnFacingWall)
				{
					gameObject2.transform.eulerAngles = new Vector3(0f, YRotationThatFacesTheNearestFromPosition(position + Vector3.up * 0.2f), 0f);
				}
				else
				{
					gameObject2.transform.eulerAngles = new Vector3(gameObject2.transform.eulerAngles.x, random.Next(0, 360), gameObject2.transform.eulerAngles.z);
				}
				if (currentLevel.indoorMapHazards[i].hazardType.spawnWithBackToWall && Physics.Raycast(gameObject2.transform.position, -gameObject2.transform.forward, out var hitInfo, 100f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					gameObject2.transform.position = hitInfo.point;
					if (currentLevel.indoorMapHazards[i].hazardType.spawnWithBackFlushAgainstWall)
					{
						gameObject2.transform.forward = hitInfo.normal;
						gameObject2.transform.eulerAngles = new Vector3(0f, gameObject2.transform.eulerAngles.y, 0f);
					}
				}
				gameObject2.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
			}
		}
		for (int n = 0; n < array.Length; n++)
		{
			UnityEngine.Object.Destroy(array[n].gameObject);
		}
	}

	public float YRotationThatFacesTheFarthestFromPosition(Vector3 pos, float maxDistance = 25f, int resolution = 6)
	{
		int num = 0;
		float num2 = 0f;
		for (int i = 0; i < 360; i += 360 / resolution)
		{
			tempTransform.eulerAngles = new Vector3(0f, i, 0f);
			if (Physics.Raycast(pos, tempTransform.forward, out var hitInfo, maxDistance, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
			{
				if (hitInfo.distance > num2)
				{
					num2 = hitInfo.distance;
					num = i;
				}
				continue;
			}
			num = i;
			break;
		}
		if (!hasInitializedLevelRandomSeed)
		{
			return UnityEngine.Random.Range(num - 15, num + 15);
		}
		int num3 = AnomalyRandom.Next(num - 15, num + 15);
		Debug.Log($"Anomaly random yrotation farthest: {num3}");
		return num3;
	}

	public float YRotationThatFacesTheNearestFromPosition(Vector3 pos, float maxDistance = 25f, int resolution = 6)
	{
		int num = 0;
		float num2 = 100f;
		bool flag = false;
		for (int i = 0; i < 360; i += 360 / resolution)
		{
			tempTransform.eulerAngles = new Vector3(0f, i, 0f);
			if (Physics.Raycast(pos, tempTransform.forward, out var hitInfo, maxDistance, StartOfRound.Instance.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
			{
				flag = true;
				if (hitInfo.distance < num2)
				{
					num2 = hitInfo.distance;
					num = i;
				}
			}
		}
		if (!flag)
		{
			return -777f;
		}
		if (!hasInitializedLevelRandomSeed)
		{
			return UnityEngine.Random.Range(num - 15, num + 15);
		}
		int num3 = AnomalyRandom.Next(num - 15, num + 15);
		Debug.Log($"Anomaly random yrotation nearest: {num3}");
		return num3;
	}

	public void GenerateNewFloor()
	{
		int num = -1;
		if (currentLevel.dungeonFlowTypes != null && currentLevel.dungeonFlowTypes.Length != 0)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < currentLevel.dungeonFlowTypes.Length; i++)
			{
				list.Add(currentLevel.dungeonFlowTypes[i].rarity);
			}
			int randomWeightedIndex = GetRandomWeightedIndex(list.ToArray(), LevelRandom);
			num = currentLevel.dungeonFlowTypes[randomWeightedIndex].id;
			dungeonGenerator.Generator.DungeonFlow = dungeonFlowTypes[num].dungeonFlow;
			currentDungeonType = num;
			if (currentLevel.dungeonFlowTypes[randomWeightedIndex].overrideLevelAmbience != null)
			{
				SoundManager.Instance.currentLevelAmbience = currentLevel.dungeonFlowTypes[randomWeightedIndex].overrideLevelAmbience;
			}
			else if (currentLevel.levelAmbienceClips != null)
			{
				SoundManager.Instance.currentLevelAmbience = currentLevel.levelAmbienceClips;
			}
		}
		else
		{
			if (currentLevel.levelAmbienceClips != null)
			{
				SoundManager.Instance.currentLevelAmbience = currentLevel.levelAmbienceClips;
			}
			currentDungeonType = 0;
		}
		dungeonGenerator.Generator.ShouldRandomizeSeed = false;
		dungeonGenerator.Generator.Seed = LevelRandom.Next();
		float num2;
		if (num != -1)
		{
			if (dungeonFlowTypes[num].restrictBounds == Vector3.zero)
			{
				dungeonGenerator.Generator.RestrictDungeonToBounds = false;
			}
			else
			{
				dungeonGenerator.Generator.RestrictDungeonToBounds = true;
				dungeonGenerator.Generator.TilePlacementBounds = new Bounds(Vector3.zero, dungeonFlowTypes[num].restrictBounds * 2f * currentLevel.factorySizeMultiplier);
			}
			if (StartOfRound.Instance.occlusionCuller != null)
			{
				StartOfRound.Instance.occlusionCuller.AdjacentTileDepth = dungeonFlowTypes[num].cullingTileDepth;
			}
			num2 = currentLevel.factorySizeMultiplier / dungeonFlowTypes[num].MapTileSize * mapSizeMultiplier;
			num2 = (float)((double)Mathf.Round(num2 * 100f) / 100.0);
		}
		else
		{
			dungeonGenerator.Generator.RestrictDungeonToBounds = false;
			num2 = currentLevel.factorySizeMultiplier * mapSizeMultiplier;
		}
		dungeonGenerator.Generator.LengthMultiplier = num2;
		dungeonIsGenerating = true;
		dungeonGenerator.Generate();
	}

	public void GeneratedFloorPostProcessing()
	{
		if (base.IsServer)
		{
			SpawnScrapInLevel();
			SpawnMapObjects();
		}
	}

	private void SpawnCaveDoorLights()
	{
		if (currentDungeonType != 4)
		{
			return;
		}
		Tile[] array = UnityEngine.Object.FindObjectsByType<Tile>(FindObjectsSortMode.None);
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].Tags.HasTag(MineshaftTunnelTag))
			{
				continue;
			}
			for (int j = 0; j < array[i].UsedDoorways.Count; j++)
			{
				if (!array[i].UsedDoorways[j].ConnectedDoorway.Tags.HasTag(CaveDoorwayTag))
				{
					continue;
				}
				UnityEngine.Object.Instantiate(caveEntranceProp, array[i].UsedDoorways[j].transform, worldPositionStays: false);
				Transform[] componentsInChildren = array[i].gameObject.GetComponentsInChildren<Transform>(includeInactive: false);
				if (componentsInChildren.Length == 0)
				{
					continue;
				}
				Transform[] array2 = componentsInChildren;
				foreach (Transform transform in array2)
				{
					if (transform.tag == "PoweredLight")
					{
						UnityEngine.Object.Destroy(transform.gameObject);
					}
				}
			}
		}
	}

	public void TurnBreakerSwitchesOff()
	{
		BreakerBox breakerBox = UnityEngine.Object.FindObjectOfType<BreakerBox>();
		if (breakerBox != null)
		{
			Debug.Log("Switching breaker switches off");
			breakerBox.SetSwitchesOff();
			SwitchPower(on: false);
			onPowerSwitch.Invoke(arg0: false);
		}
	}

	public void LoadNewLevel(int randomSeed, SelectableLevel newLevel)
	{
		if (base.IsServer)
		{
			currentLevel = newLevel;
			dungeonFinishedGeneratingForAllPlayers = false;
			playersManager.fullyLoadedPlayers.Clear();
			if (dungeonGenerator != null)
			{
				dungeonGenerator.Generator.OnGenerationStatusChanged -= Generator_OnGenerationStatusChanged;
			}
			if (loadLevelCoroutine != null)
			{
				loadLevelCoroutine = null;
			}
			loadLevelCoroutine = StartCoroutine(LoadNewLevelWait(randomSeed));
		}
	}

	private void SetChallengeFileRandomModifiers()
	{
		if (!StartOfRound.Instance.isChallengeFile)
		{
			return;
		}
		int[] array = new int[5];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = AnomalyRandom.Next(0, 100);
		}
		if (array[0] < 45)
		{
			increasedInsideEnemySpawnRateIndex = AnomalyRandom.Next(0, currentLevel.Enemies.Count);
			if (currentLevel.Enemies[increasedInsideEnemySpawnRateIndex].enemyType.spawningDisabled)
			{
				increasedInsideEnemySpawnRateIndex = AnomalyRandom.Next(0, currentLevel.Enemies.Count);
			}
		}
		if (array[1] < 45)
		{
			increasedOutsideEnemySpawnRateIndex = AnomalyRandom.Next(0, currentLevel.OutsideEnemies.Count);
		}
		if (array[2] < 45)
		{
			increasedMapHazardSpawnRateIndex = AnomalyRandom.Next(0, currentLevel.indoorMapHazards.Length);
		}
		if (array[3] < 45)
		{
			increasedMapPropSpawnRateIndex = AnomalyRandom.Next(0, currentLevel.spawnableOutsideObjects.Length);
		}
		if (array[4] < 45)
		{
			increasedScrapSpawnRateIndex = AnomalyRandom.Next(0, currentLevel.spawnableScrap.Count);
		}
	}

	private IEnumerator LoadNewLevelWait(int randomSeed)
	{
		yield return null;
		yield return null;
		playersFinishedGeneratingFloor.Clear();
		Debug.Log($"Weed: moldspreaditerations: {currentLevel.moldSpreadIterations}; position: {currentLevel.moldStartPosition}");
		if (currentLevel.moldSpreadIterations > 0)
		{
			Debug.Log("Weed 1");
			MoldSpreadManager moldSpreadManager = UnityEngine.Object.FindObjectOfType<MoldSpreadManager>();
			GetOutsideAINodes(getUnderwaterNodes: false);
			int moldStartPosition;
			if (StartOfRound.Instance.currentLevel.moldStartPosition == -1)
			{
				Debug.Log("Weed 2");
				System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 2017);
				int num = random.Next(0, outsideAINodes.Length);
				bool flag = false;
				RaycastHit hitInfo;
				if (Vector3.Distance(outsideAINodes[num].transform.position, StartOfRound.Instance.shipLandingPosition.transform.position) < 40f)
				{
					flag = true;
				}
				else if (Physics.Raycast(outsideAINodes[num].transform.position + Vector3.up * 0.5f, Vector3.down, out hitInfo, 6f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
				{
					if (Terrain.activeTerrain == null)
					{
						if (!hitInfo.collider.gameObject.CompareTag("Grass") && !hitInfo.collider.gameObject.CompareTag("Snow") && !hitInfo.collider.gameObject.CompareTag("Gravel"))
						{
							flag = true;
						}
					}
					else if (hitInfo.collider.gameObject != Terrain.activeTerrain.gameObject)
					{
						flag = true;
					}
				}
				if (flag)
				{
					int num2 = random.Next(0, outsideAINodes.Length);
					int num3 = 0;
					int num4 = 0;
					while (num4 < outsideAINodes.Length)
					{
						num4++;
						num2 = (num2 + 1) % outsideAINodes.Length;
						if (Vector3.Distance(StartOfRound.Instance.shipLandingPosition.transform.position, outsideAINodes[num2].transform.position) < 40f)
						{
							continue;
						}
						if (Physics.Raycast(outsideAINodes[num2].transform.position, Vector3.down, out hitInfo, 6f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
						{
							if (Terrain.activeTerrain == null)
							{
								if (!hitInfo.collider.gameObject.CompareTag("Grass") && !hitInfo.collider.gameObject.CompareTag("Gravel"))
								{
									continue;
								}
							}
							else if (hitInfo.collider.gameObject != Terrain.activeTerrain.gameObject)
							{
								continue;
							}
						}
						Debug.Log($"Weed: Chose a node #{num2} which is far enough away. Distance: {Vector3.Distance(StartOfRound.Instance.shipLandingPosition.transform.position, outsideAINodes[num2].transform.position)}", outsideAINodes[num2]);
						num = num2;
						num3++;
						if (random.Next(0, 100) < 30 || num3 > 15)
						{
							break;
						}
					}
				}
				Debug.Log($"Mold: Ended up with node #{num}. Distance: {Vector3.Distance(StartOfRound.Instance.shipLandingPosition.transform.position, outsideAINodes[num].transform.position)}", outsideAINodes[num]);
				StartOfRound.Instance.currentLevel.moldStartPosition = num;
				moldStartPosition = num;
			}
			else
			{
				Debug.Log("Weed 3");
				moldStartPosition = StartOfRound.Instance.currentLevel.moldStartPosition;
			}
			Debug.Log("Weed 4");
			if (moldSpreadManager.planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Count > 0)
			{
				Debug.Log($"Weed 5; moldspreaditerations: {currentLevel.moldSpreadIterations}; position: {currentLevel.moldStartPosition}");
				GenerateNewLevelClientRpc(randomSeed, currentLevel.levelID, currentLevel.moldSpreadIterations, moldStartPosition, moldSpreadManager.planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.ToArray());
			}
			else
			{
				Debug.Log($"Weed 6; moldspreaditerations: {currentLevel.moldSpreadIterations}; position: {currentLevel.moldStartPosition}");
				GenerateNewLevelClientRpc(randomSeed, currentLevel.levelID, currentLevel.moldSpreadIterations, moldStartPosition);
			}
		}
		else
		{
			GenerateNewLevelClientRpc(randomSeed, currentLevel.levelID);
		}
		if (currentLevel.spawnEnemiesAndScrap)
		{
			yield return new WaitUntil(() => dungeonCompletedGenerating);
			yield return null;
			yield return new WaitUntil(() => playersFinishedGeneratingFloor.Count >= GameNetworkManager.Instance.connectedPlayers);
			Debug.Log("Players finished generating the new floor");
		}
		yield return new WaitForSeconds(0.3f);
		SpawnSyncedProps();
		if (currentLevel.spawnEnemiesAndScrap)
		{
			GeneratedFloorPostProcessing();
		}
		yield return null;
		playersFinishedGeneratingFloor.Clear();
		dungeonFinishedGeneratingForAllPlayers = true;
		RefreshEnemyVents();
		FinishGeneratingNewLevelClientRpc();
	}

		[ClientRpc]
		public void GenerateNewLevelClientRpc(int randomSeed, int levelID, int moldIterations = 0, int moldStartPosition = -1, int[] syncDestroyedMold = null)
		{
			GetOutsideAINodes(getUnderwaterNodes: false);
			currentLevel.moldSpreadIterations = moldIterations;
			currentLevel.moldStartPosition = moldStartPosition;
			if (moldIterations > 0)
			{
				if (moldStartPosition >= outsideAINodes.Length)
				{
					Debug.LogError($"Mold error: Mold start position index {moldStartPosition} is greater than outsideAINodes count: {outsideAINodes.Length}! Cannot sync mold");
				}

		Vector3 position = outsideAINodes[Mathf.Min(moldStartPosition, outsideAINodes.Length - 1)].transform.position;
				if (syncDestroyedMold != null)
				{
					UnityEngine.Object.FindObjectOfType<MoldSpreadManager>().SyncDestroyedMoldPositions(syncDestroyedMold);
				}

				UnityEngine.Object.FindObjectOfType<MoldSpreadManager>().GenerateMold(position, moldIterations);
			}

			playersManager.randomMapSeed = randomSeed;
			currentLevel = playersManager.levels[levelID];
			Debug.Log($"RANDOM MAP SEED - {playersManager.randomMapSeed}\nClient #{GameNetworkManager.Instance.localPlayerController.playerClientId}\nMoon: {currentLevel.PlanetName}");
			InitializeRandomNumberGenerators();
			SetChallengeFileRandomModifiers();
			HUDManager.Instance.loadingText.text = $"Random seed: {randomSeed}";
			HUDManager.Instance.LoadingScreen.SetBool("IsLoading", value: true);
			dungeonCompletedGenerating = false;
			mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer");
	GameObject gameObject = GameObject.FindGameObjectWithTag("SpecialStartRoomBounds");
			if (gameObject != null)
			{
				startRoomSpecialBounds = gameObject.GetComponent<Collider>();
			}

			if (!currentLevel.spawnEnemiesAndScrap)
			{
				return;
			}

			dungeonGenerator = UnityEngine.Object.FindObjectOfType<RuntimeDungeon>(includeInactive: false);
			if (dungeonGenerator != null)
			{
				dungeonGenerator.Generator.GenerateAsynchronously = true;
				dungeonGenerator.Generator.MaxAsyncFrameMilliseconds = 1f;
				dungeonGenerator.Generator.PauseBetweenRooms = 0f;
				GenerateNewFloor();
				if (dungeonGenerator.Generator.Status == GenerationStatus.Complete)
				{
					FinishGeneratingLevel();
					Debug.Log("Dungeon finished generating in one frame.");
				}
				else
				{
					dungeonGenerator.Generator.OnGenerationStatusChanged += Generator_OnGenerationStatusChanged;
					Debug.Log("Now listening to dungeon generator status.");
				}
			}
			else
			{
				Debug.LogError($"This client could not find dungeon generator! scene count: {SceneManager.sceneCount}");
			}
		}

	private void BakeDunGenNavMesh()
	{
		if (dungeonGenerator == null)
		{
			return;
		}
		Dungeon currentDungeon = dungeonGenerator.Generator.CurrentDungeon;
		foreach (NavMeshSurface fullBakeSurface in fullBakeSurfaces)
		{
			if (fullBakeSurface != null)
			{
				fullBakeSurface.RemoveData();
			}
		}
		fullBakeSurfaces.Clear();
		int settingsCount = NavMesh.GetSettingsCount();
		for (int i = 0; i < settingsCount; i++)
		{
			NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
			NavMeshSurface navMeshSurface = (from s in currentDungeon.gameObject.GetComponents<NavMeshSurface>()
				where s.agentTypeID == settings.agentTypeID
				select s).FirstOrDefault();
			if (navMeshSurface == null)
			{
				navMeshSurface = currentDungeon.gameObject.AddComponent<NavMeshSurface>();
				navMeshSurface.agentTypeID = settings.agentTypeID;
				navMeshSurface.collectObjects = CollectObjects.Children;
				navMeshSurface.layerMask = dungeonLayermask;
			}
			fullBakeSurfaces.Add(navMeshSurface);
			navMeshSurface.BuildNavMesh();
		}
		NavMeshSurface[] componentsInChildren = currentDungeon.gameObject.GetComponentsInChildren<NavMeshSurface>();
		foreach (NavMeshSurface navMeshSurface2 in componentsInChildren)
		{
			if (!fullBakeSurfaces.Contains(navMeshSurface2))
			{
				navMeshSurface2.enabled = false;
			}
		}
	}

	private void FinishGeneratingLevel()
	{
		insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
		allCaveNodes = GameObject.FindGameObjectsWithTag("CaveNode");
		dungeonCompletedGenerating = true;
		dungeonIsGenerating = false;
		BakeDunGenNavMesh();
		SpawnCaveDoorLights();
		SetToCurrentLevelWeather();
		SpawnOutsideHazards();
		Dungeon dungeon = UnityEngine.Object.FindObjectOfType<Dungeon>();
		if (dungeon != null)
		{
			AdjacentRoomCullingModified[] array = UnityEngine.Object.FindObjectsByType<AdjacentRoomCullingModified>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				array[i].SetDungeon(dungeon);
			}
		}
		else
		{
			Debug.LogError("Error, could not find a Dungeon component!");
		}
		this.OnFinishedGeneratingDungeon?.Invoke();
		FinishedGeneratingLevelServerRpc(NetworkManager.Singleton.LocalClientId);
	}

	private IEnumerator BakeDungenNavMeshOnDelay()
	{
		yield return new WaitForSeconds(0.1f);
		BakeDunGenNavMesh();
		yield return new WaitForSeconds(0.1f);
		FinishedGeneratingLevelServerRpc(NetworkManager.Singleton.LocalClientId);
	}

	private void Generator_OnGenerationStatusChanged(DungeonGenerator generator, GenerationStatus status)
	{
		if (status == GenerationStatus.Complete)
		{
			if (!dungeonCompletedGenerating)
			{
				FinishGeneratingLevel();
				Debug.Log("Dungeon has finished generating on this client after multiple frames");
			}
			dungeonGenerator.Generator.OnGenerationStatusChanged -= Generator_OnGenerationStatusChanged;
		}
	}

		[ServerRpc(RequireOwnership = false)]
		public void FinishedGeneratingLevelServerRpc(ulong clientId)
		{
			playersFinishedGeneratingFloor.Add(clientId);
		}

	public void DespawnPropsAtEndOfRound(bool despawnAllItems = false)
	{
		if (!base.IsServer)
		{
			return;
		}
		GrabbableObject[] array = UnityEngine.Object.FindObjectsOfType<GrabbableObject>();
		try
		{
			VehicleController[] array2 = UnityEngine.Object.FindObjectsByType<VehicleController>(FindObjectsSortMode.None);
			for (int i = 0; i < array2.Length; i++)
			{
				if (!array2[i].magnetedToShip)
				{
					if (array2[i].NetworkObject != null)
					{
						Debug.Log("Despawn vehicle");
						array2[i].NetworkObject.Despawn(destroy: false);
					}
				}
				else
				{
					array2[i].CollectItemsInTruck();
				}
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"Error despawning vehicle: {arg}");
		}
		BeltBagItem[] array3 = UnityEngine.Object.FindObjectsByType<BeltBagItem>(FindObjectsSortMode.None);
		for (int j = 0; j < array3.Length; j++)
		{
			if ((bool)array3[j].insideAnotherBeltBag && (array3[j].insideAnotherBeltBag.isInShipRoom || array3[j].insideAnotherBeltBag.isHeld))
			{
				array3[j].isInElevator = true;
				array3[j].isInShipRoom = true;
			}
			if (array3[j].isInShipRoom || array3[j].isHeld)
			{
				for (int k = 0; k < array3[j].objectsInBag.Count; k++)
				{
					array3[j].objectsInBag[k].isInElevator = true;
					array3[j].objectsInBag[k].isInShipRoom = true;
				}
			}
		}
		for (int l = 0; l < array.Length; l++)
		{
			if (array[l] == null)
			{
				continue;
			}
			if (despawnAllItems || (!array[l].isHeld && !array[l].isInShipRoom) || array[l].deactivated || (StartOfRound.Instance.allPlayersDead && array[l].itemProperties.isScrap))
			{
				if (array[l].isHeld && array[l].playerHeldBy != null)
				{
					array[l].playerHeldBy.DropAllHeldItemsAndSyncNonexact();
				}
				NetworkObject component = array[l].gameObject.GetComponent<NetworkObject>();
				if (component != null && component.IsSpawned)
				{
					Debug.Log("Despawning prop");
					array[l].gameObject.GetComponent<NetworkObject>().Despawn();
				}
				else
				{
					Debug.Log("Error/warning: prop '" + array[l].gameObject.name + "' was not spawned or did not have a NetworkObject component! Skipped despawning and destroyed it instead.");
					UnityEngine.Object.Destroy(array[l].gameObject);
				}
			}
			else
			{
				array[l].scrapPersistedThroughRounds = true;
			}
			if (spawnedSyncedObjects.Contains(array[l].gameObject))
			{
				spawnedSyncedObjects.Remove(array[l].gameObject);
			}
		}
		GameObject[] array4 = GameObject.FindGameObjectsWithTag("TemporaryEffect");
		for (int m = 0; m < array4.Length; m++)
		{
			UnityEngine.Object.Destroy(array4[m]);
		}
		allPoweredLightsAnimators = null;
	}

	public void UnloadSceneObjectsEarly()
	{
		if (!base.IsServer)
		{
			return;
		}
		Debug.Log("Despawning props and enemies #3");
		isSpawningEnemies = false;
		EnemyAI[] array = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
		Debug.Log($"Enemies on map: {array.Length}");
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].thisNetworkObject.IsSpawned)
			{
				array[i].thisNetworkObject.Despawn();
			}
			else
			{
				Debug.Log($"{array[i].thisNetworkObject} was not spawned on network, so it could not be removed.");
			}
		}
		SpawnedEnemies.Clear();
		EnemyAINestSpawnObject[] array2 = UnityEngine.Object.FindObjectsByType<EnemyAINestSpawnObject>(FindObjectsSortMode.None);
		for (int j = 0; j < array2.Length; j++)
		{
			NetworkObject component = array2[j].gameObject.GetComponent<NetworkObject>();
			if (component != null && component.IsSpawned)
			{
				Debug.Log("despawn nest spawn object");
				component.Despawn();
			}
			else
			{
				UnityEngine.Object.Destroy(array2[j].gameObject);
			}
		}
		currentEnemyPower = 0f;
		currentEnemyPowerNoDeaths = 0f;
		currentDaytimeEnemyPower = 0f;
		currentDaytimeEnemyPowerNoDeaths = 0f;
		currentOutsideEnemyPower = 0f;
		currentOutsideEnemyPowerNoDeaths = 0f;
		currentWeedEnemyPower = 0f;
		currentMaxOutsideDiversityLevel = 0;
		currentMaxInsideDiversityLevel = 0;
		currentInsideEnemyDiversityLevel = 0;
		currentOutsideEnemyDiversityLevel = 0;
	}

	public override void OnDestroy()
	{
		if (dungeonGenerator != null)
		{
			dungeonGenerator.Generator.OnGenerationStatusChanged -= Generator_OnGenerationStatusChanged;
		}
		base.OnDestroy();
	}

		[ServerRpc]
		public void FinishGeneratingNewLevelServerRpc()
		{
			FinishGeneratingNewLevelClientRpc();
		}

	private void SetToCurrentLevelWeather()
	{
		TimeOfDay.Instance.currentLevelWeather = currentLevel.currentWeather;
		if (TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.None || currentLevel.randomWeathers == null)
		{
			return;
		}
		for (int i = 0; i < currentLevel.randomWeathers.Length; i++)
		{
			if (currentLevel.randomWeathers[i].weatherType != currentLevel.currentWeather)
			{
				continue;
			}
			TimeOfDay.Instance.currentWeatherVariable = currentLevel.randomWeathers[i].weatherVariable;
			TimeOfDay.Instance.currentWeatherVariable2 = currentLevel.randomWeathers[i].weatherVariable2;
			if (currentLevel.randomWeathers[i].weatherVariableColor == Color.black)
			{
				TimeOfDay.Instance.currentWeatherVariableColor = TimeOfDay.Instance.defaultWeatherColor;
			}
			else
			{
				TimeOfDay.Instance.currentWeatherVariableColor = currentLevel.randomWeathers[i].weatherVariableColor;
			}
			if (StartOfRound.Instance.isChallengeFile)
			{
				System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed);
				if (random.Next(0, 100) < 20)
				{
					TimeOfDay.Instance.currentWeatherVariable *= (float)random.Next(20, 80) * 0.02f;
				}
				if (random.Next(0, 100) < 20)
				{
					TimeOfDay.Instance.currentWeatherVariable2 *= (float)random.Next(20, 80) * 0.02f;
				}
			}
			if (TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy)
			{
				TimeOfDay.Instance.currentWeatherVariable = Mathf.Max(4f, TimeOfDay.Instance.currentWeatherVariable);
			}
			TimeOfDay.Instance.SetWeatherBasedOnVariables(currentLevel.randomWeathers[i]);
		}
	}

		[ClientRpc]
		public void FinishGeneratingNewLevelClientRpc()
		{
			HUDManager.Instance.loadingText.enabled = false;
			HUDManager.Instance.LoadingScreen.SetBool("IsLoading", value: false);
			RefreshLightsList();
			StartOfRound.Instance.StartCoroutine(playersManager.openingDoorsSequence());
			if (currentLevel.spawnEnemiesAndScrap)
			{
				SetLevelObjectVariables();
			}

			ResetEnemySpawningVariables();
			ResetEnemyTypesSpawnedCounts();
			playersManager.newGameIsLoading = false;
			FlashlightItem.globalFlashlightInterferenceLevel = 0;
			powerOffPermanently = false;
			RefreshEnemiesList();
			try
			{
				PredictAllOutsideEnemies();
			}
			catch (Exception arg)
			{
				Debug.Log($"Error caught when predicting outside enemies: {arg}");
			}

			if (StartOfRound.Instance.currentLevel.levelIncludesSnowFootprints)
			{
				StartOfRound.Instance.InstantiateFootprintsPooledObjects();
			}
		}

	public void PredictAllOutsideEnemies()
	{
		if (!base.IsServer)
		{
			return;
		}
		enemyNestSpawnObjects.Clear();
		int num = 0;
		float num2 = 0f;
		int num3 = 0;
		bool flag = true;
		System.Random random = new System.Random(playersManager.randomMapSeed + 41);
		System.Random randomSeed = new System.Random(playersManager.randomMapSeed + 21);
		while (num < TimeOfDay.Instance.numberOfHours)
		{
			num += hourTimeBetweenEnemySpawnBatches;
			float num4 = timeScript.lengthOfHours * (float)num;
			float num5 = currentLevel.outsideEnemySpawnChanceThroughDay.Evaluate(num4 / timeScript.totalTime);
			if (StartOfRound.Instance.isChallengeFile)
			{
				num5 += 1f;
			}
			float num6 = num5 + (float)Mathf.Abs(TimeOfDay.Instance.daysUntilDeadline - 3) / 1.6f;
			int num7 = Mathf.Clamp(random.Next((int)(num6 - 3f), (int)(num5 + 3f)), minOutsideEnemiesToSpawn, 20);
			Debug.Log("D");
			Debug.Log($"hour: {num}; timeUpToCurrentHour: {num4}; baseChance: {num5}, enemiesToSpawn: {num7}");
			for (int i = 0; i < num7; i++)
			{
				SpawnProbabilities.Clear();
				int num8 = 0;
				for (int j = 0; j < currentLevel.OutsideEnemies.Count; j++)
				{
					EnemyType enemyType = currentLevel.OutsideEnemies[j].enemyType;
					if (flag)
					{
						enemyType.numberSpawned = 0;
						enemyType.hasSpawnedAtLeastOne = false;
					}
					Debug.Log("G");
					if ((!enemyType.hasSpawnedAtLeastOne && enemyType.DiversityPowerLevel > currentMaxOutsideDiversityLevel - num3) || enemyType.PowerLevel > currentMaxOutsidePower - num2 || enemyType.numberSpawned >= enemyType.MaxCount || enemyType.spawningDisabled)
					{
						Debug.Log("I");
						Debug.Log($"Setting prob to 0; {enemyType.PowerLevel} > {currentMaxOutsidePower - num2}; {enemyType.numberSpawned}");
						SpawnProbabilities.Add(0);
						continue;
					}
					Debug.Log("H");
					int num9;
					if (increasedOutsideEnemySpawnRateIndex == j)
					{
						num9 = 100;
					}
					else if (enemyType.useNumberSpawnedFalloff)
					{
						num9 = (int)((float)currentLevel.OutsideEnemies[j].rarity * (enemyType.probabilityCurve.Evaluate(num4 / timeScript.totalTime) * enemyType.numberSpawnedFalloff.Evaluate((float)enemyType.numberSpawned / 10f)));
						Debug.Log($"Enemy '{currentLevel.OutsideEnemies[j].enemyType.enemyName}' rarity: {currentLevel.OutsideEnemies[j].rarity}; time multiplier: {enemyType.probabilityCurve.Evaluate(num4 / timeScript.totalTime)}");
						Debug.Log($"Enemy probability without number falloff: {(int)((float)currentLevel.OutsideEnemies[j].rarity * enemyType.probabilityCurve.Evaluate(num4 / timeScript.totalTime))}");
						Debug.Log($"Enemy number falloff probability: {num9}; number falloff multiplier y: {enemyType.numberSpawnedFalloff.Evaluate((float)enemyType.numberSpawned / 10f)}; x : {(float)enemyType.numberSpawned / 10f}");
					}
					else
					{
						num9 = (int)((float)currentLevel.OutsideEnemies[j].rarity * enemyType.probabilityCurve.Evaluate(num4 / timeScript.totalTime));
					}
					Debug.Log("J");
					SpawnProbabilities.Add(num9);
					num8 += num9;
				}
				flag = false;
				Debug.Log("K");
				if (num8 <= 0)
				{
					if (num2 >= currentMaxOutsidePower)
					{
						Debug.Log($"Round manager: No more spawnable outside enemies. Power count: {currentOutsideEnemyPower} ; max : {currentLevel.maxOutsideEnemyPowerCount}");
					}
					continue;
				}
				Debug.Log("L");
				int randomWeightedIndex = GetRandomWeightedIndex(SpawnProbabilities.ToArray(), random);
				EnemyType enemyType2 = currentLevel.OutsideEnemies[randomWeightedIndex].enemyType;
				if (enemyType2.numberSpawned <= 0)
				{
					num3 += enemyType2.DiversityPowerLevel;
				}
				float num10 = Mathf.Max(enemyType2.spawnInGroupsOf, 1);
				for (int k = 0; (float)k < num10; k++)
				{
					if (enemyType2.PowerLevel > currentMaxOutsidePower - num2)
					{
						break;
					}
					num2 += enemyType2.PowerLevel;
					enemyType2.numberSpawned++;
				}
				Debug.Log("M");
				Debug.Log($"Got enemy that will spawn for hour #{num}: {enemyType2.enemyName}");
				if (!(enemyType2.nestSpawnPrefab != null))
				{
					continue;
				}
				Debug.Log("N");
				if (!enemyType2.useMinEnemyThresholdForNest)
				{
					Debug.Log("O");
					SpawnNestObjectForOutsideEnemy(enemyType2, randomSeed);
					continue;
				}
				Debug.Log("P");
				if (enemyType2.nestsSpawned >= 1)
				{
					Debug.Log("Q");
					Debug.Log("Nests spawned was > 1; continuing without spawning nest");
					continue;
				}
				Debug.Log("R");
				if (enemyType2.numberSpawned >= enemyType2.minEnemiesToSpawnNest)
				{
					Debug.Log("S");
					SpawnNestObjectForOutsideEnemy(enemyType2, randomSeed);
				}
			}
		}
		Debug.Log("T");
		enemyNestSpawnObjects.TrimExcess();
		List<NetworkObjectReference> list = new List<NetworkObjectReference>();
		for (int l = 0; l < enemyNestSpawnObjects.Count; l++)
		{
			NetworkObject component = enemyNestSpawnObjects[l].GetComponent<NetworkObject>();
			if (component != null)
			{
				list.Add(component);
			}
		}
		if (list.Count > 0)
		{
			SyncNestSpawnObjectsOrderServerRpc(list.ToArray());
		}
	}

	public void SpawnNestObjectForOutsideEnemy(EnemyType enemyType, System.Random randomSeed)
	{
		GetOutsideAINodes();
		GameObject[] array = ((enemyType.WaterType == EnemyWaterType.WaterOnly) ? outsideAIWaterNodes : ((enemyType.WaterType != EnemyWaterType.LandOnly) ? outsideAINodes : outsideAIDryNodes));
		int num = randomSeed.Next(0, array.Length);
		Vector3 position = array[0].transform.position;
		for (int i = 0; i < array.Length; i++)
		{
			position = array[num].transform.position;
			Vector3 originalPosition = position;
			int layermaskForEnemySizeLimit = GetLayermaskForEnemySizeLimit(enemyType);
			position = GetRandomNavMeshPositionInBoxPredictable(position, 15f, default(NavMeshHit), randomSeed, layermaskForEnemySizeLimit);
			position = PositionWithDenialPointsChecked(position, array, enemyType, enemyType.nestDistanceFromShip, randomSeed);
			Vector3 vector = PositionEdgeCheck(position, enemyType.nestSpawnPrefabWidth, layermaskForEnemySizeLimit, originalPosition);
			if (vector == Vector3.zero)
			{
				num = (num + 1) % array.Length;
				continue;
			}
			position = vector;
			break;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(enemyType.nestSpawnPrefab, position, Quaternion.Euler(Vector3.zero));
		gameObject.transform.Rotate(Vector3.up, randomSeed.Next(-180, 180), Space.World);
		if (!gameObject.gameObject.GetComponentInChildren<NetworkObject>())
		{
			Debug.LogError("Error: No NetworkObject found in enemy nest spawn prefab that was just spawned on the host: '" + gameObject.name + "'");
		}
		else
		{
			gameObject.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
		}
		if (!gameObject.GetComponent<EnemyAINestSpawnObject>())
		{
			Debug.LogError("Error: No EnemyAINestSpawnObject component in nest object prefab that was just spawned on the host: '" + gameObject.name + "'");
		}
		else
		{
			enemyNestSpawnObjects.Add(gameObject.GetComponent<EnemyAINestSpawnObject>());
		}
		enemyType.nestsSpawned++;
	}

		[ServerRpc]
		public void SyncNestSpawnObjectsOrderServerRpc(NetworkObjectReference[] nestObjects)
		{
			SyncNestSpawnPositionsClientRpc(nestObjects);
		}

		[ClientRpc]
		public void SyncNestSpawnPositionsClientRpc(NetworkObjectReference[] nestObjects)
		{
			if (base.IsServer)
			{
				return;
			}

			enemyNestSpawnObjects.Clear();
			for (int i = 0; i < nestObjects.Length; i++)
			{
				if (nestObjects[i].TryGet(out var networkObject))
				{
			EnemyAINestSpawnObject component = networkObject.gameObject.GetComponent<EnemyAINestSpawnObject>();
					if (component != null)
					{
						enemyNestSpawnObjects.Add(component);
					}
				}
			}
		}

	private void ResetEnemySpawningVariables()
	{
		begunSpawningEnemies = false;
		currentHour = 0;
		cannotSpawnMoreInsideEnemies = false;
		minEnemiesToSpawn = 0;
		minOutsideEnemiesToSpawn = 0;
		for (int i = 0; i < currentLevel.OutsideEnemies.Count; i++)
		{
			currentLevel.OutsideEnemies[i].enemyType.nestsSpawned = 0;
		}
	}

	public void ResetEnemyVariables()
	{
		HoarderBugAI.grabbableObjectsInMap.Clear();
		HoarderBugAI.HoarderBugItems.Clear();
		if (CentipedeAI.allCentipedes != null)
		{
			CentipedeAI.allCentipedes = null;
		}
		BaboonBirdAI.baboonCampPosition = Vector3.zero;
		FlowerSnakeEnemy.mainSnakes = null;
		PumaAI.AllTreeNodes = null;
		PumaAI.excludeTrees = null;
		PumaAI.stalkNodesInUse = null;
		MoldSpreadManager moldSpreadManager = UnityEngine.Object.FindObjectOfType<MoldSpreadManager>();
		if (moldSpreadManager != null)
		{
			moldSpreadManager.generatedAmountThisDay = 0;
		}
	}

	public void CollectNewScrapForThisRound(GrabbableObject scrapObject)
	{
		if (scrapObject.itemProperties.isScrap && !scrapDroppedInShip.Contains(scrapObject) && !scrapObject.scrapPersistedThroughRounds)
		{
			scrapDroppedInShip.Add(scrapObject);
			HUDManager.Instance.AddNewScrapFoundToDisplay(scrapObject);
		}
	}

	public void DetectElevatorIsRunning()
	{
		if (base.IsServer)
		{
			Debug.Log("Ship is leaving. Despawning props and enemies.");
			if (elevatorRunningCoroutine != null)
			{
				StopCoroutine(elevatorRunningCoroutine);
			}
			elevatorRunningCoroutine = StartCoroutine(DetectElevatorRunning());
		}
	}

	private IEnumerator DetectElevatorRunning()
	{
		isSpawningEnemies = false;
		yield return new WaitForSeconds(1.5f);
		Debug.Log("Despawning props and enemies #2");
		UnloadSceneObjectsEarly();
	}

	public void BeginEnemySpawning()
	{
		if (base.IsServer)
		{
			if (allEnemyVents.Length != 0 && currentLevel.maxEnemyPowerCount > 0)
			{
				currentEnemySpawnIndex = 0;
				PlotOutEnemiesForNextHour();
				isSpawningEnemies = true;
			}
			else
			{
				Debug.Log("Not able to spawn enemies on map; no vents were detected or maxEnemyPowerCount is 0.");
			}
		}
	}

	public void SpawnEnemiesOutside()
	{
		if (currentOutsideEnemyPower > currentMaxOutsidePower)
		{
			Debug.Log("Cannot spawn more outside enemies: max power count has been reached");
			return;
		}
		float num = timeScript.lengthOfHours * (float)currentHour;
		float num2 = (float)(int)(currentLevel.outsideEnemySpawnChanceThroughDay.Evaluate(num / timeScript.totalTime) * 100f) / 100f;
		if (StartOfRound.Instance.isChallengeFile)
		{
			num2 += 1f;
		}
		float num3 = num2 + (float)Mathf.Abs(TimeOfDay.Instance.daysUntilDeadline - 3) / 1.6f;
		int num4 = Mathf.Clamp(OutsideEnemySpawnRandom.Next((int)(num3 - 3f), (int)(num2 + 3f)), minOutsideEnemiesToSpawn, 20);
		for (int i = 0; i < num4; i++)
		{
			if (!SpawnRandomOutsideEnemy(num))
			{
				break;
			}
		}
	}

	public void SpawnWeedEnemies()
	{
		MoldSpreadManager moldSpreadManager = UnityEngine.Object.FindObjectOfType<MoldSpreadManager>();
		int num = 0;
		if (moldSpreadManager != null)
		{
			num = moldSpreadManager.generatedAmountThisDay;
		}
		if (num <= 15 || timeScript.hour < 3 || WeedEnemySpawnRandom.Next(0, 70) > num)
		{
			return;
		}
		int num2 = WeedEnemySpawnRandom.Next(1, 3);
		GetOutsideAINodes();
		float timeUpToCurrentHour = TimeOfDay.Instance.lengthOfHours * (float)currentHour;
		for (int i = 0; i < num2; i++)
		{
			if (!SpawnRandomWeedEnemy(timeUpToCurrentHour, num))
			{
				break;
			}
		}
	}

	public bool SpawnRandomWeedEnemy(float timeUpToCurrentHour, int numberOfWeeds)
	{
		SpawnProbabilities.Clear();
		int num = 0;
		for (int i = 0; i < WeedEnemies.Count; i++)
		{
			EnemyType enemyType = WeedEnemies[i].enemyType;
			if (firstTimeSpawningWeedEnemies)
			{
				enemyType.numberSpawned = 0;
				enemyType.hasSpawnedAtLeastOne = false;
			}
			if (enemyType.PowerLevel > 4f - currentWeedEnemyPower || enemyType.numberSpawned >= enemyType.MaxCount || enemyType.spawningDisabled)
			{
				SpawnProbabilities.Add(0);
				continue;
			}
			int num2 = ((increasedOutsideEnemySpawnRateIndex == i) ? 100 : ((!enemyType.useNumberSpawnedFalloff) ? ((int)((float)WeedEnemies[i].rarity * enemyType.probabilityCurve.Evaluate(timeUpToCurrentHour / timeScript.totalTime))) : ((int)((float)WeedEnemies[i].rarity * (enemyType.probabilityCurve.Evaluate(timeUpToCurrentHour / timeScript.totalTime) * enemyType.numberSpawnedFalloff.Evaluate((float)enemyType.numberSpawned / 10f))))));
			SpawnProbabilities.Add(num2);
			num += num2;
		}
		firstTimeSpawningWeedEnemies = false;
		if (num <= 20)
		{
			_ = currentOutsideEnemyPower;
			_ = 4f;
			return false;
		}
		bool result = false;
		int randomWeightedIndex = GetRandomWeightedIndex(SpawnProbabilities.ToArray(), WeedEnemySpawnRandom);
		EnemyType enemyType2 = WeedEnemies[randomWeightedIndex].enemyType;
		float num3 = Mathf.Max(enemyType2.spawnInGroupsOf, 1);
		GetOutsideAINodes();
		GameObject[] array = ((enemyType2.WaterType == EnemyWaterType.WaterOnly) ? outsideAIWaterNodes : ((enemyType2.WaterType != EnemyWaterType.LandOnly) ? outsideAINodes : outsideAIDryNodes));
		for (int j = 0; (float)j < num3; j++)
		{
			if (enemyType2.PowerLevel > 4f - currentWeedEnemyPower)
			{
				break;
			}
			currentWeedEnemyPower += enemyType2.PowerLevel;
			Vector3 position = array[AnomalyRandom.Next(0, array.Length)].transform.position;
			position = GetRandomNavMeshPositionInBoxPredictable(position, 10f, default(NavMeshHit), WeedEnemySpawnPlacementRandom, GetLayermaskForEnemySizeLimit(enemyType2));
			position = PositionWithDenialPointsChecked(position, array, enemyType2, -1f, WeedEnemySpawnPlacementRandom);
			GameObject gameObject = UnityEngine.Object.Instantiate(enemyType2.enemyPrefab, position, Quaternion.Euler(Vector3.zero));
			gameObject.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
			SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
			gameObject.GetComponent<EnemyAI>().enemyType.numberSpawned++;
			gameObject.GetComponent<EnemyAI>().enemyType.hasSpawnedAtLeastOne = true;
			result = true;
		}
		Debug.Log("Spawned weed enemy: " + enemyType2.enemyName);
		return result;
	}

	public void SpawnDaytimeEnemiesOutside()
	{
		if (currentLevel.DaytimeEnemies == null || currentLevel.DaytimeEnemies.Count <= 0 || currentDaytimeEnemyPower > (float)currentLevel.maxDaytimeEnemyPowerCount)
		{
			return;
		}
		float num = timeScript.lengthOfHours * (float)currentHour;
		float num2 = currentLevel.daytimeEnemySpawnChanceThroughDay.Evaluate(num / timeScript.totalTime);
		int num3 = Mathf.Clamp(DaytimeEnemySpawnRandom.Next((int)(num2 - currentLevel.daytimeEnemiesProbabilityRange), (int)(num2 + currentLevel.daytimeEnemiesProbabilityRange)), 0, 20);
		GetOutsideAINodes();
		for (int i = 0; i < num3; i++)
		{
			if (!SpawnRandomDaytimeEnemy(num))
			{
				break;
			}
		}
	}

	private bool SpawnRandomDaytimeEnemy(float timeUpToCurrentHour)
	{
		float num = currentDaytimeEnemyPowerNoDeaths;
		bool flag = false;
		for (int i = 0; i < currentLevel.DaytimeEnemies.Count; i++)
		{
			EnemyType enemyType = currentLevel.DaytimeEnemies[i].enemyType;
			if (firstTimeSpawningDaytimeEnemies)
			{
				enemyType.numberSpawned = 0;
				enemyType.hasSpawnedAtLeastOne = false;
			}
			if (!(enemyType.PowerLevel > (float)currentLevel.maxDaytimeEnemyPowerCount - currentDaytimeEnemyPowerNoDeaths) && enemyType.numberSpawned < currentLevel.DaytimeEnemies[i].enemyType.MaxCount && !(enemyType.normalizedTimeInDayToLeave < TimeOfDay.Instance.normalizedTimeOfDay) && !enemyType.spawningDisabled)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			num = currentDaytimeEnemyPower;
		}
		SpawnProbabilities.Clear();
		int num2 = 0;
		for (int j = 0; j < currentLevel.DaytimeEnemies.Count; j++)
		{
			EnemyType enemyType = currentLevel.DaytimeEnemies[j].enemyType;
			if (firstTimeSpawningDaytimeEnemies)
			{
				enemyType.numberSpawned = 0;
				enemyType.hasSpawnedAtLeastOne = false;
			}
			if (enemyType.PowerLevel > (float)currentLevel.maxDaytimeEnemyPowerCount - num || enemyType.numberSpawned >= currentLevel.DaytimeEnemies[j].enemyType.MaxCount || enemyType.normalizedTimeInDayToLeave < TimeOfDay.Instance.normalizedTimeOfDay || enemyType.spawningDisabled)
			{
				SpawnProbabilities.Add(0);
				continue;
			}
			int num3 = (int)((float)currentLevel.DaytimeEnemies[j].rarity * enemyType.probabilityCurve.Evaluate(timeUpToCurrentHour / timeScript.totalTime));
			SpawnProbabilities.Add(num3);
			num2 += num3;
		}
		firstTimeSpawningDaytimeEnemies = false;
		if (num2 <= 0)
		{
			_ = (float)currentLevel.maxDaytimeEnemyPowerCount;
			return false;
		}
		int randomWeightedIndex = GetRandomWeightedIndex(SpawnProbabilities.ToArray(), DaytimeEnemySpawnRandom);
		EnemyType enemyType2 = currentLevel.DaytimeEnemies[randomWeightedIndex].enemyType;
		bool result = false;
		float num4 = Mathf.Max(enemyType2.spawnInGroupsOf, 1);
		GetOutsideAINodes();
		GameObject[] array = ((enemyType2.WaterType == EnemyWaterType.WaterOnly) ? outsideAIWaterNodes : ((enemyType2.WaterType != EnemyWaterType.LandOnly) ? outsideAINodes : outsideAIDryNodes));
		for (int k = 0; (float)k < num4; k++)
		{
			if (enemyType2.PowerLevel > (float)currentLevel.maxDaytimeEnemyPowerCount - num)
			{
				break;
			}
			currentDaytimeEnemyPower += currentLevel.DaytimeEnemies[randomWeightedIndex].enemyType.PowerLevel;
			currentDaytimeEnemyPowerNoDeaths += currentLevel.DaytimeEnemies[randomWeightedIndex].enemyType.PowerLevel;
			num += currentLevel.DaytimeEnemies[randomWeightedIndex].enemyType.PowerLevel;
			Vector3 position = array[AnomalyRandom.Next(0, array.Length)].transform.position;
			position = GetRandomNavMeshPositionInBoxPredictable(position, 10f, default(NavMeshHit), DaytimeEnemySpawnPlacementRandom, GetLayermaskForEnemySizeLimit(enemyType2));
			position = PositionWithDenialPointsChecked(position, array, enemyType2, -1f, DaytimeEnemySpawnPlacementRandom);
			GameObject gameObject = UnityEngine.Object.Instantiate(enemyType2.enemyPrefab, position, Quaternion.Euler(Vector3.zero));
			gameObject.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
			SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
			gameObject.GetComponent<EnemyAI>().enemyType.numberSpawned++;
			gameObject.GetComponent<EnemyAI>().enemyType.hasSpawnedAtLeastOne = true;
			result = true;
		}
		return result;
	}

	private bool SpawnRandomOutsideEnemy(float timeUpToCurrentHour)
	{
		float num = currentOutsideEnemyPowerNoDeaths;
		bool flag = false;
		for (int i = 0; i < currentLevel.OutsideEnemies.Count; i++)
		{
			EnemyType enemyType = currentLevel.OutsideEnemies[i].enemyType;
			if (firstTimeSpawningOutsideEnemies)
			{
				enemyType.numberSpawned = 0;
				enemyType.hasSpawnedAtLeastOne = false;
			}
			if ((enemyType.numberSpawned > 0 || enemyType.DiversityPowerLevel <= currentMaxOutsideDiversityLevel - currentOutsideEnemyDiversityLevel) && !(enemyType.PowerLevel > currentMaxOutsidePower - currentOutsideEnemyPowerNoDeaths) && enemyType.numberSpawned < enemyType.MaxCount && !enemyType.spawningDisabled)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			num = currentOutsideEnemyPower;
		}
		SpawnProbabilities.Clear();
		int num2 = 0;
		MoldSpreadManager moldSpreadManager = UnityEngine.Object.FindObjectOfType<MoldSpreadManager>();
		int num3 = 0;
		if (moldSpreadManager != null)
		{
			num3 = moldSpreadManager.generatedMold.Count;
		}
		for (int j = 0; j < currentLevel.OutsideEnemies.Count; j++)
		{
			EnemyType enemyType = currentLevel.OutsideEnemies[j].enemyType;
			if (firstTimeSpawningOutsideEnemies)
			{
				enemyType.numberSpawned = 0;
				enemyType.hasSpawnedAtLeastOne = false;
			}
			if (enemyType.numberSpawned <= 0 && enemyType.DiversityPowerLevel > currentMaxOutsideDiversityLevel - currentOutsideEnemyDiversityLevel)
			{
				SpawnProbabilities.Add(0);
				continue;
			}
			if (enemyType.PowerLevel > currentMaxOutsidePower - num || enemyType.numberSpawned >= enemyType.MaxCount || enemyType.spawningDisabled)
			{
				SpawnProbabilities.Add(0);
				continue;
			}
			int num4 = ((increasedOutsideEnemySpawnRateIndex == j) ? 100 : ((!enemyType.useNumberSpawnedFalloff) ? ((int)((float)currentLevel.OutsideEnemies[j].rarity * enemyType.probabilityCurve.Evaluate(timeUpToCurrentHour / timeScript.totalTime))) : ((int)((float)currentLevel.OutsideEnemies[j].rarity * (enemyType.probabilityCurve.Evaluate(timeUpToCurrentHour / timeScript.totalTime) * enemyType.numberSpawnedFalloff.Evaluate((float)enemyType.numberSpawned / 10f))))));
			if (enemyType.spawnFromWeeds)
			{
				num4 = (int)Mathf.Clamp((float)num4 * ((float)num3 / 60f), 0f, 200f);
			}
			SpawnProbabilities.Add(num4);
			num2 += num4;
		}
		firstTimeSpawningOutsideEnemies = false;
		if (num2 <= 0)
		{
			_ = currentMaxOutsidePower;
			return false;
		}
		bool flag2 = false;
		int randomWeightedIndex = GetRandomWeightedIndex(SpawnProbabilities.ToArray(), OutsideEnemySpawnRandom);
		EnemyType enemyType2 = currentLevel.OutsideEnemies[randomWeightedIndex].enemyType;
		GetOutsideAINodes();
		GameObject[] array = ((enemyType2.WaterType == EnemyWaterType.WaterOnly) ? outsideAIWaterNodes : ((enemyType2.WaterType != EnemyWaterType.LandOnly) ? outsideAINodes : outsideAIDryNodes));
		bool flag3 = false;
		if (!enemyType2.hasSpawnedAtLeastOne)
		{
			flag3 = true;
		}
		if (enemyType2 != enemyType2.enemyPrefab.GetComponent<EnemyAI>().enemyType)
		{
			Debug.LogError("Error! Enemy-to-spawn '" + enemyType2.enemyName + "' prefab does not have the same enemyType set as enemy-to-spawn! Fix immediately");
		}
		float num5 = Mathf.Max(enemyType2.spawnInGroupsOf, 1);
		for (int k = 0; (float)k < num5; k++)
		{
			if (enemyType2.PowerLevel > currentMaxOutsidePower - num)
			{
				break;
			}
			currentOutsideEnemyPower += currentLevel.OutsideEnemies[randomWeightedIndex].enemyType.PowerLevel;
			currentOutsideEnemyPowerNoDeaths += currentLevel.OutsideEnemies[randomWeightedIndex].enemyType.PowerLevel;
			num += currentLevel.OutsideEnemies[randomWeightedIndex].enemyType.PowerLevel;
			Vector3 position = array[AnomalyRandom.Next(0, array.Length)].transform.position;
			position = GetRandomNavMeshPositionInBoxPredictable(position, 10f, default(NavMeshHit), OutsideEnemySpawnPlacementRandom, GetLayermaskForEnemySizeLimit(enemyType2));
			position = PositionWithDenialPointsChecked(position, array, enemyType2, -1f, OutsideEnemySpawnPlacementRandom);
			GameObject gameObject = UnityEngine.Object.Instantiate(enemyType2.enemyPrefab, position, Quaternion.Euler(Vector3.zero));
			gameObject.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
			SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
			enemyType2.numberSpawned++;
			flag2 = true;
		}
		if (flag2 && flag3)
		{
			currentOutsideEnemyDiversityLevel += enemyType2.DiversityPowerLevel;
			enemyType2.hasSpawnedAtLeastOne = true;
		}
		Debug.Log("Spawned enemy: " + enemyType2.enemyName);
		return flag2;
	}

	public int GetLayermaskForEnemySizeLimit(EnemyType enemyType, bool supplyExistingMask = false, int enemyMask = -1)
	{
		int num = -1;
		if (supplyExistingMask)
		{
			num = enemyMask;
		}
		if (enemyType.SizeLimit == NavSizeLimit.MediumSpaces)
		{
			num &= -97;
		}
		else if (enemyType.SizeLimit == NavSizeLimit.SmallSpaces)
		{
			num &= -33;
		}
		if (enemyType.WaterType == EnemyWaterType.LandOnly)
		{
			num &= -4097;
		}
		else if (enemyType.WaterType == EnemyWaterType.WaterOnly)
		{
			num = 4096;
		}
		return num | 0x2000;
	}

	public Vector3 PositionWithDenialPointsChecked(Vector3 spawnPosition, GameObject[] spawnPoints, EnemyType enemyType, float distanceFromShip = -1f, System.Random randomSeed = null)
	{
		if (spawnPoints.Length == 0)
		{
			Debug.LogError("Spawn points array was null in denial points check function!");
			return spawnPosition;
		}
		int num = 0;
		bool flag = false;
		for (int i = 0; i < spawnPoints.Length - 1; i++)
		{
			for (int j = 0; j < spawnDenialPoints.Length; j++)
			{
				flag = true;
				if (Vector3.Distance(spawnPosition, spawnDenialPoints[j].transform.position) < 16f || (distanceFromShip != -1f && Vector3.Distance(spawnPosition, StartOfRound.Instance.shipLandingPosition.transform.position) < distanceFromShip))
				{
					num = (num + 1) % spawnPoints.Length;
					spawnPosition = spawnPoints[num].transform.position;
					spawnPosition = GetRandomNavMeshPositionInBoxPredictable(spawnPosition, 10f, default(NavMeshHit), AnomalyRandom, GetLayermaskForEnemySizeLimit(enemyType));
					flag = false;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		return spawnPosition;
	}

	public void PlotOutEnemiesForNextHour()
	{
		if (!base.IsServer)
		{
			return;
		}
		List<EnemyVent> list = new List<EnemyVent>();
		for (int i = 0; i < allEnemyVents.Length; i++)
		{
			if (!allEnemyVents[i].occupied)
			{
				list.Add(allEnemyVents[i]);
			}
		}
		enemySpawnTimes.Clear();
		float num = currentLevel.enemySpawnChanceThroughoutDay.Evaluate(timeScript.lengthOfHours * (float)currentHour / timeScript.totalTime);
		num -= 1f;
		if (StartOfRound.Instance.isChallengeFile)
		{
			num += 1f;
		}
		int num2 = Mathf.RoundToInt(Mathf.Clamp(Mathf.Lerp(num + (float)Mathf.Abs(TimeOfDay.Instance.daysUntilDeadline - 3) / 1.6f - currentLevel.spawnProbabilityRange, num + currentLevel.spawnProbabilityRange, (float)EnemySpawnRandom.NextDouble()), minEnemiesToSpawn, 20f));
		if (enemyRushIndex != -1)
		{
			num2 += 2;
		}
		num2 = Mathf.Clamp(num2, 0, list.Count);
		if (currentEnemyPower >= currentMaxInsidePower)
		{
			cannotSpawnMoreInsideEnemies = true;
			return;
		}
		float num3 = timeScript.lengthOfHours * (float)currentHour;
		if (currentLevel.specialEnemyRarity.overrideEnemy != null && currentLevel.specialEnemyRarity.overrideEnemy.numberSpawned < currentLevel.specialEnemyRarity.overrideEnemy.MaxCount && currentLevel.specialEnemyRarity.percentageChance >= 1f)
		{
			num2 = Mathf.Max(num2, 1);
		}
		for (int j = 0; j < num2; j++)
		{
			int num4 = EnemySpawnRandom.Next((int)(10f + num3), (int)(timeScript.lengthOfHours * (float)hourTimeBetweenEnemySpawnBatches + num3));
			int index = EnemySpawnRandom.Next(list.Count);
			if (!AssignRandomEnemyToVent(list[index], num4))
			{
				break;
			}
			list.RemoveAt(index);
			enemySpawnTimes.Add(num4);
		}
		enemySpawnTimes.Sort();
	}

	public void LogEnemySpawnTimes(bool couldNotFinish)
	{
		if (couldNotFinish)
		{
			Debug.Log("Stopped assigning enemies to vents early as there was no enemy with a power count low enough to fit.");
		}
		Debug.Log("Enemy spawn times:");
		for (int i = 0; i < enemySpawnTimes.Count; i++)
		{
			Debug.Log($"time {i}: {enemySpawnTimes[i]}");
		}
	}

	private int RoundUpToNearestTen(float x)
	{
		return (int)(x / 10f) * 10;
	}

	private bool AssignRandomEnemyToVent(EnemyVent vent, float spawnTime)
	{
		float num = currentEnemyPowerNoDeaths;
		bool flag = false;
		for (int i = 0; i < currentLevel.Enemies.Count; i++)
		{
			EnemyType enemyType = currentLevel.Enemies[i].enemyType;
			if (firstTimeSpawningEnemies)
			{
				enemyType.numberSpawned = 0;
				enemyType.hasSpawnedAtLeastOne = false;
			}
			if (!InsideEnemyCannotBeSpawned(i, currentEnemyPowerNoDeaths))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			num = currentEnemyPower;
		}
		SpawnProbabilities.Clear();
		int num2 = 0;
		float num3 = timeScript.lengthOfHours * (float)currentHour / TimeOfDay.Instance.totalTime;
		for (int j = 0; j < currentLevel.Enemies.Count; j++)
		{
			EnemyType enemyType = currentLevel.Enemies[j].enemyType;
			if (firstTimeSpawningEnemies)
			{
				enemyType.numberSpawned = 0;
				enemyType.hasSpawnedAtLeastOne = false;
			}
			if (InsideEnemyCannotBeSpawned(j, num))
			{
				SpawnProbabilities.Add(0);
				continue;
			}
			Debug.Log($"enemy rush index is {enemyRushIndex}; current index {j}");
			int num4;
			if (enemyRushIndex != -1 && enemyRushIndex == j)
			{
				num4 = 100;
			}
			else if (increasedInsideEnemySpawnRateIndex == j)
			{
				num4 = 100;
			}
			else if (enemyType.useNumberSpawnedFalloff)
			{
				num4 = (int)((float)currentLevel.Enemies[j].rarity * (enemyType.probabilityCurve.Evaluate(num3) * enemyType.numberSpawnedFalloff.Evaluate(Mathf.Clamp((float)enemyType.numberSpawned / 10f, 0f, 1f))));
				Debug.Log($"Hours: {timeScript.lengthOfHours * (float)currentHour} ; Time: {num3} | Enemy #{j} '{currentLevel.Enemies[j].enemyType.enemyName}' : probability: {num4}");
			}
			else
			{
				num4 = (int)((float)currentLevel.Enemies[j].rarity * enemyType.probabilityCurve.Evaluate(num3));
				Debug.Log($"Hours: {timeScript.lengthOfHours * (float)currentHour} ; Time: {num3} | Enemy #{j} '{currentLevel.Enemies[j].enemyType.enemyName}' : probability: {num4}");
			}
			if (enemyType.increasedChanceInterior != -1 && currentDungeonType == enemyType.increasedChanceInterior)
			{
				num4 = (int)Mathf.Min((float)num4 * 1.5f, num4 + 50);
			}
			if (enemyRushIndex == -1 && enemyRushIndex != j)
			{
				num4 = Mathf.RoundToInt((float)num4 * 0.075f);
			}
			SpawnProbabilities.Add(num4);
			num2 += num4;
		}
		firstTimeSpawningEnemies = false;
		if (num2 <= 0)
		{
			if (num >= currentMaxInsidePower)
			{
				Debug.Log($"Round manager: No more spawnable enemies. Power count: {currentLevel.maxEnemyPowerCount} Max: {currentLevel.maxEnemyPowerCount}");
				cannotSpawnMoreInsideEnemies = true;
			}
			return false;
		}
		int num5 = 0;
		bool flag2 = false;
		if (currentLevel.specialEnemyRarity.overrideEnemy != null)
		{
			if (currentLevel.specialEnemyRarity.percentageChance >= 1f)
			{
				for (int k = 0; k < currentLevel.Enemies.Count; k++)
				{
					if (currentLevel.Enemies[k].enemyType == currentLevel.specialEnemyRarity.overrideEnemy)
					{
						if (SpawnProbabilities[k] != 0)
						{
							num5 = k;
							flag2 = true;
						}
						break;
					}
				}
			}
			else
			{
				int num6 = -1;
				float num7 = 0f;
				for (int l = 0; l < currentLevel.Enemies.Count; l++)
				{
					if (currentLevel.Enemies[l].enemyType == currentLevel.specialEnemyRarity.overrideEnemy)
					{
						num6 = l;
					}
					if (num6 != l)
					{
						num7 += (float)SpawnProbabilities[l];
					}
				}
				if (num6 != -1 && SpawnProbabilities[num6] != 0 && currentLevel.specialEnemyRarity.percentageChance > 0f)
				{
					SpawnProbabilities[num6] = (int)(currentLevel.specialEnemyRarity.percentageChance * num7 / (1f - currentLevel.specialEnemyRarity.percentageChance));
				}
			}
		}
		if (!flag2)
		{
			num5 = GetRandomWeightedIndex(SpawnProbabilities.ToArray(), EnemySpawnRandom);
		}
		Debug.Log($"ADDING ENEMY #{num5}: {currentLevel.Enemies[num5].enemyType.enemyName}");
		Debug.Log($"Adding {currentLevel.Enemies[num5].enemyType.PowerLevel} to power level, enemy: {currentLevel.Enemies[num5].enemyType.enemyName}");
		currentEnemyPower += currentLevel.Enemies[num5].enemyType.PowerLevel;
		currentEnemyPowerNoDeaths += currentLevel.Enemies[num5].enemyType.PowerLevel;
		vent.enemyType = currentLevel.Enemies[num5].enemyType;
		vent.enemyTypeIndex = num5;
		vent.occupied = true;
		vent.spawnTime = spawnTime;
		if (timeScript.hour - currentHour > 0)
		{
			Debug.Log("RoundManager is catching up to current time! Not syncing vent SFX with clients since enemy will spawn from vent almost immediately.");
		}
		else
		{
			vent.SyncVentSpawnTimeClientRpc((int)spawnTime, num5);
		}
		if (!currentLevel.Enemies[num5].enemyType.hasSpawnedAtLeastOne)
		{
			Debug.Log($"Adding {currentLevel.Enemies[num5].enemyType.DiversityPowerLevel} to diversity level for enemy '{currentLevel.Enemies[num5].enemyType.enemyName}'");
			currentInsideEnemyDiversityLevel += currentLevel.Enemies[num5].enemyType.DiversityPowerLevel;
		}
		currentLevel.Enemies[num5].enemyType.numberSpawned++;
		currentLevel.Enemies[num5].enemyType.hasSpawnedAtLeastOne = true;
		return true;
	}

	private bool InsideEnemyCannotBeSpawned(int enemyIndex, float currentPowerLevel)
	{
		if ((currentLevel.Enemies[enemyIndex].enemyType.numberSpawned > 0 || currentLevel.Enemies[enemyIndex].enemyType.DiversityPowerLevel <= currentMaxInsideDiversityLevel - currentInsideEnemyDiversityLevel) && !currentLevel.Enemies[enemyIndex].enemyType.spawningDisabled && !(currentLevel.Enemies[enemyIndex].enemyType.PowerLevel > currentMaxInsidePower - currentPowerLevel))
		{
			return currentLevel.Enemies[enemyIndex].enemyType.numberSpawned >= currentLevel.Enemies[enemyIndex].enemyType.MaxCount;
		}
		return true;
	}

	public void InitializeRandomNumberGenerators()
	{
		SoundManager.Instance.InitializeRandom();
		LevelRandom = new System.Random(playersManager.randomMapSeed);
		AnomalyRandom = new System.Random(playersManager.randomMapSeed + 5);
		EnemySpawnRandom = new System.Random(playersManager.randomMapSeed + 40);
		DaytimeEnemySpawnRandom = new System.Random(playersManager.randomMapSeed + 43);
		OutsideEnemySpawnRandom = new System.Random(playersManager.randomMapSeed + 41);
		IndoorEnemySpawnPlacementRandom = new System.Random(playersManager.randomMapSeed + 30);
		DaytimeEnemySpawnPlacementRandom = new System.Random(playersManager.randomMapSeed + 33);
		OutsideEnemySpawnPlacementRandom = new System.Random(playersManager.randomMapSeed + 31);
		WeedEnemySpawnPlacementRandom = new System.Random(playersManager.randomMapSeed + 43);
		WeedEnemySpawnRandom = new System.Random(playersManager.randomMapSeed + 42);
		BreakerBoxRandom = new System.Random(playersManager.randomMapSeed + 20);
	}

	public void SpawnEnemyFromVent(EnemyVent vent)
	{
		Vector3 position = vent.floorNode.position;
		float y = vent.floorNode.eulerAngles.y;
		SpawnEnemyOnServer(position, y, vent.enemyTypeIndex);
		Debug.Log("Spawned enemy from vent");
		vent.OpenVentClientRpc();
		vent.occupied = false;
	}

	public void SpawnEnemyOnServer(Vector3 spawnPosition, float yRot, int enemyNumber = -1)
	{
		if (!base.IsServer)
		{
			SpawnEnemyServerRpc(spawnPosition, yRot, enemyNumber);
		}
		else
		{
			SpawnEnemyGameObject(spawnPosition, yRot, enemyNumber);
		}
	}

		[ServerRpc]
		public void SpawnEnemyServerRpc(Vector3 spawnPosition, float yRot, int enemyNumber)
		{
			SpawnEnemyGameObject(spawnPosition, yRot, enemyNumber);
		}

	public NetworkObjectReference SpawnEnemyGameObject(Vector3 spawnPosition, float yRot, int enemyNumber, EnemyType enemyType = null)
	{
		if (!base.IsServer)
		{
			return currentLevel.Enemies[0].enemyType.enemyPrefab.GetComponent<NetworkObject>();
		}
		if (enemyType != null)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(enemyType.enemyPrefab, spawnPosition, Quaternion.Euler(new Vector3(0f, yRot, 0f)));
			gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
			SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
			return gameObject.GetComponentInChildren<NetworkObject>();
		}
		int index = enemyNumber;
		switch (enemyNumber)
		{
		case -1:
			index = UnityEngine.Random.Range(0, currentLevel.Enemies.Count);
			break;
		case -2:
		{
			GameObject gameObject3 = UnityEngine.Object.Instantiate(currentLevel.DaytimeEnemies[UnityEngine.Random.Range(0, currentLevel.DaytimeEnemies.Count)].enemyType.enemyPrefab, spawnPosition, Quaternion.Euler(new Vector3(0f, yRot, 0f)));
			gameObject3.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
			SpawnedEnemies.Add(gameObject3.GetComponent<EnemyAI>());
			return gameObject3.GetComponentInChildren<NetworkObject>();
		}
		case -3:
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(currentLevel.OutsideEnemies[UnityEngine.Random.Range(0, currentLevel.OutsideEnemies.Count)].enemyType.enemyPrefab, spawnPosition, Quaternion.Euler(new Vector3(0f, yRot, 0f)));
			gameObject2.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
			SpawnedEnemies.Add(gameObject2.GetComponent<EnemyAI>());
			return gameObject2.GetComponentInChildren<NetworkObject>();
		}
		}
		GameObject gameObject4 = UnityEngine.Object.Instantiate(currentLevel.Enemies[index].enemyType.enemyPrefab, spawnPosition, Quaternion.Euler(new Vector3(0f, yRot, 0f)));
		gameObject4.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
		SpawnedEnemies.Add(gameObject4.GetComponent<EnemyAI>());
		return gameObject4.GetComponentInChildren<NetworkObject>();
	}

	public void DespawnEnemyOnServer(NetworkObject enemyNetworkObject)
	{
		if (!base.IsServer)
		{
			DespawnEnemyServerRpc(enemyNetworkObject);
		}
		else
		{
			DespawnEnemyGameObject(enemyNetworkObject);
		}
	}

		[ServerRpc(RequireOwnership = false)]
		public void DespawnEnemyServerRpc(NetworkObjectReference enemyNetworkObject)
		{
			DespawnEnemyGameObject(enemyNetworkObject);
		}

	private void DespawnEnemyGameObject(NetworkObjectReference enemyNetworkObject)
	{
		if (enemyNetworkObject.TryGet(out var networkObject))
		{
			EnemyAI component = networkObject.gameObject.GetComponent<EnemyAI>();
			SpawnedEnemies.Remove(component);
			if (component.enemyType.isOutsideEnemy)
			{
				currentOutsideEnemyPower -= component.enemyType.PowerLevel;
			}
			else if (component.enemyType.isDaytimeEnemy)
			{
				currentDaytimeEnemyPower -= component.enemyType.PowerLevel;
			}
			else
			{
				currentEnemyPower -= component.enemyType.PowerLevel;
			}
			cannotSpawnMoreInsideEnemies = false;
			Debug.Log("Despawning enemy");
			component.gameObject.GetComponent<NetworkObject>().Despawn();
		}
		else
		{
			Debug.LogError("Round manager despawn enemy gameobject: Could not get network object from reference!");
		}
	}

	public void SwitchPower(bool on)
	{
		if (!base.IsServer)
		{
			return;
		}
		if (on)
		{
			if (!powerOffPermanently)
			{
				PowerSwitchOnClientRpc();
			}
		}
		else
		{
			PowerSwitchOffClientRpc();
		}
	}

		[ClientRpc]
		public void PowerSwitchOnClientRpc()
		{
			onPowerSwitch.Invoke(arg0: true);
			TurnOnAllLights(on: true);
		}

		[ClientRpc]
		public void PowerSwitchOffClientRpc()
		{
			Debug.Log("Calling power switch off event from roundmanager");
			onPowerSwitch.Invoke(arg0: false);
			TurnOnAllLights(on: false);
		}

	public void TurnOnAllLights(bool on)
	{
		if (powerLightsCoroutine != null)
		{
			StopCoroutine(powerLightsCoroutine);
		}
		powerLightsCoroutine = StartCoroutine(turnOnLights(on));
	}

	private IEnumerator turnOnLights(bool turnOn)
	{
		yield return null;
		BreakerBox breakerBox = UnityEngine.Object.FindObjectOfType<BreakerBox>();
		if (breakerBox != null)
		{
			breakerBox.thisAudioSource.PlayOneShot(breakerBox.switchPowerSFX);
			breakerBox.isPowerOn = turnOn;
		}
		int b = 4;
		while (b > 0 && b != 0)
		{
			for (int i = 0; i < allPoweredLightsAnimators.Count / b; i++)
			{
				allPoweredLightsAnimators[i].SetBool("on", turnOn);
			}
			yield return new WaitForSeconds(0.03f);
			b--;
		}
	}

	public void FlickerLights(bool flickerFlashlights = false, bool disableFlashlights = false)
	{
		if (flickerLightsCoroutine != null)
		{
			StopCoroutine(flickerLightsCoroutine);
		}
		flickerLightsCoroutine = StartCoroutine(FlickerPoweredLights(flickerFlashlights, disableFlashlights));
	}

	private IEnumerator FlickerPoweredLights(bool flickerFlashlights = false, bool disableFlashlights = false)
	{
		Debug.Log("Flickering lights");
		if (flickerFlashlights)
		{
			Debug.Log("Flickering flashlights");
			FlashlightItem.globalFlashlightInterferenceLevel = 1;
			FlashlightItem[] array = UnityEngine.Object.FindObjectsOfType<FlashlightItem>();
			if (array != null)
			{
				for (int i = 0; i < array.Length; i++)
				{
					array[i].flashlightAudio.PlayOneShot(array[i].flashlightFlicker);
					WalkieTalkie.TransmitOneShotAudio(array[i].flashlightAudio, array[i].flashlightFlicker, 0.8f);
					if (disableFlashlights && array[i].playerHeldBy != null && array[i].playerHeldBy.isInsideFactory)
					{
						array[i].flashlightInterferenceLevel = 2;
					}
				}
			}
		}
		if (allPoweredLightsAnimators.Count > 0 && allPoweredLightsAnimators[0] != null)
		{
			int loopCount = 0;
			int b = 4;
			while (b > 0 && b != 0)
			{
				for (int j = loopCount; j < allPoweredLightsAnimators.Count / b; j++)
				{
					loopCount++;
					allPoweredLightsAnimators[j].SetTrigger("Flicker");
				}
				yield return new WaitForSeconds(0.05f);
				b--;
			}
		}
		if (!flickerFlashlights)
		{
			yield break;
		}
		yield return new WaitForSeconds(0.3f);
		FlashlightItem[] array2 = UnityEngine.Object.FindObjectsOfType<FlashlightItem>();
		if (array2 != null)
		{
			for (int k = 0; k < array2.Length; k++)
			{
				array2[k].flashlightInterferenceLevel = 0;
			}
		}
		FlashlightItem.globalFlashlightInterferenceLevel = 0;
	}

	private void Start()
	{
		RefreshLightsList();
		RefreshEnemyVents();
		timeScript = UnityEngine.Object.FindObjectOfType<TimeOfDay>();
		FlashlightItem.globalFlashlightInterferenceLevel = 0;
		navHit = default(NavMeshHit);
		if (StartOfRound.Instance.testRoom != null)
		{
			outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
		}
		storedPathCorners = new Vector3[1500];
		tempPlayersArray = new PlayerControllerB[StartOfRound.Instance.allPlayerScripts.Length];
	}

	private void ResetEnemyTypesSpawnedCounts()
	{
		EnemyAI[] array = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
		for (int i = 0; i < currentLevel.Enemies.Count; i++)
		{
			currentLevel.Enemies[i].enemyType.numberSpawned = 0;
			currentLevel.Enemies[i].enemyType.hasSpawnedAtLeastOne = false;
			for (int j = 0; j < array.Length; j++)
			{
				if (array[j].enemyType == currentLevel.Enemies[i].enemyType)
				{
					currentLevel.Enemies[i].enemyType.numberSpawned++;
				}
			}
		}
		for (int k = 0; k < currentLevel.OutsideEnemies.Count; k++)
		{
			currentLevel.OutsideEnemies[k].enemyType.numberSpawned = 0;
			currentLevel.OutsideEnemies[k].enemyType.hasSpawnedAtLeastOne = false;
			for (int l = 0; l < array.Length; l++)
			{
				if (array[l].enemyType == currentLevel.OutsideEnemies[k].enemyType)
				{
					currentLevel.OutsideEnemies[k].enemyType.numberSpawned++;
				}
			}
		}
	}

	private void RefreshEnemiesList()
	{
		SpawnedEnemies.Clear();
		EnemyAI[] array = UnityEngine.Object.FindObjectsOfType<EnemyAI>();
		SpawnedEnemies.AddRange(array);
		numberOfEnemiesInScene = array.Length;
		firstTimeSpawningEnemies = true;
		firstTimeSpawningOutsideEnemies = true;
		firstTimeSpawningDaytimeEnemies = true;
		firstTimeSpawningWeedEnemies = true;
		currentMaxInsideDiversityLevel = currentLevel.maxInsideDiversityPowerCount;
		currentMaxOutsideDiversityLevel = currentLevel.maxOutsideDiversityPowerCount;
		if (StartOfRound.Instance.isChallengeFile)
		{
			System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 5781);
			currentMaxInsidePower = currentLevel.maxEnemyPowerCount + random.Next(0, 8);
			currentMaxOutsidePower = currentLevel.maxOutsideEnemyPowerCount + random.Next(0, 8);
			enemyRushIndex = -1;
			return;
		}
		enemyRushIndex = -1;
		currentMaxInsidePower = currentLevel.maxEnemyPowerCount;
		DateTime dateTime = new DateTime(DateTime.Now.Year, 10, 23);
		bool num = DateTime.Now.Day == dateTime.Day && DateTime.Now.Month == dateTime.Month;
		System.Random random2 = new System.Random(StartOfRound.Instance.randomMapSeed + 5781);
		if ((num && random2.Next(0, 110) < 5) || random2.Next(0, 1000) < 4)
		{
			Debug.Log("Invasion chance has been met!");
			indoorFog.gameObject.SetActive(random2.Next(0, 100) < 20);
			if (random2.Next(0, 100) < 25)
			{
				Debug.Log("Nutcracker invasion enabled");
				for (int i = 0; i < currentLevel.Enemies.Count; i++)
				{
					if (currentLevel.Enemies[i].enemyType.enemyName == "Nutcracker")
					{
						enemyRushIndex = i;
						currentMaxInsidePower = 20f;
						break;
					}
				}
				if (enemyRushIndex == -1)
				{
					Debug.Log("Hoarding bug invasion enabled");
					for (int j = 0; j < currentLevel.Enemies.Count; j++)
					{
						if (currentLevel.Enemies[j].enemyType.enemyName == "Hoarding bug")
						{
							enemyRushIndex = j;
							currentMaxInsidePower = 30f;
							break;
						}
					}
				}
			}
			else
			{
				for (int k = 0; k < currentLevel.Enemies.Count; k++)
				{
					if (currentLevel.Enemies[k].enemyType.enemyName == "Hoarding bug")
					{
						enemyRushIndex = k;
						currentMaxInsidePower = 30f;
						break;
					}
				}
			}
		}
		else
		{
			indoorFog.gameObject.SetActive(random2.Next(0, 150) < 3);
		}
		currentMaxOutsidePower = currentLevel.maxOutsideEnemyPowerCount;
	}

	private void Update()
	{
		if (!base.IsServer || !dungeonFinishedGeneratingForAllPlayers)
		{
			return;
		}
		if (isSpawningEnemies)
		{
			SpawnInsideEnemiesFromVentsIfReady();
			if (timeScript.hour > currentHour && currentEnemySpawnIndex >= enemySpawnTimes.Count)
			{
				AdvanceHourAndSpawnNewBatchOfEnemies();
			}
		}
		else if (timeScript.currentDayTime > 85f && !begunSpawningEnemies)
		{
			begunSpawningEnemies = true;
			BeginEnemySpawning();
		}
	}

	private void SpawnInsideEnemiesFromVentsIfReady()
	{
		if (enemySpawnTimes.Count <= currentEnemySpawnIndex || !(timeScript.currentDayTime > (float)enemySpawnTimes[currentEnemySpawnIndex]))
		{
			return;
		}
		for (int i = 0; i < allEnemyVents.Length; i++)
		{
			if (allEnemyVents[i].occupied && timeScript.currentDayTime > allEnemyVents[i].spawnTime)
			{
				Debug.Log("Found enemy vent which has its time up: " + allEnemyVents[i].gameObject.name + ". Spawning " + allEnemyVents[i].enemyType.enemyName + " from vent.");
				SpawnEnemyFromVent(allEnemyVents[i]);
			}
		}
		currentEnemySpawnIndex++;
	}

	private void AdvanceHourAndSpawnNewBatchOfEnemies()
	{
		currentHour += hourTimeBetweenEnemySpawnBatches;
		SpawnDaytimeEnemiesOutside();
		SpawnEnemiesOutside();
		SpawnWeedEnemies();
		if (allEnemyVents.Length != 0 && !cannotSpawnMoreInsideEnemies)
		{
			currentEnemySpawnIndex = 0;
			if (StartOfRound.Instance.connectedPlayersAmount + 1 > 0 && TimeOfDay.Instance.daysUntilDeadline <= 2 && (((float)(valueOfFoundScrapItems / TimeOfDay.Instance.profitQuota) > 0.8f && TimeOfDay.Instance.normalizedTimeOfDay > 0.3f) || (float)valueOfFoundScrapItems / totalScrapValueInLevel > 0.65f || StartOfRound.Instance.daysPlayersSurvivedInARow >= 5) && minEnemiesToSpawn == 0)
			{
				Debug.Log("Min enemy spawn chance per hour set to 1!!!");
				minEnemiesToSpawn = 1;
			}
			PlotOutEnemiesForNextHour();
		}
		else
		{
			Debug.Log($"Could not spawn more enemies; vents #: {allEnemyVents.Length}. CannotSpawnMoreInsideEnemies: {cannotSpawnMoreInsideEnemies}");
		}
	}

	public void RefreshLightsList()
	{
		allPoweredLights.Clear();
		if (allPoweredLightsAnimators == null)
		{
			allPoweredLightsAnimators = new List<Animator>();
		}
		else
		{
			allPoweredLightsAnimators.Clear();
		}
		GameObject[] array = GameObject.FindGameObjectsWithTag("PoweredLight");
		if (array == null)
		{
			return;
		}
		List<Light> list = new List<Light>();
		bool flag = false;
		for (int i = 0; i < array.Length; i++)
		{
			Animator componentInChildren = array[i].GetComponentInChildren<Animator>();
			if (componentInChildren == null)
			{
				continue;
			}
			allPoweredLightsAnimators.Add(componentInChildren);
			array[i].GetComponentsInChildren(includeInactive: true, list);
			flag = false;
			for (int j = 0; j < list.Count; j++)
			{
				if (!list[j].CompareTag("IndirectLightSource"))
				{
					allPoweredLights.Add(list[j]);
					flag = true;
					continue;
				}
				list[j].enabled = IngamePlayerSettings.Instance.settings.advancedLightMode;
				if (flag)
				{
					break;
				}
			}
		}
		for (int k = 0; k < allPoweredLightsAnimators.Count; k++)
		{
			allPoweredLightsAnimators[k].SetFloat("flickerSpeed", UnityEngine.Random.Range(0.6f, 1.4f));
		}
	}

	public void UpdateIndirectLightMode(bool advanced = true)
	{
		if (allPoweredLightsAnimators == null || allPoweredLightsAnimators.Count <= 0)
		{
			return;
		}
		List<Light> list = new List<Light>();
		for (int i = 0; i < allPoweredLightsAnimators.Count; i++)
		{
			if (allPoweredLightsAnimators[i] == null)
			{
				continue;
			}
			allPoweredLightsAnimators[i].gameObject.GetComponentsInChildren(includeInactive: true, list);
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j].CompareTag("IndirectLightSource"))
				{
					list[j].enabled = advanced;
					break;
				}
			}
		}
	}

	public void RefreshEnemyVents()
	{
		allEnemyVents = UnityEngine.Object.FindObjectsOfType<EnemyVent>();
	}

	private void SpawnOutsideHazards()
	{
		System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 2);
		GetOutsideAINodes(getUnderwaterNodes: false);
		NavMeshHit navMeshHit = default(NavMeshHit);
		int num = 0;
		if (TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Rainy)
		{
			num = random.Next(5, 15);
			if (random.Next(0, 100) < 7)
			{
				num = random.Next(5, 30);
			}
			for (int i = 0; i < num; i++)
			{
				Vector3 position = outsideAINodes[random.Next(0, outsideAINodes.Length)].transform.position;
				Vector3 position2 = GetRandomNavMeshPositionInBoxPredictable(position, 30f, navMeshHit, random) + Vector3.up;
				GameObject gameObject = UnityEngine.Object.Instantiate(quicksandPrefab, position2, Quaternion.identity, mapPropsContainer.transform);
			}
		}
		int num2 = 0;
		List<(int, Vector3)> list = new List<(int, Vector3)>();
		spawnDenialPoints = GameObject.FindGameObjectsWithTag("SpawnDenialPoint");
		if (currentLevel.spawnableOutsideObjects != null)
		{
			for (int j = 0; j < currentLevel.spawnableOutsideObjects.Length; j++)
			{
				double num3 = random.NextDouble();
				num = (int)currentLevel.spawnableOutsideObjects[j].randomAmount.Evaluate((float)num3);
				if (increasedMapPropSpawnRateIndex == j)
				{
					num += 12;
				}
				if ((float)random.Next(0, 100) < 20f)
				{
					num *= 2;
				}
				for (int k = 0; k < num; k++)
				{
					int num4 = random.Next(0, outsideAINodes.Length);
					Vector3 position2 = GetRandomNavMeshPositionInBoxPredictable(outsideAINodes[num4].transform.position, 30f, navMeshHit, random);
					if (currentLevel.spawnableOutsideObjects[j].spawnableObject.spawnableFloorTags != null)
					{
						bool flag = false;
						if (Physics.Raycast(position2 + Vector3.up, Vector3.down, out var hitInfo, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
						{
							for (int l = 0; l < currentLevel.spawnableOutsideObjects[j].spawnableObject.spawnableFloorTags.Length; l++)
							{
								if (hitInfo.collider.transform.CompareTag(currentLevel.spawnableOutsideObjects[j].spawnableObject.spawnableFloorTags[l]))
								{
									flag = true;
									break;
								}
							}
						}
						if (!flag)
						{
							continue;
						}
					}
					position2 = PositionEdgeCheck(position2, currentLevel.spawnableOutsideObjects[j].spawnableObject.objectWidth);
					if (position2 == Vector3.zero)
					{
						continue;
					}
					bool flag2 = false;
					for (int m = 0; m < shipSpawnPathPoints.Length; m++)
					{
						if (Vector3.Distance(shipSpawnPathPoints[m].transform.position, position2) < (float)currentLevel.spawnableOutsideObjects[j].spawnableObject.objectWidth + 6f)
						{
							flag2 = true;
							break;
						}
					}
					if (flag2)
					{
						continue;
					}
					for (int n = 0; n < spawnDenialPoints.Length; n++)
					{
						if (Vector3.Distance(spawnDenialPoints[n].transform.position, position2) < (float)currentLevel.spawnableOutsideObjects[j].spawnableObject.objectWidth + 6f)
						{
							flag2 = true;
							break;
						}
					}
					if (flag2)
					{
						continue;
					}
					if (Vector3.Distance(GameObject.FindGameObjectWithTag("ItemShipLandingNode").transform.position, position2) < (float)currentLevel.spawnableOutsideObjects[j].spawnableObject.objectWidth + 4f)
					{
						flag2 = true;
						break;
					}
					if (flag2)
					{
						continue;
					}
					if (currentLevel.spawnableOutsideObjects[j].spawnableObject.objectWidth > 4)
					{
						flag2 = false;
						for (int num5 = 0; num5 < list.Count; num5++)
						{
							float num6 = Vector3.Distance(position2, list[num5].Item2);
							if (num6 < (float)currentLevel.spawnableOutsideObjects[j].spawnableObject.objectWidth || num6 < (float)currentLevel.spawnableOutsideObjects[list[num5].Item1].spawnableObject.objectWidth)
							{
								flag2 = true;
								break;
							}
						}
						if (flag2)
						{
							continue;
						}
					}
					list.Add((j, position2));
					GameObject gameObject = UnityEngine.Object.Instantiate(currentLevel.spawnableOutsideObjects[j].spawnableObject.prefabToSpawn, position2 - Vector3.up * 0.7f, Quaternion.identity, mapPropsContainer.transform);
					num2++;
					if (currentLevel.spawnableOutsideObjects[j].spawnableObject.spawnFacingAwayFromWall)
					{
						gameObject.transform.eulerAngles = new Vector3(0f, YRotationThatFacesTheFarthestFromPosition(position2 + Vector3.up * 0.2f), 0f);
					}
					else
					{
						int num7 = random.Next(0, 360);
						gameObject.transform.eulerAngles = new Vector3(gameObject.transform.eulerAngles.x, num7, gameObject.transform.eulerAngles.z);
					}
					gameObject.transform.localEulerAngles = new Vector3(gameObject.transform.localEulerAngles.x + currentLevel.spawnableOutsideObjects[j].spawnableObject.rotationOffset.x, gameObject.transform.localEulerAngles.y + currentLevel.spawnableOutsideObjects[j].spawnableObject.rotationOffset.y, gameObject.transform.localEulerAngles.z + currentLevel.spawnableOutsideObjects[j].spawnableObject.rotationOffset.z);
				}
			}
			if (list.Count > 0)
			{
				List<GameObject> list2 = GameObject.FindGameObjectsWithTag("Tree").ToList();
				for (int num8 = 0; num8 < list.Count; num8++)
				{
					if (!currentLevel.spawnableOutsideObjects[list[num8].Item1].spawnableObject.destroyTrees)
					{
						continue;
					}
					for (int num9 = list2.Count - 1; num9 >= 0; num9--)
					{
						float num6 = Vector3.Distance(list2[num9].transform.position, list[num8].Item2);
						if (num6 < (float)currentLevel.spawnableOutsideObjects[list[num8].Item1].spawnableObject.objectWidth)
						{
							UnityEngine.Object.Destroy(list2[num9]);
							list2.RemoveAt(num9);
						}
					}
				}
			}
		}
		if (num2 > 0 || currentLevel.currentWeather == LevelWeatherType.Flooded)
		{
			GameObject gameObject2 = GameObject.FindGameObjectWithTag("OutsideLevelNavMesh");
			if (gameObject2 != null)
			{
				gameObject2.GetComponent<NavMeshSurface>().BuildNavMesh();
			}
		}
		bakedNavMesh = true;
		GetOutsideAINodes();
	}

	public Vector3 PositionEdgeCheck(Vector3 position, float width, int areaMask = -1, Vector3 originalPosition = default(Vector3))
	{
		if (areaMask == -1)
		{
			areaMask = -1;
		}
		if (NavMesh.FindClosestEdge(position, out navHit, areaMask) && navHit.distance < width)
		{
			Vector3 position2 = navHit.position;
			if (position2 == position)
			{
				if (!(originalPosition != default(Vector3)) || !(position2 != originalPosition))
				{
					return Vector3.zero;
				}
				position = originalPosition;
			}
			Vector3 vector = Vector3.Normalize(position - position2);
			if (NavMesh.SamplePosition(new Ray(position2, new Vector3(vector.x, Mathf.Clamp(vector.y, -0.5f, 0.5f), vector.z)).GetPoint(width + 0.5f), out navHit, 10f, areaMask))
			{
				position = navHit.position;
				return position;
			}
			return Vector3.zero;
		}
		return position;
	}

	private void SpawnRandomStoryLogs()
	{
	}

	public void SetLevelObjectVariables()
	{
		StartCoroutine(waitForMainEntranceTeleportToSpawn());
	}

	private IEnumerator waitForMainEntranceTeleportToSpawn()
	{
		float startTime = Time.timeSinceLevelLoad;
		while (FindMainEntrancePosition() == Vector3.zero && Time.timeSinceLevelLoad - startTime < 15f)
		{
			yield return new WaitForSeconds(1f);
		}
		Vector3 vector = FindMainEntrancePosition();
		SetLockedDoors(vector);
		SetSteamValveTimes(vector);
		SetBigDoorCodes(vector);
		SetExitIDs(vector);
		TurnOnAllLights(on: false);
		yield return new WaitForSeconds(6f);
		SetPowerOffAtStart();
	}

	private void SetPowerOffAtStart()
	{
		if (new System.Random(StartOfRound.Instance.randomMapSeed + 3).NextDouble() < 0.07999999821186066)
		{
			TurnBreakerSwitchesOff();
			if (!base.IsServer)
			{
				onPowerSwitch.Invoke(arg0: false);
				TurnOnAllLights(on: false);
			}
			Debug.Log("Turning lights off at start");
		}
		else
		{
			TurnOnAllLights(on: true);
			Debug.Log("Turning lights on at start");
		}
	}

	private void SetBigDoorCodes(Vector3 mainEntrancePosition)
	{
		System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 17);
		TerminalAccessibleObject[] array = (from x in UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>()
			orderby (x.transform.position - mainEntrancePosition).sqrMagnitude
			select x).ToArray();
		int num = 3;
		int num2 = 0;
		for (int num3 = 0; num3 < array.Length; num3++)
		{
			array[num3].InitializeValues();
			array[num3].SetCodeTo(random.Next(possibleCodesForBigDoors.Length));
			if (array[num3].isBigDoor && (num2 < num || random.NextDouble() < 0.2199999988079071))
			{
				num2++;
				array[num3].SetDoorOpen(open: true);
			}
		}
	}

	private void SetExitIDs(Vector3 mainEntrancePosition)
	{
		int num = 1;
		EntranceTeleport[] array = (from x in UnityEngine.Object.FindObjectsOfType<EntranceTeleport>()
			orderby (x.transform.position - mainEntrancePosition).sqrMagnitude
			select x).ToArray();
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			if (array[num2].entranceId == 1 && !array[num2].isEntranceToBuilding)
			{
				array[num2].entranceId = num;
				num++;
			}
		}
	}

	private void SetSteamValveTimes(Vector3 mainEntrancePosition)
	{
		System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 513);
		SteamValveHazard[] array = (from x in UnityEngine.Object.FindObjectsOfType<SteamValveHazard>()
			orderby (x.transform.position - mainEntrancePosition).sqrMagnitude
			select x).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			if (random.NextDouble() < 0.75)
			{
				array[num].valveBurstTime = Mathf.Clamp((float)random.NextDouble(), 0.2f, 1f);
				array[num].valveCrackTime = array[num].valveBurstTime * (float)random.NextDouble();
				array[num].fogSizeMultiplier = Mathf.Clamp((float)random.NextDouble(), 0.6f, 0.98f);
			}
			else if (random.NextDouble() < 0.25)
			{
				array[num].valveCrackTime = Mathf.Clamp((float)random.NextDouble(), 0.3f, 0.9f);
			}
		}
	}

	private void SetLockedDoors(Vector3 mainEntrancePosition)
	{
		if (mainEntrancePosition == Vector3.zero)
		{
			Debug.Log("Main entrance teleport was not spawned on local client within 12 seconds. Locking doors based on origin instead.");
		}
		List<DoorLock> list = UnityEngine.Object.FindObjectsOfType<DoorLock>().ToList();
		for (int num = list.Count - 1; num >= 0; num--)
		{
			if (list[num].transform.position.y > -160f || !list[num].canBeLocked)
			{
				list.RemoveAt(num);
			}
		}
		list = list.OrderByDescending((DoorLock x) => (mainEntrancePosition - x.transform.position).sqrMagnitude).ToList();
		float num2 = 1.1f;
		int num3 = 0;
		for (int num4 = 0; num4 < list.Count; num4++)
		{
			if (LevelRandom.NextDouble() < (double)num2)
			{
				float timeToLockPick = Mathf.Clamp(LevelRandom.Next(2, 90), 2f, 32f);
				list[num4].LockDoor(timeToLockPick);
				num3++;
			}
			num2 /= 1.55f;
		}
		num3 += 2;
		if (!base.IsServer)
		{
			return;
		}
		GameObject[] array;
		int maxValue;
		if (currentDungeonType != 4)
		{
			array = insideAINodes;
			maxValue = insideAINodes.Length;
		}
		else
		{
			insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
			array = insideAINodes.OrderBy((GameObject x) => Vector3.Distance(x.transform.position, mainEntrancePosition)).ToArray();
			maxValue = array.Length / 3;
		}
		for (int num5 = 0; num5 < num3; num5++)
		{
			int num6 = AnomalyRandom.Next(0, maxValue);
			Vector3 randomNavMeshPositionInBoxPredictable = GetRandomNavMeshPositionInBoxPredictable(array[num6].transform.position, 8f, navHit, AnomalyRandom);
			UnityEngine.Object.Instantiate(keyPrefab, randomNavMeshPositionInBoxPredictable, Quaternion.identity, spawnedScrapContainer).GetComponent<NetworkObject>().Spawn();
		}
	}

	public void DestroyTreeOnLocalClient(Vector3 pos)
	{
		if (DestroyTreeAtPosition(pos))
		{
			BreakTreeServerRpc(pos, (int)GameNetworkManager.Instance.localPlayerController.playerClientId);
		}
	}

	public bool DestroyTreeAtPosition(Vector3 pos, float range = 5f)
	{
		int num = Physics.OverlapSphereNonAlloc(pos, range, tempColliderResults, 33554432, QueryTriggerInteraction.Ignore);
		if (num == 0)
		{
			return false;
		}
		float num2 = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, pos);
		if (num2 < 15f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
		}
		else if (num2 < 25f)
		{
			HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
		}
		GameObject original;
		AudioClip clip;
		AudioClip clip2;
		if (tempColliderResults[0].gameObject.CompareTag("Snowman"))
		{
			original = breakSnowmanPrefab;
			clip = breakSnowmanAudio1;
			clip2 = breakSnowmanAudio1;
		}
		else
		{
			original = breakTreePrefab;
			clip = breakTreeAudio1;
			clip2 = breakTreeAudio2;
		}
		PumaAI pumaAI = UnityEngine.Object.FindObjectOfType<PumaAI>();
		for (int i = 0; i < num; i++)
		{
			if (pumaAI != null)
			{
				pumaAI.RemoveTree(tempColliderResults[i].gameObject);
			}
			UnityEngine.Object.Destroy(tempColliderResults[i].gameObject);
			AudioSource component = UnityEngine.Object.Instantiate(original, tempColliderResults[i].gameObject.transform.position + Vector3.up, Quaternion.identity).GetComponent<AudioSource>();
			if (UnityEngine.Random.Range(0, 20) < 10)
			{
				component.clip = clip;
			}
			else
			{
				component.clip = clip2;
			}
			component.Play();
		}
		return true;
	}

		[ServerRpc]
		public void TurnSnowmanServerRpc(Vector3 pos, Vector3 turnRotation, bool laugh)
		{
			TurnSnowmanClientRpc(pos, turnRotation, laugh);
		}

		[ClientRpc]
		public void TurnSnowmanClientRpc(Vector3 pos, Vector3 turnRotation, bool laugh)
		{
	int num = Physics.OverlapSphereNonAlloc(pos, 10f, tempColliderResults, 33554432, QueryTriggerInteraction.Ignore);
			for (int i = 0; i < num; i++)
			{
				if (!tempColliderResults[i].gameObject.CompareTag("Snowman"))
				{
					continue;
				}

				tempColliderResults[i].gameObject.transform.eulerAngles = turnRotation;
				if (!laugh)
				{
					continue;
				}

		AudioSource component = tempColliderResults[i].gameObject.GetComponent<AudioSource>();
				if ((bool)component)
				{
					if (UnityEngine.Random.Range(0, 100) < 7)
					{
						component.pitch = UnityEngine.Random.Range(0.2f, 0.4f);
					}

					component.pitch = UnityEngine.Random.Range(0.93f, 1.1f);
					component.PlayOneShot(snowmanLaughSFX[UnityEngine.Random.Range(0, snowmanLaughSFX.Length)]);
				}
			}
		}

		[ServerRpc(RequireOwnership = false)]
		public void BreakTreeServerRpc(Vector3 pos, int playerWhoSent)
		{
			BreakTreeClientRpc(pos, playerWhoSent);
		}

		[ClientRpc]
		public void BreakTreeClientRpc(Vector3 pos, int playerWhoSent)
		{
			if ((int)GameNetworkManager.Instance.localPlayerController.playerClientId != playerWhoSent)
			{
				DestroyTreeAtPosition(pos);
			}
		}

		[ServerRpc]
		public void LightningStrikeServerRpc(Vector3 strikePosition)
		{
			LightningStrikeClientRpc(strikePosition);
		}

		[ClientRpc]
		public void LightningStrikeClientRpc(Vector3 strikePosition)
		{
			UnityEngine.Object.FindObjectOfType<StormyWeather>(includeInactive: true).LightningStrike(strikePosition, useTargetedObject: true);
		}

		[ServerRpc]
		public void ShowStaticElectricityWarningServerRpc(NetworkObjectReference warningObject, float timeLeft)
		{
			ShowStaticElectricityWarningClientRpc(warningObject, timeLeft);
		}

		[ClientRpc]
		public void ShowStaticElectricityWarningClientRpc(NetworkObjectReference warningObject, float timeLeft)
		{
			if (warningObject.TryGet(out var networkObject))
			{
				UnityEngine.Object.FindObjectOfType<StormyWeather>(includeInactive: true).SetStaticElectricityWarning(networkObject, timeLeft);
			}
		}

	public Vector3 RandomlyOffsetPosition(Vector3 pos, float maxRadius, float padding = 1f)
	{
		tempTransform.position = pos;
		tempTransform.eulerAngles = Vector3.forward;
		for (int i = 0; i < 5; i++)
		{
			float num = UnityEngine.Random.Range(-180f, 180f);
			tempTransform.localEulerAngles = new Vector3(0f, tempTransform.localEulerAngles.y + num, 0f);
			Ray ray = new Ray(tempTransform.position, tempTransform.forward);
			if (Physics.Raycast(ray, out var hitInfo, 6f, 2304))
			{
				float num2 = hitInfo.distance - padding;
				if (num2 < 0f)
				{
					return ray.GetPoint(num2);
				}
				float distance = Mathf.Clamp(UnityEngine.Random.Range(0.1f, maxRadius), 0f, num2);
				return ray.GetPoint(distance);
			}
		}
		return pos;
	}

	public static Vector3 RandomPointInBounds(Bounds bounds)
	{
		return new Vector3(UnityEngine.Random.Range(bounds.min.x, bounds.max.x), UnityEngine.Random.Range(bounds.min.y, bounds.max.y), UnityEngine.Random.Range(bounds.min.z, bounds.max.z));
	}

	public Vector3 GetNavMeshPosition(Vector3 pos, NavMeshHit navMeshHit = default(NavMeshHit), float sampleRadius = 5f, int areaMask = -1)
	{
		if (NavMesh.SamplePosition(pos, out navMeshHit, sampleRadius, areaMask))
		{
			GotNavMeshPositionResult = true;
			return navMeshHit.position;
		}
		GotNavMeshPositionResult = false;
		return pos;
	}

	public Transform GetClosestNode(Vector3 pos, bool outside = true)
	{
		GameObject[] array;
		if (outside)
		{
			if (outsideAINodes == null)
			{
				outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
			}
			array = outsideAINodes;
		}
		else
		{
			if (insideAINodes == null)
			{
				insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
			}
			array = insideAINodes;
		}
		float num = 99999f;
		int num2 = 0;
		for (int i = 0; i < array.Length; i++)
		{
			float sqrMagnitude = (array[i].transform.position - pos).sqrMagnitude;
			if (sqrMagnitude < num)
			{
				num = sqrMagnitude;
				num2 = i;
			}
		}
		return array[num2].transform;
	}

	public Vector3 GetRandomNavMeshPositionInRadius(Vector3 pos, float radius = 10f, NavMeshHit navHit = default(NavMeshHit))
	{
		float y = pos.y;
		pos = UnityEngine.Random.insideUnitSphere * radius + pos;
		pos.y = y;
		if (NavMesh.SamplePosition(pos, out navHit, radius, -1))
		{
			return navHit.position;
		}
		return pos;
	}

	public Vector3 GetRandomNavMeshPositionInBoxPredictable(Vector3 pos, float radius = 10f, NavMeshHit navHit = default(NavMeshHit), System.Random randomSeed = null, int layerMask = -1, float verticalScale = 1f)
	{
		float y = pos.y;
		float x = RandomNumberInRadius(radius, randomSeed);
		float y2 = RandomNumberInRadius(radius * verticalScale, randomSeed);
		float z = RandomNumberInRadius(radius, randomSeed);
		Vector3 vector = new Vector3(x, y2, z) + pos;
		vector.y = y;
		randomPositionInBoxRadius = vector;
		float num = Vector3.Distance(pos, vector);
		if (NavMesh.SamplePosition(vector, out navHit, num + 2f, layerMask))
		{
			GotNavMeshPositionResult = true;
			return navHit.position;
		}
		GotNavMeshPositionResult = false;
		return pos;
	}

	public Vector3 GetRandomNavMeshPositionInBoxWithLimits(Vector3 pos, float minX, float maxX, float minZ, float maxZ, float minY, float maxY, float radius = 10f, NavMeshHit navHit = default(NavMeshHit), System.Random randomSeed = null, int layerMask = -1, float verticalRange = 3f)
	{
		minX = Mathf.Max(minX, pos.x - radius);
		minY = Mathf.Max(minY, pos.y - radius);
		minZ = Mathf.Max(minZ, pos.z - radius);
		maxX = Mathf.Min(maxX, pos.x + radius);
		maxY = Mathf.Min(maxY, pos.y + radius);
		maxZ = Mathf.Min(maxZ, pos.z + radius);
		float x = math.remap(0f, 1f, minX, maxX, (float)randomSeed.NextDouble());
		float z = math.remap(0f, 1f, minZ, maxZ, (float)randomSeed.NextDouble());
		float value = math.remap(0f, 1f, minY, maxY, (float)randomSeed.NextDouble());
		value = Mathf.Clamp(value, pos.y - verticalRange, pos.y + verticalRange);
		Vector3 vector = new Vector3(x, value, z);
		if (Physics.Raycast(vector, Vector3.down, out var hitInfo, 5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			vector = hitInfo.point;
		}
		float num = Vector3.Distance(pos, vector);
		if (NavMesh.SamplePosition(vector, out navHit, num + 2f, layerMask))
		{
			return navHit.position;
		}
		return pos;
	}

	public Vector3 GetRandomNavMeshPositionInBoxPredictable(Vector3 center, float radiusX = 10f, float radiusY = 10f, float radiusZ = 10f, NavMeshHit navHit = default(NavMeshHit), System.Random randomSeed = null, int layerMask = -1)
	{
		float y = center.y;
		float x = RandomNumberInRadius(radiusX, randomSeed);
		float y2 = RandomNumberInRadius(radiusY, randomSeed);
		float z = RandomNumberInRadius(radiusZ, randomSeed);
		Vector3 sourcePosition = new Vector3(x, y2, z) + center;
		sourcePosition.y = y;
		float maxDistance = 8f;
		if (NavMesh.SamplePosition(sourcePosition, out navHit, maxDistance, layerMask))
		{
			return navHit.position;
		}
		return Vector3.zero;
	}

	public Vector3 GetRandomPositionInBoxPredictable(Vector3 pos, float radius = 10f, System.Random randomSeed = null)
	{
		float x = RandomNumberInRadius(radius, randomSeed);
		float y = RandomNumberInRadius(radius, randomSeed);
		float z = RandomNumberInRadius(radius, randomSeed);
		return new Vector3(x, y, z) + pos;
	}

	private float RandomNumberInRadius(float radius, System.Random randomSeed)
	{
		return ((float)randomSeed.NextDouble() - 0.5f) * radius;
	}

	public Vector3 GetRandomNavMeshPositionInRadiusSpherical(Vector3 pos, float radius = 10f, NavMeshHit navHit = default(NavMeshHit))
	{
		pos = UnityEngine.Random.insideUnitSphere * radius + pos;
		if (NavMesh.SamplePosition(pos, out navHit, radius + 2f, 1))
		{
			Debug.DrawRay(pos + Vector3.forward * 0.01f, Vector3.up * 2f, Color.blue);
			return navHit.position;
		}
		Debug.DrawRay(pos + Vector3.forward * 0.01f, Vector3.up * 2f, Color.yellow);
		return pos;
	}

	public Vector3 GetRandomPositionInRadius(Vector3 pos, float minRadius, float radius, System.Random randomGen = null)
	{
		radius *= 2f;
		float num = RandomFloatWithinRadius(minRadius, radius, randomGen);
		float num2 = RandomFloatWithinRadius(minRadius, radius, randomGen);
		float num3 = RandomFloatWithinRadius(minRadius, radius, randomGen);
		return new Vector3(pos.x + num, pos.y + num2, pos.z + num3);
	}

	private float RandomFloatWithinRadius(float minValue, float radius, System.Random randomGenerator)
	{
		if (randomGenerator == null)
		{
			return UnityEngine.Random.Range(minValue, radius) * ((UnityEngine.Random.value > 0.5f) ? 1f : (-1f));
		}
		return (float)randomGenerator.Next((int)minValue, (int)radius) * ((randomGenerator.NextDouble() > 0.5) ? 1f : (-1f));
	}

	public static Vector3 AverageOfLivingGroupedPlayerPositions()
	{
		Vector3 zero = Vector3.zero;
		for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
		{
			if (StartOfRound.Instance.allPlayerScripts[i].isPlayerControlled && !StartOfRound.Instance.allPlayerScripts[i].isPlayerAlone)
			{
				zero += StartOfRound.Instance.allPlayerScripts[i].transform.position;
			}
		}
		return zero / (StartOfRound.Instance.connectedPlayersAmount + 1);
	}

	public void PlayAudibleNoise(Vector3 noisePosition, float noiseRange = 10f, float noiseLoudness = 0.5f, int timesPlayedInSameSpot = 0, bool noiseIsInsideClosedShip = false, int noiseID = 0)
	{
		if (noiseIsInsideClosedShip)
		{
			noiseRange /= 2f;
		}
		int num = Physics.OverlapSphereNonAlloc(noisePosition, noiseRange, tempColliderResults, 8912896);
		for (int i = 0; i < num; i++)
		{
			if (!tempColliderResults[i].transform.TryGetComponent<INoiseListener>(out var component))
			{
				continue;
			}
			if (noiseIsInsideClosedShip)
			{
				EnemyAI component2 = tempColliderResults[i].gameObject.GetComponent<EnemyAI>();
				if ((component2 == null || !component2.isInsidePlayerShip) && noiseLoudness < 0.9f)
				{
					continue;
				}
			}
			component.DetectNoise(noisePosition, noiseLoudness, timesPlayedInSameSpot, noiseID);
		}
	}

	public static int PlayRandomClip(AudioSource audioSource, AudioClip[] clipsArray, bool randomize = true, float oneShotVolume = 1f, int audibleNoiseID = 0, int maxIndex = 1000)
	{
		if (randomize)
		{
			audioSource.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
		}
		int num = UnityEngine.Random.Range(0, Mathf.Min(maxIndex, clipsArray.Length));
		audioSource.PlayOneShot(clipsArray[num], UnityEngine.Random.Range(oneShotVolume - 0.18f, oneShotVolume));
		WalkieTalkie.TransmitOneShotAudio(audioSource, clipsArray[num], 0.85f);
		if (audioSource.spatialBlend > 0f && audibleNoiseID >= 0)
		{
			Instance.PlayAudibleNoise(audioSource.transform.position, 4f * oneShotVolume, oneShotVolume / 2f, 0, noiseIsInsideClosedShip: true, audibleNoiseID);
		}
		return num;
	}

	public static EntranceTeleport FindMainEntranceScript(bool getOutsideEntrance = false)
	{
		EntranceTeleport[] array = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>(includeInactive: false);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].entranceId != 0)
			{
				continue;
			}
			if (!getOutsideEntrance)
			{
				if (!array[i].isEntranceToBuilding)
				{
					return array[i];
				}
			}
			else if (array[i].isEntranceToBuilding)
			{
				return array[i];
			}
		}
		if (array.Length == 0)
		{
			Debug.LogError("Main entrance was not spawned and could not be found; returning null");
			return null;
		}
		Debug.LogError("Main entrance script could not be found. Returning first entrance teleport script found.");
		return array[0];
	}

	public static Vector3 FindMainEntrancePosition(bool getTeleportPosition = false, bool getOutsideEntrance = false)
	{
		EntranceTeleport[] array = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>(includeInactive: false);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].entranceId != 0)
			{
				continue;
			}
			if (!getOutsideEntrance)
			{
				if (!array[i].isEntranceToBuilding)
				{
					if (getTeleportPosition)
					{
						return array[i].entrancePoint.position;
					}
					return array[i].transform.position;
				}
			}
			else if (array[i].isEntranceToBuilding)
			{
				if (getTeleportPosition)
				{
					return array[i].entrancePoint.position;
				}
				return array[i].transform.position;
			}
		}
		Debug.LogError("Main entrance position could not be found. Returning origin.");
		return Vector3.zero;
	}

	public int GetRandomWeightedIndex(int[] weights, System.Random randomSeed = null)
	{
		if (randomSeed == null)
		{
			randomSeed = AnomalyRandom;
		}
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
			return randomSeed.Next(0, weights.Length);
		}
		float num2 = (float)randomSeed.NextDouble();
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
		if (!hasInitializedLevelRandomSeed)
		{
			InitializeRandomNumberGenerators();
		}
		return randomSeed.Next(0, weights.Length);
	}

	public int GetRandomWeightedIndexList(List<int> weights, System.Random randomSeed = null)
	{
		if (weights == null || weights.Count == 0)
		{
			Debug.Log("Could not get random weighted index; array is empty or null.");
			return -1;
		}
		int num = 0;
		for (int i = 0; i < weights.Count; i++)
		{
			if (weights[i] >= 0)
			{
				num += weights[i];
			}
		}
		float num2 = ((randomSeed != null) ? ((float)randomSeed.NextDouble()) : UnityEngine.Random.value);
		float num3 = 0f;
		for (int i = 0; i < weights.Count; i++)
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
		Debug.LogError("Error while calculating random weighted index.");
		for (int i = 0; i < weights.Count; i++)
		{
			Debug.LogError($"{weights[i]},");
		}
		if (!hasInitializedLevelRandomSeed)
		{
			InitializeRandomNumberGenerators();
		}
		return randomSeed.Next(0, weights.Count);
	}

	public int GetWeightedValue(int indexLength)
	{
		return Mathf.Clamp(UnityEngine.Random.Range(0, indexLength * 2) - (indexLength - 1), 0, indexLength);
	}

	private static int SortBySize(int p1, int p2)
	{
		return p1.CompareTo(p2);
	}
}
