using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Items;
using System;
using UnityEngine;

// ============================================================
// SPECIAL ITEMS COMMENT ARCHIVE / WIP
// DFU loader sensitivity test
// Remove this block if Convenient Clock disappears again.
// ============================================================

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InventoryAssist
    {
        private int specialItemSelectedIndex = 0;

        private readonly Vector2[] specialItemsAnchorsNative = new Vector2[]
        {
            new Vector2(0.3f, 10.2f),    // Amulet0
            new Vector2(23.2f, 10.2f),   // Amulet1
            new Vector2(0.3f, 41.2f),    // Bracelet0
            new Vector2(23.2f, 41.2f),   // Bracelet1
            new Vector2(0.3f, 72.1f),    // Ring0
            new Vector2(23.2f, 72.1f),   // Ring1
            new Vector2(0.3f, 103.1f),   // Bracer0
            new Vector2(23.2f, 103.1f),  // Bracer1
            new Vector2(0.3f, 134.2f),   // Mark0
            new Vector2(23.2f, 134.2f),  // Mark1
            new Vector2(0.3f, 165.2f),   // Crystal0
            new Vector2(23.2f, 165.2f),  // Crystal1
        };

        private const int SPECIAL_AMULET0 = 0;
        private const int SPECIAL_AMULET1 = 1;
        private const int SPECIAL_BRACELET0 = 2;
        private const int SPECIAL_BRACELET1 = 3;
        private const int SPECIAL_RING0 = 4;
        private const int SPECIAL_RING1 = 5;
        private const int SPECIAL_BRACER0 = 6;
        private const int SPECIAL_BRACER1 = 7;
        private const int SPECIAL_MARK0 = 8;
        private const int SPECIAL_MARK1 = 9;
        private const int SPECIAL_CRYSTAL0 = 10;
        private const int SPECIAL_CRYSTAL1 = 11;
        private void HandleSpecialItemsRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentRegion();

            if (cm.Action1Released)
            {
                InvokeSelectedSpecialItemLeftAction(menuWindow);
                return;
            }

            if (cm.Action2Released)
            {
                InvokeSelectedSpecialItemRightAction(menuWindow);
                return;
            }

            if (cm.DPadUpPressed)
            {
                InvokeSelectedSpecialItemMiddleAction(menuWindow);
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                MoveSpecialItemSelection(menuWindow, 0, -1);
                return;
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                MoveSpecialItemSelection(menuWindow, 0, 1);
                return;
            }

            if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
            {
                MoveSpecialItemSelection(menuWindow, -1, 0);
                return;
            }

            if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                MoveSpecialItemSelection(menuWindow, 1, 0);
                return;
            }

        }

        private EquipSlots? GetSpecialItemSelectedSlot()
        {
            switch (specialItemSelectedIndex)
            {
                case SPECIAL_AMULET0: return EquipSlots.Amulet0;
                case SPECIAL_AMULET1: return EquipSlots.Amulet1;

                case SPECIAL_BRACELET0: return EquipSlots.Bracelet0;
                case SPECIAL_BRACELET1: return EquipSlots.Bracelet1;

                case SPECIAL_RING0: return EquipSlots.Ring0;
                case SPECIAL_RING1: return EquipSlots.Ring1;

                case SPECIAL_BRACER0: return EquipSlots.Bracer0;
                case SPECIAL_BRACER1: return EquipSlots.Bracer1;

                case SPECIAL_MARK0: return EquipSlots.Mark0;
                case SPECIAL_MARK1: return EquipSlots.Mark1;

                case SPECIAL_CRYSTAL0: return EquipSlots.Crystal0;
                case SPECIAL_CRYSTAL1: return EquipSlots.Crystal1;
            }

            return null;
        }

        private DaggerfallUnityItem GetSelectedSpecialItem(DaggerfallInventoryWindow menuWindow)
        {
            EquipSlots? slot = GetSpecialItemSelectedSlot();
            if (!slot.HasValue)
                return null;

            if (GameManager.Instance.PlayerEntity == null || GameManager.Instance.PlayerEntity.ItemEquipTable == null)
                return null;

            return GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(slot.Value);
        }

        private void InvokeSelectedSpecialItemLeftAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedSpecialItem(menuWindow);

            if (item == null || fiSelectedActionMode == null)
                return;

            SaveResumeSelectorState();

            object actionModeObj = fiSelectedActionMode.GetValue(menuWindow);
            if (actionModeObj == null)
                return;

            int actionMode = Convert.ToInt32(actionModeObj);

            switch (actionMode)
            {
                case 0: // Info
                    if (miShowInfoPopup != null)
                        miShowInfoPopup.Invoke(menuWindow, new object[] { item });
                    break;

                case 1: // Equip
                case 4: // Select
                    if (miUnequipItem != null)
                        miUnequipItem.Invoke(menuWindow, new object[] { item, true });
                    break;

                case 2: // Remove
                    break;

                case 3: // Use
                    if (miUseItem != null)
                        miUseItem.Invoke(menuWindow, new object[] { item, null });
                    break;
            }
        }

        private void InvokeSelectedSpecialItemRightAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedSpecialItem(menuWindow);
            if (item == null || fiSelectedActionMode == null)
                return;

            SaveResumeSelectorState();

            object actionModeObj = fiSelectedActionMode.GetValue(menuWindow);
            if (actionModeObj == null)
                return;

            int actionMode = Convert.ToInt32(actionModeObj);

            if (actionMode == 1)
                actionMode = 2;
            else if (actionMode == 2)
                actionMode = 1;
            else if (actionMode == 4)
                actionMode = 2;

            switch (actionMode)
            {
                case 0: // Info
                    if (miShowInfoPopup != null)
                        miShowInfoPopup.Invoke(menuWindow, new object[] { item });
                    break;

                case 1: // Equip
                case 4: // Select
                    if (miUnequipItem != null)
                        miUnequipItem.Invoke(menuWindow, new object[] { item, true });
                    break;

                case 2: // Remove
                    break;

                case 3: // Use
                    if (miUseItem != null)
                        miUseItem.Invoke(menuWindow, new object[] { item, null });
                    break;
            }
        }

        private void InvokeSelectedSpecialItemMiddleAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedSpecialItem(menuWindow);
            if (item == null || miNextVariant == null)
                return;

            SaveResumeSelectorState();
            miNextVariant.Invoke(menuWindow, new object[] { item });
        }

        private bool MoveSpecialItemSelection(DaggerfallInventoryWindow menuWindow, int directionX, int directionY)
        {
            int next = specialItemSelectedIndex;

            switch (specialItemSelectedIndex)
            {
                case SPECIAL_AMULET0:
                    if (directionY < 0) next = SPECIAL_CRYSTAL0;
                    else if (directionX > 0) next = SPECIAL_AMULET1;
                    else if (directionY > 0) next = SPECIAL_BRACELET0;
                    else if (directionX < 0) { SwitchRegion(menuWindow, REGION_RIGHT_GRID, 1, 0); return true; }
                    break;

                case SPECIAL_AMULET1:
                    if (directionY < 0) next = SPECIAL_CRYSTAL1;
                    else if (directionX > 0) { currentRegion = REGION_CLOTHING; DestroySelectorBox(); EnsureClothingTargetList(menuWindow); EnsureGearExpandLabel(menuWindow); return true; }
                    else if (directionY > 0) next = SPECIAL_BRACELET1;
                    else if (directionX < 0) next = SPECIAL_AMULET0;
                    break;

                case SPECIAL_BRACELET0:
                    if (directionY < 0) next = SPECIAL_AMULET0;
                    else if (directionX > 0) next = SPECIAL_BRACELET1;
                    else if (directionY > 0) next = SPECIAL_RING0;
                    else if (directionX < 0) { SwitchRegion(menuWindow, REGION_RIGHT_GRID, 1, 0); return true; }
                    break;

                case SPECIAL_BRACELET1:
                    if (directionY < 0) next = SPECIAL_AMULET1;
                    else if (directionX > 0) { currentRegion = REGION_CLOTHING; DestroySelectorBox(); EnsureClothingTargetList(menuWindow); EnsureGearExpandLabel(menuWindow); return true; }
                    else if (directionY > 0) next = SPECIAL_RING1;
                    else if (directionX < 0) next = SPECIAL_BRACELET0;
                    break;

                case SPECIAL_RING0:
                    if (directionY < 0) next = SPECIAL_BRACELET0;
                    else if (directionX > 0) next = SPECIAL_RING1;
                    else if (directionY > 0) next = SPECIAL_BRACER0;
                    else if (directionX < 0) { SwitchRegion(menuWindow, REGION_RIGHT_GRID, 1, 1); return true; }
                    break;

                case SPECIAL_RING1:
                    if (directionY < 0) next = SPECIAL_BRACELET1;
                    else if (directionX > 0) { currentRegion = REGION_CLOTHING; DestroySelectorBox(); EnsureClothingTargetList(menuWindow); EnsureGearExpandLabel(menuWindow); return true; }
                    else if (directionY > 0) next = SPECIAL_BRACER1;
                    else if (directionX < 0) next = SPECIAL_RING0;
                    break;

                case SPECIAL_BRACER0:
                    if (directionY < 0) next = SPECIAL_RING0;
                    else if (directionX > 0) next = SPECIAL_BRACER1;
                    else if (directionY > 0) next = SPECIAL_MARK0;
                    else if (directionX < 0) { SwitchRegion(menuWindow, REGION_RIGHT_GRID, 1, 3); return true; }
                    break;

                case SPECIAL_BRACER1:
                    if (directionY < 0) next = SPECIAL_RING1;
                    else if (directionX > 0) { currentRegion = REGION_CLOTHING; DestroySelectorBox(); EnsureClothingTargetList(menuWindow); EnsureGearExpandLabel(menuWindow); return true; }
                    else if (directionY > 0) next = SPECIAL_MARK1;
                    else if (directionX < 0) next = SPECIAL_BRACER0;
                    break;

                case SPECIAL_MARK0:
                    if (directionY < 0) next = SPECIAL_BRACER0;
                    else if (directionX > 0) next = SPECIAL_MARK1;
                    else if (directionY > 0) next = SPECIAL_CRYSTAL0;
                    else if (directionX < 0) { SwitchRegion(menuWindow, REGION_RIGHT_GRID, 1, 5); return true; }
                    break;

                case SPECIAL_MARK1:
                    if (directionY < 0) next = SPECIAL_BRACER1;
                    else if (directionX > 0) { currentRegion = REGION_CLOTHING; DestroySelectorBox(); EnsureClothingTargetList(menuWindow); EnsureGearExpandLabel(menuWindow); return true; }
                    else if (directionY > 0) next = SPECIAL_CRYSTAL1;
                    else if (directionX < 0) next = SPECIAL_MARK0;
                    break;

                case SPECIAL_CRYSTAL0:
                    if (directionY < 0) next = SPECIAL_MARK0;
                    else if (directionX > 0) next = SPECIAL_CRYSTAL1;
                    else if (directionY > 0) next = SPECIAL_AMULET0;
                    else if (directionX < 0) { SwitchRegion(menuWindow, REGION_RIGHT_GRID, 1, 6); return true; }
                    break;

                case SPECIAL_CRYSTAL1:
                    if (directionY < 0) next = SPECIAL_MARK1;
                    else if (directionX > 0) { currentRegion = REGION_CLOTHING; DestroySelectorBox(); EnsureClothingTargetList(menuWindow); EnsureGearExpandLabel(menuWindow); return true; }
                    else if (directionY > 0) next = SPECIAL_AMULET1;
                    else if (directionX < 0) next = SPECIAL_CRYSTAL0;
                    break;
            }

            if (next != specialItemSelectedIndex)
            {
                specialItemSelectedIndex = next;
                RefreshSelectorToCurrentRegion();
                return true;
            }

            return false;
        }

    }
    

}
