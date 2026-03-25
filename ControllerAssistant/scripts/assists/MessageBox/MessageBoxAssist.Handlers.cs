using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private interface IMessageBoxAssistHandler
        {
            bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow);
            void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm);
            void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm);
            void OnClose(MessageBoxAssist owner, ControllerManager cm);
        }
    }
}
