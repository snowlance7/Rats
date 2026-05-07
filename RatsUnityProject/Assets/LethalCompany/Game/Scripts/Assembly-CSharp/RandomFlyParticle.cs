using System;
using UnityEngine;

public class RandomFlyParticle : GrabbableObject
{
	public GameObject flyPrefab;

	public GameObject badFlyPrefab;

	private AudioSource flyAudio;

	private ParticleSystem flyParticle;

	public override void InitializeAfterPositioning()
	{
		base.InitializeAfterPositioning();
		System.Random random = new System.Random((int)targetFloorPosition.x + (int)targetFloorPosition.y);
		if (random.NextDouble() < 0.25)
		{
			GameObject gameObject;
			if (random.NextDouble() < 0.10000000149011612)
			{
				gameObject = UnityEngine.Object.Instantiate(badFlyPrefab, base.transform.position, Quaternion.identity, base.transform);
				flyParticle = gameObject.GetComponent<ParticleSystem>();
				ParticleSystem.ShapeModule shape = gameObject.GetComponentsInChildren<ParticleSystem>()[1].shape;
				shape.meshRenderer = base.gameObject.GetComponent<MeshRenderer>();
			}
			else
			{
				gameObject = UnityEngine.Object.Instantiate(flyPrefab, base.transform.position, Quaternion.identity, base.transform);
				flyParticle = gameObject.GetComponent<ParticleSystem>();
			}
			flyAudio = gameObject.GetComponent<AudioSource>();
			if (flyAudio != null)
			{
				flyAudio.pitch = UnityEngine.Random.Range(0.9f, 1.5f);
				flyAudio.volume = UnityEngine.Random.Range(0.5f, 1f);
				flyAudio.Play();
			}
		}
	}

	public override void PocketItem()
	{
		base.PocketItem();
		if (flyParticle != null)
		{
			flyParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
		if (flyAudio != null)
		{
			flyAudio.volume *= 0.35f;
		}
	}

	public override void EquipItem()
	{
		base.EquipItem();
		if (flyParticle != null)
		{
			flyParticle.Play(withChildren: true);
		}
		if (flyAudio != null)
		{
			flyAudio.volume = UnityEngine.Random.Range(0.5f, 1f);
		}
	}

	public override void DiscardItem()
	{
		base.DiscardItem();
		if (flyParticle != null && !flyParticle.isPlaying)
		{
			flyParticle.Play(withChildren: true);
		}
		if (flyAudio != null)
		{
			flyAudio.volume = UnityEngine.Random.Range(0.5f, 1f);
		}
	}
}
