using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private class GenericCloseHandler : IMessageBoxAssistHandler
        {
            public bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (menuWindow == null)
                    return false;

                return owner.IsButtonlessMessageBox(menuWindow);
            }

            public void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                owner.EnsureLegendUI(
                    menuWindow,
                    "Legend",
                    new List<LegendOverlay.LegendRow>()
                    {
                        new LegendOverlay.LegendRow(cm.Action1Name, "Close"),
                    });

                owner.SetLegendVisible(false);
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                if (cm.Action1Released)
                {
                    owner.ActivateMessageBox(menuWindow);
                    return;
                }

                if (cm.LegendPressed)
                {
                    bool visible = !owner.GetLegendVisible();
                    owner.SetLegendVisible(visible);
                }
            }

            public void OnClose(MessageBoxAssist owner, ControllerManager cm)
            {
            }
        }
    }
}
