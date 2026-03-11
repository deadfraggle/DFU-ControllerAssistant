using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class CAFavoritesWindowAssist : MenuAssistModule<ControllerAssistantFavoritesWindow>
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool reflectionCached = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        protected override void OnTickOpen(ControllerAssistantFavoritesWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            bool isAssisting =
                (cm.DPadUpPressed ||
                 cm.DPadDownPressed ||
                 cm.DPadLeftPressed ||
                 cm.DPadRightPressed ||
                 cm.RStickUpPressed ||
                 cm.RStickDownPressed ||
                 cm.Action2Pressed ||
                 cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.DPadUpPressed)
                    SelectPreviousLocation(menuWindow);

                if (cm.DPadDownPressed)
                    SelectNextLocation(menuWindow);

                if (cm.DPadLeftPressed)
                    PreviousRegion(menuWindow);

                if (cm.DPadRightPressed)
                    NextRegion(menuWindow);

                if (cm.RStickUpPressed)
                    MoveLocationUp(menuWindow);

                if (cm.RStickDownPressed)
                    MoveLocationDown(menuWindow);

                if (cm.Action2Pressed)
                    DeleteSelectedLocation(menuWindow);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;

                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                menuWindow.CloseWindow();
                return;
            }
        }

        protected override void OnOpened(ControllerAssistantFavoritesWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);
        }

        protected override void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("ControllerAssistantFavoritesWindow closed");
        }

        public override void ResetState()
        {
            base.ResetState(); // sets wasOpen = false

            legendVisible = false;
            legend = null;
            panelRenderWindow = null;
        }

        private void EnsureInitialized(ControllerAssistantFavoritesWindow menuWindow)
        {
            if (reflectionCached)
                return;

            if (menuWindow == null)
                return;

            var type = menuWindow.GetType();

            // ControllerAssistantFavoritesWindow uses a private Panel named mainPanel
            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        // =========================
        // Assist action helpers
        // =========================

        private void SelectPreviousLocation(ControllerAssistantFavoritesWindow menuWindow)
        {
            // TODO: wire to Favorites window selection logic
            if (debugMODE)
                DaggerfallUI.AddHUDText("Favorites: SelectPreviousLocation");
        }

        private void SelectNextLocation(ControllerAssistantFavoritesWindow menuWindow)
        {
            // TODO: wire to Favorites window selection logic
            if (debugMODE)
                DaggerfallUI.AddHUDText("Favorites: SelectNextLocation");
        }

        private void PreviousRegion(ControllerAssistantFavoritesWindow menuWindow)
        {
            // TODO: wire to Favorites window region paging
            if (debugMODE)
                DaggerfallUI.AddHUDText("Favorites: PreviousRegion");
        }

        private void NextRegion(ControllerAssistantFavoritesWindow menuWindow)
        {
            // TODO: wire to Favorites window region paging
            if (debugMODE)
                DaggerfallUI.AddHUDText("Favorites: NextRegion");
        }

        private void MoveLocationUp(ControllerAssistantFavoritesWindow menuWindow)
        {
            // TODO: wire to Favorites reordering
            if (debugMODE)
                DaggerfallUI.AddHUDText("Favorites: MoveLocationUp");
        }

        private void MoveLocationDown(ControllerAssistantFavoritesWindow menuWindow)
        {
            // TODO: wire to Favorites reordering
            if (debugMODE)
                DaggerfallUI.AddHUDText("Favorites: MoveLocationDown");
        }

        private void DeleteSelectedLocation(ControllerAssistantFavoritesWindow menuWindow)
        {
            // TODO: open confirmation message box and delete selected favorite
            if (debugMODE)
                DaggerfallUI.AddHUDText("Favorites: DeleteSelectedLocation");
        }

        private void EnsureLegendUI(ControllerAssistantFavoritesWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

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
                    new LegendOverlay.LegendRow("D-Pad Up/Down", "Select location"),
                    new LegendOverlay.LegendRow("D-Pad Left/Right", "Change region"),
                    new LegendOverlay.LegendRow("Right Stick Up/Down", "Move location"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Delete"),
                };

                legend.Build("Favorites", rows);
            }
        }

        private void RefreshLegendAttachment(ControllerAssistantFavoritesWindow menuWindow)
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

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }
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