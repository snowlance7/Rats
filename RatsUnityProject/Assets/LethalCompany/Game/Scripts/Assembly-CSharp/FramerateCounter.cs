using TMPro;
using UnityEngine;

public class FramerateCounter : MonoBehaviour
{
	public TextMeshProUGUI FPSCounter;

	private float time;

	private int frameCount;

	private void Update()
	{
		time += Time.deltaTime;
		frameCount++;
		if (time >= 0.05f)
		{
			int num = ((IngamePlayerSettings.Instance.unsavedSettings.framerateCapIndex == 1) ? Mathf.RoundToInt((float)frameCount / time) : ((IngamePlayerSettings.Instance.unsavedSettings.framerateCapIndex == 0) ? Mathf.Min(Mathf.RoundToInt((float)frameCount / time), (int)Screen.currentResolution.refreshRateRatio.value) : Mathf.Min(Mathf.RoundToInt((float)frameCount / time), Application.targetFrameRate)));
			FPSCounter.text = $"FPS: {num}";
			time = 0f;
			frameCount = 0;
		}
	}
}
