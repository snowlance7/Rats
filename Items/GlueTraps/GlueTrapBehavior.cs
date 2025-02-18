using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items.GlueTraps
{
    internal class GlueTrapBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

        public GameObject GlueBoardPrefab;
        public ScanNodeProperties ScanNode;

        int glueTrapAmount;

        public override void Start()
        {
            base.Start();
            ScanNode.subText = "";
            glueTrapAmount = configGlueBoardAmount.Value;
            SetControlTipsForItem();
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

            if (buttonDown && glueTrapAmount > 0)
            {
                if (!Physics.Raycast(transform.position, -Vector3.up, out var hitInfo, 80f, 268437761, QueryTriggerInteraction.Ignore)) { return; }
                if (IsServerOrHost)
                {
                    GameObject glueBoardObj = Instantiate(GlueBoardPrefab, hitInfo.point, playerHeldBy.transform.rotation);
                    glueBoardObj.GetComponent<NetworkObject>().Spawn(true);
                }
                glueTrapAmount--;
                SetControlTipsForItem();
            }
        }

        public override int GetItemDataToSave()
        {
            return glueTrapAmount;
        }

        public override void LoadItemSaveData(int saveData)
        {
            glueTrapAmount = saveData;
        }
    }
}