using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private bool reflectionCached = false;
        private bool wasOpen = false;

        private DaggerfallMessageBox currentWindow;

        // Shared reflection cache
        private FieldInfo fiPanelRenderWindow;
        private FieldInfo fiOnButtonClick;
        private FieldInfo fiButtons;
        private FieldInfo fiNextMessageBox;

        // Shared UI state
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private float legendPosXNorm = 1.0f;
        private float legendPosYNorm = 0.50f;

        // Active handler for this popup instance
        private IMessageBoxAssistHandler activeHandler;

        // Status selector persistent state
        internal int statusButtonSelected = 0;

        private AnchorEditor editor;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallMessageBox;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallMessageBox menuWindow = top as DaggerfallMessageBox;

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

        private void OnOpened(DaggerfallMessageBox menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            // Anchor Editor
            if (editor == null)
            {
                // Match Inventory's default selector size: 25 x 19 native-ish feel
                editor = new AnchorEditor(25f, 19f);
            }

            activeHandler = ResolveHandler(menuWindow);

            if (debugMODE && activeHandler != null)
                Debug.Log("[ControllerAssistant] MessageBoxAssist handler = " + activeHandler.GetType().Name);

            if (activeHandler != null)
                activeHandler.OnOpen(this, menuWindow, cm);
        }

        private void OnTickOpen(DaggerfallMessageBox menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionNormalized(legendPosXNorm, legendPosYNorm, LegendOverlay.LegendAnchor.Center);

            if (activeHandler != null)
                activeHandler.Tick(this, menuWindow, cm);

            // Anchor Editor
            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow != null)
                editor.Tick(panelRenderWindow);

            if (cm.BackPressed && legend != null)
            {
                DestroyLegend();
            }
        }

        private void OnClosed(ControllerManager cm)
        {
            if (activeHandler != null)
                activeHandler.OnClose(this, cm);

            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallMessageBox closed");
        }

        public void ResetState()
        {
            wasOpen = false;
            activeHandler = null;
            DestroyLegend();
            panelRenderWindow = null;
            currentWindow = null;
        }

        private IMessageBoxAssistHandler ResolveHandler(DaggerfallMessageBox menuWindow)
        {
            IMessageBoxAssistHandler[] handlers = new IMessageBoxAssistHandler[]
            {
                new StatusProbeHandler(),   // temporary detector only
                new YesNoHandler(),
            };

            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i].CanHandle(this, menuWindow))
                    return handlers[i];
            }

            return null;
        }

        private void EnsureInitialized(DaggerfallMessageBox menuWindow)
        {
            if (reflectionCached || menuWindow == null)
                return;

            Type type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiOnButtonClick = CacheField(type, "OnButtonClick");
            fiButtons = CacheField(type, "buttons");
            fiNextMessageBox = CacheField(type, "nextMessageBox");

            reflectionCached = true;
        }

        internal void EnsureLegendUI(
            DaggerfallMessageBox menuWindow,
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

            float scale = Mathf.Clamp(panelRenderWindow.Rectangle.width / 3840f, 0.50f, 1.00f);
            legend.ApplyScale(scale);
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

        private void RefreshLegendAttachment(DaggerfallMessageBox menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                DestroyLegend();
                panelRenderWindow = current;
                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legend = null;
                legendVisible = false;
            }
        }

        internal bool TryGetSemanticButtons(
            DaggerfallMessageBox menuWindow,
            out List<DaggerfallMessageBox.MessageBoxButtons> buttons)
        {
            buttons = new List<DaggerfallMessageBox.MessageBoxButtons>();

            if (menuWindow == null || fiButtons == null)
                return false;

            object value = fiButtons.GetValue(menuWindow);
            IList list = value as IList;
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                Button uiButton = list[i] as Button;
                if (uiButton == null)
                    continue;

                if (uiButton.Tag is DaggerfallMessageBox.MessageBoxButtons)
                    buttons.Add((DaggerfallMessageBox.MessageBoxButtons)uiButton.Tag);
            }

            return true;
        }

        internal bool HasExactButtons(
            DaggerfallMessageBox menuWindow,
            params DaggerfallMessageBox.MessageBoxButtons[] expected)
        {
            List<DaggerfallMessageBox.MessageBoxButtons> actual;
            if (!TryGetSemanticButtons(menuWindow, out actual))
                return false;

            if (actual.Count != expected.Length)
                return false;

            for (int i = 0; i < expected.Length; i++)
            {
                if (!actual.Contains(expected[i]))
                    return false;
            }

            return true;
        }

        internal void SelectButton(DaggerfallMessageBox menuWindow, DaggerfallMessageBox.MessageBoxButtons button)
        {
            if (menuWindow == null)
                return;

            DestroyLegend();

            if (fiOnButtonClick != null)
            {
                object value = fiOnButtonClick.GetValue(menuWindow);
                Delegate del = value as Delegate;

                if (del != null)
                {
                    try
                    {
                        Delegate[] calls = del.GetInvocationList();
                        for (int i = 0; i < calls.Length; i++)
                            calls[i].DynamicInvoke(menuWindow, button);

                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("[ControllerAssistant] MessageBoxAssist button invoke failed: " + ex);
                    }
                }
            }

            menuWindow.CloseWindow();
        }
        internal bool HasNextMessageBox(DaggerfallMessageBox menuWindow)
        {
            if (menuWindow == null || fiNextMessageBox == null)
                return false;

            object value = fiNextMessageBox.GetValue(menuWindow);
            return value != null;
        }

        internal Panel GetMessageBoxRenderPanel(DaggerfallMessageBox menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        internal void ToggleAnchorEditor()
        {
            if (editor != null)
                editor.Toggle();
        }

        private FieldInfo CacheField(Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null && debugMODE)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        private void DumpWindowMembers(object window)
        {
            Type type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (MethodInfo m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (FieldInfo f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
    }
}