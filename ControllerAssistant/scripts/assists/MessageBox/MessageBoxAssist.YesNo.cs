using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private sealed class YesNoHandler : IMessageBoxAssistHandler
        {
            public bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                return owner.HasExactButtons(
                    menuWindow,
                    DaggerfallMessageBox.MessageBoxButtons.Yes,
                    DaggerfallMessageBox.MessageBoxButtons.No);
            }

            public void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                bool isAssisting =
                    cm.DPadUpPressed ||
                    cm.DPadDownPressed ||
                    cm.LegendPressed;

                if (!isAssisting)
                    return;

                if (cm.DPadUpPressed)
                    owner.SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Yes);

                if (cm.DPadDownPressed)
                    owner.SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.No);

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Legend",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("D-Pad Up", "Yes"),
                            new LegendOverlay.LegendRow("D-Pad Down", "No"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }
            }

            public void OnClose(MessageBoxAssist owner, ControllerManager cm)
            {
            }
        }
    }
}
