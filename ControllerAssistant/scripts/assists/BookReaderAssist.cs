using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class BookReaderAssist : IMenuAssist
    {
        private const BindingFlags BF =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private bool wasOpen = false;

        private FieldInfo fiParentPanel;
        private Panel panelRenderWindow;

        private MethodInfo miNextPageClick;
        private MethodInfo miPreviousPageClick;

        private LegendOverlay legend;
        private bool legendVisible = false;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallBookReaderWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallBookReaderWindow menuWindow =
                top as DaggerfallBookReaderWindow;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed();
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow);
            }

            OnTickOpen(menuWindow, cm);
        }

        public void ResetState()
        {
            wasOpen = false;
            panelRenderWindow = null;
            DestroyLegend();
        }

        private void OnOpened(DaggerfallBookReaderWindow menuWindow)
        {
            Type t = typeof(DaggerfallBookReaderWindow);

            fiParentPanel = t.GetField("parentPanel", BF);

            miNextPageClick = t.GetMethod("NextPageButton_OnMouseClick", BF);
            miPreviousPageClick = t.GetMethod("PreviousPageButton_OnMouseClick", BF);

            if (fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;
        }

        private void OnClosed()
        {
            ResetState();
        }

        private void OnTickOpen(DaggerfallBookReaderWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir == ControllerManager.StickDir8.N && miPreviousPageClick != null)
            {
                miPreviousPageClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
                return;
            }

            if (dir == ControllerManager.StickDir8.S && miNextPageClick != null)
            {
                miNextPageClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
                return;
            }

            if (cm.LegendPressed)
            {
                ToggleLegend(menuWindow, cm);
                return;
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                menuWindow.CloseWindow();
                return;
            }
        }

        private void EnsureLegendUI(DaggerfallBookReaderWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiParentPanel != null)
                panelRenderWindow = fiParentPanel.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null)
                return;

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

                legend.HeaderScale = 6.0f;
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
                    new LegendOverlay.LegendRow("Right Stick", "Scroll"),
                    new LegendOverlay.LegendRow("Back", "Close"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallBookReaderWindow menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return;

            Panel current = fiParentPanel.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                DestroyLegend();
                panelRenderWindow = current;
                legendVisible = false;
                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }

        private void ToggleLegend(DaggerfallBookReaderWindow menuWindow, ControllerManager cm)
        {
            EnsureLegendUI(menuWindow, cm);

            legendVisible = !legendVisible;

            if (legend != null)
                legend.SetEnabled(legendVisible);
        }

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
        }
    }
}