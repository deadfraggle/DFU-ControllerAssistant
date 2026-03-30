using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InputMessageBoxAssist
    {
        private sealed class SpellNameHandler : IInputMessageBoxAssistHandler
        {
            private OnScreenKeyboardOverlay keyboardOverlay;

            public bool CanHandle(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                return owner.IsSpellNamePopup(menuWindow);
            }

            public void OnOpen(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                Panel panel = owner.GetInputMessageBoxRenderPanel(menuWindow);
                if (panel != null)
                {
                    keyboardOverlay = new OnScreenKeyboardOverlay(panel);
                    keyboardOverlay.SetLayout(new UnityEngine.Vector2(90f, 145f), 1.8f, 2.0f);
                    keyboardOverlay.Build();
                }
            }

            public void Tick(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                if (menuWindow == null || menuWindow.TextBox == null)
                    return;

                RefreshKeyboardAttachment(owner, menuWindow);

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
                    (cm.DPadLeftPressed || cm.DPadLeftHeldSlow ||
                     cm.Action1Released || cm.Action2Pressed || cm.LegendPressed ||
                     dir != ControllerManager.StickDir8.None);

                // --- DPad shortcuts ---
                if (keyboardOverlay != null)
                {
                    if (cm.DPadUpPressed)
                    {
                        keyboardOverlay.ToggleShift();
                    }

                    if (cm.DPadDownPressed)
                    {
                        keyboardOverlay.Toggle123();
                    }

                    if (cm.DPadRightReleased)
                    {
                        owner.SubmitInputBox(menuWindow);
                        return;
                    }
                }

                if (!isAssisting)
                    return;

                // D-Pad Left = Backspace
                if (cm.DPadLeftPressed || cm.DPadLeftHeldSlow)
                    BackspaceText(menuWindow);

                // Action2 = Clear
                if (cm.Action2Pressed)
                    menuWindow.TextBox.Text = string.Empty;

                // Action1 = Activate selected key
                if (cm.Action1Released && keyboardOverlay != null)
                {
                    OnScreenKeyboardActivation activation = keyboardOverlay.ActivateSelectedKey();

                    switch (activation.Action)
                    {
                        case OnScreenKeyboardKeyAction.InsertText:
                            if (!string.IsNullOrEmpty(activation.Text))
                                menuWindow.TextBox.Text += activation.Text;
                            break;

                        case OnScreenKeyboardKeyAction.Space:
                            menuWindow.TextBox.Text += " ";
                            break;

                        case OnScreenKeyboardKeyAction.Backspace:
                            BackspaceText(menuWindow);
                            break;

                        case OnScreenKeyboardKeyAction.Ok:
                            owner.SubmitInputBox(menuWindow);
                            return;

                        case OnScreenKeyboardKeyAction.Shift:
                            keyboardOverlay.ToggleShift();
                            break;

                        case OnScreenKeyboardKeyAction.Toggle123:
                            keyboardOverlay.Toggle123();
                            break;
                    }
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Spell Name",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("Right Stick", "Move Selector"),
                            new LegendOverlay.LegendRow("D-Pad Up", "Shift"),
                            new LegendOverlay.LegendRow("D-Pad Down", "123 toggle"),
                            new LegendOverlay.LegendRow("D-Pad Right", "Submit"),
                            new LegendOverlay.LegendRow("D-Pad Left", "Backspace"),
                            new LegendOverlay.LegendRow(cm.Action1Name, "Activate Key"),
                            new LegendOverlay.LegendRow(cm.Action2Name, "Clear Text"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }
            }

            public void OnClose(InputMessageBoxAssist owner, ControllerManager cm)
            {
                if (keyboardOverlay != null)
                {
                    keyboardOverlay.Destroy();
                    keyboardOverlay = null;
                }
            }

            private void RefreshKeyboardAttachment(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetInputMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                if (keyboardOverlay == null)
                {
                    keyboardOverlay = new OnScreenKeyboardOverlay(currentPanel);
                    keyboardOverlay.SetLayout(new UnityEngine.Vector2(90f, 145f), 1.8f, 2.0f);
                    keyboardOverlay.Build();
                    return;
                }

                if (!keyboardOverlay.IsAttached())
                {
                    keyboardOverlay = new OnScreenKeyboardOverlay(currentPanel);
                    keyboardOverlay.SetLayout(new UnityEngine.Vector2(90f, 145f), 1.8f, 2.0f);
                    keyboardOverlay.Build();
                    return;
                }

                keyboardOverlay.RefreshAttachment();
            }

            private void BackspaceText(DaggerfallInputMessageBox menuWindow)
            {
                string text = menuWindow.TextBox.Text;

                if (string.IsNullOrEmpty(text))
                {
                    menuWindow.TextBox.Text = string.Empty;
                }
                else if (text.Length <= 1)
                {
                    menuWindow.TextBox.Text = string.Empty;
                }
                else
                {
                    menuWindow.TextBox.Text = text.Substring(0, text.Length - 1);
                }
            }
        }
    }
}