using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InputMessageBoxAssist
    {
        private sealed class InventoryGoldHandler : IInputMessageBoxAssistHandler
        {
            private OnScreenNumberpadOverlay numberpadOverlay;

            public bool CanHandle(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                return owner.IsInventoryGoldPopup(menuWindow);
            }

            public void OnOpen(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                BuildNumberpad(owner, menuWindow);
            }

            public void Tick(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                RefreshNumberpadAttachment(owner, menuWindow);

                ControllerManager.StickDir8 dir =
                    cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                    ? cm.RStickDir8Pressed
                    : cm.RStickDir8HeldSlow;

                if (dir != ControllerManager.StickDir8.None && numberpadOverlay != null)
                {
                    switch (dir)
                    {
                        case ControllerManager.StickDir8.W:
                        case ControllerManager.StickDir8.NW:
                        case ControllerManager.StickDir8.SW:
                            numberpadOverlay.MoveLeft();
                            break;

                        case ControllerManager.StickDir8.E:
                        case ControllerManager.StickDir8.NE:
                        case ControllerManager.StickDir8.SE:
                            numberpadOverlay.MoveRight();
                            break;

                        case ControllerManager.StickDir8.N:
                            numberpadOverlay.MoveUp();
                            break;

                        case ControllerManager.StickDir8.S:
                            numberpadOverlay.MoveDown();
                            break;
                    }
                }

                bool isAssisting =
                    (cm.DPadH != 0 || cm.DPadV != 0 ||
                     cm.Action1Released || cm.Action2Pressed || cm.LegendPressed ||
                     dir != ControllerManager.StickDir8.None);

                if (!isAssisting)
                {
                    owner.EndGoldHold();
                    return;
                }

                if (cm.DPadUpPressed)
                    owner.BeginGoldHold(menuWindow, 1);

                if (cm.DPadDownPressed)
                    owner.BeginGoldHold(menuWindow, -1);

                if (cm.DPadRightPressed)
                    owner.IncreaseIncrement(menuWindow, cm);

                if (cm.DPadLeftPressed)
                    owner.DecreaseIncrement(menuWindow, cm);

                owner.UpdateGoldHold(menuWindow, cm);

                if (cm.Action2Pressed)
                    owner.SetGoldAmount(menuWindow, 0);

                if (cm.Action1Released && numberpadOverlay != null)
                {
                    ActivateNumberpadKey(owner, menuWindow, numberpadOverlay.ActivateSelectedKey());
                }

                if (cm.LegendPressed)
                {
                    bool show = !owner.GetLegendVisible();
                    owner.SetLegendVisible(show);

                    if (show)
                        RefreshGoldLegendWithNumberpad(owner, menuWindow, cm);
                    else
                        owner.DestroyLegend();
                }
            }

            public void OnClose(InputMessageBoxAssist owner, ControllerManager cm)
            {
                owner.EndGoldHold();

                if (numberpadOverlay != null)
                {
                    numberpadOverlay.Destroy();
                    numberpadOverlay = null;
                }
            }

            private void BuildNumberpad(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                Panel panel = owner.GetInputMessageBoxRenderPanel(menuWindow);
                if (panel == null)
                    return;

                numberpadOverlay = new OnScreenNumberpadOverlay(panel);
                numberpadOverlay.SetLayout(new Vector2(126f, 121f), 3.0f, 2.0f);
                numberpadOverlay.SetDefaultSelectedLabel("1");
                numberpadOverlay.SetMaxValue(owner.GetPlayerGold());
                numberpadOverlay.SetOnKeyClicked(delegate (OnScreenNumberpadActivation activation)
                {
                    ActivateNumberpadKey(owner, menuWindow, activation);
                });
                numberpadOverlay.Build();
            }

            private void RefreshNumberpadAttachment(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetInputMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                if (numberpadOverlay == null)
                {
                    BuildNumberpad(owner, menuWindow);
                    return;
                }

                if (!numberpadOverlay.IsAttached())
                {
                    BuildNumberpad(owner, menuWindow);
                    return;
                }

                numberpadOverlay.RefreshAttachment();
            }

            private void InsertDigit(DaggerfallInputMessageBox menuWindow, string digit)
            {
                if (menuWindow == null || menuWindow.TextBox == null || string.IsNullOrEmpty(digit))
                    return;

                string current = menuWindow.TextBox.Text;

                if (string.IsNullOrEmpty(current) || current == "0")
                    menuWindow.TextBox.Text = digit;
                else
                    menuWindow.TextBox.Text += digit;
            }

            private void RefreshGoldLegendWithNumberpad(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                bool wasVisible = owner.GetLegendVisible();
                if (!wasVisible)
                    return;

                owner.DestroyLegend();

                owner.EnsureLegendUI(
                    menuWindow,
                    "Legend",
                    new List<LegendOverlay.LegendRow>()
                    {
                        new LegendOverlay.LegendRow("Right Stick", "Move Selector"),
                        new LegendOverlay.LegendRow(cm.Action1Name, "Activate Key"),
                        new LegendOverlay.LegendRow(cm.Action2Name, "Reset to 0"),
                        new LegendOverlay.LegendRow("D-Pad Up", "Increase amount"),
                        new LegendOverlay.LegendRow("D-Pad Down", "Decrease amount"),
                        new LegendOverlay.LegendRow("Current increment:", owner.goldIncrement.ToString()),
                        new LegendOverlay.LegendRow("D-Pad Right", "Increase increment"),
                        new LegendOverlay.LegendRow("D-Pad Left", "Decrease increment"),
                    });

                owner.SetLegendVisible(true);
            }
            private void ActivateNumberpadKey(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, OnScreenNumberpadActivation activation)
            {
                if (menuWindow == null || numberpadOverlay == null)
                    return;

                switch (activation.Action)
                {
                    case OnScreenNumberpadKeyAction.InsertText:
                        InsertDigit(menuWindow, activation.Text);
                        break;

                    case OnScreenNumberpadKeyAction.Backspace:
                        owner.BackspaceGoldAmount(menuWindow);
                        break;

                    case OnScreenNumberpadKeyAction.InsertMax:
                        owner.SetGoldAmount(menuWindow, owner.GetPlayerGold());
                        break;

                    case OnScreenNumberpadKeyAction.Ok:
                        owner.SubmitInputBox(menuWindow);
                        return;
                }
            }
        }
    }
}