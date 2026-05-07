using System.Collections.Generic;
using UnityEngine;

public class SpawnGrassOnMesh : MonoBehaviour
{
	public bool onlyRenderOutdoors = true;

	public Mesh mesh;

	public Material material;

	private List<List<Matrix4x4>> Batches = new List<List<Matrix4x4>>();

	private List<Vector3> ChunkPositions = new List<Vector3>();

	public int numberOfBlades = 1000;

	public float minScale = 0.8f;

	public float maxScale = 1.2f;

	public float minVerticalScale = 0.5f;

	public float spawnOffset = 0.05f;

	public float maxSlope = 76f;

	public float chunkDrawDistance = 70f;

	public int cellSize;

	private Mesh terrainMesh;

	public Collider terrainMeshBounds;

	private MaterialPropertyBlock grassVariation;

	public bool spawnManually;

	private bool spawnedGrass;

	public void ClearGrassData()
	{
		ChunkPositions.Clear();
		Batches.Clear();
		spawnedGrass = false;
	}

	public void SpawnGrassManually()
	{
		SpawnGrass();
	}

	private void Start()
	{
		terrainMesh = GetComponent<MeshFilter>().mesh;
		if (mesh == null || terrainMesh == null)
		{
			Debug.LogError("GrassSpawner requires a Mesh component");
			base.enabled = false;
			return;
		}
		grassVariation = new MaterialPropertyBlock();
		if (!spawnManually)
		{
			SpawnGrass();
		}
	}

	private void Update()
	{
		if ((!onlyRenderOutdoors || !(GameNetworkManager.Instance != null) || !(GameNetworkManager.Instance.localPlayerController != null) || !GameNetworkManager.Instance.localPlayerController.isInsideFactory) && spawnedGrass)
		{
			RenderBatches();
		}
	}

	private void RenderBatches()
	{
		int num = 1;
		Camera camera = ((!(StartOfRound.Instance != null) || !(StartOfRound.Instance.activeCamera != null)) ? Camera.main : StartOfRound.Instance.activeCamera);
		foreach (List<Matrix4x4> batch in Batches)
		{
			if (num >= 0 && num < ChunkPositions.Count)
			{
				Vector3 vector = new Vector3(ChunkPositions[num].x - (float)cellSize / 2f, camera.transform.position.y, ChunkPositions[num].z - (float)cellSize / 2f);
				Debug.DrawLine(vector, vector + Vector3.up * 10f, Color.yellow);
				Debug.DrawLine(ChunkPositions[num], ChunkPositions[num] + Vector3.up * 10f, Color.red);
				float num2 = Vector3.Distance(camera.transform.position, new Vector3(ChunkPositions[num].x - (float)cellSize / 2f, camera.transform.position.y, ChunkPositions[num].z - (float)cellSize / 2f));
				if (num2 > chunkDrawDistance)
				{
					num++;
					continue;
				}
				if (num2 > (float)cellSize + 5f && Vector3.Angle(camera.transform.forward, vector - camera.transform.position) > 110f)
				{
					num++;
					continue;
				}
			}
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				Graphics.DrawMeshInstanced(mesh, i, material, batch);
			}
			num++;
		}
	}

	private void SpawnGrass()
	{
		List<Vector3> list = new List<Vector3>(terrainMesh.vertices);
		List<int> list2 = new List<int>(terrainMesh.triangles);
		Transform transform = base.transform;
		List<Vector3> list3 = new List<Vector3>();
		List<int> list4 = new List<int>();
		for (int i = 0; i < list.Count; i++)
		{
			Vector3 point = base.transform.TransformPoint(list[i]);
			if (terrainMeshBounds.bounds.Contains(point))
			{
				list4.Add(i);
				list3.Add(list[i]);
			}
		}
		List<int> list5 = new List<int>();
		for (int j = 0; j < list2.Count; j += 3)
		{
			int item = list2[j];
			int item2 = list2[j + 1];
			int item3 = list2[j + 2];
			if (list4.Contains(item) && list4.Contains(item2) && list4.Contains(item3))
			{
				list5.Add(item);
				list5.Add(item2);
				list5.Add(item3);
			}
		}
		int num = 0;
		_ = Vector3.zero;
		_ = Vector3.zero;
		List<Matrix4x4> list6 = new List<Matrix4x4>();
		for (int k = 0; k < numberOfBlades; k++)
		{
			num = Random.Range(0, list5.Count / 3);
			int index = list5[num * 3];
			int index2 = list5[num * 3 + 1];
			int index3 = list5[num * 3 + 2];
			Vector3 vector = transform.TransformPoint(list[index]);
			Vector3 vector2 = transform.TransformPoint(list[index2]);
			Vector3 vector3 = transform.TransformPoint(list[index3]);
			Vector3 randomPointInTriangle = GetRandomPointInTriangle(vector, vector2, vector3);
			Vector3 normalized = Vector3.Cross(vector2 - vector, vector3 - vector).normalized;
			if (Vector3.Angle(normalized, Vector3.up) > maxSlope)
			{
				num++;
			}
			else
			{
				list6.Add(Matrix4x4.TRS(randomPointInTriangle + normalized * spawnOffset, Quaternion.AngleAxis(Random.Range(0f, 360f), normalized) * Quaternion.LookRotation(normalized, Vector3.up), Vector3.Scale(Vector3.one, new Vector3(1f, Random.Range(minVerticalScale, 1f), 1f)) * Random.Range(minScale, maxScale)));
			}
		}
		Bounds bounds = terrainMeshBounds.bounds;
		Vector3 vector4 = bounds.min + new Vector3((float)cellSize / 2f, 0f, (float)cellSize / 2f);
		Debug.Log($"bounds sizes {bounds.size.x}; {bounds.size.z} ; {cellSize}");
		int num2 = Mathf.FloorToInt(bounds.size.x / (float)cellSize);
		int num3 = Mathf.FloorToInt(bounds.size.z / (float)cellSize);
		for (int l = 0; l < num2; l++)
		{
			for (int m = 0; m < num3; m++)
			{
				Vector3 vector5 = vector4 + new Vector3(l * cellSize, 0f, m * cellSize);
				ChunkPositions.Add(vector5);
				Debug.DrawLine(vector5, vector5 + Vector3.up * 10f, Color.red);
			}
		}
		int num4 = 0;
		Vector3 zero = Vector3.zero;
		int num5 = 0;
		int num6 = 0;
		Random.ColorHSV();
		for (int n = 1; n < ChunkPositions.Count; n++)
		{
			num6++;
			if (num6 > num3)
			{
				num6 = 0;
				num5++;
			}
			Batches.Add(new List<Matrix4x4>());
			num4 = 0;
			Random.ColorHSV();
			for (int num7 = list6.Count - 1; num7 >= 0; num7--)
			{
				zero = list6[num7].GetPosition();
				if (!(zero.z > ChunkPositions[n].z) && !(zero.x > ChunkPositions[n].x) && (num5 <= 0 || !(zero.x < ChunkPositions[n - num3].x)) && (num6 == 0 || !(zero.z < ChunkPositions[n - 1].z)))
				{
					if (num4 > 1000)
					{
						num4 = 0;
						break;
					}
					Batches[Batches.Count - 1].Add(list6[num7]);
					list6.RemoveAt(num7);
				}
			}
		}
		spawnedGrass = true;
	}

	private Vector3 GetRandomPointInTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
	{
		float num = Random.Range(0f, 1f);
		float num2 = Random.Range(0f, 1f);
		if (num + num2 > 1f)
		{
			num = 1f - num;
			num2 = 1f - num2;
		}
		return p1 + num * (p2 - p1) + num2 * (p3 - p1);
	}
}
