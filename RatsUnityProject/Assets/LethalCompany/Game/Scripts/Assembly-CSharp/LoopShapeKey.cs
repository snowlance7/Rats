using UnityEngine;

public class LoopShapeKey : MonoBehaviour
{
	public AudioClip audioOn;

	public AudioClip audioOff;

	private bool playAudioOn;

	public AudioSource repeatingAudioSource;

	private float audioRepeatInterval = 0.9f;

	private float audioInterval;

	public GrabbableObject thisGrabbableObject;

	private float fearMultiplier;

	private SkinnedMeshRenderer skinnedMeshRenderer;

	private void Update()
	{
		if (skinnedMeshRenderer == null)
		{
			skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
		}
		if (thisGrabbableObject != null && StartOfRound.Instance != null)
		{
			if (thisGrabbableObject.isHeld && thisGrabbableObject.playerHeldBy != null && thisGrabbableObject.playerHeldBy == GameNetworkManager.Instance.localPlayerController)
			{
				fearMultiplier = Mathf.Lerp(fearMultiplier, Mathf.Clamp(StartOfRound.Instance.fearLevel, 0f, 1f) + 1f, Time.deltaTime * 16f);
			}
			else
			{
				fearMultiplier = 1f;
			}
			audioInterval -= Time.deltaTime;
			if (audioInterval <= 0f)
			{
				audioInterval = audioRepeatInterval / (fearMultiplier * 1.75f);
				if (audioInterval > 0.45f)
				{
					repeatingAudioSource.volume = 0.5f;
				}
				else
				{
					repeatingAudioSource.volume = 1f;
				}
				playAudioOn = !playAudioOn;
				if (playAudioOn)
				{
					repeatingAudioSource.clip = audioOn;
				}
				else
				{
					repeatingAudioSource.clip = audioOff;
				}
				repeatingAudioSource.Play();
			}
		}
		skinnedMeshRenderer.SetBlendShapeWeight(0, Mathf.PingPong(Time.time * 260f * (fearMultiplier * 0.6f), 100f));
		skinnedMeshRenderer.SetBlendShapeWeight(1, Mathf.PingPong(Time.time * 200f * (fearMultiplier * 0.6f), 100f));
	}
}
