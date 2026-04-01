using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private sealed class AlterSpellHandler : IMessageBoxAssistHandler
        {
            private const int EditButton = 0;
            private const int DeleteButton = 1;

            private static readonly Rect EditRect = new Rect(111.7f, 97.1f, 32.7f, 16.9f);
            private static readonly Rect DeleteRect = new Rect(175.6f, 97.1f, 32.7f, 16.9f);

            private bool loggedOpen = false;
            private int selectedButton = EditButton;
            private DefaultSelectorBoxHost selectorHost;

            public bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (menuWindow == null)
                    return false;

                if (!owner.HasExactButtons(
                    menuWindow,
                    DaggerfallMessageBox.MessageBoxButtons.Edit,
                    DaggerfallMessageBox.MessageBoxButtons.Delete))
                {
                    return false;
                }

                if (!(menuWindow.PreviousWindow is DaggerfallSpellMakerWindow))
                    return false;

                if (menuWindow.ClickAnywhereToClose)
                    return false;

                Debug.Log("[ControllerAssistant] AlterSpell structural match confirmed.");
                return true;
            }

            public void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                if (!loggedOpen)
                {
                    Debug.Log("[ControllerAssistant] AlterSpellHandler matched popup.");
                    loggedOpen = true;
                }

                // Always default to Edit. No memory.
                selectedButton = EditButton;
                RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                RefreshSelectorAttachment(owner, menuWindow);

                bool moveLeft = cm.RStickLeftPressed || cm.RStickLeftHeldSlow;
                bool moveRight = cm.RStickRightPressed || cm.RStickRightHeldSlow;

                bool isAssisting =
                    moveLeft ||
                    moveRight ||
                    cm.DPadUpPressed ||
                    cm.DPadDownPressed ||
                    cm.Action1Released ||
                    cm.LegendPressed;

                if (!isAssisting)
                    return;

                if (cm.DPadUpPressed)
                    owner.ClickSemanticButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Edit);

                if (cm.DPadDownPressed)
                    owner.ClickSemanticButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Delete);

                if (moveLeft || moveRight)
                {
                    TryMoveSelector(owner, menuWindow);
                }

                if (cm.Action1Released)
                {
                    ActivateSelectedButton(owner, menuWindow);
                    return;
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Alter Spell",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("D-Pad Up", "Edit"),
                            new LegendOverlay.LegendRow("D-Pad Down", "Delete"),
                            new LegendOverlay.LegendRow("Right Stick Left/Right", "Move Selector"),
                            new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }
            }

            public void OnClose(MessageBoxAssist owner, ControllerManager cm)
            {
                DestroySelectorBox();
            }

            private void TryMoveSelector(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                int previous = selectedButton;
                selectedButton = (selectedButton == EditButton) ? DeleteButton : EditButton;

                if (selectedButton != previous)
                    RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            private void ActivateSelectedButton(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (selectedButton == EditButton)
                    owner.ClickSemanticButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Edit);
                else
                    owner.ClickSemanticButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Delete);
            }

            private void RefreshSelectorToCurrentButton(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.ShowAtNativeRect(
                    currentPanel,
                    selectedButton == EditButton ? EditRect : DeleteRect,
                    new Color(0.1f, 1f, 1f, 1f)
                );
            }

            private void RefreshSelectorAttachment(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.RefreshAttachment(currentPanel);
            }

            private void DestroySelectorBox()
            {
                if (selectorHost != null)
                    selectorHost.Destroy();
            }
        }
    }
}