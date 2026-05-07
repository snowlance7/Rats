using System;
using System.Collections.Generic;
using System.Linq;
using DunGen;
using DunGen.Tags;
using UnityEngine;

[AddComponentMenu("DunGen/Culling/Adjacent Room Culling Modified")]
public class AdjacentRoomCullingModified : MonoBehaviour
{
	public delegate void VisibilityChangedDelegate(Tile tile, bool visible);

	public int AdjacentTileDepth = 1;

	public bool CullBehindClosedDoors = true;

	public Transform TargetOverride;

	public bool IncludeDisabledComponents;

	public Tag DisableCullingTag;

	[NonSerialized]
	public Dictionary<Renderer, bool> OverrideRendererVisibilities = new Dictionary<Renderer, bool>();

	[NonSerialized]
	public Dictionary<Light, bool> OverrideLightVisibilities = new Dictionary<Light, bool>();

	protected List<Dungeon> dungeons = new List<Dungeon>();

	protected List<Tile> allTiles = new List<Tile>();

	protected List<Door> allDoors = new List<Door>();

	protected List<Tile> oldVisibleTiles = new List<Tile>();

	protected List<Tile> visibleTiles = new List<Tile>();

	protected Dictionary<Tile, bool> tileVisibilities = new Dictionary<Tile, bool>();

	protected Dictionary<Tile, List<Renderer>> tileRenderers = new Dictionary<Tile, List<Renderer>>();

	protected Dictionary<Tile, List<Light>> lightSources = new Dictionary<Tile, List<Light>>();

	protected Dictionary<Tile, List<ReflectionProbe>> reflectionProbes = new Dictionary<Tile, List<ReflectionProbe>>();

	protected Dictionary<Door, List<Renderer>> doorRenderers = new Dictionary<Door, List<Renderer>>();

	private bool dirty;

	private DungeonGenerator generator;

	public Tile currentTile;

	private Queue<Tile> tilesToSearch;

	private List<Tile> searchedTiles;

	public bool Ready { get; protected set; }

	protected Transform targetTransform
	{
		get
		{
			if (!(TargetOverride != null))
			{
				return base.transform;
			}
			return TargetOverride;
		}
	}

	public event VisibilityChangedDelegate TileVisibilityChanged;

	protected virtual void OnEnable()
	{
		RuntimeDungeon runtimeDungeon = UnityUtil.FindObjectByType<RuntimeDungeon>();
		if (runtimeDungeon != null)
		{
			generator = runtimeDungeon.Generator;
			generator.OnGenerationComplete += OnDungeonGenerationComplete;
			if (generator.Status == GenerationStatus.Complete)
			{
				AddDungeon(generator.CurrentDungeon);
			}
		}
	}

	protected virtual void OnDisable()
	{
		if (generator != null)
		{
			generator.OnGenerationComplete -= OnDungeonGenerationComplete;
		}
		for (int i = 0; i < allTiles.Count; i++)
		{
			if (allTiles[i] != null)
			{
				SetTileVisibility(allTiles[i], visible: true);
			}
		}
		ClearAllDungeons();
	}

	public virtual void SetDungeon(Dungeon newDungeon)
	{
		if (!(newDungeon == null))
		{
			ClearAllDungeons();
			AddDungeon(newDungeon);
		}
	}

	public virtual void AddDungeon(Dungeon dungeon)
	{
		if (dungeon == null || dungeons.Contains(dungeon))
		{
			return;
		}
		dungeons.Add(dungeon);
		List<Tile> list = new List<Tile>(dungeon.AllTiles);
		List<Door> list2 = new List<Door>(GetAllDoorsInDungeon(dungeon));
		allTiles.AddRange(list);
		allDoors.AddRange(list2);
		UpdateRendererLists(list, list2);
		foreach (Tile item in list)
		{
			if (!item.Tags.Tags.Contains(DisableCullingTag))
			{
				SetTileVisibility(item, visible: false);
			}
		}
		foreach (Door item2 in list2)
		{
			item2.OnDoorStateChanged += OnDoorStateChanged;
			SetDoorVisibility(item2, visible: false);
		}
		Ready = true;
		dirty = true;
	}

	private void RemoveNullKeys<TKey, TValue>(ref Dictionary<TKey, TValue> dictionary)
	{
		TKey[] array = dictionary.Keys.Where((TKey val) => val == null).ToArray();
		foreach (TKey key in array)
		{
			if (dictionary.ContainsKey(key))
			{
				dictionary.Remove(key);
			}
		}
	}

	public virtual void RemoveDungeon(Dungeon dungeon)
	{
		if (dungeon == null || !dungeons.Contains(dungeon))
		{
			return;
		}
		dungeons.Remove(dungeon);
		allTiles.RemoveAll((Tile x) => !x);
		visibleTiles.RemoveAll((Tile x) => !x);
		allDoors.RemoveAll((Door x) => !x);
		RemoveNullKeys(ref tileVisibilities);
		RemoveNullKeys(ref tileRenderers);
		RemoveNullKeys(ref lightSources);
		RemoveNullKeys(ref reflectionProbes);
		RemoveNullKeys(ref doorRenderers);
		foreach (Tile allTile in dungeon.AllTiles)
		{
			SetTileVisibility(allTile, visible: true);
			allTiles.Remove(allTile);
			tileVisibilities.Remove(allTile);
			tileRenderers.Remove(allTile);
			lightSources.Remove(allTile);
			reflectionProbes.Remove(allTile);
			visibleTiles.Remove(allTile);
			oldVisibleTiles.Remove(allTile);
		}
		foreach (GameObject door in dungeon.Doors)
		{
			if (!(door == null) && door.TryGetComponent<Door>(out var component))
			{
				SetDoorVisibility(component, visible: true);
				component.OnDoorStateChanged -= OnDoorStateChanged;
				allDoors.Remove(component);
				doorRenderers.Remove(component);
			}
		}
		if (allTiles.Count == 0)
		{
			Ready = false;
		}
	}

	public virtual void ClearAllDungeons()
	{
		Ready = false;
		foreach (Door allDoor in allDoors)
		{
			if (allDoor != null)
			{
				allDoor.OnDoorStateChanged -= OnDoorStateChanged;
			}
		}
		dungeons.Clear();
		allTiles.Clear();
		visibleTiles.Clear();
		allDoors.Clear();
		oldVisibleTiles.Clear();
		tileVisibilities.Clear();
		tileRenderers.Clear();
		lightSources.Clear();
		reflectionProbes.Clear();
		doorRenderers.Clear();
	}

	public virtual bool IsTileVisible(Tile tile)
	{
		if (tileVisibilities.TryGetValue(tile, out var value))
		{
			return value;
		}
		return false;
	}

	protected IEnumerable<Door> GetAllDoorsInDungeon(Dungeon dungeon)
	{
		foreach (GameObject door in dungeon.Doors)
		{
			if (!(door == null))
			{
				Door component = door.GetComponent<Door>();
				if (component != null)
				{
					yield return component;
				}
			}
		}
	}

	protected virtual void OnDoorStateChanged(Door door, bool isOpen)
	{
		dirty = true;
	}

	protected virtual void OnDungeonGenerationComplete(DungeonGenerator generator)
	{
		if ((generator.AttachmentSettings == null || generator.AttachmentSettings.TileProxy == null) && dungeons.Count > 0)
		{
			RemoveDungeon(dungeons[dungeons.Count - 1]);
		}
		AddDungeon(generator.CurrentDungeon);
	}

	protected virtual void LateUpdate()
	{
		if (Ready)
		{
			Tile tile = currentTile;
			if (currentTile == null)
			{
				currentTile = FindCurrentTile();
			}
			else if (!currentTile.Bounds.Contains(targetTransform.position))
			{
				currentTile = SearchForNewCurrentTile();
			}
			if (currentTile != tile && currentTile != null)
			{
				dirty = true;
			}
			if (dirty)
			{
				RefreshVisibility();
			}
			dirty = false;
		}
	}

	public Tile GetStartTile()
	{
		for (int i = 0; i < allTiles.Count; i++)
		{
			if (allTiles[i].Placement.NormalizedPathDepth == 0f)
			{
				return allTiles[i];
			}
		}
		return null;
	}

	public void SetToStartTile()
	{
		if (!Ready)
		{
			return;
		}
		Tile tile = currentTile;
		if (RoundManager.Instance.dungeonGenerator == null)
		{
			Debug.LogError("RoundManager dungeon generator is null! Cannot set StartTile as current tile!");
			return;
		}
		for (int i = 0; i < allTiles.Count; i++)
		{
			if (allTiles[i] == RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.MainPathTiles[0])
			{
				Debug.Log("Adjacent room culling: Got start tile!");
				currentTile = allTiles[i];
				break;
			}
		}
		if (currentTile != tile && currentTile != null)
		{
			dirty = true;
		}
		if (dirty)
		{
			RefreshVisibility();
		}
		dirty = false;
	}

	protected virtual void RefreshVisibility()
	{
		List<Tile> list = visibleTiles;
		visibleTiles = oldVisibleTiles;
		oldVisibleTiles = list;
		UpdateVisibleTiles();
		foreach (Tile oldVisibleTile in oldVisibleTiles)
		{
			if (!visibleTiles.Contains(oldVisibleTile) && !oldVisibleTile.Tags.Tags.Contains(DisableCullingTag))
			{
				SetTileVisibility(oldVisibleTile, visible: false);
			}
		}
		foreach (Tile visibleTile in visibleTiles)
		{
			if (!oldVisibleTiles.Contains(visibleTile))
			{
				SetTileVisibility(visibleTile, visible: true);
			}
		}
		oldVisibleTiles.Clear();
		RefreshDoorVisibilities();
	}

	protected virtual void RefreshDoorVisibilities()
	{
		foreach (Door allDoor in allDoors)
		{
			bool visible = visibleTiles.Contains(allDoor.DoorwayA.Tile) || visibleTiles.Contains(allDoor.DoorwayB.Tile);
			SetDoorVisibility(allDoor, visible);
		}
	}

	protected virtual void SetDoorVisibility(Door door, bool visible)
	{
		if (!doorRenderers.TryGetValue(door, out var value))
		{
			return;
		}
		for (int num = value.Count - 1; num >= 0; num--)
		{
			Renderer renderer = value[num];
			bool value2;
			if (renderer == null)
			{
				value.RemoveAt(num);
			}
			else if (OverrideRendererVisibilities.TryGetValue(renderer, out value2))
			{
				renderer.enabled = value2;
			}
			else
			{
				renderer.enabled = visible;
			}
		}
	}

	protected virtual void UpdateVisibleTiles()
	{
		visibleTiles.Clear();
		if (currentTile != null)
		{
			visibleTiles.Add(currentTile);
		}
		int num = 0;
		for (int i = 0; i < AdjacentTileDepth; i++)
		{
			int count = visibleTiles.Count;
			for (int j = num; j < count; j++)
			{
				foreach (Doorway usedDoorway in visibleTiles[j].UsedDoorways)
				{
					Tile tile = usedDoorway.ConnectedDoorway.Tile;
					if (tile == null || visibleTiles.Contains(tile))
					{
						continue;
					}
					if (CullBehindClosedDoors)
					{
						Door doorComponent = usedDoorway.DoorComponent;
						if (doorComponent != null && doorComponent.ShouldCullBehind)
						{
							continue;
						}
					}
					visibleTiles.Add(tile);
				}
			}
			num = count;
		}
	}

	protected virtual void SetTileVisibility(Tile tile, bool visible)
	{
		tileVisibilities[tile] = visible;
		if (tileRenderers.TryGetValue(tile, out var value))
		{
			for (int num = value.Count - 1; num >= 0; num--)
			{
				Renderer renderer = value[num];
				bool value2;
				if (renderer == null)
				{
					value.RemoveAt(num);
				}
				else if (OverrideRendererVisibilities.TryGetValue(renderer, out value2))
				{
					renderer.enabled = value2;
				}
				else
				{
					renderer.enabled = visible;
				}
			}
		}
		if (lightSources.TryGetValue(tile, out var value3))
		{
			for (int num2 = value3.Count - 1; num2 >= 0; num2--)
			{
				Light light = value3[num2];
				bool value4;
				if (light == null)
				{
					value3.RemoveAt(num2);
				}
				else if (OverrideLightVisibilities.TryGetValue(light, out value4))
				{
					light.enabled = value4;
				}
				else
				{
					light.enabled = visible;
				}
			}
		}
		if (reflectionProbes.TryGetValue(tile, out var value5))
		{
			for (int num3 = value5.Count - 1; num3 >= 0; num3--)
			{
				ReflectionProbe reflectionProbe = value5[num3];
				if (reflectionProbe == null)
				{
					value5.RemoveAt(num3);
				}
				else
				{
					reflectionProbe.enabled = visible;
				}
			}
		}
		this.TileVisibilityChanged?.Invoke(tile, visible);
	}

	public virtual void UpdateRendererLists()
	{
		UpdateRendererLists(allTiles, allDoors);
	}

	protected void UpdateRendererLists(List<Tile> tiles, List<Door> doors)
	{
		foreach (Tile tile in tiles)
		{
			if (!tileRenderers.TryGetValue(tile, out var value))
			{
				value = (tileRenderers[tile] = new List<Renderer>());
			}
			else
			{
				value.Clear();
			}
			Renderer[] componentsInChildren = tile.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in componentsInChildren)
			{
				if (IncludeDisabledComponents || (renderer.enabled && renderer.gameObject.activeInHierarchy))
				{
					value.Add(renderer);
				}
			}
			if (!lightSources.TryGetValue(tile, out var value2))
			{
				value2 = (lightSources[tile] = new List<Light>());
			}
			else
			{
				value2.Clear();
			}
			Light[] componentsInChildren2 = tile.GetComponentsInChildren<Light>();
			foreach (Light light in componentsInChildren2)
			{
				if (IncludeDisabledComponents || (light.enabled && light.gameObject.activeInHierarchy))
				{
					value2.Add(light);
				}
			}
			if (!reflectionProbes.TryGetValue(tile, out var value3))
			{
				value3 = (reflectionProbes[tile] = new List<ReflectionProbe>());
			}
			else
			{
				value3.Clear();
			}
			ReflectionProbe[] componentsInChildren3 = tile.GetComponentsInChildren<ReflectionProbe>();
			foreach (ReflectionProbe reflectionProbe in componentsInChildren3)
			{
				if (IncludeDisabledComponents || (reflectionProbe.enabled && reflectionProbe.gameObject.activeInHierarchy))
				{
					value3.Add(reflectionProbe);
				}
			}
		}
		foreach (Door door in doors)
		{
			List<Renderer> list4 = new List<Renderer>();
			doorRenderers[door] = list4;
			Renderer[] componentsInChildren = door.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer2 in componentsInChildren)
			{
				if (IncludeDisabledComponents || (renderer2.enabled && renderer2.gameObject.activeInHierarchy))
				{
					list4.Add(renderer2);
				}
			}
		}
	}

	protected Tile FindCurrentTile()
	{
		foreach (Tile allTile in allTiles)
		{
			if (!(allTile == null) && allTile.Bounds.Contains(targetTransform.position))
			{
				return allTile;
			}
		}
		return null;
	}

	protected Tile SearchForNewCurrentTile()
	{
		if (RoundManager.Instance.startRoomSpecialBounds != null && RoundManager.Instance.startRoomSpecialBounds.bounds.Contains(targetTransform.position))
		{
			Tile startTile = GetStartTile();
			if (startTile != null)
			{
				return startTile;
			}
		}
		if (tilesToSearch == null)
		{
			tilesToSearch = new Queue<Tile>();
		}
		if (searchedTiles == null)
		{
			searchedTiles = new List<Tile>();
		}
		foreach (Doorway usedDoorway in currentTile.UsedDoorways)
		{
			Tile tile = usedDoorway.ConnectedDoorway.Tile;
			if (!(tile == null) && !tilesToSearch.Contains(tile))
			{
				tilesToSearch.Enqueue(tile);
			}
		}
		while (tilesToSearch.Count > 0)
		{
			Tile tile2 = tilesToSearch.Dequeue();
			if (tile2.Bounds.Contains(targetTransform.position))
			{
				tilesToSearch.Clear();
				searchedTiles.Clear();
				return tile2;
			}
			searchedTiles.Add(tile2);
			foreach (Doorway usedDoorway2 in tile2.UsedDoorways)
			{
				Tile tile3 = usedDoorway2.ConnectedDoorway.Tile;
				if (!(tile3 == null) && !tilesToSearch.Contains(tile3) && !searchedTiles.Contains(tile3))
				{
					tilesToSearch.Enqueue(tile3);
				}
			}
		}
		searchedTiles.Clear();
		return null;
	}
}
