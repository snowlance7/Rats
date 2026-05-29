using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Configs;
using static Rats.Plugin;
using static UnityEngine.SendMouseEvents;

namespace Rats.Items
{
    internal class GlueTrapBehavior : PhysicsProp
    {
        public GameObject glueBoardPrefab = null!;
        public ScanNodeProperties scanNode = null!;

        int glueTrapAmount;

        public void Awake()
        {
            itemProperties.floorYOffset = 180;
            itemProperties.rotationOffset = new Vector3(180, 0, 0);
            itemProperties.positionOffset = new Vector3(0.2f, 0.2f, -0.25f);
            itemProperties.meshOffset = true;
        }

        public override void Start()
        {
            base.Start();
            scanNode.subText = "";
            glueTrapAmount = cfgGlueBoardAmount;
        }

        public override void SetControlTipsForItem()
        {
            if (playerHeldBy != localPlayer) { return; }
            string[] toolTips = itemProperties.toolTips;
            toolTips[0] = $"Drop Glue Trap [LMB] ({glueTrapAmount} left)";
            HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            if (!buttonDown || glueTrapAmount <= 0) { return; }
            if (!Physics.Raycast(transform.position, -Vector3.up, out var hitInfo, 80f, 268437761, QueryTriggerInteraction.Ignore)) { return; }
            SpawnGlueBoardServerRpc(hitInfo.point, playerHeldBy.transform.rotation);
        }

        public override void GrabItem()
        {
            base.GrabItem();
            SetControlTipsForItem();
        }

        public override int GetItemDataToSave()
        {
            return glueTrapAmount;
        }

        public override void LoadItemSaveData(int saveData)
        {
            glueTrapAmount = saveData;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpawnGlueBoardServerRpc(Vector3 position, Quaternion rotation)
        {
            if (!IsServer) { return; }
            GameObject glueBoardObj = Instantiate(glueBoardPrefab, position, rotation);
            glueBoardObj.GetComponent<NetworkObject>().Spawn(true);
            SpawnGlueBoardClientRpc();
        }

        [ClientRpc]
        public void SpawnGlueBoardClientRpc()
        {
            glueTrapAmount--;
            SetControlTipsForItem();
        }
    }

    internal class GlueBoardBehavior : PhysicsProp
    {
        public BoxCollider GlueCollider = null!;
        public GameObject RatProp = null!;
        public GameObject RatPropJerma = null!;
        public ScanNodeProperties ScanNode = null!;

        List<GameObject> ratsOnBoard = [];

        const float minSlowTime = 1f;
        const float maxSlowTime = 2.5f;

        public void Awake()
        {
            itemProperties.floorYOffset = 0;
            itemProperties.rotationOffset = new Vector3(0, 0, 0);
            itemProperties.positionOffset = new Vector3(-0.5f, 0.1f, 0f);
            itemProperties.meshOffset = true;
        }

        public override void Start()
        {
            base.Start();

            GrabbableObjectPhysicsTrigger glueTrigger = GlueCollider.gameObject.AddComponent<GrabbableObjectPhysicsTrigger>();
            glueTrigger.itemScript = this;
        }

        public override void ActivatePhysicsTrigger(Collider other)
        {
            base.ActivatePhysicsTrigger(other);

            logger.LogDebug("In ActivatePhysicsTrigger()");
            if (ratsOnBoard.Count >= cfgMaxRatsOnGlueTrap)
            {
                return;
            }

            if (!IsServerOrHost) { return; }

            if (!other.gameObject.TryGetComponent(out RatAICollisionDetect ratCollision)) { return; }

            float delay = UnityEngine.Random.Range(minSlowTime, maxSlowTime);
            StartCoroutine(KillRatCoroutine(ratCollision.mainScript, delay));
        }

        IEnumerator KillRatCoroutine(RatAI rat, float delay)
        {
            float elapsedTime = 0f;
            float startSpeed = 0.5f;

            while (elapsedTime < delay)
            {
                rat.nav.agent.speed = Mathf.Lerp(startSpeed, 0f, elapsedTime / delay);
                elapsedTime += Time.deltaTime;
                logger.LogDebug("Elapsed Time: " + elapsedTime);
                yield return null;
            }

            logger.LogDebug("Finished slowing, calling rpc now");

            rat.KillEnemyServerRpc();
            yield return new WaitForSeconds(1f);
            logger.LogDebug("Calling AddRatToBoardClientRpc");
            AddRatToBoardClientRpc(rat.transform.position, rat.transform.rotation);
            rat.NetworkObject.Despawn(true);
        }

        public override int GetItemDataToSave()
        {
            return ratsOnBoard.Count;
        }

        public override void LoadItemSaveData(int saveData)
        {
            scrapValue = 0;
            if (!IsServerOrHost) { return; }
            for (int i = 0; i < saveData; i++)
            {
                Vector3 pos = GetRandomPositionOnBoard();
                Quaternion rot = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
                AddRatToBoardClientRpc(pos, rot);
            }
        }
        private Vector3 GetRandomPositionOnBoard()
        {
            Vector3 boardCenter = transform.position; // Center of the glue board
            float boardWidth = 0.3f;  // Adjust to fit the glue board size
            float boardHeight = 0.2f;

            float randomX = UnityEngine.Random.Range(-boardWidth / 2, boardWidth / 2);
            float randomZ = UnityEngine.Random.Range(-boardHeight / 2, boardHeight / 2);

            return boardCenter + new Vector3(randomX, 0.01f, randomZ); // Offset to prevent floating
        }


        [ClientRpc]
        public void AddRatToBoardClientRpc(Vector3 pos, Quaternion rot)
        {
            logger.LogDebug("In ParentRatToBoardClientRpc()");

            GameObject prefab = cfgUseJermaRats ? RatPropJerma : RatProp;
            GameObject rat = Instantiate(prefab, pos, rot);
            logger.LogDebug("Parenting rat to board");
            rat.transform.SetParent(transform);
            ratsOnBoard.Add(rat);
            SetScrapValue(ratsOnBoard.Count * cfgScrapValuePerRat);
        }
    }
}