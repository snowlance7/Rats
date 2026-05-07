using System;
using UnityEngine;

[Serializable]
public class SpawnableEnemyWithRarity
{
	public EnemyType enemyType;

	[Range(0f, 200f)]
	public int rarity;

	public SpawnableEnemyWithRarity(EnemyType newEnemy, int newRarity)
	{
		enemyType = newEnemy;
		rarity = newRarity;
	}
}
