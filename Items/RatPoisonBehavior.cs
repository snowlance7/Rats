using BepInEx.Logging;
using GameNetcodeStuff;
using System;
using System.Collections;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Configs;
using static Rats.Plugin;

namespace Rats.Items
{
    internal class RatPoisonBehavior : PhysicsProp // TODO: Make it act like weed killer and kill cadaver weeds and fill gas in company cruiser
    {
        public AudioSource audioSource = null!;
        public ParticleSystem particleSystem = null!;
        public Animator animator = null!;
        public Transform pourDirection = null!;
        public ScanNodeProperties scanNode = null!;

        public PlayerControllerB? lastPlayerHeldBy;

        readonly float downAngle = 0.7f;
        bool pouring;
        float currentFluid;

        float pourRate;

        public void Awake()
        {
            itemProperties.floorYOffset = 90;
            itemProperties.rotationOffset = new Vector3(180, 180, 60);
            itemProperties.positionOffset = new Vector3(-0.65f, 0.55f, 0f);
            itemProperties.restingRotation = new Vector3(0, 0, 0);
            itemProperties.syncUseFunction = true; // TODO
            itemProperties.weight = 1.2f;
        }

        public override void Start()
        {
            base.Start();
            scanNode.subText = "";
            currentFluid = cfgMaxFluid;
            pourRate = cfgPourRate;

            //SetUpWeedKillingVariables();
        }

        public override void Update()
        {
            base.Update();

            if (playerHeldBy != null)
            {
                lastPlayerHeldBy = playerHeldBy;
            }

            if (pouring)
            {
                currentFluid -= pourRate * Time.deltaTime;
                SetControlTipsForItem();

                if (currentFluid <= 0)
                {
                    SetPouring(false);
                    currentFluid = 0;
                    return;
                }

                if (!isPlayerLookingDown())
                {
                    SetPouring(false);
                    return;
                }

                if (playerHeldBy == null || localPlayer != playerHeldBy) { return; }

                // Run on client
                if (Physics.Raycast(pourDirection.position, -Vector3.up, out var hitInfo, 80f))
                {
                    if (hitInfo.collider.gameObject.TryGetComponent(out RatNest nest) && nest.poisonInNest < cfgPoisonToCloseNest)
                    {
                        nest.AddPoisonServerRpc(pourRate * Time.deltaTime);
                    }
                }
            }
        }

        public override void SetControlTipsForItem()
        {
            if (playerHeldBy == null) { return; }
            if (playerHeldBy != localPlayer) { return; }
            string[] toolTips = itemProperties.toolTips;
            toolTips[0] = $"Pour [LMB] ({currentFluid.ToString("F1")}L left)";
            HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
        }

        public override void ItemActivate(bool used, bool buttonDown = true) // Synced TODO: create rpcs
        {
            base.ItemActivate(used, buttonDown);

            bool lookingDown = isPlayerLookingDown();
            bool isPouring = buttonDown && lookingDown && currentFluid > 0f;

            SetPouring(isPouring);
        }

        bool isPlayerLookingDown() => Vector3.Dot(playerHeldBy.gameplayCamera.transform.forward, Vector3.down) > downAngle;

        void SetPouring(bool _pouring)
        {
            if (lastPlayerHeldBy == null) { return; }
            pouring = _pouring;
            lastPlayerHeldBy.activatingItem = pouring;
            animator.SetBool("pour", pouring);

            if (pouring)
            {
                particleSystem.Play();
                audioSource.Play();
            }
            else
            {
                particleSystem.Stop();
                audioSource.Stop();
            }
        }

        public override void DiscardItem()
        {
            base.DiscardItem();
            SetPouring(false);
        }

        public override void GrabItem()
        {
            base.GrabItem();
            SetControlTipsForItem();
        }

        public override int GetItemDataToSave() => (int)currentFluid;
        public override void LoadItemSaveData(int saveData) => currentFluid = (float)saveData;

        /*
        #region Weed Killer // TODO
        // Weed Killer stuff

        private RaycastHit sprayHit;

        public bool isWeedKillerSprayBottle;

        private Collider[] weedColliders;

        private float addVehicleHPInterval;

        private MoldSpreadManager moldManager;

        private (int batchNum, int positionIndex) killingWeed;

        private (int batchNum, int positionIndex, int plantType) killingCadaverPlant;

        private CadaverGrowthAI cadaverGrowthAI;

        private float sprayOnPlayerMeter;

        private bool healingPlayerInfection;

        private void SetUpWeedKillingVariables()
        {
            weedColliders = new Collider[3];
            killingWeed = (batchNum: -1, positionIndex: -1);
            killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
            if (isWeedKillerSprayBottle)
            {
                moldManager = UnityEngine.Object.FindObjectOfType<MoldSpreadManager>();
                return;
            }
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
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Execute)
                {
                    RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
                    RpcParams rpcParams = default(RpcParams);
                    FastBufferWriter bufferWriter = __beginSendRpc(853698447u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
                    BytePacker.WriteValueBitPacked(bufferWriter, batchNum);
                    BytePacker.WriteValueBitPacked(bufferWriter, positionIndex);
                    __endSendRpc(ref bufferWriter, 853698447u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Execute)
                {
                    __rpc_exec_stage = __RpcExecStage.Send;
                    killingWeed = (batchNum: batchNum, positionIndex: positionIndex);
                }
            }
        }

        [Rpc(SendTo.NotMe)]
        public void SyncKillingCadaverPlantRpc(int batchNum, int positionIndex, int plantType)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Execute)
                {
                    RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
                    RpcParams rpcParams = default(RpcParams);
                    FastBufferWriter bufferWriter = __beginSendRpc(2181546544u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
                    BytePacker.WriteValueBitPacked(bufferWriter, batchNum);
                    BytePacker.WriteValueBitPacked(bufferWriter, positionIndex);
                    BytePacker.WriteValueBitPacked(bufferWriter, plantType);
                    __endSendRpc(ref bufferWriter, 2181546544u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Execute)
                {
                    __rpc_exec_stage = __RpcExecStage.Send;
                    killingCadaverPlant = (batchNum: batchNum, positionIndex: positionIndex, plantType: plantType);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopKillingCadaverPlantServerRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
                {
                    ServerRpcParams serverRpcParams = default(ServerRpcParams);
                    FastBufferWriter bufferWriter = __beginSendServerRpc(3342850939u, serverRpcParams, RpcDelivery.Reliable);
                    __endSendServerRpc(ref bufferWriter, 3342850939u, serverRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
                {
                    __rpc_exec_stage = __RpcExecStage.Send;
                    StopKillingWeedClientRpc();
                }
            }
        }

        [ClientRpc]
        public void StopKillingCadaverPlantClientRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(1830928072u, clientRpcParams, RpcDelivery.Reliable);
                __endSendClientRpc(ref bufferWriter, 1830928072u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
            {
                __rpc_exec_stage = __RpcExecStage.Send;
                if (!base.IsOwner)
                {
                    killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopKillingWeedServerRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
                {
                    ServerRpcParams serverRpcParams = default(ServerRpcParams);
                    FastBufferWriter bufferWriter = __beginSendServerRpc(3462977352u, serverRpcParams, RpcDelivery.Reliable);
                    __endSendServerRpc(ref bufferWriter, 3462977352u, serverRpcParams, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
                {
                    __rpc_exec_stage = __RpcExecStage.Send;
                    StopKillingWeedClientRpc();
                }
            }
        }

        [ClientRpc]
        public void StopKillingWeedClientRpc()
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Execute && (networkManager.IsServer || networkManager.IsHost))
            {
                ClientRpcParams clientRpcParams = default(ClientRpcParams);
                FastBufferWriter bufferWriter = __beginSendClientRpc(1040528291u, clientRpcParams, RpcDelivery.Reliable);
                __endSendClientRpc(ref bufferWriter, 1040528291u, clientRpcParams, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Execute && (networkManager.IsClient || networkManager.IsHost))
            {
                __rpc_exec_stage = __RpcExecStage.Send;
                if (!base.IsOwner)
                {
                    killingWeed = (batchNum: -1, positionIndex: -1);
                }
            }
        }

        [Rpc(SendTo.NotMe)]
        public void KillWeedRpc(Vector3 destroyAtPos)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager != null && networkManager.IsListening)
            {
                if (__rpc_exec_stage != __RpcExecStage.Execute)
                {
                    RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
                    RpcParams rpcParams = default(RpcParams);
                    FastBufferWriter bufferWriter = __beginSendRpc(96399641u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
                    bufferWriter.WriteValueSafe(in destroyAtPos);
                    __endSendRpc(ref bufferWriter, 96399641u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
                }
                if (__rpc_exec_stage == __RpcExecStage.Execute)
                {
                    __rpc_exec_stage = __RpcExecStage.Send;
                    killingWeed = (batchNum: -1, positionIndex: -1);
                    moldManager.DestroyMoldAtPosition(destroyAtPos, playEffect: true);
                }
            }
        }

        [Rpc(SendTo.NotMe)]
        public void KillCadaverPlantRpc(Vector3 destroyAtPos)
        {
            NetworkManager networkManager = base.NetworkManager;
            if ((object)networkManager == null || !networkManager.IsListening)
            {
                return;
            }
            if (__rpc_exec_stage != __RpcExecStage.Execute)
            {
                RpcAttribute.RpcAttributeParams attributeParams = default(RpcAttribute.RpcAttributeParams);
                RpcParams rpcParams = default(RpcParams);
                FastBufferWriter bufferWriter = __beginSendRpc(2604920715u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
                bufferWriter.WriteValueSafe(in destroyAtPos);
                __endSendRpc(ref bufferWriter, 2604920715u, rpcParams, attributeParams, SendTo.NotMe, RpcDelivery.Reliable);
            }
            if (__rpc_exec_stage == __RpcExecStage.Execute)
            {
                __rpc_exec_stage = __RpcExecStage.Send;
                if (cadaverGrowthAI == null)
                {
                    cadaverGrowthAI = UnityEngine.Object.FindObjectOfType<CadaverGrowthAI>();
                }
                killingCadaverPlant = (batchNum: -1, positionIndex: -1, plantType: -1);
                cadaverGrowthAI.DestroyPlantAtPosition(destroyAtPos, playEffect: true);
            }
        }
        #endregion
        */
    }
}