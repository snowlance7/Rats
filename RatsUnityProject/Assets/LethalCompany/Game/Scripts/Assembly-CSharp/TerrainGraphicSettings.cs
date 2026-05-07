using UnityEngine;

public class TerrainGraphicSettings : MonoBehaviour
{
	private Terrain thisTerrain;

	[Header("Detail distance")]
	public float TerrainDetailDistance_Ultra;

	public float TerrainDetailDistance_High;

	public float TerrainDetailDistance_Medium;

	public float TerrainDetailDistance_Low;

	private void Start()
	{
		thisTerrain = GetComponent<Terrain>();
		UpdateTerrainSettingsToMatchGraphics(IngamePlayerSettings.Instance.settings.terrainGrassDistance);
	}

	public void UpdateTerrainSettingsToMatchGraphics(int graphicsLevel)
	{
		switch (graphicsLevel)
		{
		case 0:
			thisTerrain.detailObjectDistance = 150f;
			thisTerrain.detailObjectDensity = 0.096f;
			thisTerrain.heightmapPixelError = 5f;
			break;
		case 1:
			thisTerrain.detailObjectDistance = TerrainDetailDistance_High;
			thisTerrain.detailObjectDensity = 0.096f;
			thisTerrain.heightmapPixelError = 10f;
			break;
		case 2:
			thisTerrain.detailObjectDistance = TerrainDetailDistance_Medium;
			thisTerrain.detailObjectDensity = 0.077f;
			thisTerrain.heightmapPixelError = 45f;
			break;
		case 3:
			thisTerrain.detailObjectDistance = TerrainDetailDistance_Low;
			thisTerrain.detailObjectDensity = 0.05f;
			thisTerrain.heightmapPixelError = 70f;
			break;
		}
	}
}
