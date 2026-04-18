using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Reflection;
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

            RefreshSelectorToCurrentRegion(menuWindow);

            if (cm.Action1Released)
            {
                InvokeSelectedButton(menuWindow);
                return;
            }

            if (cm.RStickUpPressed || cm.RStickUpHeldSlow)
            {
                if (MoveButtonSelectionUp(menuWindow))
                    RefreshSelectorToCurrentRegion(menuWindow);
            }

            if (cm.RStickDownPressed || cm.RStickDownHeldSlow)
            {
                if (MoveButtonSelectionDown(menuWindow))
                    RefreshSelectorToCurrentRegion(menuWindow);
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

            if (IsTradeWindow(menuWindow))
            {
                InvokeSelectedTradeButton(menuWindow);
                return;
            }

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

                case 6: // Exit
                    DestroyOwnedUi();
                    menuWindow.CloseWindow();
                    break;
            }
        }

        private void InvokeTradeButtonClick(DaggerfallInventoryWindow menuWindow, FieldInfo fiButton, MethodInfo miClick)
        {
            if (menuWindow == null || fiButton == null || miClick == null)
                return;

            object button = fiButton.GetValue(menuWindow);
            if (button == null)
                return;

            BaseScreenComponent component = button as BaseScreenComponent;
            if (component != null && !component.Enabled)
                return;

            miClick.Invoke(menuWindow, new object[] { button, Vector2.zero });
        }

        private void InvokeSelectedTradeButton(DaggerfallInventoryWindow menuWindow)
        {
            switch (buttonSelectedIndex)
            {
                case 0: // Wagon
                    InvokeTradeButtonClick(menuWindow, fiWagonButton, miWagonButtonClick);
                    break;

                case 1: // Info
                    InvokeSelectActionMode(menuWindow, 0);
                    break;

                case 2: // Select
                    InvokeTradeButtonClick(menuWindow, fiSelectButton, miSelectButtonClick);
                    break;

                case 3: // Steal (Buy only)
                    if (IsTradeBuyMode(menuWindow))
                        InvokeTradeButtonClick(menuWindow, fiStealButton, miStealButtonClick);
                    break;

                case 4: // Buy / Sell / Repair / Identify
                    InvokeTradeButtonClick(menuWindow, fiModeActionButton, miModeActionButtonClick);
                    break;

                case 5: // Clear
                    InvokeTradeButtonClick(menuWindow, fiClearButton, miClearButtonClick);
                    break;

                case 6: // Exit
                    DestroyOwnedUi();
                    menuWindow.CloseWindow();
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
        private bool MoveButtonSelectionUp(DaggerfallInventoryWindow menuWindow)
        {
            ClearGridRowMemory();

            if (!IsTradeWindow(menuWindow))
            {
                if (buttonSelectedIndex <= 0)
                    return false;

                buttonSelectedIndex--;
                return true;
            }

            int next = buttonSelectedIndex - 1;
            while (next >= 0)
            {
                if (IsTradeButtonIndexValid(menuWindow, next))
                {
                    buttonSelectedIndex = next;
                    return true;
                }

                next--;
            }

            return false;
        }

        private bool MoveButtonSelectionDown(DaggerfallInventoryWindow menuWindow)
        {
            ClearGridRowMemory();

            if (!IsTradeWindow(menuWindow))
            {
                if (buttonSelectedIndex >= buttonAnchorsNative.Length - 1)
                    return false;

                buttonSelectedIndex++;
                return true;
            }

            int next = buttonSelectedIndex + 1;
            while (next < tradeButtonAnchorsNative.Length)
            {
                if (IsTradeButtonIndexValid(menuWindow, next))
                {
                    buttonSelectedIndex = next;
                    return true;
                }

                next++;
            }

            return false;
        }

        private int GetButtonIndexFromLocalGridRow(DaggerfallInventoryWindow menuWindow, int row)
        {
            if (IsTradeWindow(menuWindow))
                return GetTradeButtonIndexFromLocalGridRow(menuWindow, row);

            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Equip
                case 2: return 3; // Remove
                case 3: return 4; // Use
                case 4:
                case 5:
                case 6: return 5; // Gold
                case 7: return 6; // Exit
                default: return 5;
            }
        }

        private int GetButtonIndexFromRemoteGridRow(DaggerfallInventoryWindow menuWindow, int row)
        {
            if (IsTradeWindow(menuWindow))
                return GetTradeButtonIndexFromRemoteGridRow(menuWindow, row);

            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Equip
                case 2: return 3; // Remove
                case 3: return 4; // Use
                case 4:
                case 5:
                case 6: return 5; // Gold
                case 7: return 6; // Exit
                default: return 5;
            }
        }

        private int GetLocalRowFromButtons(DaggerfallInventoryWindow menuWindow)
        {
            if (IsTradeWindow(menuWindow))
                return GetTradeLocalRowFromButtons(menuWindow);

            if (gridRowMemory >= 0)
            {
                int rememberedRow = gridRowMemory;
                gridRowMemory = -1;
                return rememberedRow;
            }

            switch (buttonSelectedIndex)
            {
                case 0: return 0; // Wagon
                case 1: return 0; // Info
                case 2: return 1; // Equip
                case 3: return 2; // Remove
                case 4: return 3; // Use
                case 5: return 4; // Gold
                case 6: return 7; // Exit
                default: return 0;
            }
        }

        private int GetRemoteRowFromButtons(DaggerfallInventoryWindow menuWindow)
        {
            if (IsTradeWindow(menuWindow))
                return GetTradeRemoteRowFromButtons(menuWindow);

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
                case 6: return 7; // Exit
                default: return 0;
            }
        }

        private int GetTradeLocalRowFromButtons(DaggerfallInventoryWindow menuWindow)
        {
            if (gridRowMemory >= 0)
            {
                int rememberedRow = gridRowMemory;
                gridRowMemory = -1;
                return rememberedRow;
            }

            switch (buttonSelectedIndex)
            {
                case 0: return 0; // Wagon
                case 1: return 0; // Info
                case 2: return 1; // Select
                case 3: return 3; // Steal
                case 4: return 4; // ModeAction
                case 5: return 5; // Clear
                case 6: return 7; // Exit
                default: return 0;
            }
        }

        private int GetTradeRemoteRowFromButtons(DaggerfallInventoryWindow menuWindow)
        {
            if (gridRowMemory >= 0)
            {
                int rememberedRow = gridRowMemory;
                gridRowMemory = -1;
                return rememberedRow;
            }

            switch (buttonSelectedIndex)
            {
                case 0: return 0; // Wagon
                case 1: return 0; // Info
                case 2: return 1; // Select
                case 3: return 3; // Steal
                case 4: return 4; // ModeAction
                case 5: return 5; // Clear
                case 6: return 7; // Exit
                default: return 0;
            }
        }

        private int GetTradeButtonIndexFromLocalGridRow(DaggerfallInventoryWindow menuWindow, int row)
        {
            if (IsTradeBuyMode(menuWindow))
            {
                switch (row)
                {
                    case 0: return 1; // Info
                    case 1: return 2; // Select
                    case 2: return 3; // Steal
                    case 3: return 3; // Steal
                    case 4: return 4; // Buy
                    case 5:
                    case 6: return 5; // Clear
                    case 7: return 6; // Exit
                    default: return 5;
                }
            }

            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Select
                case 2: return 4; // Sell / Repair / Identify
                case 3: return 4; // Sell / Repair / Identify
                case 4: return 4; // Sell / Repair / Identify
                case 5:
                case 6: return 5; // Clear
                case 7: return 6; // Exit
                default: return 5;
            }
        }

        private int GetTradeButtonIndexFromRemoteGridRow(DaggerfallInventoryWindow menuWindow, int row)
        {
            if (IsTradeBuyMode(menuWindow))
            {
                switch (row)
                {
                    case 0: return 1; // Info
                    case 1: return 2; // Select
                    case 2: return 3; // Steal
                    case 3: return 3; // Steal
                    case 4: return 4; // Buy
                    case 7: return 6;
                    default: return 5; // Clear
                }
            }

            switch (row)
            {
                case 0: return 1; // Info
                case 1: return 2; // Select
                case 2: return 4; // Sell / Repair
                case 3: return 4; // Sell / Repair
                case 4: return 4; // Sell / Repair
                case 7: return 6;
                default: return 5; // Clear
            }
        }

        private void RouteLeftGridToButtons(DaggerfallInventoryWindow menuWindow)
        {
            if (selectedColumn == 1 && selectedRow == 7)
            {
                ClearGridRowMemory();
                SwitchRegionToButtons(menuWindow, InventoryExitButtonIndex);
                return;
            }

            RememberGridRowIfExtended();
            SwitchRegionToButtons(menuWindow, GetButtonIndexFromLocalGridRow(menuWindow, selectedRow));
        }

        private void RouteRightGridToButtons(DaggerfallInventoryWindow menuWindow)
        {
            if (selectedColumn == 0 && selectedRow == 7)
            {
                ClearGridRowMemory();
                SwitchRegionToButtons(menuWindow, InventoryExitButtonIndex);
                return;
            }

            RememberGridRowIfExtended();
            SwitchRegionToButtons(menuWindow, GetButtonIndexFromRemoteGridRow(menuWindow, selectedRow));
        }

        private void RouteButtonsToLeftGrid(DaggerfallInventoryWindow menuWindow)
        {
            int targetRow = GetLocalRowFromButtons(menuWindow);
            SwitchRegion(menuWindow, REGION_LEFT_GRID, 1, targetRow); // Local col 2 = zero-based col 1
        }

        private void RouteButtonsToRightGrid(DaggerfallInventoryWindow menuWindow)
        {
            int targetRow = GetRemoteRowFromButtons(menuWindow);
            SwitchRegion(menuWindow, REGION_RIGHT_GRID, 0, targetRow); // Remote col 1 = zero-based col 0
        }
    }
}
