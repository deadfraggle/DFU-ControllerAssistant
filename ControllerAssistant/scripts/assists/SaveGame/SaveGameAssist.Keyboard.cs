using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class SaveGameAssist
    {
        private static readonly Vector2 keyboardAnchorNative = new Vector2(88f, 39f);
        private const float keyboardSpacingX = 1.8f;
        private const float keyboardSpacingY = 2.0f;

        private void RefreshKeyboardAttachment(DaggerfallUnitySaveGameWindow menuWindow)
        {
            if (currentRegion != RegionNaming || !IsSaveMode(menuWindow))
            {
                DestroyKeyboard();
                return;
            }

            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (keyboardOverlay == null)
            {
                BuildKeyboard(menuWindow);
                return;
            }

            if (!keyboardOverlay.IsAttached())
            {
                BuildKeyboard(menuWindow);
                return;
            }

            keyboardOverlay.RefreshAttachment();
        }

        private void BuildKeyboard(DaggerfallUnitySaveGameWindow menuWindow)
        {
            Panel panel = GetCurrentRenderPanel(menuWindow);
            if (panel == null)
                return;

            DestroyKeyboard();

            keyboardOverlay = new OnScreenKeyboardOverlay(panel);
            keyboardOverlay.SetLayout(keyboardAnchorNative, keyboardSpacingX, keyboardSpacingY);
            keyboardOverlay.SetOnKeyClicked(delegate (OnScreenKeyboardActivation activation)
            {
                ActivateKeyboardKey(menuWindow, activation);
            });
            keyboardOverlay.Build();
        }

        private void DestroyKeyboard()
        {
            if (keyboardOverlay != null)
            {
                keyboardOverlay.Destroy();
                keyboardOverlay = null;
            }
        }

        private void ActivateKeyboardKey(DaggerfallUnitySaveGameWindow menuWindow, OnScreenKeyboardActivation activation)
        {
            TextBox textBox = GetSaveNameTextBox(menuWindow);
            if (menuWindow == null || textBox == null || keyboardOverlay == null)
                return;

            switch (activation.Action)
            {
                case OnScreenKeyboardKeyAction.InsertText:
                    if (!string.IsNullOrEmpty(activation.Text))
                        textBox.Text += activation.Text;
                    break;

                case OnScreenKeyboardKeyAction.ReplaceText:
                    textBox.Text = activation.Text ?? string.Empty;
                    break;

                case OnScreenKeyboardKeyAction.Space:
                    textBox.Text += " ";
                    break;

                case OnScreenKeyboardKeyAction.Backspace:
                    BackspaceSaveName(menuWindow);
                    break;

                case OnScreenKeyboardKeyAction.Ok:
                    ActivateGo(menuWindow);
                    return;

                case OnScreenKeyboardKeyAction.Shift:
                    keyboardOverlay.ToggleShift();
                    break;

                case OnScreenKeyboardKeyAction.Toggle123:
                    keyboardOverlay.Toggle123();
                    break;
            }
        }

        private void BackspaceSaveName(DaggerfallUnitySaveGameWindow menuWindow)
        {
            TextBox textBox = GetSaveNameTextBox(menuWindow);
            if (textBox == null)
                return;

            string text = textBox.Text;

            if (string.IsNullOrEmpty(text))
            {
                textBox.Text = string.Empty;
            }
            else if (text.Length <= 1)
            {
                textBox.Text = string.Empty;
            }
            else
            {
                textBox.Text = text.Substring(0, text.Length - 1);
            }
        }
    }
}
