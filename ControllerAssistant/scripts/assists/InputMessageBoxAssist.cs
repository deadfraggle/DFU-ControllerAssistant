using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public enum InputMessageBoxPopupMode
    {
        None,
        InventoryGold,
        Wait,
    }

    public class InputMessageBoxAssist : IMenuAssist
    {
        private const bool debugMODE = true;
        private bool reflectionCached = false;  //prevents re-caching Reflection methods
        private bool wasOpen = false;

        private InputMessageBoxPopupMode activeMode = InputMessageBoxPopupMode.None;
        private float goldRepeatDelay = 0.30f;      // pause before repeat starts
        private float goldRepeatInterval = 0.08f;   // repeat speed after delay
        private float goldHoldTimer = 0f;
        private float goldRepeatTimer = 0f;
        private int goldHeldDirection = 0;          // 1 = up, -1 = down, 0 = none
        private readonly int[] goldIncrementSteps = new int[] { 1, 10, 100, 1000, 10000 };
        private int goldIncrementIndex = 0;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private int goldIncrement = 1;
        private float legendPosXNorm = 1.0f;
        private float legendPosYNorm = 0.50f;


        //private bool isInventoryGoldPopup = false;

       private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        //private FieldInfo fiWindowBinding;
        private FieldInfo fiOnGotUserInput;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallInputMessageBox;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallInputMessageBox menuWindow = top as DaggerfallInputMessageBox;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(cm);
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            switch (activeMode)
            {
                case InputMessageBoxPopupMode.InventoryGold:
                    TickInventoryGold(menuWindow, cm);
                    break;

                case InputMessageBoxPopupMode.Wait:
                    TickWait(menuWindow, cm);
                    break;
            }
        }
        private void TickInventoryGold(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionNormalized(legendPosXNorm, legendPosYNorm, LegendOverlay.LegendAnchor.Center);

            // Read current controller state

            bool isAssisting =
                (cm.DPadH != 0 || cm.DPadV != 0 || cm.RStickLeftPressed ||
                 cm.Action1 || cm.Action2 || cm.Legend);

            if (isAssisting)
            {
                if (cm.DPadUpPressed)
                    BeginGoldHold(menuWindow, 1);

                if (cm.DPadDownPressed)
                    BeginGoldHold(menuWindow, -1);

                if (cm.DPadRightPressed)
                    IncreaseIncrement(menuWindow, cm);

                if (cm.DPadLeftPressed)
                    DecreaseIncrement(menuWindow, cm);

                if (cm.RStickLeftPressed)
                    BackspaceGoldAmount(menuWindow);

                UpdateGoldHold(menuWindow, cm);

                if (cm.Action1Pressed)
                    SubmitInputBox(menuWindow);

                if (cm.Action2Pressed)
                    SetGoldAmount(menuWindow, 0);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;
                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }
            else
            {
                EndGoldHold();
            }

            if ((cm.BackPressed) && (legend != null))
            {
                legend.Destroy();
                legend = null;
                legendVisible = false;
            }

        }
        private void TickWait(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            //...
        }

        // =========================
        // Assist action helpers
        // =========================
        private void BeginGoldHold(DaggerfallInputMessageBox menuWindow, int direction)
        {
            goldHeldDirection = direction;
            goldHoldTimer = 0f;
            goldRepeatTimer = 0f;

            if (menuWindow == null || menuWindow.TextBox == null)
                return;

            int amountShown = 0;
            int.TryParse(menuWindow.TextBox.Text, out amountShown);

            // Special case:
            // pressing Down while currently at 0 jumps to max gold
            if (direction == -1 && amountShown == 0)
            {
                SetGoldAmount(menuWindow, GetPlayerGold());
                return;
            }

            StepGoldAmount(menuWindow, direction);
        }

        private void UpdateGoldHold(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            // Still holding same direction?
            bool stillHolding =
                (goldHeldDirection == 1 && cm.DPadV == 1) ||
                (goldHeldDirection == -1 && cm.DPadV == -1);

            if (!stillHolding)
            {
                EndGoldHold();
                return;
            }

            goldHoldTimer += Time.unscaledDeltaTime;

            if (goldHoldTimer < goldRepeatDelay)
                return;

            goldRepeatTimer += Time.unscaledDeltaTime;

            while (goldRepeatTimer >= goldRepeatInterval)
            {
                goldRepeatTimer -= goldRepeatInterval;
                StepGoldAmount(menuWindow, goldHeldDirection);
            }
        }

        private void EndGoldHold()
        {
            goldHeldDirection = 0;
            goldHoldTimer = 0f;
            goldRepeatTimer = 0f;
        }
        private void StepGoldAmount(DaggerfallInputMessageBox menuWindow, int direction)
        {
            if (menuWindow == null || menuWindow.TextBox == null)
                return;

            int amountShown = 0;
            int.TryParse(menuWindow.TextBox.Text, out amountShown);

            int maxGold = GetPlayerGold();

            amountShown += direction * goldIncrement;

            if (amountShown < 0)
                amountShown = 0;

            if (amountShown > maxGold)
                amountShown = maxGold;

            menuWindow.TextBox.Text = amountShown.ToString();
        }
        private void SetGoldAmount(DaggerfallInputMessageBox menuWindow, int value)
        {
            if (menuWindow == null || menuWindow.TextBox == null)
                return;

            int maxGold = GetPlayerGold();

            if (value < 0)
                value = 0;

            if (value > maxGold)
                value = maxGold;

            menuWindow.TextBox.Text = value.ToString();
        }
        private int GetPlayerGold()
        {
            if (GameManager.Instance == null || GameManager.Instance.PlayerEntity == null)
                return 0;

            return GameManager.Instance.PlayerEntity.GoldPieces; // GameManager.Instance.PlayerEntity.GetGoldAmount();
        }
        private void SubmitInputBox(DaggerfallInputMessageBox menuWindow)
        {
            EndGoldHold();

            if (menuWindow == null || menuWindow.TextBox == null || fiOnGotUserInput == null)
                return;

            object value = fiOnGotUserInput.GetValue(menuWindow);
            System.Delegate del = value as System.Delegate;
            if (del == null)
                return;

            string text = menuWindow.TextBox.Text;

            try
            {
                System.Delegate[] calls = del.GetInvocationList();
                for (int i = 0; i < calls.Length; i++)
                    calls[i].DynamicInvoke(menuWindow, text);
            }
            catch (Exception ex)
            {
                Debug.Log("[ControllerAssistant] SubmitInputBox failed: " + ex);
                return;
            }

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            menuWindow.CloseWindow();
        }
        private void IncreaseIncrement(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            if (goldIncrementIndex < goldIncrementSteps.Length - 1)
                goldIncrementIndex++;

            goldIncrement = goldIncrementSteps[goldIncrementIndex];

            RefreshGoldLegend(menuWindow, cm);
        }

        private void DecreaseIncrement(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            if (goldIncrementIndex > 0)
                goldIncrementIndex--;

            goldIncrement = goldIncrementSteps[goldIncrementIndex];

            RefreshGoldLegend(menuWindow, cm);
        }
        private void BackspaceGoldAmount(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || menuWindow.TextBox == null)
                return;

            string text = menuWindow.TextBox.Text;

            if (string.IsNullOrEmpty(text))
            {
                menuWindow.TextBox.Text = "0";
                return;
            }

            if (text.Length <= 1)
            {
                menuWindow.TextBox.Text = "0";
                return;
            }

            text = text.Substring(0, text.Length - 1);

            if (string.IsNullOrEmpty(text))
                text = "0";

            int value;
            if (!int.TryParse(text, out value))
                value = 0;

            menuWindow.TextBox.Text = value.ToString();
        }
        private void RefreshGoldLegend(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            if (!legendVisible)
                return;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            EnsureLegendUI(menuWindow, cm);

            if (legend != null)
                legend.SetEnabled(true);
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);

            if (IsInventoryGoldPopup(menuWindow))
                activeMode = InputMessageBoxPopupMode.InventoryGold;
            else
                activeMode = InputMessageBoxPopupMode.None;

            if (debugMODE) DaggerfallUI.AddHUDText("InputMessageBox mode: " + activeMode);
        }
        private void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DaggerfallInputMessageBox closed");
        }
        public void ResetState()
        {
            wasOpen = false;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
            panelRenderWindow = null;
            activeMode = InputMessageBoxPopupMode.None;

            EndGoldHold();

            goldIncrementIndex = 0;
            goldIncrement = goldIncrementSteps[0];
        }

        // =========================
        // Per-window/per-open setup
        // =========================

        // cache reflection handles once (expensive + stable)
        private void EnsureInitialized(DaggerfallInputMessageBox menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiOnGotUserInput = CacheField(type, "OnGotUserInput");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;
            if (legend != null) return;

            legend = new LegendOverlay(panelRenderWindow);

            switch (activeMode)
            {
                case InputMessageBoxPopupMode.InventoryGold:
                    BuildInventoryGoldLegend(cm);
                    break;

                //case PopupMode.Wait:
                //    BuildWaitLegend(cm);
                //    break;

                default:
                    legend = null;
                    return;
            }
        }
        private void BuildInventoryGoldLegend(ControllerManager cm)
        {
            legend.HeaderScale = 6.0f;
            legend.HeaderScaleBoost = 0.2f;
            legend.RowScale = 5.0f;
            legend.PadL = 18f;
            legend.PadT = 16f;
            legend.LineGap = 36f;
            legend.ColGap = 22f;
            legend.MarginX = 8f;
            legend.MarginFromBottom = 24f;
            legend.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);

            List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
            {
                new LegendOverlay.LegendRow("D-Pad Up", "Increase amount"),
                new LegendOverlay.LegendRow("D-Pad Down", "Decrease amount"),
                new LegendOverlay.LegendRow("Current increment:", goldIncrement.ToString()),
                new LegendOverlay.LegendRow("D-Pad Right", "Increase increment"),
                new LegendOverlay.LegendRow("D-Pad Left", "Decrease increment"),
                new LegendOverlay.LegendRow("Right Stick Left", "Backspace"),
                new LegendOverlay.LegendRow(cm.Action1Name, "Submit"),
                new LegendOverlay.LegendRow(cm.Action2Name, "Reset to 0"),
             };

            float scale = Mathf.Clamp(panelRenderWindow.Rectangle.width / 3840f, 0.50f, 1.00f);
            legend.ApplyScale(scale);
            legend.Build("Legend", rows);
            legend.PositionNormalized(legendPosXNorm, legendPosYNorm, LegendOverlay.LegendAnchor.Center);
        }
        private void RefreshLegendAttachment(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            // If DFU swapped the panel instance, our old legend is invalid
            if (panelRenderWindow != current)
            {
                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                panelRenderWindow = current;
                legendVisible = false;
                return;
            }

            // If DFU cleared components, our legend may be detached
            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }

        // =========================
        // Reflection helpers
        // =========================
        private bool IsInventoryGoldPopup(DaggerfallInputMessageBox menuWindow)
        {
            //if (debugMODE) DaggerfallUI.AddHUDText("IsInventoryGoldPopup called");
            if (menuWindow == null || fiOnGotUserInput == null)
                return false;

            object value = fiOnGotUserInput.GetValue(menuWindow);
            if (value == null)
                return false;

            System.Delegate del = value as System.Delegate;
            if (del == null)
                return false;

            System.Delegate[] calls = del.GetInvocationList();
            for (int i = 0; i < calls.Length; i++)
            {
                if (calls[i].Method.Name == "DropGoldPopup_OnGotUserInput")
                    return true;
            }

            return false;
        }
        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name); //or Debug.Log(m.ToString());

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }

    }
}


