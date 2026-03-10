using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class MessageBoxAssist : MenuAssistModule<DaggerfallMessageBox>
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private float legendPosXNorm = 1.0f;
        private float legendPosYNorm = 0.50f;

        // Reflection cache
        private FieldInfo fiOnButtonClick;
        private FieldInfo fiButtons;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // =========================
        // Core tick / main behavior
        // =========================
        protected override void OnTickOpen(DaggerfallMessageBox menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionNormalized(legendPosXNorm, legendPosYNorm, LegendOverlay.LegendAnchor.Center);

            bool isYesNoPopup = IsYesNoPopup(menuWindow);
            //if (debugMODE)
            //    DaggerfallUI.AddHUDText("YesNo=" + isYesNoPopup);

            //if (isYesNoPopup)
            //{
                bool isAssisting =
                    (cm.DPadUpPressed || cm.DPadDownPressed || cm.Legend);

                if (isAssisting)
                {
                    if (cm.DPadUpPressed)
                        SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Yes);

                    if (cm.DPadDownPressed)
                        SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.No);

                    if (cm.LegendPressed)
                    {
                        EnsureLegendUI(menuWindow, cm);
                        legendVisible = !legendVisible;
                        if (legend != null)
                            legend.SetEnabled(legendVisible);
                    }
                }
            //}
            //else
            //{
            //    // If this popup is not a Yes/No prompt, make sure our legend isn't hanging around.
            //    if (legend != null)
            //    {
            //        legend.Destroy();
            //        legend = null;
            //        legendVisible = false;
            //    }
            //}

            // Match the lightweight popup behavior from InputMessageBoxAssist:
            // back only dismisses our legend, not the popup itself.
            if (cm.BackPressed && legend != null)
            {
                legend.Destroy();
                legend = null;
                legendVisible = false;
            }
        }

        // =========================
        // Assist action helpers
        // =========================
        private void SelectButton(DaggerfallMessageBox menuWindow, DaggerfallMessageBox.MessageBoxButtons button)
        {
            if (menuWindow == null)
                return;

            // Clean up overlay first.
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
                legendVisible = false;
            }

            // Most DFU handlers subscribe to OnButtonClick and then close in the handler.
            // We invoke the delegate directly, mirroring the semantic button choice.
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

            // Fallback: if there is no delegate, at least close the popup.
            menuWindow.CloseWindow();
        }

        private bool IsYesNoPopup(DaggerfallMessageBox menuWindow)
        {
            if (menuWindow == null || fiButtons == null)
                return false;

            object value = fiButtons.GetValue(menuWindow);
            if (value == null)
                return false;

            IList list = value as IList;
            if (list == null)
                return false;

            bool hasYes = false;
            bool hasNo = false;
            int count = 0;

            for (int i = 0; i < list.Count; i++)
            {
                object entry = list[i];
                if (entry == null)
                    continue;

                count++;

                if (!(entry is DaggerfallMessageBox.MessageBoxButtons))
                    continue;

                DaggerfallMessageBox.MessageBoxButtons button =
                    (DaggerfallMessageBox.MessageBoxButtons)entry;

                if (button == DaggerfallMessageBox.MessageBoxButtons.Yes)
                    hasYes = true;
                else if (button == DaggerfallMessageBox.MessageBoxButtons.No)
                    hasNo = true;
            }

            // Keep this assist limited to simple two-choice Yes/No prompts.
            return count == 2 && hasYes && hasNo;
        }

        // =========================
        // Lifecycle hooks
        // =========================
        protected override void OnOpened(DaggerfallMessageBox menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);
        }

        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DaggerfallMessageBox closed");
        }

        public override void ResetState()
        {
            base.ResetState();

            legendVisible = false;
            legend = null;
            panelRenderWindow = null;
        }

        // =========================
        // Per-window/per-open setup
        // =========================
        private void EnsureInitialized(DaggerfallMessageBox menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");
            fiOnButtonClick = CacheField(type, "OnButtonClick");
            fiButtons = CacheField(type, "buttons");

            reflectionCached = true;
        }

        // =========================
        // Optional UI helpers
        // =========================
        private void EnsureLegendUI(DaggerfallMessageBox menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;
            if (legend != null) return;

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

            List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
            {
                new LegendOverlay.LegendRow("D-Pad Up", "Yes"),
                new LegendOverlay.LegendRow("D-Pad Down", "No"),
            };

            float scale = Mathf.Clamp(panelRenderWindow.Rectangle.width / 3840f, 0.50f, 1.00f);
            legend.ApplyScale(scale);
            legend.Build("Legend", rows);
            legend.PositionNormalized(legendPosXNorm, legendPosYNorm, LegendOverlay.LegendAnchor.Center);
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
                panelRenderWindow = current;
                legendVisible = false;
                legend = null;
                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }

        // =========================
        // Reflection helpers
        // =========================
        private FieldInfo CacheField(Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null && debugMODE)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        private void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
    }
}
