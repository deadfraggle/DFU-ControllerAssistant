using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InputMessageBoxAssist
    {
        private interface IInputMessageBoxAssistHandler
        {
            bool CanHandle(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow);
            void OnOpen(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm);
            void Tick(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm);
            void OnClose(InputMessageBoxAssist owner, ControllerManager cm);
        }
    }
}
