using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private sealed class StatusProbeHandler : IMessageBoxAssistHandler
        {
            private const int TalkButton = 0;
            private const int InfoButton = 1;
            private const int GrabButton = 2;
            private const int StealButton = 3;

            private bool loggedOpen = false;
            private MessageBoxStatusLabelOverlay labelOverlay;

            private DefaultSelectorBoxHost selectorHost;

            private SelectorButtonInfo[] menuButton;

            public bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (menuWindow == null)
                    return false;

                List<DaggerfallMessageBox.MessageBoxButtons> buttons;
                if (!owner.TryGetSemanticButtons(menuWindow, out buttons))
                    return false;

                if (buttons.Count != 0)
                    return false;

                KeyCode statusKey = InputManager.Instance.GetBinding(InputManager.Actions.Status);
                if (menuWindow.ExtraProceedBinding != statusKey)
                    return false;

                if (menuWindow.ClickAnywhereToClose)
                    return false;

                if (!owner.HasNextMessageBox(menuWindow))
                    return false;

                Debug.Log("[ControllerAssistant] StatusProbe structural match confirmed.");
                return true;
            }

            public void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                if (!loggedOpen)
                {
                    Debug.Log("[ControllerAssistant] StatusProbeHandler matched popup.");
                    loggedOpen = true;
                }

                Panel panel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (panel != null)
                {
                    labelOverlay = new MessageBoxStatusLabelOverlay(panel);
                    labelOverlay.Build();
                }

                
                BuildButtonMap();
                RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                RefreshSelectorAttachment(owner, menuWindow);

                bool moveLeft = cm.RStickLeftPressed || cm.RStickLeftHeldSlow;
                bool moveRight = cm.RStickRightPressed || cm.RStickRightHeldSlow;

                bool isAssisting =
                    moveLeft ||
                    moveRight ||
                    cm.Action1Released ||
                    cm.LegendPressed;

                if (!isAssisting)
                    return;

                if (moveLeft)
                    TryMoveSelector(owner, menuWindow, ControllerManager.StickDir8.W);
                else if (moveRight)
                    TryMoveSelector(owner, menuWindow, ControllerManager.StickDir8.E);

                if (cm.Action1Released)
                {
                    ActivateSelectedButton(owner, menuWindow);
                    return;
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Interaction Mode",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("Right Stick Left/Right", "Move Selector"),
                            new LegendOverlay.LegendRow(cm.Action1Name, "Select Mode"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }

                //if (cm.Action2Pressed)
                //    owner.ToggleAnchorEditor();

                if (labelOverlay != null && !labelOverlay.IsAttached())
                {
                    Panel panel = owner.GetMessageBoxRenderPanel(menuWindow);
                    if (panel != null)
                    {
                        labelOverlay = new MessageBoxStatusLabelOverlay(panel);
                        labelOverlay.Build();
                    }
                }
            }

            public void OnClose(MessageBoxAssist owner, ControllerManager cm)
            {
                if (labelOverlay != null)
                {
                    labelOverlay.Destroy();
                    labelOverlay = null;
                }

                DestroySelectorBox();
            }

            private void BuildButtonMap()
            {
                menuButton = new SelectorButtonInfo[]
                {
                    new SelectorButtonInfo
                    {
                        rect = new Rect(95.2f, 161.2f, 14.7f, 6.2f),
                        W = StealButton,
                        E = InfoButton,
                    }, // Talk

                    new SelectorButtonInfo
                    {
                        rect = new Rect(133.4f, 161.2f, 14.7f, 6.2f),
                        W = TalkButton,
                        E = GrabButton,
                    }, // Info

                    new SelectorButtonInfo
                    {
                        rect = new Rect(171.7f, 161.2f, 14.7f, 6.2f),
                        W = InfoButton,
                        E = StealButton,
                    }, // Grab

                    new SelectorButtonInfo
                    {
                        rect = new Rect(209.9f, 161.2f, 14.7f, 6.2f),
                        W = GrabButton,
                        E = TalkButton,
                    }, // Steal
                };
            }

            private void ActivateSelectedButton(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (GameManager.Instance == null || GameManager.Instance.PlayerActivate == null)
                    return;

                string modeName = null;

                if (owner.statusButtonSelected == TalkButton)
                    modeName = "Talk";
                else if (owner.statusButtonSelected == InfoButton)
                    modeName = "Info";
                else if (owner.statusButtonSelected == GrabButton)
                    modeName = "Grab";
                else if (owner.statusButtonSelected == StealButton)
                    modeName = "Steal";

                if (string.IsNullOrEmpty(modeName))
                    return;

                object playerActivate = GameManager.Instance.PlayerActivate;
                System.Type playerActivateType = playerActivate.GetType();

                try
                {
                    // Get runtime enum type from CurrentMode property
                    object currentModeValue = playerActivateType.GetProperty("CurrentMode").GetValue(playerActivate, null);
                    System.Type modeType = currentModeValue.GetType();

                    // Build enum value without referencing PlayerActivateModes by name in code
                    object modeValue = System.Enum.Parse(modeType, modeName);

                    // Invoke ChangeInteractionMode(enumValue, true)
                    System.Reflection.MethodInfo miChangeInteractionMode =
                        playerActivateType.GetMethod("ChangeInteractionMode", new System.Type[] { modeType, typeof(bool) });

                    if (miChangeInteractionMode != null)
                        miChangeInteractionMode.Invoke(playerActivate, new object[] { modeValue, true });

                    // Prevent release from falling through into world activation
                    System.Reflection.MethodInfo miSetClickDelay =
                        playerActivateType.GetMethod("SetClickDelay", System.Type.EmptyTypes);

                    if (miSetClickDelay != null)
                        miSetClickDelay.Invoke(playerActivate, null);

                    if (menuWindow != null)
                        menuWindow.CloseWindow();
                }
                catch (System.Exception ex)
                {
                    Debug.Log("[ControllerAssistant] Status mode activation failed: " + ex);
                }
            }

            private void TryMoveSelector(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow,
                ControllerManager.StickDir8 dir)
            {
                if (dir == ControllerManager.StickDir8.None || menuButton == null)
                    return;

                int previous = owner.statusButtonSelected;
                SelectorButtonInfo btn = menuButton[owner.statusButtonSelected];

                int next = -1;

                switch (dir)
                {
                    case ControllerManager.StickDir8.E:
                    case ControllerManager.StickDir8.NE:
                    case ControllerManager.StickDir8.SE:
                        next = btn.E;
                        break;

                    case ControllerManager.StickDir8.W:
                    case ControllerManager.StickDir8.NW:
                    case ControllerManager.StickDir8.SW:
                        next = btn.W;
                        break;
                }

                if (next > -1)
                    owner.statusButtonSelected = next;

                if (owner.statusButtonSelected != previous)
                    RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            private void RefreshSelectorToCurrentButton(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null || menuButton == null)
                    return;

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.ShowAtNativeRect(
                    currentPanel,
                    menuButton[owner.statusButtonSelected].rect,
                    new Color(0.1f, 1f, 1f, 1f)
                );
            }

            private void RefreshSelectorAttachment(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.RefreshAttachment(currentPanel);
            }

            private void DestroySelectorBox()
            {
                if (selectorHost != null)
                    selectorHost.Destroy();
            }
            
        }
    }
}