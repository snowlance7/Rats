using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/IndoorMapHazardType", order = 3)]
public class IndoorMapHazardType : ScriptableObject
{
	public GameObject prefabToSpawn;

	public bool spawnFacingAwayFromWall;

	public bool spawnFacingWall;

	[Space(3f)]
	public bool spawnWithBackToWall;

	public bool spawnWithBackFlushAgainstWall;

	[Space(2f)]
	public bool requireDistanceBetweenSpawns;

	public bool disallowSpawningNearEntrances;

	public bool allowInMineshaft = true;
}
