using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class SaveGameAssist
    {
        private static readonly Rect filesPanelRect = new Rect(23.9f, 39.9f, 100.5f, 131.3f);
        private static readonly Rect namingRect = new Rect(23.7f, 26.6f, 272.4f, 9.7f);

        private void TickFilesPanelRegion(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager cm)
        {
            EnsureFilesPanelFocus(menuWindow);
            ForceFirstSaveSelectionIfNeeded(menuWindow);

            bool moveUp = cm.RStickUpPressed || cm.RStickUpHeldSlow;
            bool moveDown = cm.RStickDownPressed || cm.RStickDownHeldSlow;
            bool moveRight = cm.RStickRightPressed || cm.RStickRightHeldSlow;

            bool renamePressed = cm.DPadLeftPressed || cm.DPadLeftHeldSlow;
            bool isAssisting =
                moveUp ||
                moveDown ||
                moveRight ||
                renamePressed ||
                cm.Action1Released ||
                cm.Action2Pressed ||
                cm.LegendPressed;

            if (!isAssisting)
                return;

            if (moveUp)
            {
                FilesPanelMoveUp(menuWindow);
                return;
            }

            if (moveDown)
            {
                FilesPanelMoveDown(menuWindow);
                return;
            }

            if (moveRight)
            {
                ExitFilesPanelToGo(menuWindow);
                return;
            }

            if (renamePressed)
            {
                ActivateRename(menuWindow);
                return;
            }

            if (cm.Action2Pressed)
            {
                ActivateDelete(menuWindow);
                return;
            }

            if (cm.Action1Released)
            {
                ActivateSelectedFile(menuWindow);
                return;
            }

            if (cm.LegendPressed)
            {
                bool show = !legendVisible;
                legendVisible = show;

                if (show)
                    EnsureFilesLegendUI(menuWindow, cm);
                else
                    DestroyLegend();

                return;
            }
        }

        private void RefreshSelectorToFilesPanel(DaggerfallUnitySaveGameWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                filesPanelRect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private void RefreshSelectorToNaming(DaggerfallUnitySaveGameWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                namingRect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private void EnterFilesPanelRegion(DaggerfallUnitySaveGameWindow menuWindow)
        {
            currentRegion = RegionFilesPanel;
            buttonSelected = IsLoadMode(menuWindow) ? lwFilesPanel : FilesPanel;

            DestroyKeyboard();
            BeginRestoreBackBinding();
            EnsureFilesPanelFocus(menuWindow);
            ForceFirstSaveSelectionIfNeeded(menuWindow);
            RefreshSelectorToFilesPanel(menuWindow);
        }

        private void EnterNamingRegion(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (!IsSaveMode(menuWindow))
                return;

            if (legendVisible)
                DestroyLegend();

            currentRegion = RegionNaming;
            buttonSelected = NamingBox;
            RefreshSelectorToNaming(menuWindow);
            BuildKeyboard(menuWindow);
        }

        private void TickNamingRegion(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager cm)
        {
            RefreshKeyboardAttachment(menuWindow);

            if (backBindingSuppressed &&
                suppressedBackButton != KeyCode.None)
            {
                if (!closeDeferred && InputManager.Instance != null &&
                    InputManager.Instance.GetKeyDown(suppressedBackButton, false))
                {
                    closeDeferred = true;
                }

                if (closeDeferred && InputManager.Instance != null &&
                    InputManager.Instance.GetKeyUp(suppressedBackButton, false))
                {
                    closeDeferred = false;
                    EnterFilesPanelRegion(menuWindow);
                    BeginRestoreBackBinding();
                    DestroyLegend();
                    return;
                }
            }

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None && keyboardOverlay != null)
            {
                switch (dir)
                {
                    case ControllerManager.StickDir8.W:
                    case ControllerManager.StickDir8.NW:
                    case ControllerManager.StickDir8.SW:
                        keyboardOverlay.MoveLeft();
                        break;

                    case ControllerManager.StickDir8.E:
                    case ControllerManager.StickDir8.NE:
                    case ControllerManager.StickDir8.SE:
                        keyboardOverlay.MoveRight();
                        break;

                    case ControllerManager.StickDir8.N:
                        keyboardOverlay.MoveUp();
                        break;

                    case ControllerManager.StickDir8.S:
                        keyboardOverlay.MoveDown();
                        break;
                }
            }

            bool isAssisting =
                dir != ControllerManager.StickDir8.None ||
                cm.DPadLeftPressed || cm.DPadLeftHeldSlow ||
                cm.DPadUpPressed ||
                cm.DPadDownPressed ||
                cm.DPadRightReleased ||
                cm.Action1Released ||
                cm.Action2Pressed ||
                cm.LegendPressed;

            if (!isAssisting)
                return;

            if (cm.DPadUpPressed && keyboardOverlay != null)
                keyboardOverlay.ToggleShift();

            if (cm.DPadDownPressed && keyboardOverlay != null)
                keyboardOverlay.Toggle123();

            if (cm.DPadRightReleased)
            {
                ActivateGo(menuWindow);
                return;
            }

            if (cm.DPadLeftPressed || cm.DPadLeftHeldSlow)
                BackspaceSaveName(menuWindow);

            if (cm.Action2Pressed)
            {
                TextBox textBox = GetSaveNameTextBox(menuWindow);
                if (textBox != null)
                    textBox.Text = string.Empty;
            }

            if (cm.Action1Released && keyboardOverlay != null)
            {
                ActivateKeyboardKey(menuWindow, keyboardOverlay.ActivateSelectedKey());
                return;
            }

            if (cm.LegendPressed)
            {
                bool show = !legendVisible;
                legendVisible = show;

                if (show)
                    EnsureKeyboardLegendUI(menuWindow, cm);
                else
                    DestroyLegend();

                return;
            }
        }

        private void FilesPanelMoveUp(DaggerfallUnitySaveGameWindow menuWindow)
        {
            ListBox savesList = GetSavesList(menuWindow);
            if (savesList == null)
                return;

            if (savesList.Count == 0)
            {
                if (IsSaveMode(menuWindow))
                    EnterNamingRegion(menuWindow);
                return;
            }

            int selectedIndex = savesList.SelectedIndex;
            if (selectedIndex < 0)
            {
                ForceFirstSaveSelectionIfNeeded(menuWindow);
                selectedIndex = savesList.SelectedIndex;
            }

            if (selectedIndex <= 0)
            {
                if (IsSaveMode(menuWindow))
                    EnterNamingRegion(menuWindow);
                return;
            }

            DaggerfallUI.Instance.OnKeyPress(KeyCode.UpArrow, true);
            DaggerfallUI.Instance.OnKeyPress(KeyCode.UpArrow, false);
        }

        private void FilesPanelMoveDown(DaggerfallUnitySaveGameWindow menuWindow)
        {
            ListBox savesList = GetSavesList(menuWindow);
            if (savesList == null)
                return;

            if (savesList.Count == 0)
            {
                ExitFilesPanelToRename(menuWindow);
                return;
            }

            int selectedIndex = savesList.SelectedIndex;
            if (selectedIndex < 0)
            {
                ForceFirstSaveSelectionIfNeeded(menuWindow);
                selectedIndex = savesList.SelectedIndex;
            }

            if (selectedIndex >= savesList.Count - 1)
            {
                ExitFilesPanelToRename(menuWindow);
                return;
            }

            DaggerfallUI.Instance.OnKeyPress(KeyCode.DownArrow, true);
            DaggerfallUI.Instance.OnKeyPress(KeyCode.DownArrow, false);
        }

        private void ExitFilesPanelToRename(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (IsLoadMode(menuWindow))
                EnterButtonsRegionAt(lwRenameButton, menuWindow);
            else
                EnterButtonsRegionAt(RenameButton, menuWindow);
        }

        private void ExitFilesPanelToGo(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (IsLoadMode(menuWindow))
                EnterButtonsRegionAt(lwLoadButton, menuWindow);
            else
                EnterButtonsRegionAt(SaveButton, menuWindow);
        }

        private void ActivateSelectedFile(DaggerfallUnitySaveGameWindow menuWindow)
        {
            ListBox savesList = GetSavesList(menuWindow);
            if (savesList == null || savesList.Count <= 0)
                return;

            if (savesList.SelectedIndex < 0)
                ForceFirstSaveSelectionIfNeeded(menuWindow);

            ActivateGo(menuWindow);
        }

        private void EnsureFilesPanelFocus(DaggerfallUnitySaveGameWindow menuWindow)
        {
            ListBox savesList = GetSavesList(menuWindow);
            if (menuWindow == null || savesList == null)
                return;

            savesList.AlwaysAcceptKeyboardInput = true;

            if (!object.ReferenceEquals(menuWindow.FocusControl, savesList))
                menuWindow.SetFocus(savesList);
        }

        private void ForceFirstSaveSelectionIfNeeded(DaggerfallUnitySaveGameWindow menuWindow)
        {
            ListBox savesList = GetSavesList(menuWindow);
            if (savesList == null || savesList.Count <= 0)
                return;

            if (savesList.SelectedIndex >= 0)
                return;

            savesList.SelectIndex(0);
        }

        private bool IsSaveMode(DaggerfallUnitySaveGameWindow menuWindow)
        {
            object value = fiMode != null ? fiMode.GetValue(menuWindow) : null;
            return value != null && value.ToString() == "SaveGame";
        }

        private bool IsLoadMode(DaggerfallUnitySaveGameWindow menuWindow)
        {
            object value = fiMode != null ? fiMode.GetValue(menuWindow) : null;
            return value != null && value.ToString() == "LoadGame";
        }

        private ListBox GetSavesList(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || fiSavesList == null)
                return null;

            return fiSavesList.GetValue(menuWindow) as ListBox;
        }

        private TextBox GetSaveNameTextBox(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || fiSaveNameTextBox == null)
                return null;

            return fiSaveNameTextBox.GetValue(menuWindow) as TextBox;
        }
    }
}
