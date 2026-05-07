using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/SpawnableOutsideObject", order = 2)]
public class SpawnableOutsideObject : ScriptableObject
{
	public GameObject prefabToSpawn;

	public bool spawnFacingAwayFromWall;

	[Tooltip("This is used to determine how close this object can spawn to edges of the nav mesh.")]
	public int objectWidth;

	[Tooltip("Destroy trees if they are within object width.")]
	public bool destroyTrees;

	[Tooltip("If null, allows spawning this object on any surface.")]
	public string[] spawnableFloorTags;

	public Vector3 rotationOffset;
}
