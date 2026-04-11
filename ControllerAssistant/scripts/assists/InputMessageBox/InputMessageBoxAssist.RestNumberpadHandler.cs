using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InputMessageBoxAssist
    {
        private sealed class RestNumberpadHandler : IInputMessageBoxAssistHandler
        {
            private OnScreenNumberpadOverlay numberpadOverlay;

            public bool CanHandle(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                return owner.IsRestHoursPopup(menuWindow) || owner.IsLoiterHoursPopup(menuWindow);
            }

            public void OnOpen(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                BuildNumberpad(owner, menuWindow);
            }

            public void Tick(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                if (menuWindow == null || menuWindow.TextBox == null)
                    return;

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
                    (cm.DPadLeftPressed || cm.DPadLeftHeldSlow ||
                     cm.DPadRightReleased ||
                     cm.Action1Released || cm.Action2Pressed || cm.LegendPressed ||
                     dir != ControllerManager.StickDir8.None);

                if (!isAssisting)
                    return;

                if (cm.DPadLeftPressed || cm.DPadLeftHeldSlow)
                    BackspaceText(menuWindow);

                if (cm.DPadRightReleased)
                {
                    owner.SubmitInputBox(menuWindow);
                    return;
                }

                if (cm.Action2Pressed)
                    //menuWindow.TextBox.Text = "0";
                    owner.ToggleAnchorEditor();

                if (cm.Action1Released && numberpadOverlay != null)
                {
                    ActivateNumberpadKey(owner, menuWindow, numberpadOverlay.ActivateSelectedKey());
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Input",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("Right Stick", "Move Selector"),
                            new LegendOverlay.LegendRow("D-Pad Left", "Backspace"),
                            new LegendOverlay.LegendRow("D-Pad Right", "Submit"),
                            new LegendOverlay.LegendRow(cm.Action1Name, "Activate Key"),
                            new LegendOverlay.LegendRow(cm.Action2Name, "Reset to 0"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }
            }

            public void OnClose(InputMessageBoxAssist owner, ControllerManager cm)
            {
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
                numberpadOverlay.SetMaxValue(99);
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

            private void BackspaceText(DaggerfallInputMessageBox menuWindow)
            {
                string text = menuWindow.TextBox.Text;

                if (string.IsNullOrEmpty(text))
                {
                    menuWindow.TextBox.Text = "0";
                }
                else if (text.Length <= 1)
                {
                    menuWindow.TextBox.Text = "0";
                }
                else
                {
                    text = text.Substring(0, text.Length - 1);

                    if (string.IsNullOrEmpty(text))
                        text = "0";

                    menuWindow.TextBox.Text = text;
                }
            }
            private void ActivateNumberpadKey(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, OnScreenNumberpadActivation activation)
            {
                if (menuWindow == null || menuWindow.TextBox == null || numberpadOverlay == null)
                    return;

                switch (activation.Action)
                {
                    case OnScreenNumberpadKeyAction.InsertText:
                        InsertDigit(menuWindow, activation.Text);
                        break;

                    case OnScreenNumberpadKeyAction.Backspace:
                        BackspaceText(menuWindow);
                        break;

                    case OnScreenNumberpadKeyAction.InsertMax:
                        menuWindow.TextBox.Text = "99";
                        break;

                    case OnScreenNumberpadKeyAction.Ok:
                        owner.SubmitInputBox(menuWindow);
                        return;
                }
            }
        }
    }
}