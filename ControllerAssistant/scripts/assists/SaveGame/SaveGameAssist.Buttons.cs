using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class SaveGameAssist
    {
        private readonly SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(23.7f, 26.6f, 272.4f, 9.7f),  N = -1,              S = FilesPanel,      E = SaveButton,         W = -1 },          // NamingBox
            new SelectorButtonInfo { rect = new Rect(23.9f, 39.9f, 100.5f, 131.3f), N = NamingBox,       S = RenameButton,    E = SaveButton,         W = RenameButton },// FilesPanel
            new SelectorButtonInfo { rect = new Rect(25.7f, 171.5f, 48.6f, 9.0f),   N = FilesPanel,      S = -1,              E = DeleteButton,       W = -1 },          // RenameButton
            new SelectorButtonInfo { rect = new Rect(74.7f, 171.5f, 48.6f, 9.0f),   N = FilesPanel,      S = -1,              E = SaveButton,         W = RenameButton },// DeleteButton
            new SelectorButtonInfo { rect = new Rect(127.7f, 164.7f, 40.6f, 16.7f), N = NamingBox,       S = -1,              E = CancelButton,       W = DeleteButton },// SaveButton
            new SelectorButtonInfo { rect = new Rect(255.8f, 164.7f, 40.6f, 16.7f), N = NamingBox,       S = -1,              E = -1,                 W = SaveButton },  // CancelButton

            new SelectorButtonInfo { rect = new Rect(23.9f, 39.9f, 100.5f, 131.3f), N = -1,              S = lwRenameButton,  E = lwLoadButton,       W = lwRenameButton },// lwFilesPanel
            new SelectorButtonInfo { rect = new Rect(25.7f, 171.5f, 48.6f, 9.0f),   N = lwFilesPanel,    S = -1,              E = lwDeleteButton,     W = -1 },            // lwRenameButton
            new SelectorButtonInfo { rect = new Rect(74.7f, 171.5f, 48.6f, 9.0f),   N = lwFilesPanel,    S = -1,              E = lwLoadButton,       W = lwRenameButton },// lwDeleteButton
            new SelectorButtonInfo { rect = new Rect(127.7f, 164.7f, 40.6f, 16.7f), N = lwSwitchCharButton, S = -1,           E = lwCancelButton,     W = lwDeleteButton },// lwLoadButton
            new SelectorButtonInfo { rect = new Rect(255.8f, 164.7f, 40.6f, 16.7f), N = lwSwitchCharButton, S = -1,           E = -1,                 W = lwLoadButton },  // lwCancelButton
            new SelectorButtonInfo { rect = new Rect(235.8f, 16.6f, 60.5f, 8.8f),   N = -1,              S = lwCancelButton,  E = -1,                 W = lwFilesPanel },   // lwSwitchCharButton
        };

        private void TickButtonsRegion(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager cm)
        {
            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            bool isAssisting =
                dir != ControllerManager.StickDir8.None ||
                cm.Action1Released ||
                cm.LegendPressed;

            if (!isAssisting)
                return;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            if (cm.Action1Released)
            {
                ActivateSelectedButton(menuWindow);
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

        private void TryMoveSelector(DaggerfallUnitySaveGameWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            int previous = buttonSelected;
            SelectorButtonInfo btn = menuButton[buttonSelected];
            int next = -1;

            switch (dir)
            {
                case ControllerManager.StickDir8.N: next = btn.N; break;
                case ControllerManager.StickDir8.NE: next = btn.NE; break;
                case ControllerManager.StickDir8.E: next = btn.E; break;
                case ControllerManager.StickDir8.SE: next = btn.SE; break;
                case ControllerManager.StickDir8.S: next = btn.S; break;
                case ControllerManager.StickDir8.SW: next = btn.SW; break;
                case ControllerManager.StickDir8.W: next = btn.W; break;
                case ControllerManager.StickDir8.NW: next = btn.NW; break;
            }

            if (next == -1)
                return;

            // Buttons -> files panel becomes a region handoff.
            if (next == FilesPanel || next == lwFilesPanel)
            {
                EnterFilesPanelRegion(menuWindow);
                return;
            }

            // Buttons -> naming becomes a region handoff.
            if (next == NamingBox)
            {
                if (IsSaveMode(menuWindow))
                    EnterNamingRegion(menuWindow);
                return;
            }

            buttonSelected = next;

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void RefreshSelectorToCurrentButton(DaggerfallUnitySaveGameWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[buttonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private void RefreshSelectorAttachment(DaggerfallUnitySaveGameWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null || selectorHost == null)
                return;

            selectorHost.RefreshAttachment(currentPanel);
        }

        private void DestroySelectorBox()
        {
            if (selectorHost != null)
            {
                selectorHost.Destroy();
                selectorHost = null;
            }
        }

        private void ActivateSelectedButton(DaggerfallUnitySaveGameWindow menuWindow)
        {
            switch (buttonSelected)
            {
                case RenameButton:
                case lwRenameButton:
                    ActivateRename(menuWindow);
                    break;
                case DeleteButton:
                case lwDeleteButton:
                    ActivateDelete(menuWindow);
                    break;
                case SaveButton:
                case lwLoadButton:
                    ActivateGo(menuWindow);
                    break;
                case CancelButton:
                case lwCancelButton:
                    ActivateCancel(menuWindow);
                    break;
                case lwSwitchCharButton:
                    ActivateSwitchChar(menuWindow);
                    break;
            }
        }

        private void ActivateRename(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || miRenameSaveButton_OnMouseClick == null)
                return;

            miRenameSaveButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateDelete(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || miDeleteSaveButton_OnMouseClick == null)
                return;

            miDeleteSaveButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateGo(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || miSaveLoadEventHandler == null)
                return;

            miSaveLoadEventHandler.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateCancel(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || miCancelButton_OnMouseClick == null)
                return;

            DestroyLegend();
            miCancelButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void ActivateSwitchChar(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (menuWindow == null || miSwitchCharButton_OnMouseClick == null)
                return;

            miSwitchCharButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void EnterButtonsRegionAt(int buttonIndex, DaggerfallUnitySaveGameWindow menuWindow)
        {
            buttonSelected = buttonIndex;
            currentRegion = RegionButtons;
            RefreshSelectorToCurrentButton(menuWindow);
        }
    }
}
