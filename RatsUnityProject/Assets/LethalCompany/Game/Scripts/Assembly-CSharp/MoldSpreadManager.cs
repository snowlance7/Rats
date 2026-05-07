using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class MoldSpreadManager : NetworkBehaviour
{
	private bool finishedGeneratingMold;

	public PlanetMoldState[] planetMoldStates;

	public List<GameObject> generatedMold = new List<GameObject>();

	public List<Vector2> allAttemptedMolds = new List<Vector2>();

	public GameObject moldPrefab;

	public Transform moldContainer;

	public BatchAllMeshChildren grassInstancer;

	public int moldBranchCount = 3;

	public int maxSporesInSingleIteration = 25;

	public int maxIterations = 25;

	public int moldDistance = 9;

	private Collider[] weedColliders;

	public GameObject destroyParticle;

	public AudioSource destroyAudio;

	public int iterationsThisDay;

	public Vector3 mostHiddenPosition;

	public Vector3 aggressivePosition;

	public int[] moldPositions;

	public GameObject weedColliderPrefab;

	private int mostSurroundingSpores;

	public int generatedAmountThisDay;

	public Material[] moldMaterials;

	private void Start()
	{
		moldPositions = new int[40];
		for (int i = 0; i < moldPositions.Length; i++)
		{
			moldPositions[i] = -1;
		}
		grassInstancer = UnityEngine.Object.FindObjectOfType<BatchAllMeshChildren>();
		planetMoldStates = new PlanetMoldState[StartOfRound.Instance.levels.Length];
		for (int j = 0; j < planetMoldStates.Length; j++)
		{
			planetMoldStates[j] = new PlanetMoldState();
			if (base.IsServer)
			{
				planetMoldStates[j].destroyedMold = ES3.Load($"Level{StartOfRound.Instance.levels[j].levelID}DestroyedMold", GameNetworkManager.Instance.currentSaveFileName, new int[0]).ToList();
			}
			else
			{
				planetMoldStates[j].destroyedMold = new List<int>();
			}
		}
		Debug.Log($"planet mold states length: {planetMoldStates.Length}");
		weedColliders = new Collider[3];
	}

	public void ResetMoldData()
	{
		RemoveAllMold();
		for (int i = 0; i < planetMoldStates.Length; i++)
		{
			planetMoldStates[i].destroyedMold.Clear();
		}
	}

	public void SyncDestroyedMoldPositions(int[] destroyedMoldSpots)
	{
		if (!base.IsServer)
		{
			Debug.Log($"Sync A; {destroyedMoldSpots.Length}");
			for (int i = 0; i < destroyedMoldSpots.Length; i++)
			{
				Debug.Log($"Sync B{i}; {destroyedMoldSpots[i]}");
				AddToDestroyedMoldList(destroyedMoldSpots[i], 2f);
			}
		}
	}

	public void AddToDestroyedMoldList(int index, float radius = 1.5f, bool playEffect = false)
	{
		Debug.Log($"C {planetMoldStates[StartOfRound.Instance.currentLevelID] != null}");
		Debug.Log($"D {planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold != null}");
		if (!planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Contains(index))
		{
			planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Add(index);
		}
	}

	public bool DestroyMoldAtIndex(int batchNum, int positionIndex, bool playEffect = true, Vector3 checkWithPosition = default(Vector3))
	{
		Vector3 position = grassInstancer.Batches[batchNum][positionIndex].GetPosition();
		if (checkWithPosition != Vector3.zero && checkWithPosition != position)
		{
			return false;
		}
		Debug.Log($"Destroying mold at index. Pos gotten from batches: {position} ; pos gotten from batchedPositions: {grassInstancer.batchedPositions[positionIndex + batchNum * 1000]}. (They should be equal)");
		grassInstancer.DestroyWeedAtIndexInMatrixList(positionIndex + batchNum * 1000);
		if (playEffect)
		{
			UnityEngine.Object.Instantiate(destroyParticle, position, Quaternion.identity, null);
			destroyAudio.Stop();
			destroyAudio.transform.position = position;
			destroyAudio.Play();
			RoundManager.Instance.PlayAudibleNoise(destroyAudio.transform.position, 6f, 0.5f, 0, noiseIsInsideClosedShip: false, 99611);
		}
		return true;
	}

	public void DestroyMoldAtPosition(Vector3 pos, bool playEffect = false)
	{
		List<Vector3> batchedPositions = grassInstancer.batchedPositions;
		int allMoldPositionsInRadius = GetAllMoldPositionsInRadius(pos, 2f, fillArray: true);
		Debug.Log($"weeds found at pos {pos}: {allMoldPositionsInRadius}");
		for (int i = 0; i < allMoldPositionsInRadius; i++)
		{
			for (int j = 0; j < allAttemptedMolds.Count; j++)
			{
				if (allAttemptedMolds[j].x == batchedPositions[moldPositions[i]].x && allAttemptedMolds[j].y == batchedPositions[moldPositions[i]].z)
				{
					if (!planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Contains(j))
					{
						planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Add(j);
					}
					break;
				}
			}
			if (playEffect)
			{
				Vector3 position = batchedPositions[moldPositions[i]] + Vector3.up * 0.5f;
				UnityEngine.Object.Instantiate(destroyParticle, position, Quaternion.identity, null);
				destroyAudio.Stop();
				destroyAudio.transform.position = position;
				destroyAudio.Play();
				RoundManager.Instance.PlayAudibleNoise(destroyAudio.transform.position, 6f, 0.5f, 0, noiseIsInsideClosedShip: false, 99611);
			}
		}
		int[] array = moldPositions.OrderByDescending((int x) => x).ToArray();
		for (int num = 0; num < array.Length && array[num] >= 0 && array[num] < batchedPositions.Count; num++)
		{
			grassInstancer.DestroyWeedAtIndexInMatrixList(array[num]);
		}
		CheckIfAllSporesDestroyed();
	}

	private void CheckIfAllSporesDestroyed()
	{
		if (grassInstancer.batchedPositions.Count <= 0)
		{
			StartOfRound.Instance.currentLevel.moldSpreadIterations = 0;
			StartOfRound.Instance.currentLevel.moldStartPosition = -1;
		}
	}

	private Vector3 ChooseMoldSpawnPosition(Vector3 pos, int xOffset, int zOffset)
	{
		pos += new Vector3(xOffset, 0f, zOffset);
		pos = RoundManager.Instance.GetNavMeshPosition(pos, default(NavMeshHit), 12f, 1);
		if (!RoundManager.Instance.GotNavMeshPositionResult)
		{
			Debug.Log($"Mold: Could not get nav mesh position within 12 meters of {pos}");
			Debug.DrawRay(pos, Vector3.up * 2f, Color.magenta, 5f);
			return Vector3.zero;
		}
		return pos;
	}

	public void GenerateMold(Vector3 startingPosition, int iterations)
	{
		if (iterations == 0 || finishedGeneratingMold)
		{
			return;
		}
		iterationsThisDay = iterations;
		System.Random random = new System.Random((int)startingPosition.x + (int)startingPosition.z);
		Vector3 vector = startingPosition;
		if (Physics.Raycast(startingPosition, Vector3.down, out var hitInfo, 100f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
		{
			vector = new Vector3(Mathf.Round(hitInfo.point.x), Mathf.Round(hitInfo.point.y), Mathf.Round(hitInfo.point.z));
		}
		GameObject item = UnityEngine.Object.Instantiate(moldPrefab, vector, Quaternion.Euler(new Vector3(0f, UnityEngine.Random.Range(-180f, 180f), 0f)), moldContainer);
		generatedMold.Add(item);
		List<MoldSpore> list = new List<MoldSpore>();
		List<MoldSpore> list2 = new List<MoldSpore>();
		Vector3 zero = Vector3.zero;
		list.Add(new MoldSpore(vector, marked: false, 0));
		int num = 0;
		bool flag = true;
		bool flag2 = false;
		iterations = Mathf.Min(iterations, maxIterations);
		List<MoldSpore> list3 = new List<MoldSpore>();
		for (int i = 0; i < iterations; i++)
		{
			GameObject gameObject = GameObject.FindGameObjectWithTag("MoldAttractionPoint");
			bool flag3 = gameObject != null;
			int num2 = Mathf.Min(list.Count, maxSporesInSingleIteration);
			for (int j = 0; j < num2; j++)
			{
				int num3 = random.Next(1, moldBranchCount);
				for (int k = 0; k < num3; k++)
				{
					int num4;
					int num5;
					if (flag3 && Vector3.Distance(list[j].spawnPosition, gameObject.transform.position) > 42f && Vector3.Distance(list[j].spawnPosition, StartOfRound.Instance.elevatorTransform.position) > 42f)
					{
						num4 = ((!(list[j].spawnPosition.x < gameObject.transform.position.x)) ? random.Next(-moldDistance, 2) : random.Next(-2, moldDistance));
						num5 = ((!(list[j].spawnPosition.z < gameObject.transform.position.z)) ? random.Next(-moldDistance, 2) : random.Next(-2, moldDistance));
					}
					else
					{
						num4 = random.Next(-moldDistance, moldDistance);
						num5 = random.Next(-moldDistance, moldDistance);
					}
					zero = ChooseMoldSpawnPosition(xOffset: (num4 >= 0) ? Mathf.Max(num4, 2) : Mathf.Min(num4, -2), zOffset: (num5 >= 0) ? Mathf.Max(num5, 2) : Mathf.Min(num5, -2), pos: list[j].spawnPosition);
					flag2 = zero != Vector3.zero;
					allAttemptedMolds.Add(new Vector2(zero.x, zero.z));
					bool flag4 = false;
					float num6 = (float)random.Next(75, 130) / 100f;
					num++;
					MoldSpore moldSpore = new MoldSpore(zero, flag4, num);
					bool flag5 = GetClosestMoldDistance(zero, hasNotFinishedGeneratingMold: true) < 0.05f;
					if (!flag5 && (planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Contains(num) || (list[j].destroyedByPlayer && i + 1 == iterations)))
					{
						list3.Add(moldSpore);
						moldSpore.destroyedByPlayer = true;
					}
					else if (!flag2 || list[j].markedForDestruction || flag5)
					{
						flag4 = true;
					}
					else
					{
						item = UnityEngine.Object.Instantiate(moldPrefab, zero, Quaternion.Euler(new Vector3(0f, UnityEngine.Random.Range(-180f, 180f), 0f)), moldContainer);
						item.transform.localScale = item.transform.localScale * num6;
						flag = false;
						generatedMold.Add(item);
					}
					moldSpore.markedForDestruction = flag4;
					list2.Add(moldSpore);
					if (num == 1)
					{
						Debug.DrawRay(zero, Vector3.up * 10f, Color.yellow, 10f);
					}
					if (moldSpore.destroyedByPlayer || moldSpore.markedForDestruction)
					{
						Debug.DrawRay(list[j].spawnPosition, zero - list[j].spawnPosition, Color.red, 10f);
					}
					else
					{
						Debug.DrawRay(list[j].spawnPosition, zero - list[j].spawnPosition, Color.green, 10f);
					}
				}
			}
			if (!flag)
			{
				list = new List<MoldSpore>(list2);
				list2.Clear();
				continue;
			}
			if (i == 0)
			{
				iterationsThisDay = 0;
				StartOfRound.Instance.currentLevel.moldSpreadIterations = 0;
				StartOfRound.Instance.currentLevel.moldStartPosition = -1;
			}
			break;
		}
		list.Clear();
		for (int l = 0; l < list3.Count; l++)
		{
			if (GetClosestMoldDistance(list3[l].spawnPosition, hasNotFinishedGeneratingMold: true) < 8f)
			{
				if (planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Contains(list3[l].generationId))
				{
					planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Remove(list3[l].generationId);
					list.Add(list3[l]);
				}
			}
			else if (!planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Contains(list3[l].generationId))
			{
				planetMoldStates[StartOfRound.Instance.currentLevelID].destroyedMold.Add(list3[l].generationId);
			}
		}
		for (int m = 0; m < list.Count; m++)
		{
			zero = list[m].spawnPosition;
			item = UnityEngine.Object.Instantiate(moldPrefab, zero, Quaternion.Euler(new Vector3(0f, UnityEngine.Random.Range(-180f, 180f), 0f)), moldContainer);
			item.transform.localScale = item.transform.localScale * 1f;
		}
		finishedGeneratingMold = true;
		if (!flag)
		{
			if (RoundManager.Instance.currentLevel.moldType >= 0 && RoundManager.Instance.currentLevel.moldType < moldMaterials.Length)
			{
				grassInstancer.material = moldMaterials[RoundManager.Instance.currentLevel.moldType];
			}
			grassInstancer.BatchChildren();
			GetBiggestWeedPatch();
			generatedAmountThisDay = generatedMold.Count;
		}
		else
		{
			if (generatedMold.Count == 1 && generatedMold[0] != null)
			{
				UnityEngine.Object.Destroy(generatedMold[0]);
			}
			generatedAmountThisDay = 0;
		}
		generatedMold.Clear();
	}

	public bool GetWeeds()
	{
		if (grassInstancer.batchedPositions == null || grassInstancer.batchedPositions.Count == 0)
		{
			Debug.Log("No game objects found with spore tag; cancelling");
			return false;
		}
		if (mostSurroundingSpores == 0)
		{
			Debug.Log("All spores found were lone spores; cancelling");
			return false;
		}
		return true;
	}

	public void GetBiggestWeedPatch(bool spawnNodeObjects = false)
	{
		List<Vector3> batchedPositions = grassInstancer.batchedPositions;
		if (batchedPositions == null || batchedPositions.Count == 0)
		{
			Debug.Log("No game objects found with spore tag; cancelling");
			return;
		}
		GameObject gameObject = GameObject.FindGameObjectWithTag("MoldAttractionPoint");
		mostSurroundingSpores = 0;
		int num = 0;
		List<Vector3> list = new List<Vector3>();
		Vector3 vector = batchedPositions[0];
		float radius = Mathf.Clamp((float)batchedPositions.Count * 0.5f, 5f, 15f);
		for (int i = 0; i < batchedPositions.Count; i++)
		{
			if (!((batchedPositions[i] - StartOfRound.Instance.shipLandingPosition.position).sqrMagnitude <= 784f))
			{
				num = GetAllMoldPositionsInRadius(batchedPositions[i], radius);
				if (num > mostSurroundingSpores)
				{
					mostSurroundingSpores = num;
					vector = batchedPositions[i];
				}
			}
		}
		if (mostSurroundingSpores == 0)
		{
			Debug.Log("All spores found were lone spores; cancelling");
			return;
		}
		for (int j = 0; j < batchedPositions.Count; j++)
		{
			if (!((batchedPositions[j] - StartOfRound.Instance.elevatorTransform.position).sqrMagnitude <= 784f))
			{
				num = GetAllMoldPositionsInRadius(batchedPositions[j], 4f);
				if (num > 3 || batchedPositions.Count <= 3)
				{
					list.Add(batchedPositions[j]);
				}
			}
		}
		for (int num2 = list.Count - 1; num2 >= 0; num2--)
		{
			for (int num3 = list.Count - 1; num3 >= 0; num3--)
			{
				if (!(list[num2] == list[num3]) && (list[num2] - list[num3]).sqrMagnitude <= 16f)
				{
					list.RemoveAt(num3);
					if (num3 < num2)
					{
						num2--;
					}
				}
			}
		}
		for (int k = 0; k < list.Count; k++)
		{
			UnityEngine.Object.Instantiate(weedColliderPrefab, list[k], Quaternion.identity, moldContainer.transform);
		}
		Debug.Log($"Number of weeds: {list.Count}");
		mostHiddenPosition = RoundManager.Instance.GetNavMeshPosition(vector, default(NavMeshHit), 8f);
		if (!(gameObject != null))
		{
			return;
		}
		if (Vector3.Distance(vector, gameObject.transform.position) > 35f)
		{
			float num4 = 90f;
			float num5 = 0f;
			Vector3 vector2 = Vector3.zero;
			for (int l = 0; l < list.Count; l++)
			{
				num5 = Vector3.Distance(list[l], gameObject.transform.position);
				if (num5 < num4)
				{
					num4 = num5;
					vector2 = list[l];
				}
			}
			if (vector2 != Vector3.zero)
			{
				aggressivePosition = RoundManager.Instance.GetNavMeshPosition(vector2, default(NavMeshHit), 8f);
				if (!RoundManager.Instance.GotNavMeshPositionResult)
				{
					aggressivePosition = mostHiddenPosition;
					Debug.Log("Found no aggressive position reason A");
				}
			}
			else
			{
				Debug.Log("Found no aggressive position reason B");
				aggressivePosition = mostHiddenPosition;
			}
		}
		else
		{
			aggressivePosition = mostHiddenPosition;
			Debug.Log("Found no aggressive position reason C");
		}
	}

	public int GetClosestMoldPositionIndex(Vector3 position)
	{
		List<Vector3> batchedPositions = grassInstancer.batchedPositions;
		Debug.Log($"Getting closest mold postiion to {position}");
		int result = 0;
		float num = 2000f;
		for (int i = 0; i < batchedPositions.Count; i++)
		{
			float num2 = Vector3.Distance(position, batchedPositions[i]);
			if (num2 < num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}

	public float GetClosestMoldDistance(Vector3 position)
	{
		List<Vector3> batchedPositions = grassInstancer.batchedPositions;
		float num = 2000f;
		for (int i = 0; i < batchedPositions.Count; i++)
		{
			float num2 = Vector3.Distance(position, batchedPositions[i]);
			if (num2 < num)
			{
				num = num2;
			}
		}
		return num;
	}

	public float GetClosestMoldDistance(Vector3 position, bool hasNotFinishedGeneratingMold)
	{
		float num = 2000f;
		for (int i = 0; i < generatedMold.Count; i++)
		{
			float num2 = Vector3.Distance(position, generatedMold[i].transform.position);
			if (num2 < num)
			{
				num = num2;
			}
		}
		return num;
	}

	public int GetAllMoldPositionsInRadius(Vector3 position, float radius, bool fillArray = false)
	{
		int num = 0;
		List<Vector3> batchedPositions = grassInstancer.batchedPositions;
		for (int i = 0; i < batchedPositions.Count; i++)
		{
			if (!((batchedPositions[i] - position).sqrMagnitude <= radius * radius))
			{
				continue;
			}
			if (fillArray)
			{
				moldPositions[num] = i;
				num++;
				if (num >= moldPositions.Length)
				{
					return moldPositions.Length;
				}
			}
			else
			{
				num++;
			}
		}
		if (fillArray)
		{
			for (int j = num; j < moldPositions.Length; j++)
			{
				moldPositions[j] = -1;
			}
		}
		return num;
	}

	public void RemoveAllMold()
	{
		if (generatedMold != null)
		{
			for (int i = 0; i < generatedMold.Count; i++)
			{
				if (generatedMold[i] != null)
				{
					UnityEngine.Object.Destroy(generatedMold[i]);
				}
			}
		}
		foreach (Transform item in moldContainer.transform)
		{
			UnityEngine.Object.Destroy(item.gameObject);
		}
		grassInstancer.ClearBatchedMeshes();
		finishedGeneratingMold = false;
	}
}
