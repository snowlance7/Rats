using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class SprayPaintItem : GrabbableObject
{
	public AudioSource sprayAudio;

	public AudioClip spraySFX;

	public AudioClip sprayNeedsShakingSFX;

	public AudioClip sprayStart;

	public AudioClip sprayStop;

	public AudioClip sprayCanEmptySFX;

	public AudioClip sprayCanNeedsShakingSFX;

	public AudioClip sprayCanShakeEmptySFX;

	public AudioClip[] sprayCanShakeSFX;

	public ParticleSystem sprayParticle;

	public ParticleSystem sprayCanNeedsShakingParticle;

	private bool isSpraying;

	private float sprayInterval;

	public float sprayIntervalSpeed = 0.2f;

	private Vector3 previousSprayPosition;

	public static List<GameObject> sprayPaintDecals = new List<GameObject>();

	public static int sprayPaintDecalsIndex;

	public GameObject sprayPaintPrefab;

	public int maxSprayPaintDecals = 1000;

	private float sprayCanTank = 1f;

	private float sprayCanShakeMeter;

	public static DecalProjector previousSprayDecal;

	private float shakingCanTimer;

	private bool tryingToUseEmptyCan;

	public Material[] sprayCanMats;

	public Material[] particleMats;

	private int sprayCanMatsIndex;

	private RaycastHit sprayHit;

	public bool debugSprayPaint;

	private int addSprayPaintWithFrameDelay;

	private DecalProjector delayedSprayPaintDecal;

	private int sprayPaintMask = 605030721;

	private bool makingAudio;

	private float audioInterval;

	[Space(5f)]
	public bool isWeedKillerSprayBottle;

	private Collider[] weedColliders;

	private float addVehicleHPInterval;

	private MoldSpreadManager moldManager;

	private (int batchNum, int positionIndex) killingWeed;

	private (int batchNum, int positionIndex, int plantType) killingCadaverPlant;

	private CadaverGrowthAI cadaverGrowthAI;

	private float sprayOnPlayerMeter;

	private bool healingPlayerInfection;

	public override void Start()
	{
		base.Start();
		weedColliders = new Collider[3];
		sprayHit = default(RaycastHit);
		killingWeed = (batchNum: -1, positionIndex: -1);
		killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
		if (isWeedKillerSprayBottle)
		{
			moldManager = UnityEngine.Object.FindObjectOfType<MoldSpreadManager>();
			return;
		}
		System.Random random = new System.Random(StartOfRound.Instance.randomMapSeed + 151);
		sprayCanMatsIndex = random.Next(0, sprayCanMats.Length);
		sprayParticle.GetComponent<ParticleSystemRenderer>().material = particleMats[sprayCanMatsIndex];
		sprayCanNeedsShakingParticle.GetComponent<ParticleSystemRenderer>().material = particleMats[sprayCanMatsIndex];
	}

	public override void LoadItemSaveData(int saveData)
	{
		base.LoadItemSaveData(saveData);
		sprayCanTank = (float)saveData / 100f;
	}

	public override int GetItemDataToSave()
	{
		return (int)(sprayCanTank * 100f);
	}

	public override void EquipItem()
	{
		base.EquipItem();
		playerHeldBy.equippedUsableItemQE = true;
	}

	public override void ItemActivate(bool used, bool buttonDown = true)
	{
		base.ItemActivate(used, buttonDown);
		if (buttonDown)
		{
			Debug.Log("Start using spray");
			if (sprayCanTank <= 0f || sprayCanShakeMeter <= 0f)
			{
				Debug.Log("Spray empty");
				if (isSpraying)
				{
					StopSpraying();
				}
				PlayCanEmptyEffect(sprayCanTank <= 0f);
			}
			else
			{
				Debug.Log("Spray not empty");
				StartSpraying();
			}
			return;
		}
		Debug.Log("Stop using spray");
		if (tryingToUseEmptyCan)
		{
			addVehicleHPInterval = 0f;
			tryingToUseEmptyCan = false;
			sprayAudio.Stop();
			sprayCanNeedsShakingParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
		}
		if (isWeedKillerSprayBottle)
		{
			sprayCanShakeMeter = 1f;
		}
		if (isSpraying)
		{
			StopSpraying();
		}
	}

	private void PlayCanEmptyEffect(bool isEmpty)
	{
		if (tryingToUseEmptyCan)
		{
			return;
		}
		tryingToUseEmptyCan = true;
		if (!isEmpty)
		{
			if (sprayCanNeedsShakingParticle.isPlaying)
			{
				sprayCanNeedsShakingParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
			sprayCanNeedsShakingParticle.Play();
			sprayAudio.clip = sprayNeedsShakingSFX;
			sprayAudio.Play();
		}
		else
		{
			sprayAudio.PlayOneShot(sprayCanEmptySFX);
		}
	}

	public override void ItemInteractLeftRight(bool right)
	{
		base.ItemInteractLeftRight(right);
		Debug.Log($"interact {right} ; {playerHeldBy == null}; {isSpraying}");
		if (!isWeedKillerSprayBottle && !right && !(playerHeldBy == null) && !isSpraying)
		{
			if (sprayCanTank <= 0f)
			{
				sprayAudio.PlayOneShot(sprayCanShakeEmptySFX);
				WalkieTalkie.TransmitOneShotAudio(sprayAudio, sprayCanShakeEmptySFX);
			}
			else
			{
				RoundManager.PlayRandomClip(sprayAudio, sprayCanShakeSFX);
				WalkieTalkie.TransmitOneShotAudio(sprayAudio, sprayCanShakeEmptySFX);
			}
			playerHeldBy.playerBodyAnimator.SetTrigger("shakeItem");
			sprayCanShakeMeter = Mathf.Min(sprayCanShakeMeter + 0.15f, 1f);
		}
	}

	public static Vector3 ExtractScaleFromMatrix(Matrix4x4 matrix)
	{
		float magnitude = new Vector3(matrix.m00, matrix.m10, matrix.m20).magnitude;
		float magnitude2 = new Vector3(matrix.m01, matrix.m11, matrix.m21).magnitude;
		float magnitude3 = new Vector3(matrix.m02, matrix.m12, matrix.m22).magnitude;
		return new Vector3(magnitude, magnitude2, magnitude3);
	}

	public static Matrix4x4 ResizeMatrix(Matrix4x4 yourMatrix, float shrinkSpeed, bool shrink = true)
	{
		Matrix4x4 matrix4x = yourMatrix;
		Vector3 vector = matrix4x.GetColumn(3);
		Vector3 vector2 = matrix4x.GetColumn(0);
		Vector3 vector3 = matrix4x.GetColumn(1);
		Vector3 vector4 = matrix4x.GetColumn(2);
		float magnitude = vector2.magnitude;
		float magnitude2 = vector3.magnitude;
		float magnitude3 = vector4.magnitude;
		float num = ((!shrink) ? (1f + shrinkSpeed * Time.deltaTime) : (1f - shrinkSpeed * Time.deltaTime));
		magnitude = Mathf.Clamp(magnitude * num, 0.001f, 10f);
		magnitude2 = Mathf.Clamp(magnitude2 * num, 0.001f, 10f);
		magnitude3 = Mathf.Clamp(magnitude3 * num, 0.001f, 10f);
		vector2 = vector2.normalized * magnitude;
		vector3 = vector3.normalized * magnitude2;
		vector4 = vector4.normalized * magnitude3;
		Matrix4x4 identity = Matrix4x4.identity;
		identity.SetColumn(0, vector2);
		identity.SetColumn(1, vector3);
		identity.SetColumn(2, vector4);
		identity.SetColumn(3, vector);
		return identity;
	}

	public static Matrix4x4 SetMatrixScale(Matrix4x4 yourMatrix, float setScale)
	{
		Matrix4x4 matrix4x = yourMatrix;
		Vector3 vector = matrix4x.GetColumn(3);
		Vector3 vector2 = matrix4x.GetColumn(0);
		Vector3 vector3 = matrix4x.GetColumn(1);
		Vector3 vector4 = matrix4x.GetColumn(2);
		float magnitude = vector2.magnitude;
		float magnitude2 = vector3.magnitude;
		float magnitude3 = vector4.magnitude;
		magnitude = Mathf.Clamp(setScale, 0.001f, 10f);
		magnitude2 = Mathf.Clamp(setScale, 0.001f, 10f);
		magnitude3 = Mathf.Clamp(setScale, 0.001f, 10f);
		vector2 = vector2.normalized * magnitude;
		vector3 = vector3.normalized * magnitude2;
		vector4 = vector4.normalized * magnitude3;
		Matrix4x4 identity = Matrix4x4.identity;
		identity.SetColumn(0, vector2);
		identity.SetColumn(1, vector3);
		identity.SetColumn(2, vector4);
		identity.SetColumn(3, vector);
		return identity;
	}

	public override void LateUpdate()
	{
		base.LateUpdate();
		if (isWeedKillerSprayBottle)
		{
			if (killingCadaverPlant.batchNum != -1)
			{
				if (cadaverGrowthAI == null)
				{
					cadaverGrowthAI = UnityEngine.Object.FindObjectOfType<CadaverGrowthAI>();
				}
				if (cadaverGrowthAI == null)
				{
					killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
				}
				Debug.Log($"planttype:{killingCadaverPlant.plantType}, batchnum:{killingCadaverPlant.batchNum}, index:{killingCadaverPlant.positionIndex}");
				Matrix4x4 matrix4x = cadaverGrowthAI.plantBatchers[killingCadaverPlant.plantType].Batches[killingCadaverPlant.batchNum][killingCadaverPlant.positionIndex];
				if (ExtractScaleFromMatrix(matrix4x).x < 3f && base.IsOwner)
				{
					Vector3 position = cadaverGrowthAI.plantBatchers[killingCadaverPlant.plantType].Batches[killingCadaverPlant.batchNum][killingCadaverPlant.positionIndex].GetPosition();
					Debug.Log($"DPosition: {cadaverGrowthAI.plantBatchers[killingCadaverPlant.plantType].Batches[killingCadaverPlant.batchNum][killingCadaverPlant.positionIndex].GetPosition()}");
					KillCadaverPlantRpc(position);
					cadaverGrowthAI.DestroyPlantAtPosition(position, playEffect: true);
					killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
				}
				else
				{
					cadaverGrowthAI.plantBatchers[killingCadaverPlant.plantType].Batches[killingCadaverPlant.batchNum][killingCadaverPlant.positionIndex] = ResizeMatrix(matrix4x, 2.2f);
				}
			}
			else if (killingWeed.batchNum != -1)
			{
				Matrix4x4 matrix4x2 = moldManager.grassInstancer.Batches[killingWeed.batchNum][killingWeed.positionIndex];
				if (ExtractScaleFromMatrix(matrix4x2).x < 0.5f && base.IsOwner)
				{
					Vector3 position2 = moldManager.grassInstancer.Batches[killingWeed.batchNum][killingWeed.positionIndex].GetPosition();
					KillWeedRpc(position2);
					moldManager.DestroyMoldAtPosition(position2, playEffect: true);
					killingWeed = (batchNum: -1, positionIndex: -1);
				}
				else
				{
					moldManager.grassInstancer.Batches[killingWeed.batchNum][killingWeed.positionIndex] = ResizeMatrix(matrix4x2, 1.7f);
				}
			}
		}
		if (makingAudio)
		{
			if (audioInterval <= 0f)
			{
				audioInterval = 1f;
				RoundManager.Instance.PlayAudibleNoise(base.transform.position, 10f, 0.65f, 0, isInShipRoom && StartOfRound.Instance.hangarDoorsClosed);
			}
			else
			{
				audioInterval -= Time.deltaTime;
			}
		}
		if (addSprayPaintWithFrameDelay > 1)
		{
			addSprayPaintWithFrameDelay--;
		}
		else if (addSprayPaintWithFrameDelay == 1)
		{
			addSprayPaintWithFrameDelay = 0;
			delayedSprayPaintDecal.enabled = true;
		}
		if (isSpraying && isHeld)
		{
			if (isWeedKillerSprayBottle)
			{
				sprayCanTank = Mathf.Max(sprayCanTank - Time.deltaTime / 15f, 0f);
				sprayCanShakeMeter = Mathf.Max(sprayCanShakeMeter - Time.deltaTime * 2f, 0f);
				TrySprayingWeedKillerOnLocalPlayer();
			}
			else
			{
				sprayCanTank = Mathf.Max(sprayCanTank - Time.deltaTime / 30f, 0f);
				sprayCanShakeMeter = Mathf.Max(sprayCanShakeMeter - Time.deltaTime / 10f, 0f);
			}
			if (sprayCanTank <= 0f || sprayCanShakeMeter <= 0f)
			{
				isSpraying = false;
				StopSpraying();
				PlayCanEmptyEffect(sprayCanTank <= 0f);
			}
			else
			{
				if (!base.IsOwner)
				{
					return;
				}
				if (sprayInterval <= 0f)
				{
					if (isWeedKillerSprayBottle)
					{
						sprayInterval = sprayIntervalSpeed;
						TrySprayingWeedKillerBottle();
					}
					else if (TrySpraying())
					{
						sprayInterval = sprayIntervalSpeed;
					}
					else
					{
						sprayInterval = 0.037f;
					}
				}
				else
				{
					sprayInterval -= Time.deltaTime;
				}
			}
		}
		else if (isWeedKillerSprayBottle)
		{
			StopKillingWeedLocalClient();
			StopKillingCadaverPlantLocalClient();
		}
	}

	private void TrySprayingWeedKillerBottle()
	{
		bool flag = false;
		Vector3 vector = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position - GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward * 0.7f;
		if (Physics.Raycast(vector, GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward, out sprayHit, 4.5f, 1073742080, QueryTriggerInteraction.Collide))
		{
			if (addVehicleHPInterval <= 0f)
			{
				addVehicleHPInterval = 0.3f;
				VehicleController vehicleController = UnityEngine.Object.FindObjectOfType<VehicleController>();
				if (vehicleController != null && Vector3.Distance(sprayHit.point, vehicleController.oilPipePoint.position) < 5f)
				{
					StopKillingWeedLocalClient();
					StopKillingCadaverPlantLocalClient();
					flag = true;
					if (vehicleController.carHP >= vehicleController.baseCarHP)
					{
						vehicleController.AddTurboBoost();
					}
					else
					{
						vehicleController.AddEngineOil();
					}
				}
				Debug.DrawRay(sprayHit.point, Vector3.up * 0.5f, Color.red, 1f);
				Debug.DrawLine(vector, sprayHit.point, Color.green, 5f);
			}
			else
			{
				addVehicleHPInterval -= Time.deltaTime;
			}
		}
		if (!flag && !StartOfRound.Instance.inShipPhase)
		{
			if (isInFactory)
			{
				CheckForCadaverPlantsInSprayPath();
			}
			else
			{
				CheckForWeedsInSprayPath();
			}
		}
	}

	private void TrySprayingWeedKillerOnLocalPlayer()
	{
		if (cadaverGrowthAI == null)
		{
			cadaverGrowthAI = UnityEngine.Object.FindObjectOfType<CadaverGrowthAI>();
		}
		if (cadaverGrowthAI != null && playerHeldBy != null && playerHeldBy != GameNetworkManager.Instance.localPlayerController && cadaverGrowthAI.playerInfections[GameNetworkManager.Instance.localPlayerController.playerClientId].infected)
		{
			Vector3 position = GameNetworkManager.Instance.localPlayerController.transform.position;
			position.y = base.transform.position.y;
			if (Vector3.Distance(playerHeldBy.transform.position, GameNetworkManager.Instance.localPlayerController.transform.position) < 5f && Vector3.Angle(base.transform.forward, position - base.transform.position) < 40f)
			{
				HealPlayerInfection(cadaverGrowthAI.playerInfections[GameNetworkManager.Instance.localPlayerController.playerClientId]);
			}
		}
	}

	private void HealPlayerInfection(PlayerInfection infection)
	{
		if (sprayOnPlayerMeter > 0.33f)
		{
			sprayOnPlayerMeter = 0f;
			PlayerControllerB localPlayerController = GameNetworkManager.Instance.localPlayerController;
			if (infection.burstMeter > 0f)
			{
				cadaverGrowthAI.BurstFromPlayer(GameNetworkManager.Instance.localPlayerController, localPlayerController.transform.position, localPlayerController.transform.eulerAngles);
				cadaverGrowthAI.SyncBurstFromPlayerRpc((int)GameNetworkManager.Instance.localPlayerController.playerClientId, localPlayerController.transform.position, localPlayerController.transform.eulerAngles);
				return;
			}
			GameNetworkManager.Instance.localPlayerController.DamagePlayer(8, hasDamageSFX: true, callRPC: true, CauseOfDeath.Suffocation);
			cadaverGrowthAI.HealInfection((int)localPlayerController.playerClientId, 0.1f);
			if (GameNetworkManager.Instance.localPlayerController.criticallyInjured)
			{
				sprayOnPlayerMeter = -0.2f;
			}
		}
		else
		{
			sprayOnPlayerMeter += Time.deltaTime;
		}
	}

	private bool CheckForCadaverPlantsInSprayPath()
	{
		Vector3 vector = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position + GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward * 1.8f;
		if (cadaverGrowthAI == null)
		{
			cadaverGrowthAI = UnityEngine.Object.FindObjectOfType<CadaverGrowthAI>();
		}
		if (cadaverGrowthAI == null || cadaverGrowthAI.isEnemyDead)
		{
			return false;
		}
		if (cadaverGrowthAI.GetAllPlantPositionsInRadius(vector, 2f, fillArray: true))
		{
			float num = 2000f;
			int num2 = 0;
			int num3 = -1;
			for (int i = 0; i < cadaverGrowthAI.plantBatchers.Length; i++)
			{
				int length = cadaverGrowthAI.moldPositions.GetLength(0);
				for (int j = 0; j < length && cadaverGrowthAI.moldPositions[j, i] != -1; j++)
				{
					if (!cadaverGrowthAI.growingRecentPlant || !(cadaverGrowthAI.plantBatchers[i].batchedPositions[cadaverGrowthAI.moldPositions[j, i]] == cadaverGrowthAI.recentPlantPosition))
					{
						float num4 = Vector3.Distance(vector, cadaverGrowthAI.plantBatchers[i].batchedPositions[cadaverGrowthAI.moldPositions[j, i]]);
						if (num4 < num)
						{
							num = num4;
							num2 = j;
							num3 = i;
						}
					}
				}
			}
			if (num3 == -1)
			{
				Debug.LogError("Error when finding index of closest plant in batches: plant type/batch number not defined");
				return false;
			}
			killingCadaverPlant = cadaverGrowthAI.plantBatchers[num3].GetWeedPositionInMatrixListForCadaverPlants(cadaverGrowthAI.moldPositions[num2, num3], num3);
			Debug.Log($"KillingCadaverPlant returned: ({killingCadaverPlant.batchNum}, {killingCadaverPlant.positionIndex}, {killingCadaverPlant.plantType})");
			SyncKillingCadaverPlantRpc(killingCadaverPlant.batchNum, killingCadaverPlant.positionIndex, killingCadaverPlant.plantType);
			return true;
		}
		return false;
	}

	private bool CheckForWeedsInSprayPath()
	{
		if (moldManager == null)
		{
			moldManager = UnityEngine.Object.FindObjectOfType<MoldSpreadManager>();
		}
		if (moldManager == null)
		{
			return false;
		}
		Vector3 vector = GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position + GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward * 1.8f;
		int allMoldPositionsInRadius = moldManager.GetAllMoldPositionsInRadius(vector, 2f, fillArray: true);
		if (allMoldPositionsInRadius > 0)
		{
			float num = 2000f;
			int num2 = 0;
			for (int i = 0; i < allMoldPositionsInRadius; i++)
			{
				float num3 = Vector3.Distance(vector, moldManager.grassInstancer.batchedPositions[moldManager.moldPositions[i]]);
				if (num3 < num)
				{
					num = num3;
					num2 = i;
				}
			}
			killingWeed = moldManager.grassInstancer.GetWeedPositionInMatrixList(moldManager.moldPositions[num2]);
			Debug.Log($"Killingweed returned: ({killingWeed.batchNum}, {killingWeed.positionIndex})");
			SyncKillingWeedRpc(killingWeed.batchNum, killingWeed.positionIndex);
			return true;
		}
		return false;
	}

	private void StopKillingWeedLocalClient()
	{
		(int, int) tuple = killingWeed;
		if (tuple.Item1 != -1 || tuple.Item2 != -1)
		{
			killingWeed = (batchNum: -1, positionIndex: -1);
			StopKillingWeedServerRpc();
		}
	}

	private void StopKillingCadaverPlantLocalClient()
	{
		(int, int, int) tuple = killingCadaverPlant;
		if (tuple.Item1 != -1 || tuple.Item2 != -1 || tuple.Item3 != -1)
		{
			killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
			StopKillingCadaverPlantServerRpc();
		}
	}

		[Rpc(SendTo.NotMe)]
		public void SyncKillingWeedRpc(int batchNum, int positionIndex)
		{
			killingWeed = (batchNum: batchNum, positionIndex: positionIndex);
		}

		[Rpc(SendTo.NotMe)]
		public void SyncKillingCadaverPlantRpc(int batchNum, int positionIndex, int plantType)
		{
			killingCadaverPlant = (batchNum: batchNum, positionIndex: positionIndex, plantType: plantType);
		}

		[ServerRpc(RequireOwnership = false)]
		public void StopKillingCadaverPlantServerRpc()
		{
			StopKillingWeedClientRpc();
		}

		[ClientRpc]
		public void StopKillingCadaverPlantClientRpc()
		{
			if (!base.IsOwner)
			{
				killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
			}
		}

		[ServerRpc(RequireOwnership = false)]
		public void StopKillingWeedServerRpc()
		{
			StopKillingWeedClientRpc();
		}

		[ClientRpc]
		public void StopKillingWeedClientRpc()
		{
			if (!base.IsOwner)
			{
				killingWeed = (batchNum: -1, positionIndex: -1);
			}
		}

		[Rpc(SendTo.NotMe)]
		public void KillWeedRpc(Vector3 destroyAtPos)
		{
			killingWeed = (batchNum: -1, positionIndex: -1);
			moldManager.DestroyMoldAtPosition(destroyAtPos, playEffect: true);
		}

		[Rpc(SendTo.NotMe)]
		public void KillCadaverPlantRpc(Vector3 destroyAtPos)
		{
			if (cadaverGrowthAI == null)
			{
				cadaverGrowthAI = UnityEngine.Object.FindObjectOfType<CadaverGrowthAI>();
			}

			killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
			cadaverGrowthAI.DestroyPlantAtPosition(destroyAtPos, playEffect: true);
		}

	public bool TrySpraying()
	{
		Debug.DrawRay(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position, GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward, Color.magenta, 0.05f);
		if (AddSprayPaintLocal(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position + GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward * 1f, GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward))
		{
			SprayPaintServerRpc(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.position + GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward * 1f, GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform.forward);
			return true;
		}
		return false;
	}

		[ServerRpc]
		public void SprayPaintServerRpc(Vector3 sprayPos, Vector3 sprayRot)
		{
			SprayPaintClientRpc(sprayPos, sprayRot);
		}

		[ClientRpc]
		public void SprayPaintClientRpc(Vector3 sprayPos, Vector3 sprayRot)
		{
			if (!base.IsOwner)
			{
				AddSprayPaintLocal(sprayPos, sprayRot);
			}
		}

	private void ToggleSprayCollisionOnHolder(bool enable)
	{
		if (playerHeldBy == null)
		{
			Debug.Log("playerheldby is null!!!!!");
		}
		else if (!enable)
		{
			for (int i = 0; i < playerHeldBy.bodyPartSpraypaintColliders.Length; i++)
			{
				playerHeldBy.bodyPartSpraypaintColliders[i].enabled = false;
				playerHeldBy.bodyPartSpraypaintColliders[i].gameObject.layer = 2;
			}
		}
		else
		{
			for (int j = 0; j < playerHeldBy.bodyPartSpraypaintColliders.Length; j++)
			{
				playerHeldBy.bodyPartSpraypaintColliders[j].enabled = false;
				playerHeldBy.bodyPartSpraypaintColliders[j].gameObject.layer = 29;
			}
		}
	}

	private bool AddSprayPaintLocal(Vector3 sprayPos, Vector3 sprayRot)
	{
		if (playerHeldBy == null)
		{
			return false;
		}
		ToggleSprayCollisionOnHolder(enable: false);
		if (RoundManager.Instance.mapPropsContainer == null)
		{
			RoundManager.Instance.mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer");
		}
		Ray ray = new Ray(sprayPos, sprayRot);
		if (!Physics.Raycast(ray, out sprayHit, 7f, sprayPaintMask, QueryTriggerInteraction.Collide))
		{
			ToggleSprayCollisionOnHolder(enable: true);
			return false;
		}
		if (Vector3.Distance(sprayHit.point, previousSprayPosition) < 0.175f)
		{
			ToggleSprayCollisionOnHolder(enable: true);
			return false;
		}
		if (debugSprayPaint)
		{
			Debug.DrawRay(sprayPos, sprayRot * 7f, Color.green, 5f);
		}
		int num = -1;
		Transform transform;
		if (sprayHit.collider.gameObject.layer == 11 || sprayHit.collider.gameObject.layer == 8 || sprayHit.collider.gameObject.layer == 0)
		{
			transform = ((!playerHeldBy.isInElevator && !StartOfRound.Instance.inShipPhase && !(RoundManager.Instance.mapPropsContainer == null)) ? RoundManager.Instance.mapPropsContainer.transform : StartOfRound.Instance.elevatorTransform);
		}
		else
		{
			if (debugSprayPaint)
			{
				Debug.Log("spray paint parenting to this object : " + sprayHit.collider.gameObject.name);
				Debug.Log($"{sprayHit.collider.tag}; {sprayHit.collider.tag.Length}");
			}
			if (sprayHit.collider.tag.StartsWith("PlayerBody"))
			{
				switch (sprayHit.collider.tag)
				{
				case "PlayerBody":
					num = 0;
					break;
				case "PlayerBody1":
					num = 1;
					break;
				case "PlayerBody2":
					num = 2;
					break;
				case "PlayerBody3":
					num = 3;
					break;
				}
				if (num == (int)playerHeldBy.playerClientId)
				{
					ToggleSprayCollisionOnHolder(enable: true);
					return false;
				}
			}
			else if (sprayHit.collider.tag.StartsWith("PlayerRagdoll"))
			{
				switch (sprayHit.collider.tag)
				{
				case "PlayerRagdoll":
					num = 0;
					break;
				case "PlayerRagdoll1":
					num = 1;
					break;
				case "PlayerRagdoll2":
					num = 2;
					break;
				case "PlayerRagdoll3":
					num = 3;
					break;
				}
			}
			transform = sprayHit.collider.transform;
		}
		sprayPaintDecalsIndex = (sprayPaintDecalsIndex + 1) % maxSprayPaintDecals;
		DecalProjector decalProjector = null;
		GameObject gameObject;
		if (sprayPaintDecals.Count <= sprayPaintDecalsIndex)
		{
			if (debugSprayPaint)
			{
				Debug.Log("Adding to spray paint decals pool");
			}
			for (int i = 0; i < 200; i++)
			{
				if (sprayPaintDecals.Count >= maxSprayPaintDecals)
				{
					break;
				}
				gameObject = UnityEngine.Object.Instantiate(sprayPaintPrefab, transform);
				sprayPaintDecals.Add(gameObject);
				decalProjector = gameObject.GetComponent<DecalProjector>();
				if (decalProjector.material != sprayCanMats[sprayCanMatsIndex])
				{
					decalProjector.material = sprayCanMats[sprayCanMatsIndex];
				}
			}
		}
		if (debugSprayPaint)
		{
			Debug.Log($"Spraypaint B {sprayPaintDecals.Count}; index: {sprayPaintDecalsIndex}");
		}
		if (sprayPaintDecals[sprayPaintDecalsIndex] == null)
		{
			Debug.LogError($"ERROR: spray paint at index {sprayPaintDecalsIndex} is null; creating new object in its place");
			gameObject = UnityEngine.Object.Instantiate(sprayPaintPrefab, transform);
			sprayPaintDecals[sprayPaintDecalsIndex] = gameObject;
		}
		else
		{
			if (!sprayPaintDecals[sprayPaintDecalsIndex].activeSelf)
			{
				sprayPaintDecals[sprayPaintDecalsIndex].SetActive(value: true);
			}
			gameObject = sprayPaintDecals[sprayPaintDecalsIndex];
		}
		decalProjector = gameObject.GetComponent<DecalProjector>();
		if (decalProjector.material != sprayCanMats[sprayCanMatsIndex])
		{
			decalProjector.material = sprayCanMats[sprayCanMatsIndex];
		}
		if (debugSprayPaint)
		{
			Debug.Log($"decal player num: {num}");
		}
		switch (num)
		{
		case 0:
			decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer4;
			break;
		case 1:
			decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer5;
			break;
		case 2:
			decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer6;
			break;
		case 3:
			decalProjector.decalLayerMask = DecalLayerEnum.DecalLayer7;
			break;
		case -1:
			decalProjector.decalLayerMask = DecalLayerEnum.DecalLayerDefault;
			break;
		}
		gameObject.transform.position = ray.GetPoint(sprayHit.distance - 0.1f);
		gameObject.transform.forward = sprayRot;
		if (gameObject.transform.parent != transform)
		{
			gameObject.transform.SetParent(transform);
		}
		previousSprayPosition = sprayHit.point;
		addSprayPaintWithFrameDelay = 2;
		delayedSprayPaintDecal = decalProjector;
		ToggleSprayCollisionOnHolder(enable: true);
		return true;
	}

	public void StartSpraying()
	{
		sprayAudio.clip = spraySFX;
		sprayAudio.Play();
		sprayParticle.Play(withChildren: true);
		isSpraying = true;
		sprayAudio.PlayOneShot(sprayStart);
	}

	public void StopSpraying()
	{
		sprayAudio.Stop();
		sprayParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
		isSpraying = false;
		sprayAudio.PlayOneShot(sprayStop);
	}

	public override void PocketItem()
	{
		base.PocketItem();
		if (playerHeldBy != null)
		{
			playerHeldBy.activatingItem = false;
			playerHeldBy.equippedUsableItemQE = false;
		}
		StopSpraying();
	}

	public override void DiscardItem()
	{
		if (playerHeldBy != null)
		{
			playerHeldBy.activatingItem = false;
			playerHeldBy.equippedUsableItemQE = false;
		}
		base.DiscardItem();
		StopSpraying();
	}
}
