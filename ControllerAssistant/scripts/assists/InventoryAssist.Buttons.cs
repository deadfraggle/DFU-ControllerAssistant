using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InventoryAssist
    {
        private void HandleButtonsRegion(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
        {
            EnsureInventoryGrids(menuWindow);

            if (selectorBox == null)
                EnsureSelectorBox(menuWindow);

            RefreshSelectorToCurrentRegion();

            if (cm.Action1Pressed)
            {
                InvokeSelectedButton(menuWindow);
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                if (MoveButtonSelectionUp())
                    RefreshSelectorToCurrentRegion();
                return;
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                if (MoveButtonSelectionDown())
                    RefreshSelectorToCurrentRegion();
                return;
            }

            if (cm.RStickLeftPressed || cm.RStickLeftHeldSlow)
            {
                RouteButtonsToLeftGrid(menuWindow);
                return;
            }

            if (cm.RStickRightPressed || cm.RStickRightHeldSlow)
            {
                RouteButtonsToRightGrid(menuWindow);
                return;
            }
        }
        private void InvokeSelectedButton(DaggerfallInventoryWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            SaveResumeSelectorState();

            switch (buttonSelectedIndex)
            {
                case 0: // Wagon
                    if (miWagonButtonClick != null && fiWagonButton != null)
                    {
                        object button = fiWagonButton.GetValue(menuWindow);
                        if (button != null)
                            miWagonButtonClick.Invoke(menuWindow, new object[] { button, Vector2.zero });
                    }
                    break;

                case 1: // Info
                    InvokeSelectActionMode(menuWindow, 0);
                    break;

                case 2: // Equip
                    InvokeSelectActionMode(menuWindow, 1);
                    break;

                case 3: // Remove
                    InvokeSelectActionMode(menuWindow, 2);
                    break;

                case 4: // Use
                    InvokeSelectActionMode(menuWindow, 3);
                    break;

                case 5: // Gold
                    if (miGoldButtonClick != null && fiGoldButton != null)
                    {
                        object button = fiGoldButton.GetValue(menuWindow);
                        if (button != null)
                            miGoldButtonClick.Invoke(menuWindow, new object[] { button, Vector2.zero });
                    }
                    break;
            }
        }
        private void InvokeSelectActionMode(DaggerfallInventoryWindow menuWindow, int modeValue)
        {
            if (menuWindow == null || miSelectActionMode == null || fiSelectedActionMode == null)
                return;

            object currentValue = fiSelectedActionMode.GetValue(menuWindow);
            if (currentValue == null)
                return;

            object nextEnum = Enum.ToObject(currentValue.GetType(), modeValue);
            miSelectActionMode.Invoke(menuWindow, new object[] { nextEnum });
        }
        private bool MoveButtonSelectionUp()
        {
            if (buttonSelectedIndex <= 0)
                return false;

            ClearGridRowMemory();
            buttonSelectedIndex--;
            return true;
        }

        private bool MoveButtonSelectionDown()
        {
            if (buttonSelectedIndex >= buttonAnchorsNative.Length - 1)
                return false;

            ClearGridRowMemory();
            buttonSelectedIndex++;
            return true;
        }

        private int GetButtonIndexFromLocalGridRow(int row)
        {
            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Equip
                case 2: return 3; // Remove
                case 3: return 4; // Use
                default: return 5; // Gold for rows 5-8 visual (and anything below Use)
            }
        }

        private int GetButtonIndexFromRemoteGridRow(int row)
        {
            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Equip
                case 2: return 3; // Remove
                case 3: return 4; // Use
                default: return 5; // Gold for rows 5-8 visual (and anything below Use)
            }
        }

        private int GetLocalRowFromButtons()
        {
            if (gridRowMemory >= 0)
            {
                int rememberedRow = gridRowMemory;
                gridRowMemory = -1;
                return rememberedRow;
            }

            switch (buttonSelectedIndex)
            {
                case 0: return 0; // Wagon  -> Local 2,1
                case 1: return 0; // Info   -> Local 2,1
                case 2: return 1; // Equip  -> Local 2,2
                case 3: return 2; // Remove -> Local 2,3
                case 4: return 3; // Use    -> Local 2,4
                case 5: return 4; // Gold   -> Local 2,5
                default: return 0;
            }
        }

        private int GetRemoteRowFromButtons()
        {
            if (gridRowMemory >= 0)
            {
                int rememberedRow = gridRowMemory;
                gridRowMemory = -1;
                return rememberedRow;
            }

            switch (buttonSelectedIndex)
            {
                case 0: return 0; // Wagon  -> Remote 1,1
                case 1: return 0; // Info   -> Remote 1,1
                case 2: return 1; // Equip  -> Remote 1,2
                case 3: return 2; // Remove -> Remote 1,3
                case 4: return 3; // Use    -> Remote 1,4
                case 5: return 4; // Gold   -> Remote 1,5
                default: return 0;
            }
        }

        private void RouteLeftGridToButtons(DaggerfallInventoryWindow menuWindow)
        {
            RememberGridRowIfExtended();
            SwitchRegionToButtons(menuWindow, GetButtonIndexFromLocalGridRow(selectedRow));
        }

        private void RouteRightGridToButtons(DaggerfallInventoryWindow menuWindow)
        {
            RememberGridRowIfExtended();
            SwitchRegionToButtons(menuWindow, GetButtonIndexFromRemoteGridRow(selectedRow));
        }

        private void RouteButtonsToLeftGrid(DaggerfallInventoryWindow menuWindow)
        {
            int targetRow = GetLocalRowFromButtons();
            SwitchRegion(menuWindow, REGION_LEFT_GRID, 1, targetRow); // Local col 2 = zero-based col 1
        }

        private void RouteButtonsToRightGrid(DaggerfallInventoryWindow menuWindow)
        {
            int targetRow = GetRemoteRowFromButtons();
            SwitchRegion(menuWindow, REGION_RIGHT_GRID, 0, targetRow); // Remote col 1 = zero-based col 0
        }
    }
}
