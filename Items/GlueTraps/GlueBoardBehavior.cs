using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items.GlueTraps
{
    internal class GlueBoardBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

        public BoxCollider GlueCollider;
        public GameObject RatProp;
        public GameObject RatPropJerma;
        public ScanNodeProperties ScanNode;

        List<GameObject> ratsOnBoard = [];

        const float minSlowTime = 1f;
        const float maxSlowTime = 2.5f;

        public override void Start()
        {
            base.Start();

            GrabbableObjectPhysicsTrigger glueTrigger = GlueCollider.gameObject.AddComponent<GrabbableObjectPhysicsTrigger>();
            glueTrigger.itemScript = this;
        }

        public override void ActivatePhysicsTrigger(Collider other)
        {
            base.ActivatePhysicsTrigger(other);

            Plugin.LoggerInstance.LogDebug("In ActivatePhysicsTrigger()");
            if (ratsOnBoard.Count >= configMaxRatsOnGlueTrap.Value && !grabbable)
            {
                grabbable = true;
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
                rat.agent.speed = Mathf.Lerp(startSpeed, 0f, elapsedTime / delay);
                elapsedTime += Time.deltaTime;
                logger.LogDebug("Elapsed Time: " + elapsedTime);
                yield return null;
            }

            logger.LogDebug("Finished slowing, calling rpc now");

            rat.KillEnemyOnOwnerClient();
            yield return new WaitForSeconds(1f);
            AddRatToBoardClientRpc(rat.transform.position, rat.transform.rotation);
            rat.NetworkObject.Despawn(true);
            //rat.agent.speed = 0f;
            //ParentRatToBoardClientRpc(rat.NetworkObject);
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

            GameObject prefab = configUseJermaRats.Value ? RatPropJerma : RatProp;
            GameObject rat = Instantiate(prefab, pos, rot);
            rat.transform.SetParent(transform);
            ratsOnBoard.Add(rat);
            SetScrapValue(ratsOnBoard.Count * configScrapValuePerRat.Value);
        }

        /*[ClientRpc]
        public void ParentRatToBoardClientRpc(NetworkObjectReference netRef)
        {
            logger.LogDebug("In ParentRatToBoardClientRpc()");
            if (!netRef.TryGet(out NetworkObject netObj)) {  return; }
            RatAI rat = netObj.GetComponent<RatAI>();
            rat.KillEnemy();
            rat.agent.enabled = false;
            rat.gameObject.transform.SetParent(transform);
            ratsOnGlueTrap++;
        }*/
    }
}