using System;
using UnityEngine;
using UnityEngine.VFX;

[Serializable]
public class PlayerInfection
{
	public bool infected;

	public bool severe;

	public bool emittingSpores;

	public bool bloomOnDeath;

	public int healing;

	public float infectionMeter;

	public float multiplier = 1f;

	public float burstMeter;

	public GameObject backFlowers;

	public GameObject backFlowersScanNode;

	public MeshRenderer[] backFlowerRenderers;

	public VisualEffect faceSpores;

	public float faceSporesOutput;

	public bool hinderingPlayerMovement;
}
