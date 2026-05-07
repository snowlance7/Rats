using Unity.Netcode;
using UnityEngine;

public class HighAndLowAltitudeAudio : MonoBehaviour
{
	public AudioSource HighAudio;

	public AudioSource LowAudio;

	public float maxAltitude;

	public float minAltitude;

	public bool transitionFromDayToNight;

	public AudioSource stopAudioAtTime;

	public float normalizedDayTimeForEvent = 0.7f;

	public AudioSource NightAudio;

	private void OnEnable()
	{
		Debug.Log("Subscribe to startedLandingShip");
		StartOfRound.Instance.StartedLandingShip += StartAudios;
	}

	private void OnDisable()
	{
		StartOfRound.Instance.StartedLandingShip -= StartAudios;
	}

	private void StartAudios()
	{
		Debug.Log($"Start audios played from highandlowaudio; {HighAudio.isPlaying} ; {LowAudio.isPlaying}");
		if (!HighAudio.isPlaying)
		{
			HighAudio.Play();
		}
		if (!LowAudio.isPlaying)
		{
			LowAudio.Play();
		}
	}

	private void Update()
	{
		if (GameNetworkManager.Instance.localPlayerController == null || NetworkManager.Singleton == null)
		{
			return;
		}
		if (GameNetworkManager.Instance.localPlayerController.isInsideFactory)
		{
			HighAudio.volume = 0f;
			LowAudio.volume = 0f;
			if (NightAudio != null)
			{
				NightAudio.volume = 0f;
			}
		}
		else if (!GameNetworkManager.Instance.localPlayerController.isPlayerDead)
		{
			SetAudioVolumeBasedOnAltitude(GameNetworkManager.Instance.localPlayerController.transform.position.y);
		}
		else if (GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript != null)
		{
			SetAudioVolumeBasedOnAltitude(GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript.transform.position.y);
		}
	}

	private void SetAudioVolumeBasedOnAltitude(float playerHeight)
	{
		if (NightAudio == null && transitionFromDayToNight)
		{
			HighAudio.volume = Mathf.Lerp(1f, 0f, TimeOfDay.Instance.normalizedTimeOfDay);
			LowAudio.volume = Mathf.Abs(HighAudio.volume - 1f);
			if (stopAudioAtTime.isPlaying && TimeOfDay.Instance.currentDayTimeStarted && TimeOfDay.Instance.normalizedTimeOfDay > normalizedDayTimeForEvent)
			{
				stopAudioAtTime.Stop();
			}
			return;
		}
		float num = Mathf.Clamp((playerHeight - minAltitude) / maxAltitude, 0f, 1f);
		float num2 = Mathf.Abs(HighAudio.volume - 1f);
		if (StartOfRound.Instance.activeCamera.transform.position.y < -100f)
		{
			num = 0f;
			num2 = 0f;
			HighAudio.volume = Mathf.Lerp(HighAudio.volume, num, 5f * Time.deltaTime);
			LowAudio.volume = Mathf.Lerp(LowAudio.volume, num2, 5f * Time.deltaTime);
			if (NightAudio != null)
			{
				NightAudio.volume = Mathf.Lerp(NightAudio.volume, 0f, Time.deltaTime * 5f);
			}
			return;
		}
		if (TimeOfDay.Instance.insideLighting)
		{
			num *= 0.25f;
			num2 *= 0.25f;
		}
		if (NightAudio != null && TimeOfDay.Instance.currentDayTimeStarted && TimeOfDay.Instance.normalizedTimeOfDay > 0.2f)
		{
			num = Mathf.Max(Mathf.Lerp(num, -0.3f, TimeOfDay.Instance.normalizedTimeOfDay), 0f);
			num2 = Mathf.Max(Mathf.Lerp(num2, -0.1f, TimeOfDay.Instance.normalizedTimeOfDay), 0f);
			if (!NightAudio.isPlaying && TimeOfDay.Instance.normalizedTimeOfDay > 0.4f)
			{
				NightAudio.Play();
			}
			else
			{
				float num3 = Mathf.Clamp(TimeOfDay.Instance.normalizedTimeOfDay + 0.25f, 0f, 1f);
				if (TimeOfDay.Instance.insideLighting)
				{
					NightAudio.volume = Mathf.Lerp(NightAudio.volume, num3 * 0.25f, Time.deltaTime * 3f);
				}
				else
				{
					NightAudio.volume = Mathf.Lerp(NightAudio.volume, num3, Time.deltaTime * 0.75f);
				}
			}
		}
		HighAudio.volume = Mathf.Lerp(HighAudio.volume, num, 2f * Time.deltaTime);
		LowAudio.volume = Mathf.Lerp(LowAudio.volume, num2, 2f * Time.deltaTime);
	}
}
