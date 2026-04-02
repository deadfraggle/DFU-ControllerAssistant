using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Items;
using System;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InventoryAssist
    {
        private void HandleClothingRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureClothingTargetList(menuWindow);
            EnsureGearExpandLabel(menuWindow);

            if (cm.Action1Released)
            {
                InvokeSelectedClothingLeftAction(menuWindow);
                return;
            }

            if (cm.Action2Released)
            {
                InvokeSelectedClothingRightAction(menuWindow);
                return;
            }

            if (cm.DPadUpPressed)
            {
                InvokeSelectedClothingMiddleAction(menuWindow);
                return;
            }

            if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
            {
                DestroyClothingTargetList();
                DestroyGearExpandLabel();
                DestroySelectorBox();

                currentRegion = REGION_SPECIAL_ITEMS;
                specialItemSelectedIndex = SPECIAL_RING1;
                EnsureSelectorBox(menuWindow);
                RefreshSelectorToCurrentRegion();
                return;
            }


            if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                DestroyClothingTargetList();
                DestroyGearExpandLabel();
                DestroySelectorBox();

                currentRegion = REGION_PAPERDOLL;
                EnsurePaperDollIndicator(menuWindow);
                EnsurePaperDollTargetList(menuWindow);
                EnsureClothingExpandLabel(menuWindow);
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                if (clothingSelectedIndex > 0)
                {
                    clothingSelectedIndex--;
                    EnsureClothingTargetList(menuWindow);
                }
                return;
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                if (clothingSelectedIndex < clothingTargetNames.Length - 1)
                {
                    clothingSelectedIndex++;
                    EnsureClothingTargetList(menuWindow);
                }
                return;
            }
        }
        private EquipSlots? GetClothingSelectedSlot()
        {
            switch (clothingSelectedIndex)
            {
                case 0: return EquipSlots.Head;           // Hat
                case 1: return GetPreferredCloakSlot();   // Cloak
                case 2: return EquipSlots.ChestClothes;   // Chest
                case 3: return EquipSlots.Gloves;         // Gloves
                case 4: return EquipSlots.LegsClothes;    // Legs
                case 5: return EquipSlots.Feet;           // Feet
                default: return null;
            }
        }
        private EquipSlots GetPreferredCloakSlot()
        {
            if (GameManager.Instance.PlayerEntity == null || GameManager.Instance.PlayerEntity.ItemEquipTable == null)
                return EquipSlots.Cloak1;

            DaggerfallUnityItem cloak1 = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.Cloak1);
            DaggerfallUnityItem cloak2 = GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(EquipSlots.Cloak2);

            if (cloak1 != null)
                return EquipSlots.Cloak1;

            if (cloak2 != null)
                return EquipSlots.Cloak2;

            return EquipSlots.Cloak1;
        }
        private DaggerfallUnityItem GetSelectedClothingItem(DaggerfallInventoryWindow menuWindow)
        {
            EquipSlots? slot = GetClothingSelectedSlot();
            if (!slot.HasValue)
                return null;

            if (GameManager.Instance.PlayerEntity == null || GameManager.Instance.PlayerEntity.ItemEquipTable == null)
                return null;

            return GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(slot.Value);
        }

        private void InvokeSelectedClothingLeftAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedClothingItem(menuWindow);

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

        private void InvokeSelectedClothingRightAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedClothingItem(menuWindow);
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

        private void InvokeSelectedClothingMiddleAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedClothingItem(menuWindow);
            if (item == null || miNextVariant == null)
                return;

            SaveResumeSelectorState();
            miNextVariant.Invoke(menuWindow, new object[] { item });
        }

        private void EnsureClothingExpandLabel(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (clothingExpandOverlay == null)
            {
                clothingExpandOverlay = new ClothingExpandOverlay(panelRenderWindow);
                clothingExpandOverlay.Build("Clothing...");
            }

            Rect rect = NativeInventoryRectToOverlayRect(49f, 189.8f, 26f, 50f);

            // Match the body-parts list text scale by using its native height (50)
            float matchingTextScale = Mathf.Max(1.8f, (50f * inventoryUiScale) / 62f);

            clothingExpandOverlay.SetRect(rect, matchingTextScale);
        }

        private void EnsureClothingTargetList(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (clothingTargetList == null)
            {
                clothingTargetList = new ClothingTargetListOverlay(panelRenderWindow);
                clothingTargetList.Build(clothingTargetNames);
            }

            clothingTargetList.SetSelectedIndex(clothingSelectedIndex);

            // Same x as Clothing..., lower y so the list rises upward from that area.
            Rect listRect = NativeInventoryRectToOverlayRect(49f, 163f, 26f, 78f);

            // Match paper-doll list text scale by using the same 50-native-height reference
            float matchingTextScale = Mathf.Max(1.8f, (50f * inventoryUiScale) / 62f);

            clothingTargetList.SetRect(listRect, matchingTextScale);
        }

        private void EnsureGearExpandLabel(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (gearExpandOverlay == null)
            {
                gearExpandOverlay = new GearExpandOverlay(panelRenderWindow);
                gearExpandOverlay.Build("Gear...");
            }

            Rect rect = NativeInventoryRectToOverlayRect(134f, 189.8f, 26f, 50f);
            float matchingTextScale = Mathf.Max(1.8f, (50f * inventoryUiScale) / 62f);

            gearExpandOverlay.SetRect(rect, matchingTextScale);
        }
    }
}
