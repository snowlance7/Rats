using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class BatchAllMeshChildren : MonoBehaviour
{
	public bool onlyRenderOutdoors = true;

	public bool onlyRenderIndoors;

	public int Instances;

	public Mesh mesh;

	public Material material;

	public List<List<Matrix4x4>> Batches = new List<List<Matrix4x4>>();

	public bool batchManually;

	private bool batchedChildren;

	public bool savePositionArray;

	public List<Vector3> batchedPositions = new List<Vector3>();

	public List<int> batchedPositionTiles;

	public int amountOfMatrices;

	private void RenderBatches(bool renderOnHeadmountedCam = false)
	{
		Camera camera = StartOfRound.Instance.activeCamera;
		if (renderOnHeadmountedCam)
		{
			camera = StartOfRound.Instance.mapScreen.headMountedCam;
		}
		if (!camera.enabled)
		{
			return;
		}
		foreach (List<Matrix4x4> batch in Batches)
		{
			for (int i = 0; i < mesh.subMeshCount; i++)
			{
				Graphics.DrawMeshInstanced(mesh, i, material, batch, null, ShadowCastingMode.On, receiveShadows: true, 24, camera);
			}
		}
	}

	private void Update()
	{
		if (StartOfRound.Instance.activeCamera == null)
		{
			return;
		}
		bool flag = StartOfRound.Instance.activeCamera.transform.position.y < -100f;
		bool renderOnHeadmountedCam = false;
		if (onlyRenderOutdoors && flag)
		{
			return;
		}
		if (onlyRenderIndoors && !flag)
		{
			if (!GameNetworkManager.Instance.localPlayerController.isInHangarShipRoom || !StartOfRound.Instance.mapScreen.enableHeadMountedCam || !(StartOfRound.Instance.mapScreen.headMountedCam.transform.position.y < -100f))
			{
				return;
			}
			renderOnHeadmountedCam = true;
		}
		if (batchedChildren)
		{
			RenderBatches(renderOnHeadmountedCam);
		}
	}

	private void Start()
	{
		if (!batchManually)
		{
			BatchChildren();
		}
	}

	public void ClearBatchedMeshes()
	{
		Batches.Clear();
		batchedPositions = null;
		batchedChildren = false;
	}

	public (int batch, int batchIndex) GetWeedPositionInMatrixList(int positionIndex)
	{
		Debug.Log($"Input: {positionIndex}");
		if (Batches != null)
		{
			Debug.Log($"Batches: {Batches.Count}");
		}
		if (Batches.Count > 0 && Batches != null)
		{
			Debug.Log($"Amount in batch #0: {Batches[0].Count}");
		}
		Debug.Log($"GetWeedPositionInMatrixList Returning: ({positionIndex / 1000}, {positionIndex % 1000})");
		return (batch: positionIndex / 1000, batchIndex: positionIndex % 1000);
	}

	public (int batch, int batchIndex, int plantType) GetWeedPositionInMatrixListForCadaverPlants(int positionIndex, int plantType)
	{
		Debug.Log($"Input: {positionIndex}");
		if (Batches != null)
		{
			Debug.Log($"Batches: {Batches.Count}");
		}
		if (Batches.Count > 0 && Batches != null)
		{
			Debug.Log($"Amount in batch #0: {Batches[0].Count}");
		}
		Debug.Log($"GetWeedPositionInMatrixList Returning: ({positionIndex / 1000}, {positionIndex % 1000})");
		return (batch: positionIndex / 1000, batchIndex: positionIndex % 1000, plantType: plantType);
	}

	public void DestroyWeedAtIndexInMatrixList(int positionIndex)
	{
		Debug.Log($"Input: {positionIndex} -- {batchedPositions[positionIndex]}");
		if (Batches != null)
		{
			Debug.Log($"Batches: {Batches.Count}");
		}
		if (Batches.Count > 0 && Batches != null)
		{
			Debug.Log($"Amount in batch #0: {Batches[0].Count}");
		}
		Debug.Log($"GetWeedPositionInMatrixList Returning: Batches[{positionIndex / 1000}][{positionIndex % 1000}]");
		int num = positionIndex / 1000;
		int index = positionIndex % 1000;
		Debug.Log($"Destroying position: {Batches[num][index].GetPosition()}");
		Batches[num].RemoveAt(index);
		if (Batches[num].Count <= 0)
		{
			Batches.RemoveAt(num);
		}
		else
		{
			for (int i = num; i < Batches.Count; i++)
			{
				if (num >= Batches.Count - 1)
				{
					break;
				}
				Batches[i].Add(Batches[i + 1][0]);
				Batches[i + 1].RemoveAt(0);
			}
		}
		if (batchedPositionTiles != null && batchedPositionTiles.Count > positionIndex)
		{
			batchedPositionTiles.RemoveAt(positionIndex);
		}
		batchedPositions.RemoveAt(positionIndex);
	}

	public (int, int) AddToBatchedChildren(MeshFilter mf, bool savePositionArray, int GrowthTileIndex = -1)
	{
		if (mf.sharedMesh == mesh)
		{
			Transform transform = mf.transform;
			if (savePositionArray)
			{
				_ = mf.transform.position;
			}
			float num = amountOfMatrices % 1000 + 1;
			if (num < 1000f && Batches.Count != 0)
			{
				Batches[Batches.Count - 1].Add(transform.localToWorldMatrix);
				num += 1f;
				amountOfMatrices++;
			}
			else
			{
				Batches.Add(new List<Matrix4x4>());
				Batches[Batches.Count - 1].Add(transform.localToWorldMatrix);
				num = 0f;
			}
			Object.Destroy(transform.gameObject);
			if (savePositionArray)
			{
				batchedPositions.Add(transform.position);
				if (GrowthTileIndex != -1)
				{
					if (batchedPositionTiles == null)
					{
						batchedPositionTiles = new List<int>();
					}
					batchedPositionTiles.Add(GrowthTileIndex);
				}
			}
			batchedChildren = true;
			return (Batches.Count - 1, Batches[Batches.Count - 1].Count - 1);
		}
		Debug.Log("Plant Bfail");
		Debug.LogError("Attempted to add mesh to BatchAllMeshChildren in '" + base.gameObject.name + "' which did not match the component's mesh");
		return (-1, -1);
	}

	public void BatchChildren()
	{
		if (batchedChildren)
		{
			ClearBatchedMeshes();
			batchedChildren = false;
		}
		int num = 0;
		List<Transform> list = new List<Transform>();
		List<Vector3> list2 = new List<Vector3>();
		MeshFilter[] array = GetComponentsInChildren<MeshFilter>(includeInactive: true);
		if (batchManually)
		{
			array = array.OrderByDescending((MeshFilter x) => Vector3.Distance(x.transform.position, StartOfRound.Instance.shipLandingPosition.position)).ToArray();
		}
		MeshFilter[] array2 = array;
		foreach (MeshFilter meshFilter in array2)
		{
			if (meshFilter.sharedMesh == mesh)
			{
				list.Add(meshFilter.transform);
				if (savePositionArray)
				{
					list2.Add(meshFilter.transform.position);
				}
			}
		}
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			if (num < 1000 && Batches.Count != 0)
			{
				Batches[Batches.Count - 1].Add(list[num3].localToWorldMatrix);
				num++;
				amountOfMatrices++;
			}
			else
			{
				Batches.Add(new List<Matrix4x4>());
				Batches[Batches.Count - 1].Add(list[num3].localToWorldMatrix);
				num = 0;
			}
		}
		foreach (Transform item in list)
		{
			Object.Destroy(item.gameObject);
		}
		if (savePositionArray)
		{
			batchedPositions = list2;
		}
		batchedChildren = true;
	}
}
