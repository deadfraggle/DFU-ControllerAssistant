using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class BankingAssist
    {
        private bool IsTransactionInputActive(DaggerfallBankingWindow menuWindow)
        {
            if (menuWindow == null || fiTransactionInput == null)
                return false;

            TextBox textBox = fiTransactionInput.GetValue(menuWindow) as TextBox;
            return textBox != null && textBox.Enabled;
        }

        private TextBox GetTransactionInput(DaggerfallBankingWindow menuWindow)
        {
            if (menuWindow == null || fiTransactionInput == null)
                return null;

            return fiTransactionInput.GetValue(menuWindow) as TextBox;
        }

        private void RefreshNumberpadAttachment(DaggerfallBankingWindow menuWindow)
        {
            if (!IsTransactionInputActive(menuWindow))
            {
                DestroyNumberpad();
                return;
            }

            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            if (numberpadOverlay == null)
            {
                BuildNumberpad(menuWindow);
                return;
            }

            if (!numberpadOverlay.IsAttached())
            {
                BuildNumberpad(menuWindow);
                return;
            }

            numberpadOverlay.RefreshAttachment();
        }

        private void BuildNumberpad(DaggerfallBankingWindow menuWindow)
        {
            Panel panel = GetCurrentRenderPanel(menuWindow);
            if (panel == null)
                return;

            DestroyNumberpad();

            numberpadOverlay = new OnScreenNumberpadOverlay(panel);
            numberpadOverlay.SetLayout(new Vector2(155f, 90f), 3.0f, 2.0f);
            numberpadOverlay.SetDefaultSelectedLabel("1");
            numberpadOverlay.SetMaxValue(999999999);
            numberpadOverlay.SetOnKeyClicked(delegate (OnScreenNumberpadActivation activation)
            {
                ActivateNumberpadKey(menuWindow, activation);
            });
            numberpadOverlay.Build();
        }

        private void DestroyNumberpad()
        {
            if (numberpadOverlay != null)
            {
                numberpadOverlay.Destroy();
                numberpadOverlay = null;
            }
        }

        private void TickNumberpadMode(DaggerfallBankingWindow menuWindow, ControllerManager cm)
        {
            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None && numberpadOverlay != null)
            {
                switch (dir)
                {
                    case ControllerManager.StickDir8.W:
                        numberpadOverlay.MoveLeft();
                        break;
                    case ControllerManager.StickDir8.E:
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

            if (cm.Action1Released && numberpadOverlay != null)
            {
                ActivateNumberpadKey(menuWindow, numberpadOverlay.ActivateSelectedKey());
                return;
            }

            if (cm.LegendPressed)
            {
                bool show = !legendVisible;
                legendVisible = show;

                if (show)
                    EnsureTransactionLegendUI(menuWindow, cm);
                else
                    DestroyLegend();

                return;
            }

            if (backBindingSuppressed &&
                suppressedBackButton != KeyCode.None &&
                InputManager.Instance != null)
            {
                if (InputManager.Instance.GetKeyDown(suppressedBackButton, false))
                    closeDeferred = true;

                if (closeDeferred && InputManager.Instance.GetKeyUp(suppressedBackButton, false))
                {
                    closeDeferred = false;
                    CancelTransactionInput(menuWindow);
                    BeginRestoreBackBinding();
                    DestroyLegend();
                    return;
                }
            }
        }

        private void ActivateNumberpadKey(DaggerfallBankingWindow menuWindow, OnScreenNumberpadActivation activation)
        {
            TextBox textBox = GetTransactionInput(menuWindow);
            if (textBox == null)
                return;

            switch (activation.Action)
            {
                case OnScreenNumberpadKeyAction.InsertText:
                    InsertDigit(menuWindow, activation.Text);
                    break;

                case OnScreenNumberpadKeyAction.Backspace:
                    BackspaceTransactionInput(menuWindow);
                    break;

                case OnScreenNumberpadKeyAction.InsertMax:
                    // Banking has no natural "max" helper here. Use the key text as provided by overlay.
                    if (!string.IsNullOrEmpty(activation.Text))
                        textBox.Text = activation.Text;
                    break;

                case OnScreenNumberpadKeyAction.Ok:
                    SubmitTransactionInput(menuWindow);
                    break;
            }
        }

        private void InsertDigit(DaggerfallBankingWindow menuWindow, string digit)
        {
            TextBox textBox = GetTransactionInput(menuWindow);
            if (textBox == null || string.IsNullOrEmpty(digit))
                return;

            string current = textBox.Text;

            if (string.IsNullOrEmpty(current) || current == "0")
                textBox.Text = digit;
            else
                textBox.Text += digit;
        }

        private void BackspaceTransactionInput(DaggerfallBankingWindow menuWindow)
        {
            TextBox textBox = GetTransactionInput(menuWindow);
            if (textBox == null || string.IsNullOrEmpty(textBox.Text))
                return;

            string current = textBox.Text;
            if (current.Length <= 1)
                textBox.Text = string.Empty;
            else
                textBox.Text = current.Substring(0, current.Length - 1);
        }

        private void SubmitTransactionInput(DaggerfallBankingWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (miHandleTransactionInput != null)
                miHandleTransactionInput.Invoke(menuWindow, null);

            CancelTransactionInput(menuWindow);
        }

        private void CancelTransactionInput(DaggerfallBankingWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            TextBox textBox = GetTransactionInput(menuWindow);
            if (textBox != null)
                textBox.Text = string.Empty;

            if (miToggleTransactionInput != null && fiTransactionType != null)
            {
                object currentType = fiTransactionType.GetValue(menuWindow);
                System.Type enumType = currentType != null ? currentType.GetType() : null;
                if (enumType != null)
                {
                    object noneValue = System.Enum.Parse(enumType, "None");
                    miToggleTransactionInput.Invoke(menuWindow, new object[] { noneValue });
                }
            }

            DestroyNumberpad();
            BeginRestoreBackBinding();
        }
    }
}
