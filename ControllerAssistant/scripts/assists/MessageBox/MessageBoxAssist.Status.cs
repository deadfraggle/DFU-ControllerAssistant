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
            private bool loggedOpen = false;
            private MessageBoxStatusLabelOverlay labelOverlay;

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
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Status Probe",
                        new System.Collections.Generic.List<LegendOverlay.LegendRow>()
                        {
                new LegendOverlay.LegendRow("Matched", "Status popup v20"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }

                if (cm.Action2Pressed)
                {
                    owner.ToggleAnchorEditor();
                }

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
            }
        }
    }
}
