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
    public partial class InputMessageBoxAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;  //prevents re-caching Reflection methods
        private bool wasOpen = false;

        private DaggerfallInputMessageBox currentWindow;
        private IInputMessageBoxAssistHandler activeHandler;

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

        private FieldInfo fiOnGotUserInput;
        private MethodInfo miReturnPlayerInputEvent;

        //private AnchorEditor editor;

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
                    currentWindow = null;
                }
                return;
            }

            bool windowChanged = !object.ReferenceEquals(currentWindow, menuWindow);

            if (!wasOpen || windowChanged)
            {
                if (wasOpen)
                    OnClosed(cm);

                wasOpen = true;
                currentWindow = menuWindow;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        // =========================
        // Core tick / main behavior
        // =========================
        private void OnTickOpen(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionNormalized(legendPosXNorm, legendPosYNorm, LegendOverlay.LegendAnchor.Center);

            if (activeHandler != null)
                activeHandler.Tick(this, menuWindow, cm);

            //// Anchor Editor
            //if (panelRenderWindow == null && fiPanelRenderWindow != null)
            //    panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            //if (panelRenderWindow != null)
            //    editor.Tick(panelRenderWindow);

            if (cm.BackPressed && legend != null)
                DestroyLegend();
        }


        private IInputMessageBoxAssistHandler ResolveHandler(DaggerfallInputMessageBox menuWindow)
        {
            IInputMessageBoxAssistHandler[] handlers = new IInputMessageBoxAssistHandler[]
            {
                new InventoryGoldHandler(),
                new FindLocationHandler(),
                new RestNumberpadHandler(),
                new TavernRoomNumberpadHandler(),
                new DonationNumberpadHandler(),
                new DefaultKeyboardHandler(),   // fallback must stay last
            };

            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i].CanHandle(this, menuWindow))
                    return handlers[i];
            }

            return null;
        }
        
        private bool IsRestHoursPopup(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || fiOnGotUserInput == null)
                return false;

            object value = fiOnGotUserInput.GetValue(menuWindow);
            if (value == null)
                return false;

            Delegate del = value as Delegate;
            if (del == null)
                return false;

            Delegate[] calls = del.GetInvocationList();
            for (int i = 0; i < calls.Length; i++)
            {
                if (calls[i].Method.Name == "TimedRestPrompt_OnGotUserInput")
                    return true;
            }

            return false;
        }

        private bool IsLoiterHoursPopup(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || fiOnGotUserInput == null)
                return false;

            object value = fiOnGotUserInput.GetValue(menuWindow);
            if (value == null)
                return false;

            Delegate del = value as Delegate;
            if (del == null)
                return false;

            Delegate[] calls = del.GetInvocationList();
            for (int i = 0; i < calls.Length; i++)
            {
                if (calls[i].Method.Name == "LoiterPrompt_OnGotUserInput")
                    return true;
            }

            return false;
        }
        private bool IsTavernRoomPopup(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || fiOnGotUserInput == null)
                return false;

            object value = fiOnGotUserInput.GetValue(menuWindow);
            if (value == null)
                return false;

            Delegate del = value as Delegate;
            if (del == null)
                return false;

            Delegate[] calls = del.GetInvocationList();
            for (int i = 0; i < calls.Length; i++)
            {
                if (calls[i].Method.Name == "InputMessageBox_OnGotUserInput" &&
                    calls[i].Target != null &&
                    calls[i].Target.GetType().Name == "DaggerfallTavernWindow")
                {
                    return true;
                }
            }

            return false;
        }
        private bool IsDonationPopup(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || fiOnGotUserInput == null)
                return false;

            object value = fiOnGotUserInput.GetValue(menuWindow);
            if (value == null)
                return false;

            Delegate del = value as Delegate;
            if (del == null)
                return false;

            Delegate[] calls = del.GetInvocationList();
            for (int i = 0; i < calls.Length; i++)
            {
                if (calls[i].Method.Name == "DonationMsgBox_OnGotUserInput")
                    return true;
            }

            return false;
        }

        internal Panel GetInputMessageBoxRenderPanel(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        // =========================
        // Assist action helpers
        // =========================
        
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
            
            if (menuWindow == null || menuWindow.TextBox == null || miReturnPlayerInputEvent == null)
                return;

            string text = menuWindow.TextBox.Text;

            try
            {
                miReturnPlayerInputEvent.Invoke(menuWindow, new object[] { menuWindow, text });
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
        internal void RefreshGoldLegend(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            bool wasVisible = legendVisible;
            if (!wasVisible)
                return;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            EnsureLegendUI(
                menuWindow,
                "Legend",
                new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("D-Pad Up", "Increase amount"),
                    new LegendOverlay.LegendRow("D-Pad Down", "Decrease amount"),
                    new LegendOverlay.LegendRow("Current increment:", goldIncrement.ToString()),
                    new LegendOverlay.LegendRow("D-Pad Right", "Increase increment"),
                    new LegendOverlay.LegendRow("D-Pad Left", "Decrease increment"),
                    new LegendOverlay.LegendRow("Right Stick Left", "Backspace"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Submit"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Reset to 0"),
                });

            legendVisible = wasVisible;

            if (legend != null)
                legend.SetEnabled(legendVisible);
        }

        // =========================
        // Lifecycle hooks
        // =========================
        private void OnOpened(DaggerfallInputMessageBox menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            activeHandler = ResolveHandler(menuWindow);

            if (debugMODE && activeHandler != null)
                Debug.Log("[ControllerAssistant] InputMessageBoxAssist handler = " + activeHandler.GetType().Name);

            if (activeHandler != null)
                activeHandler.OnOpen(this, menuWindow, cm);

            //// Anchor Editor
            //if (editor == null)
            //{
            //    // Match Inventory's default selector size: 25 x 19 native-ish feel
            //    editor = new AnchorEditor(25f, 19f);
            //}
        }
        private void OnClosed(ControllerManager cm)
        {
            if (activeHandler != null)
                activeHandler.OnClose(this, cm);

            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallInputMessageBox closed");
        }

        public void ResetState()
        {
            wasOpen = false;
            activeHandler = null;
            currentWindow = null;

            DestroyLegend();
            panelRenderWindow = null;

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

            var type = typeof(DaggerfallInputMessageBox);

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiOnGotUserInput = CacheField(type, "OnGotUserInput");
            miReturnPlayerInputEvent = CacheMethod(type, "ReturnPlayerInputEvent");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        internal void EnsureLegendUI(
            DaggerfallInputMessageBox menuWindow,
            string header,
            List<LegendOverlay.LegendRow> rows)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (legend != null)
                return;

            legend = new LegendOverlay(panelRenderWindow);
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

            legend.Build(header, rows);
            legend.PositionNormalized(legendPosXNorm, legendPosYNorm, LegendOverlay.LegendAnchor.Center);
        }

        internal void SetLegendVisible(bool visible)
        {
            legendVisible = visible;
            if (legend != null)
                legend.SetEnabled(visible);
        }

        internal bool GetLegendVisible()
        {
            return legendVisible;
        }

        internal void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
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
                DestroyLegend();
                panelRenderWindow = current;
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
        //internal void ToggleAnchorEditor()
        //{
        //    if (editor != null)
        //        editor.Toggle();
        //}
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


