using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items
{
    internal class BoxOfSnapTrapsBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

        public GameObject SnapTrapPrefab;
        public ScanNodeProperties ScanNode;

        // Configs // TODO: Set up configs
        int snapTrapAmount;

        public override void Start()
        {
            base.Start();
            ScanNode.subText = "";
            snapTrapAmount = configSnapTrapAmount.Value;
            SetControlTipsForItem();
        }

        public override void SetControlTipsForItem()
        {
            string[] toolTips = itemProperties.toolTips;
            toolTips[0] = $"Drop Snap Trap [LMB] ({snapTrapAmount} left)"; 
            HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);

            if (buttonDown && snapTrapAmount > 0)
            {
                if (!Physics.Raycast(transform.position, -Vector3.up, out var hitInfo, 80f, 268437761, QueryTriggerInteraction.Ignore)) { return; }
                GameObject.Instantiate(SnapTrapPrefab, hitInfo.point, playerHeldBy.transform.rotation);
                snapTrapAmount--;
                SetControlTipsForItem();
            }
        }

        public override int GetItemDataToSave()
        {
            return snapTrapAmount;
        }

        public override void LoadItemSaveData(int saveData)
        {
            snapTrapAmount = saveData;
        }
    }
}
