using DaggerfallWorkshop.Game.UserInterface;

namespace gigantibyte.DFU.ControllerAssistant
{
    public interface IMenuAssist
    {
        bool Claims(IUserInterfaceWindow top);
        void Tick(IUserInterfaceWindow top, ControllerManager cm);
        void ResetState();
    }
}
