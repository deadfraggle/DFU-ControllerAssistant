using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

/* BankPurchaseAssist high-res preview fix:
DFU bank purchase popup sometimes initializes display render target at native 1x size 
when opened via controller-assisted button activation. Assist rebuilds display texture/render 
texture using displayPanelRect * NativePanel.LocalScale if low-res state detected. */

namespace gigantibyte.DFU.ControllerAssistant
{
    public class BankPurchaseAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool wasOpen = false;
        private bool reflectionCached = false;
        private bool forcedHighResOnce = false;

        private DaggerfallBankPurchasePopUp activeWindow;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        // Legend
        private FieldInfo fiParentPanel;
        private Panel parentPanel;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private FieldInfo fiPriceListBox;
        private MethodInfo miBuyButton_OnMouseClick;
        private FieldInfo fiDisplayResolution;
        private FieldInfo fiDisplayPanelRect;
        private FieldInfo fiCamera;
        private FieldInfo fiDisplayTexture;

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
            forcedHighResOnce = false;
        }

        private void OnOpened(DaggerfallBankPurchasePopUp menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);

            ListBox listBox = GetPriceListBox(menuWindow);
            if (listBox != null)
            {
                listBox.AlwaysAcceptKeyboardInput = true;

                if (listBox.Count > 0 && listBox.SelectedIndex < 0)
                    listBox.SelectedIndex = 0;
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
            FixDisplayResolutionIfNeeded(menuWindow);

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
            ListBox listBox = GetPriceListBox(menuWindow);
            if (menuWindow == null || listBox == null || listBox.SelectedIndex < 0 || miBuyButton_OnMouseClick == null)
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
        private void FixDisplayResolutionIfNeeded(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (menuWindow == null || forcedHighResOnce)
                return;

            if (fiDisplayResolution == null || fiDisplayPanelRect == null || fiDisplayTexture == null || fiCamera == null)
                return;

            Camera cam = fiCamera.GetValue(menuWindow) as Camera;
            if (cam == null)
                return;

            Rect displayPanelRect = (Rect)fiDisplayPanelRect.GetValue(menuWindow);
            Vector2 currentRes = (Vector2)fiDisplayResolution.GetValue(menuWindow);

            if (menuWindow.NativePanel == null)
                return;

            float scaleX = menuWindow.NativePanel.LocalScale.x;
            float scaleY = menuWindow.NativePanel.LocalScale.y;

            int targetWidth = Mathf.RoundToInt(displayPanelRect.width * scaleX);
            int targetHeight = Mathf.RoundToInt(displayPanelRect.height * scaleY);

            if (targetWidth <= 0 || targetHeight <= 0)
                return;

            int currentWidth = Mathf.RoundToInt(currentRes.x);
            int currentHeight = Mathf.RoundToInt(currentRes.y);

            // Only fix the bad low-res state, and only if the computed target is meaningfully larger.
            if (currentWidth == targetWidth && currentHeight == targetHeight)
            {
                forcedHighResOnce = true;
                return;
            }

            if (currentWidth > 104 || currentHeight > 91)
            {
                forcedHighResOnce = true;
                return;
            }

            Vector2 correctedResolution = new Vector2(targetWidth, targetHeight);
            fiDisplayResolution.SetValue(menuWindow, correctedResolution);

            Texture2D newDisplayTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.ARGB32, false);
            fiDisplayTexture.SetValue(menuWindow, newDisplayTexture);

            if (cam.targetTexture != null)
            {
                RenderTexture oldRT = cam.targetTexture;
                cam.targetTexture = null;
                Object.Destroy(oldRT);
            }

            RenderTexture newRT = new RenderTexture(targetWidth, targetHeight, 16);
            cam.targetTexture = newRT;

            forcedHighResOnce = true;
        }

        private void EnsureInitialized(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (reflectionCached || menuWindow == null)
                return;

            System.Type type = menuWindow.GetType();
            fiParentPanel = CacheField(type, "parentPanel");
            fiPriceListBox = CacheField(type, "priceListBox");
            miBuyButton_OnMouseClick = CacheMethod(type, "BuyButton_OnMouseClick");
            fiDisplayResolution = CacheField(type, "displayResolution");
            fiDisplayPanelRect = CacheField(type, "displayPanelRect");
            fiCamera = CacheField(type, "camera");
            fiDisplayTexture = CacheField(type, "displayTexture");

            reflectionCached = true;
        }
        private ListBox GetPriceListBox(DaggerfallBankPurchasePopUp menuWindow)
        {
            if (menuWindow == null || fiPriceListBox == null)
                return null;

            return fiPriceListBox.GetValue(menuWindow) as ListBox;
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

        
    }
}