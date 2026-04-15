using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private sealed class GenericButtonsHandler : IMessageBoxAssistHandler
        {
            private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private sealed class SemanticButtonInfo
            {
                public Button uiButton;
                public DaggerfallMessageBox.MessageBoxButtons semantic;
                public Rect nativeRect;
            }

            private readonly List<SemanticButtonInfo> buttons = new List<SemanticButtonInfo>();
            private int selectedIndex = 0;
            private DefaultSelectorBoxHost selectorHost;

            public bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (menuWindow == null)
                    return false;

                // Yes/No is handled by YesNoHandler earlier.
                if (owner.HasExactButtons(
                    menuWindow,
                    DaggerfallMessageBox.MessageBoxButtons.Yes,
                    DaggerfallMessageBox.MessageBoxButtons.No))
                {
                    return false;
                }

                BuildSemanticButtonList(owner, menuWindow);

                // Generic handler is only for 2- or 3-button semantic popups.
                return buttons.Count == 2 || buttons.Count == 3;
            }

            public void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                BuildSemanticButtonList(owner, menuWindow);

                selectedIndex = GetInitialSelectionIndex(menuWindow);
                RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                RefreshSelectorAttachment(owner, menuWindow);
                RefreshLiveRects(owner, menuWindow);

                bool moveLeft = cm.RStickLeftPressed || cm.RStickLeftHeldSlow;
                bool moveRight = cm.RStickRightPressed || cm.RStickRightHeldSlow;

                bool isAssisting =
                    moveLeft ||
                    moveRight ||
                    cm.DPadLeftReleased ||
                    cm.DPadRightReleased ||
                    cm.DPadUpReleased ||
                    cm.Action1Released ||
                    cm.LegendPressed;

                if (!isAssisting)
                    return;

                if (cm.DPadLeftReleased)
                {
                    ActivateLeftChoice(owner, menuWindow);
                    return;
                }

                if (cm.DPadRightReleased)
                {
                    ActivateRightChoice(owner, menuWindow);
                    return;
                }

                if (cm.DPadUpReleased)
                {
                    ActivateMiddleChoice(owner, menuWindow);
                    return;
                }

                if (moveLeft)
                {
                    MoveSelection(-1);
                    RefreshSelectorToCurrentButton(owner, menuWindow);
                }
                else if (moveRight)
                {
                    MoveSelection(1);
                    RefreshSelectorToCurrentButton(owner, menuWindow);
                }

                if (cm.Action1Released)
                {
                    ActivateSelectedButton(owner, menuWindow);
                    return;
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Buttons",
                        BuildLegendRows(cm));

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }
            }

            public void OnClose(MessageBoxAssist owner, ControllerManager cm)
            {
                DestroySelectorBox();
                buttons.Clear();
                selectedIndex = 0;
            }

            private void ActivateLeftChoice(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (buttons.Count < 1)
                    return;

                owner.ClickSemanticButton(menuWindow, buttons[0].semantic);
            }

            private void ActivateRightChoice(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (buttons.Count < 1)
                    return;

                owner.ClickSemanticButton(menuWindow, buttons[buttons.Count - 1].semantic);
            }

            private void ActivateMiddleChoice(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (buttons.Count != 3)
                    return;

                owner.ClickSemanticButton(menuWindow, buttons[1].semantic);
            }

            private void ActivateSelectedButton(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (selectedIndex < 0 || selectedIndex >= buttons.Count)
                    return;

                owner.ClickSemanticButton(menuWindow, buttons[selectedIndex].semantic);
            }

            private void MoveSelection(int delta)
            {
                if (buttons.Count <= 1)
                    return;

                selectedIndex += delta;

                if (selectedIndex < 0)
                    selectedIndex = buttons.Count - 1;
                else if (selectedIndex >= buttons.Count)
                    selectedIndex = 0;
            }

            private int GetInitialSelectionIndex(DaggerfallMessageBox menuWindow)
            {
                Button defaultButton = menuWindow.GetDefaultButton();
                if (defaultButton != null)
                {
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        if (buttons[i].uiButton == defaultButton)
                            return i;
                    }
                }

                if (buttons.Count == 2)
                    return 1;   // right choice by default, like Yes/No
                if (buttons.Count == 3)
                    return 1;   // middle choice for 3-button rows

                return 0;
            }

            private List<LegendOverlay.LegendRow> BuildLegendRows(ControllerManager cm)
            {
                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("D-Pad Left", "Left Choice"),
                    new LegendOverlay.LegendRow("D-Pad Right", "Right Choice"),
                    new LegendOverlay.LegendRow("Right Stick Left/Right", "Move Selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                };

                if (buttons.Count == 3)
                    rows.Insert(2, new LegendOverlay.LegendRow("D-Pad Up", "Middle Choice"));

                return rows;
            }

            private void RefreshSelectorToCurrentButton(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null || buttons.Count == 0)
                    return;

                if (selectedIndex < 0 || selectedIndex >= buttons.Count)
                    selectedIndex = 0;

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                float borderThickness = 2f;
                if (currentPanel.Size.y > 0f)
                {
                    float scaleY = currentPanel.Size.y / 200f;
                    borderThickness = Mathf.Max(2f, scaleY * 0.5f);
                }

                selectorHost.ShowAtNativeRect(
                    currentPanel,
                    buttons[selectedIndex].nativeRect,
                    borderThickness,
                    new Color(0.1f, 1f, 1f, 1f));
            }

            private void RefreshSelectorAttachment(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null || selectorHost == null)
                    return;

                selectorHost.RefreshAttachment(currentPanel);
            }

            private void RefreshLiveRects(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (buttons.Count == 0)
                    return;

                Panel renderPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (renderPanel == null)
                    return;

                for (int i = 0; i < buttons.Count; i++)
                {
                    Button button = buttons[i].uiButton;
                    if (button == null)
                        continue;

                    buttons[i].nativeRect = ScreenRectToNativeRect(renderPanel, button.Rectangle);
                }
            }

            private void BuildSemanticButtonList(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                buttons.Clear();

                if (menuWindow == null)
                    return;

                FieldInfo fiButtons = typeof(DaggerfallMessageBox).GetField("buttons", BF);
                if (fiButtons == null)
                    return;

                IList list = fiButtons.GetValue(menuWindow) as IList;
                if (list == null)
                    return;

                Panel renderPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (renderPanel == null)
                    return;

                for (int i = 0; i < list.Count; i++)
                {
                    Button uiButton = list[i] as Button;
                    if (uiButton == null || !uiButton.Enabled)
                        continue;

                    if (!(uiButton.Tag is DaggerfallMessageBox.MessageBoxButtons))
                        continue;

                    buttons.Add(new SemanticButtonInfo()
                    {
                        uiButton = uiButton,
                        semantic = (DaggerfallMessageBox.MessageBoxButtons)uiButton.Tag,
                        nativeRect = ScreenRectToNativeRect(renderPanel, uiButton.Rectangle),
                    });
                }
            }

            private Rect ScreenRectToNativeRect(Panel renderPanel, Rect screenRect)
            {
                Rect panelRect = renderPanel.Rectangle;
                if (panelRect.width <= 0 || panelRect.height <= 0)
                    return Rect.zero;

                float localX = screenRect.x - panelRect.x;
                float localY = screenRect.y - panelRect.y;

                float nativeX = (localX / panelRect.width) * 320f;
                float nativeY = (localY / panelRect.height) * 200f;
                float nativeW = (screenRect.width / panelRect.width) * 320f;
                float nativeH = (screenRect.height / panelRect.height) * 200f;

                return new Rect(nativeX, nativeY, nativeW, nativeH);
            }

            private void DestroySelectorBox()
            {
                if (selectorHost != null)
                {
                    selectorHost.Destroy();
                    selectorHost = null;
                }
            }
        }
    }
}