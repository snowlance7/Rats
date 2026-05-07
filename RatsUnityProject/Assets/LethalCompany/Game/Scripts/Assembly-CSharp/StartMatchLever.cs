using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMatchLever : NetworkBehaviour
{
	public bool singlePlayerEnabled;

	public bool leverHasBeenPulled;

	public InteractTrigger triggerScript;

	public StartOfRound playersManager;

	public Animator leverAnimatorObject;

	private float updateInterval;

	private bool clientSentRPC;

	public bool hasDisplayedTimeWarning;

	private Coroutine shakeCoroutine;

	public void LeverAnimation()
	{
		Debug.Log("Start lever animation A");
		if (GameNetworkManager.Instance.localPlayerController.isPlayerDead)
		{
			return;
		}
		Debug.Log("Start lever animation B");
		if (playersManager.travellingToNewLevel)
		{
			return;
		}
		Debug.Log("Start lever animation C");
		if (playersManager.inShipPhase && playersManager.connectedPlayersAmount + 1 <= 1 && !singlePlayerEnabled)
		{
			return;
		}
		Debug.Log($"Start lever animation D; {playersManager.beganLoadingNewLevel} ");
		if (playersManager.beganLoadingNewLevel)
		{
			return;
		}
		Debug.Log($"Start lever animation E; {SceneManager.sceneCount > 0} ; {RoundManager.Instance.dungeonIsGenerating}");
		if (SceneManager.sceneCount <= 1 || !playersManager.inShipPhase)
		{
			Debug.Log("Start lever animation F");
			if (playersManager.shipHasLanded)
			{
				PullLeverAnim(leverPulled: false);
				clientSentRPC = true;
				PlayLeverPullEffectsServerRpc(leverPulled: false);
			}
			else if (playersManager.inShipPhase)
			{
				PullLeverAnim(leverPulled: true);
				clientSentRPC = true;
				SetStartingShipEffects();
				PlayLeverPullEffectsServerRpc(leverPulled: true);
			}
		}
	}

	private void PullLeverAnim(bool leverPulled)
	{
		Debug.Log($"Lever animation: setting bool to {leverPulled}");
		leverAnimatorObject.SetBool("pullLever", leverPulled);
		leverHasBeenPulled = leverPulled;
		triggerScript.interactable = false;
	}

		[ServerRpc(RequireOwnership = false)]
		public void PlayLeverPullEffectsServerRpc(bool leverPulled)
		{
			PlayLeverPullEffectsClientRpc(leverPulled);
		}

		[ClientRpc]
		private void PlayLeverPullEffectsClientRpc(bool leverPulled)
		{
			if (clientSentRPC)
			{
				clientSentRPC = false;
				Debug.Log("Sent lever animation RPC on this client");
				return;
			}

			PullLeverAnim(leverPulled);
			if (leverPulled)
			{
				SetStartingShipEffects();
			}
		}

	private void SetStartingShipEffects()
	{
		StartOfRound.Instance.startGameWhir.Play();
		StartOfRound.Instance.shipLandingAudio.pitch = Random.Range(0.92f, 1.08f);
		StartOfRound.Instance.shipDoorsClosingJingle.pitch = 1f;
		switch (Random.Range(1, 7))
		{
		case 1:
		case 2:
			StartOfRound.Instance.shipDoorsClosingJingle.pitch *= Mathf.Pow(1.05946f, 1f);
			break;
		case 3:
		case 4:
			StartOfRound.Instance.shipDoorsClosingJingle.pitch /= Mathf.Pow(1.05946f, 1f);
			break;
		}
		StartOfRound.Instance.securityCameraScreen.overrideCameraForOtherUse = true;
		StartOfRound.Instance.securityCameraScreen.cam.enabled = false;
		StartOfRound.Instance.insideCameraScreen.overrideCameraForOtherUse = true;
		StartOfRound.Instance.insideCameraScreen.cam.enabled = false;
		shakeCoroutine = StartCoroutine(startShakeOnDelay());
	}

	private IEnumerator startShakeOnDelay()
	{
		yield return new WaitForSeconds(2f);
		StartOfRound.Instance.shipLandingAudio.Play();
		int num = 0;
		GrabbableObject[] array = Object.FindObjectsOfType<GrabbableObject>();
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].itemProperties.isScrap)
			{
				num++;
				if (num > 6)
				{
					StartOfRound.Instance.shipLandingScrapAudio.Play();
					break;
				}
			}
		}
		HUDManager.Instance.ShakeCamera(ScreenShakeType.Constant);
		shakeCoroutine = null;
	}

	public void CancelShakingEffects()
	{
		if (shakeCoroutine != null)
		{
			StopCoroutine(shakeCoroutine);
			shakeCoroutine = null;
		}
	}

	private void CancelStartingShipEffects()
	{
		StartOfRound.Instance.startGameWhir.Stop();
		StartOfRound.Instance.shipLandingAudio.Stop();
		StartOfRound.Instance.securityCameraScreen.overrideCameraForOtherUse = false;
		StartOfRound.Instance.insideCameraScreen.overrideCameraForOtherUse = false;
		if (shakeCoroutine != null)
		{
			StopCoroutine(shakeCoroutine);
			shakeCoroutine = null;
		}
		HUDManager.Instance.StopShakingCamera();
	}

	public void PullLever()
	{
		if (leverHasBeenPulled)
		{
			StartGame();
		}
		else
		{
			EndGame();
		}
	}

	public void StartGame()
	{
		Debug.Log("Start game A");
		if (playersManager.travellingToNewLevel)
		{
			return;
		}
		Debug.Log("Start game B");
		if (!playersManager.inShipPhase)
		{
			return;
		}
		Debug.Log("Start game C");
		if (playersManager.beganLoadingNewLevel)
		{
			return;
		}
		Debug.Log($"Start game D; {SceneManager.sceneCount > 0} ; {RoundManager.Instance.dungeonIsGenerating}");
		if (SceneManager.sceneCount > 1 || RoundManager.Instance.dungeonIsGenerating)
		{
			return;
		}
		Debug.Log("Start game E");
		if (playersManager.connectedPlayersAmount + 1 <= 1 && !singlePlayerEnabled)
		{
			return;
		}
		Debug.Log("Start game successful");
		if (playersManager.fullyLoadedPlayers.Count >= playersManager.connectedPlayersAmount + 1)
		{
			if (!base.IsServer)
			{
				playersManager.StartGameServerRpc();
			}
			else
			{
				playersManager.StartGame();
			}
		}
		else
		{
			triggerScript.hoverTip = "[ Players are loading. ]";
			Debug.Log("Attempted to start the game while routing to a new planet");
			Debug.Log($"Number of loaded players: {playersManager.fullyLoadedPlayers}");
			updateInterval = 4f;
			CancelStartGame();
		}
	}

		[ClientRpc]
		public void CancelStartGameClientRpc()
		{
			CancelStartGame();
		}

	private void CancelStartGame()
	{
		CancelStartingShipEffects();
		leverHasBeenPulled = false;
		leverAnimatorObject.SetBool("pullLever", value: false);
	}

	public void EndGame()
	{
		if ((GameNetworkManager.Instance.localPlayerController.isPlayerDead || playersManager.shipHasLanded) && !playersManager.shipIsLeaving && !playersManager.shipLeftAutomatically)
		{
			triggerScript.interactable = false;
			playersManager.shipIsLeaving = true;
			playersManager.EndGameServerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId);
		}
	}

	public void BeginHoldingInteractOnLever()
	{
		if (playersManager.inShipPhase && !hasDisplayedTimeWarning && StartOfRound.Instance.currentLevel.planetHasTime)
		{
			hasDisplayedTimeWarning = true;
			if (TimeOfDay.Instance.daysUntilDeadline <= 0)
			{
				triggerScript.timeToHold = 4f;
				HUDManager.Instance.DisplayTip("HALT!", "You have 0 days left to meet the quota. Use the terminal to route to the company and sell.", isWarning: true);
			}
		}
	}

	private void Start()
	{
		if (!base.IsServer)
		{
			triggerScript.hoverTip = "[ Must be server host. ]";
			triggerScript.interactable = false;
		}
	}

	private void Update()
	{
		if (updateInterval <= 0f)
		{
			updateInterval = 2f;
			if (!leverHasBeenPulled)
			{
				if (!base.IsServer && !GameNetworkManager.Instance.gameHasStarted)
				{
					return;
				}
				if (playersManager.connectedPlayersAmount + 1 > 1 || singlePlayerEnabled)
				{
					if (GameNetworkManager.Instance.gameHasStarted)
					{
						triggerScript.hoverTip = "Land ship : [LMB]";
					}
					else
					{
						triggerScript.hoverTip = "Start game : [LMB]";
					}
				}
				else
				{
					triggerScript.hoverTip = "[ At least two players needed to start! ]";
				}
			}
			else
			{
				triggerScript.hoverTip = "Start ship : [LMB]";
			}
		}
		else
		{
			updateInterval -= Time.deltaTime;
		}
	}
}
