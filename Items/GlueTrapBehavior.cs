using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static Rats.Plugin;

namespace Rats.Items
{
    internal class GlueTrapBehavior : PhysicsProp
    {
        public GameObject GlueBoardPrefab;

        // Configs
        int glueTrapAmount = 10;

        public override void Start()
        {
            base.Start();
            // TODO: Set up configs
            SetControlTipsForItem();
        }

        public override void SetControlTipsForItem()
        {
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
                GameObject.Instantiate(GlueBoardPrefab, hitInfo.point, playerHeldBy.transform.rotation);
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