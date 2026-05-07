using System;
using UnityEngine;

[Serializable]
public class SpawnableOutsideObjectWithRarity
{
	public SpawnableOutsideObject spawnableObject;

	public AnimationCurve randomAmount;

	public SpawnableOutsideObjectWithRarity(SpawnableOutsideObject newObject, AnimationCurve newCurve)
	{
		spawnableObject = newObject;
		randomAmount = newCurve;
	}
}
