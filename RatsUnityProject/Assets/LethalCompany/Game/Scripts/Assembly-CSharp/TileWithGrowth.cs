using System;
using System.Collections.Generic;
using DunGen;
using DunGen.Tags;
using UnityEngine;

[Serializable]
public class TileWithGrowth
{
	public Tile tile;

	public int plantsInTile;

	public int tileCapacity = 40;

	public Vector3 lastPlantGrownPosition;

	public GameObject plantsContainer;

	public GameObject particlesContainer;

	public GameObject scanNodesContainer;

	public GameObject decalsContainer;

	public bool cannotSpread;

	public bool eradicated;

	public float eradicatedAtTime;

	public List<Vector3> plantPositions;

	public TileWithGrowth(Tile newTile, int DefaultTileCapacity, Tag CaveTileTag, Tag RoomTileTag, Tag TunnelTileTag, Vector3 startPlantPos)
	{
		tile = newTile;
		plantsInTile = 0;
		lastPlantGrownPosition = startPlantPos;
		if (RoundManager.Instance.currentDungeonType == 4)
		{
			if (tile.Tags.Tags.Contains(CaveTileTag))
			{
				tileCapacity = DefaultTileCapacity / 2;
			}
		}
		else if (!tile.Tags.Tags.Contains(RoomTileTag) && (RoundManager.Instance.currentDungeonType != 1 || !tile.Tags.Tags.Contains(TunnelTileTag)))
		{
			tileCapacity = DefaultTileCapacity / 2;
		}
		plantPositions = new List<Vector3>(tileCapacity);
	}
}
