using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class InteriorAssist : IMenuAssist
    {
        //debugging
        private const bool debugHUD = true;

        //variables to save the automap bindings
        //private KeyCode primaryBinding = KeyCode.None;
        //private KeyCode secondaryBinding = KeyCode.None;
        //private FieldInfo fiAutomapBinding;
        //private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private bool wasOpen = false;

        public bool Claims(IUserInterfaceWindow top) => top is DaggerfallAutomapWindow;

        //// Cache for reflection so we don’t re-query every press
        private DaggerfallAutomapWindow cachedAutomap;
        //private MethodInfo miActionMoveLeft;
        //private MethodInfo miActionMoveRight;
        //private MethodInfo miActionMoveForward;
        //private MethodInfo miActionMoveBackward;
        //private MethodInfo miActionMoveUpstairs;
        //private MethodInfo miActionMoveDownstairs;
        //private MethodInfo miActionResetView;
        //private MethodInfo miActionRotateLeft;
        //private MethodInfo miActionRotateRight;


        //private bool closeDeferred = false;
        //private KeyCode closeKey = KeyCode.None;

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            bool open = Claims(top);

            // Pass 'top' into OnOpened so it can access the type
            if (open && !wasOpen) OnOpened(top);
            if (!open && wasOpen) OnClosed();
            wasOpen = open;

            if (!open) return;

            var automap = top as DaggerfallAutomapWindow;
            if (automap == null) return;

            //    // OPTIMIZED REFLECTION: Use the cached FieldInfo to clear the close effect on binding
            //    if (fiAutomapBinding != null)
            //    {
            //        fiAutomapBinding.SetValue(automap, KeyCode.None);
            //    }

            //    // INPUT PROCESSING

            //    int dpadHdir = cm.DPadH;
            //    int dpadVdir = cm.DPadV;
            //    int rstickVdir = cm.RStickV;
            //    int rstickHdir = cm.RStickH;

            //    if (dpadHdir != 0) DaggerfallUI.AddHUDText("dpadHdir is NOT 0");

            CacheWindow(automap);

            //    //bool isAssisting = (dpadHdir != 0 || dpadVdir != 0 || rstickVdir != 0 || rstickHdir != 0 || Input.GetKey(KeyCode.JoystickButton9));
            //    bool isAssisting = (dpadHdir != 0 || dpadVdir != 0 || rstickVdir != 0 || rstickHdir != 0 || cm.RStickPressDown);

            //    // EXECUTE ASSIST ACTIONS
            //    if (isAssisting)
            //    {
            //        DaggerfallUI.AddHUDText("isAssisting is TRUE");
            //        if (dpadHdir == 1) PanMapLeft(automap);
            //        else if (dpadHdir == -1) PanMapRight(automap);
            //        else if (dpadVdir == 1) PanMapDown(automap);
            //        else if (dpadVdir == -1) PanMapUp(automap);
            //        else if (rstickVdir == 1) ZoomMapOut(automap);
            //        else if (rstickVdir == -1) ZoomMapIn(automap);
            //        else if (rstickHdir == 1) RotateMapClockwise(automap);
            //        else if (rstickHdir == -1) RotateMapCounterClockwise(automap);
            //        //else if (Input.GetKeyDown(KeyCode.JoystickButton9)) CenterMapOnPlayer(automap);
            //        else if (cm.RStickPressDown) CenterMapOnPlayer(automap);
            //    }

            //    // MANUAL EXIT LOGIC (Back closes immediately)
            //    //if (InputManager.Instance.GetBackButtonDown())
            //    if (cm.BackPressed)
            //    {
            //        automap.CloseWindow();
            //        return;
            //    }

            //    // Hotkey close: defer until key is released to prevent immediate reopen.
            //    if (closeDeferred)
            //    {
            //        if (closeKey != KeyCode.None && Input.GetKeyUp(closeKey))
            //        {
            //            closeDeferred = false;
            //            closeKey = KeyCode.None;
            //            automap.CloseWindow();
            //            return;
            //        }
            //    }
            //    else
            //    {
            //        // Only arm if we're not assisting (your choice), and only for real KeyCodes.
            //        if (!isAssisting)
            //        {
            //            if (primaryBinding != KeyCode.None && Input.GetKeyDown(primaryBinding))
            //            {
            //                closeDeferred = true;
            //                closeKey = primaryBinding;
            //            }
            //            else if (secondaryBinding != KeyCode.None && Input.GetKeyDown(secondaryBinding))
            //            {
            //                closeDeferred = true;
            //                closeKey = secondaryBinding;
            //            }
            //        }
            //    }


        }

        //private void PanMapLeft(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionMoveRight?.Invoke(automap, null);
        //}

        //private void PanMapRight(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionMoveLeft?.Invoke(automap, null);
        //}

        //private void PanMapDown(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionMoveForward?.Invoke(automap, null);
        //}


        //private void PanMapUp(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionMoveBackward?.Invoke(automap, null);
        //}


        //private void ZoomMapOut(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionMoveUpstairs?.Invoke(automap, null);
        //}


        //private void ZoomMapIn(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionMoveDownstairs?.Invoke(automap, null);
        //}

        //private void RotateMapClockwise(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionRotateRight?.Invoke(automap, null);
        //}

        //private void RotateMapCounterClockwise(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionRotateLeft?.Invoke(automap, null);
        //}

        //private void CenterMapOnPlayer(DaggerfallExteriorAutomapWindow automap)
        //{
        //    miActionResetView?.Invoke(automap, null);
        //}

        private void OnOpened(IUserInterfaceWindow top)
        {
            if (top == null) return;
            DaggerfallUI.AddHUDText("InteriorAssist OnOpened called");
            //    var type = top.GetType();
            //    var flags = BindingFlags.Instance | BindingFlags.NonPublic;

            //    // Cache the Hotkey Field
            //    if (fiAutomapBinding == null)
            //        fiAutomapBinding = type.GetField("automapBinding", flags);

            //    // Cache all MethodInfos at once
            //    miActionMoveLeft = type.GetMethod("ActionMoveLeft", flags);
            //    miActionMoveRight = type.GetMethod("ActionMoveRight", flags);
            //    miActionMoveForward = type.GetMethod("ActionMoveForward", flags);
            //    miActionMoveBackward = type.GetMethod("ActionMoveBackward", flags);
            //    miActionMoveUpstairs = type.GetMethod("ActionMoveUpstairs", flags);
            //    miActionMoveDownstairs = type.GetMethod("ActionMoveDownstairs", flags);
            //    miActionRotateLeft = type.GetMethod("ActionRotateLeft", flags);
            //    miActionRotateRight = type.GetMethod("ActionRotateRight", flags);
            //    miActionResetView = type.GetMethod("ActionResetView", flags);

            //    // 2. Capture current User Bindings from InputManager
            //    InputManager im = InputManager.Instance;
            //    var pDictField = typeof(InputManager).GetField("actionKeyDict", flags);
            //    var sDictField = typeof(InputManager).GetField("secondaryActionKeyDict", flags);

            //    var pDict = pDictField.GetValue(im) as System.Collections.Generic.Dictionary<KeyCode, InputManager.Actions>;
            //    var sDict = sDictField.GetValue(im) as System.Collections.Generic.Dictionary<KeyCode, InputManager.Actions>;

            //    // Reset our local storage
            //    primaryBinding = KeyCode.None;
            //    secondaryBinding = KeyCode.None;

            //    // Find which keys are currently mapped to AutoMap
            //    if (pDict != null)
            //    {
            //        foreach (var kvp in pDict)
            //        {
            //            if (kvp.Value == InputManager.Actions.AutoMap) { primaryBinding = kvp.Key; break; }
            //        }
            //    }
            //    if (sDict != null)
            //    {
            //        foreach (var kvp in sDict)
            //        {
            //            if (kvp.Value == InputManager.Actions.AutoMap) { secondaryBinding = kvp.Key; break; }
            //        }
            //    }

            //      if (debugHUD) DaggerfallUI.AddHUDText($"Assist Active. Saved: {primaryBinding}/{secondaryBinding}");
        }

        private void OnClosed()
        {
            ResetState();
            if (debugHUD) DaggerfallUI.AddHUDText("Automap closed");
        }

        private void CacheWindow(DaggerfallAutomapWindow automap)
        {
            if (automap == cachedAutomap)
                return;

            cachedAutomap = automap;

        }

        public void ResetState()
        {
            cachedAutomap = null;
        }

    }
}



