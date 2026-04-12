using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class BankPurchaseAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool wasOpen = false;
        private bool reflectionCached = false;

        private DaggerfallBankPurchasePopUp activeWindow;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        // Legend
        private FieldInfo fiParentPanel;
        private Panel parentPanel;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private FieldInfo fiPriceListBox;
        private MethodInfo miPriceListBox_OnSelectItem;
        private MethodInfo miBuyButton_OnMouseClick;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallBankPurchasePopUp;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallBankPurchasePopUp menuWindow = top as DaggerfallBankPurchasePopUp;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(activeWindow, cm);
                    activeWindow = null;
                    wasOpen = false;
                }
                return;
            }

            if (wasOpen && !object.ReferenceEquals(menuWindow, activeWindow))
            {
                OnClosed(activeWindow, cm);
                activeWindow = menuWindow;
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }
            else if (!wasOpen)
            {
                wasOpen = true;
                activeWindow = menuWindow;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        public void ResetState()
        {
            wasOpen = false;
            legendVisible = false;
            parentPanel = null;
            DestroyLegend();
        }

        private void OnOpened(DaggerfallBankPurchasePopUp menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);

            ListBox listBox = GetPriceListBox(menuWindow);
            if (listBox != null)
            {
                listBox.AlwaysAcceptKeyboardInput = true;

                if (listBox != null)
                {
                    listBox.AlwaysAcceptKeyboardInput = true;
                }
            }
        }

        private void OnClosed(DaggerfallBankPurchasePopUp menuWindow, ControllerManager cm)
        {
            ListBox listBox = GetPriceListBox(menuWindow);
            if (listBox != null)
                listBox.AlwaysAcceptKeyboardInput = false;

            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallBankPurchasePopUp closed");
        }

        private void OnTickOpen(DaggerfallBankPurchasePopUp menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            EnsureListBoxFocus(menuWindow);

            ListBox listBox = GetPriceListBox(menuWindow);
            if (listBox != null)
                listBox.AlwaysAcceptKeyboardInput = true;

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                switch (dir)
                {
                    case ControllerManager.StickDir8.N:
                        SelectPrevious(menuWindow);
                        break;

                    case ControllerManager.StickDir8.S:
                        SelectNext(menuWindow);
                        break;
                }
            }

            bool isAssisting = (cm.Action1Released || cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.Action1Released)
                    ActivateSelected(menuWindow);

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
                return;
            }
        }

        private void SelectPrevious(DaggerfallBankPurchasePopUp menuWindow)
        {
            ListBox listBox = GetPriceListBox(menuWindow);
            if (listBox == null || listBox.Count <= 0)
                return;

            if (listBox.SelectedIndex <= 0)
                return;

            DaggerfallUI.Instance.OnKeyPress(KeyCode.UpArrow, true);
            DaggerfallUI.Instance.OnKeyPress(KeyCode.UpArrow, false);
        }

        private void SelectNext(DaggerfallBankPurchasePopUp menuWindow)
        {
            ListBox listBox = GetPriceListBox(menuWindow);
            if (listBox == null || listBox.Count <= 0)
                return;

            DaggerfallUI.Instance.OnKeyPress(KeyCode.DownArrow, true);
            DaggerfallUI.Instance.OnKeyPress(KeyCode.DownArrow, false);
        }

        private void ActivateSelected(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (menuWindow == null || miBuyButton_OnMouseClick == null)
                return;

            miBuyButton_OnMouseClick.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void EnsureListBoxFocus(DaggerfallBankPurchasePopUp menuWindow)
        {
            ListBox listBox = GetPriceListBox(menuWindow);
            if (listBox == null)
                return;

            if (!object.ReferenceEquals(menuWindow.FocusControl, listBox))
                menuWindow.SetFocus(listBox);
        }

        private void EnsureInitialized(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (reflectionCached || menuWindow == null)
                return;

            System.Type type = menuWindow.GetType();
            fiParentPanel = CacheField(type, "parentPanel");
            fiPriceListBox = CacheField(type, "priceListBox");
            miPriceListBox_OnSelectItem = CacheMethod(type, "PriceListBox_OnSelectItem");
            miBuyButton_OnMouseClick = CacheMethod(type, "BuyButton_OnMouseClick");

            reflectionCached = true;
        }
        private ListBox GetPriceListBox(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (menuWindow == null || fiPriceListBox == null)
                return null;

            return fiPriceListBox.GetValue(menuWindow) as ListBox;
        }
        private void RefreshPreviewSelection(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (menuWindow == null || miPriceListBox_OnSelectItem == null)
                return;

            miPriceListBox_OnSelectItem.Invoke(menuWindow, null);
        }

        private void EnsureLegendUI(DaggerfallBankPurchasePopUp menuWindow, ControllerManager cm)
        {
            if (menuWindow == null)
                return;

            if (parentPanel == null && fiParentPanel != null)
                parentPanel = fiParentPanel.GetValue(menuWindow) as Panel;

            if (parentPanel == null)
                return;

            if (legend == null)
            {
                legend = new LegendOverlay(parentPanel);
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
                    new LegendOverlay.LegendRow("Version", "6"),
                    new LegendOverlay.LegendRow("Right Stick", "Select"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Buy"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (menuWindow == null || fiParentPanel == null)
                return;

            Panel current = fiParentPanel.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (parentPanel != current)
            {
                DestroyLegend();
                parentPanel = current;
                legendVisible = false;
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

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }
        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private void DumpWindowMembers(object window)
        {
            System.Type type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (MethodInfo m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (FieldInfo f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
    }
}