using System;
using UnityEngine;

[Serializable]
public class SpawnableItemWithRarity
{
	public Item spawnableItem;

	[Range(0f, 100f)]
	public int rarity;

	public SpawnableItemWithRarity(Item newItem, int newRarity)
	{
		spawnableItem = newItem;
		rarity = newRarity;
	}
}
