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
        private void HandlePaperDollRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsurePaperDollIndicator(menuWindow);
            EnsurePaperDollTargetList(menuWindow);
            EnsureClothingExpandLabel(menuWindow);

            if (cm.Action1Pressed)
            {
                InvokeSelectedPaperDollLeftAction(menuWindow);
                return;
            }

            if (cm.Action2Pressed)
            {
                InvokeSelectedPaperDollRightAction(menuWindow);
                return;
            }

            if (cm.DPadUpPressed)
            {
                InvokeSelectedPaperDollMiddleAction(menuWindow);
                return;
            }

            if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
            {
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();
                DestroyClothingExpandLabel();
                DestroySelectorBox();

                currentRegion = REGION_CLOTHING;
                EnsureClothingTargetList(menuWindow);
                EnsureGearExpandLabel(menuWindow);
                return;
            }

            if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                SwitchRegion(menuWindow, REGION_LEFT_GRID, 0, 0);
                DestroyPaperDollIndicator();
                DestroyPaperDollTargetList();
                DestroyClothingExpandLabel();
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                if (paperDollSelectedIndex > 0)
                {
                    paperDollSelectedIndex--;
                    RefreshPaperDollIndicatorPosition();
                    EnsurePaperDollTargetList(menuWindow);
                }
                return;
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                if (paperDollSelectedIndex < paperDollAnchorsNative.Length - 1)
                {
                    paperDollSelectedIndex++;
                    RefreshPaperDollIndicatorPosition();
                    EnsurePaperDollTargetList(menuWindow);
                }
                return;
            }
        }

        private void EnsurePaperDollIndicator(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (paperDollIndicator == null)
            {
                paperDollIndicator = new DiamondIndicatorOverlay(panelRenderWindow);
                float diamondRadius = Mathf.Max(6f, 7f * inventoryUiScale);
                float pointSize = Mathf.Max(4f, 5f * inventoryUiScale);

                paperDollIndicator.Build(
                    diamondRadius,
                    pointSize,
                    new Color(1f, 1f, 0f, 0.95f)
                );
            }

            RefreshPaperDollIndicatorPosition();
        }


        private void EnsurePaperDollTargetList(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (paperDollTargetList == null)
            {
                paperDollTargetList = new PaperDollTargetListOverlay(panelRenderWindow);
                paperDollTargetList.Build(paperDollTargetNames);
            }

            paperDollTargetList.SetSelectedIndex(paperDollSelectedIndex);

            // First-pass native placement inside inventory UI space.
            // Tune these later if needed.
            Rect listRect = NativeInventoryRectToOverlayRect(134f, 147f, 26f, 50f);
            paperDollTargetList.SetRect(listRect);
        }

        private void RefreshPaperDollIndicatorPosition()
        {
            if (paperDollIndicator == null)
                return;

            if (paperDollSelectedIndex < 0 || paperDollSelectedIndex >= paperDollAnchorsNative.Length)
                return;

            Vector2 pos = NativeInventoryPointToOverlay(paperDollAnchorsNative[paperDollSelectedIndex]);
            paperDollIndicator.SetCenter(pos);
        }

        private EquipSlots? GetPaperDollSelectedSlot()
        {
            switch (paperDollSelectedIndex)
            {
                case 0: return EquipSlots.Head;
                case 1: return EquipSlots.RightArm;
                case 2: return EquipSlots.LeftArm;
                case 3: return EquipSlots.ChestArmor;
                case 4: return EquipSlots.RightHand;
                case 5: return EquipSlots.LeftHand;
                case 6: return EquipSlots.Gloves;
                case 7: return EquipSlots.LegsArmor;
                case 8: return EquipSlots.Feet;
                default: return null;
            }
        }


        private DaggerfallUnityItem GetSelectedPaperDollItem(DaggerfallInventoryWindow menuWindow)
        {
            EquipSlots? slot = GetPaperDollSelectedSlot();
            if (!slot.HasValue)
                return null;

            if (GameManager.Instance.PlayerEntity == null || GameManager.Instance.PlayerEntity.ItemEquipTable == null)
                return null;

            return GameManager.Instance.PlayerEntity.ItemEquipTable.GetItem(slot.Value);
        }

        private void InvokeSelectedPaperDollLeftAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedPaperDollItem(menuWindow);

            if (item == null || fiSelectedActionMode == null)
                return;

            SaveResumeSelectorState();

            object actionModeObj = fiSelectedActionMode.GetValue(menuWindow);
            if (actionModeObj == null)
                return;

            int actionMode = Convert.ToInt32(actionModeObj);

            // ActionModes:
            // 0 = Info
            // 1 = Equip
            // 2 = Remove
            // 3 = Use
            // 4 = Select
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
                        // Paper doll left-click in Remove mode should do nothing useful in vanilla path.
                        // Remove is for item lists/containers, not equipped-slot clicks.
                    break;

                case 3: // Use
                    if (miUseItem != null)
                        miUseItem.Invoke(menuWindow, new object[] { item, null });
                    break;
            }
        }

        private void InvokeSelectedPaperDollRightAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedPaperDollItem(menuWindow);
            if (item == null || fiSelectedActionMode == null)
                return;

            SaveResumeSelectorState();

            object actionModeObj = fiSelectedActionMode.GetValue(menuWindow);
            if (actionModeObj == null)
                return;

            int actionMode = Convert.ToInt32(actionModeObj);

            // Mirror DFU GetActionModeRightClick():
            // Equip -> Remove
            // Remove -> Equip
            // Select -> Remove
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
                        // Again, no direct paper-doll remove behavior needed here.
                    break;

                case 3: // Use
                    if (miUseItem != null)
                        miUseItem.Invoke(menuWindow, new object[] { item, null });
                    break;
            }
        }

        private void InvokeSelectedPaperDollMiddleAction(DaggerfallInventoryWindow menuWindow)
        {
            DaggerfallUnityItem item = GetSelectedPaperDollItem(menuWindow);
            if (item == null || miNextVariant == null)
                return;

            SaveResumeSelectorState();
            miNextVariant.Invoke(menuWindow, new object[] { item });
        }
    }
}
