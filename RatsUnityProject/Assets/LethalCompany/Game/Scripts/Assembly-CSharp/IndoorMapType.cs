using System;
using DunGen.Graph;
using UnityEngine;

[Serializable]
public class IndoorMapType
{
	public DungeonFlow dungeonFlow;

	public float MapTileSize;

	public AudioClip firstTimeAudio;

	public Vector3 restrictBounds = Vector3.zero;

	public int cullingTileDepth = 6;

	public IndoorMapType(DungeonFlow newFlow, float newTileSize, AudioClip newFirstTime)
	{
		dungeonFlow = newFlow;
		MapTileSize = newTileSize;
		firstTimeAudio = newFirstTime;
	}
}
