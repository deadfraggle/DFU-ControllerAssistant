/*
    MenuAssist<TWindow>
    -------------------

    Generic base class for menu-specific assist modules used by ControllerAssistant.

    Each assist module targets a specific DFU UIWindow type (inventory, automap, travel map, etc.).
    The generic parameter <TWindow> defines which window the module handles. This allows the base
    class to perform the window-type check and safe cast automatically so individual modules do not
    need to repeat that logic.

    Responsibilities of this base class:
    ------------------------------------
    1. Determine whether the currently active UI window matches the assist's target type.
    2. Detect window open/close transitions using a simple edge-detection flag (wasOpen).
    3. Provide lifecycle hooks for assist modules:
       - OnOpened()     : called once when the target window first opens
       - OnTickOpen()   : called every frame while the window remains open
       - OnClosed()     : called once when the window closes
    4. Ensure modules only run when their window type is active.

    Execution Flow:
    ----------------
        ControllerManager
            ↓
        Dispatcher calls Tick() on all modules
            ↓
        Tick() checks if top window is TWindow
            ↓
        OnOpened() fires once when window becomes active
            ↓
        OnTickOpen() runs every frame while open
            ↓
        OnClosed() fires once when the window closes

    Notes:
    ------
    • ResetState() clears the open-state flag and is used as a safety reset when menus close
      in ways that bypass normal detection (for example when the HUD resumes control).

    • This base class intentionally handles the window cast once and passes the typed window
      to assist methods, eliminating repeated casts and reducing copy-paste errors in modules.

    Typical derived class structure:
    --------------------------------
        public class InventoryAssist : MenuAssist<DaggerfallInventoryWindow>
        {
            protected override void OnTickOpen(DaggerfallInventoryWindow menuWindow, ControllerManager cm)
            {
                // assist logic here
            }
        }
*/

using DaggerfallWorkshop.Game.UserInterface;

namespace gigantibyte.DFU.ControllerAssistant
{
    public abstract class MenuAssistModule<TWindow> : IMenuAssist
        where TWindow : class, IUserInterfaceWindow
    {
        protected bool wasOpen = false;

        public bool Claims(IUserInterfaceWindow top) => top is TWindow;

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            TWindow menuWindow = top as TWindow;
            bool open = menuWindow != null;

            if (open && !wasOpen)
                OnOpened(menuWindow, cm);

            if (!open && wasOpen)
                OnClosed(cm);

            wasOpen = open;

            if (!open)
                return;

            OnTickOpen(menuWindow, cm);
        }

        protected virtual void OnOpened(TWindow window, ControllerManager cm) { }
        protected virtual void OnClosed(ControllerManager cm) { }
        protected abstract void OnTickOpen(TWindow window, ControllerManager cm);

        public virtual void ResetState()
        {
            wasOpen = false;
        }
    }
}
