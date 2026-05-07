using System;
using UnityEngine;

[Serializable]
public class OverrideEnemyRarity
{
	public EnemyType overrideEnemy;

	[Range(0f, 1f)]
	public float percentageChance;
}
