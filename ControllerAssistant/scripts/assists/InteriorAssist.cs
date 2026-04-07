using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class InteriorAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        // Cache for reflection so we don’t re-query every press
        private MethodInfo miActionMoveLeft;
        private MethodInfo miActionMoveRight;
        private MethodInfo miActionMoveForward;
        private MethodInfo miActionMoveBackward;
        private MethodInfo miActionMoveUpstairs;
        private MethodInfo miActionMoveDownstairs;
        private MethodInfo miActionResetView;
        private MethodInfo miActionRotateLeft;
        private MethodInfo miActionRotateRight;
        private MethodInfo miActionZoomIn;
        private MethodInfo miActionZoomOut;
        private MethodInfo miActionChangeAutomapGridMode;
        private MethodInfo miActionSwitchFocusToNextBeaconObject;
        private MethodInfo miActionrotateCameraOnCameraYZplaneAroundObject;

        private FieldInfo fiZoomSpeed;
        private FieldInfo fiRotateCameraOnCameraYZplaneAroundObjectSpeedInView3D;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        private FieldInfo fiWindowBinding;
        private bool closeDeferred = false;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallAutomapWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallAutomapWindow menuWindow = top as DaggerfallAutomapWindow;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(cm);
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        public void ResetState()
        {
            wasOpen = false;
            closeDeferred = false;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            legendVisible = false;
            panelRenderWindow = null;
        }

        private void OnTickOpen(DaggerfallAutomapWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.AutoMap);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);


            bool isAssisting =
                (cm.DPadH != 0 || cm.DPadV != 0 || cm.RStickV != 0 || cm.RStickH != 0 ||
                 cm.Action1 || cm.Action2 || cm.Action2Tapped || cm.Action2Held || cm.Legend);

            if (isAssisting)
            {
                if (cm.Action2Held)
                {
                    if (cm.Action1Pressed)
                        SwitchFocusToNextBeacon(menuWindow);

                    if (cm.RStickV == -1)
                        RotateCameraOnYZUp(menuWindow);

                    if (cm.RStickV == 1)
                        RotateCameraOnYZDown(menuWindow);

                    if (cm.RStickH == 1)
                        MoveMapDownstairs(menuWindow);

                    if (cm.RStickH == -1)
                        MoveMapUpstairs(menuWindow);
                }
                else
                {
                    if (cm.RStickH == 1) PanMapLeft(menuWindow);
                    if (cm.RStickH == -1) PanMapRight(menuWindow);

                    if (cm.RStickV == 1) PanMapDown(menuWindow);
                    if (cm.RStickV == -1) PanMapUp(menuWindow);

                    if (cm.DPadV == -1) ZoomMapOut(menuWindow);
                    if (cm.DPadV == 1) ZoomMapIn(menuWindow);

                    if (cm.DPadH == 1) RotateMapClockwise(menuWindow);
                    if (cm.DPadH == -1) RotateMapCounterClockwise(menuWindow);

                    if (cm.Action1Pressed)
                        ResetMapView(menuWindow);

                    if (cm.Action2Tapped)
                        ChangeAutomapGridMode(menuWindow);
                }

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;
                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }

            //if (cm.BackPressed)
            //{
            //    menuWindow.CloseWindow();
            //    return;
            //}

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
            {
                closeDeferred = true;
            }

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;
                menuWindow.CloseWindow();
                return;
            }
        }

        private void OnOpened(DaggerfallAutomapWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("Automap closed");
        }

        private void PanMapLeft(DaggerfallAutomapWindow menuWindow)
        {
            miActionMoveRight?.Invoke(menuWindow, null);
        }

        private void PanMapRight(DaggerfallAutomapWindow menuWindow)
        {
            miActionMoveLeft?.Invoke(menuWindow, null);
        }

        private void PanMapDown(DaggerfallAutomapWindow menuWindow)
        {
            miActionMoveForward?.Invoke(menuWindow, null);
        }

        private void PanMapUp(DaggerfallAutomapWindow menuWindow)
        {
            miActionMoveBackward?.Invoke(menuWindow, null);
        }

        private void ZoomMapOut(DaggerfallAutomapWindow menuWindow)
        {
            if (menuWindow == null || miActionZoomOut == null)
                return;

            float zoomStep = GetCachedFloat(fiZoomSpeed, 3.0f) * Time.unscaledDeltaTime;
            miActionZoomOut.Invoke(menuWindow, new object[] { zoomStep });
        }

        private void ZoomMapIn(DaggerfallAutomapWindow menuWindow)
        {
            if (menuWindow == null || miActionZoomIn == null)
                return;

            float zoomStep = GetCachedFloat(fiZoomSpeed, 3.0f) * Time.unscaledDeltaTime;
            miActionZoomIn.Invoke(menuWindow, new object[] { zoomStep });
        }
        private void MoveMapUpstairs(DaggerfallAutomapWindow menuWindow)
        {
            miActionMoveUpstairs?.Invoke(menuWindow, null);
        }

        private void MoveMapDownstairs(DaggerfallAutomapWindow menuWindow)
        {
            miActionMoveDownstairs?.Invoke(menuWindow, null);
        }

        private void RotateMapClockwise(DaggerfallAutomapWindow menuWindow)
        {
            miActionRotateRight?.Invoke(menuWindow, null);
        }

        private void RotateMapCounterClockwise(DaggerfallAutomapWindow menuWindow)
        {
            miActionRotateLeft?.Invoke(menuWindow, null);
        }

        private void ResetMapView(DaggerfallAutomapWindow menuWindow)
        {
            miActionResetView?.Invoke(menuWindow, null);
        }

        private void ChangeAutomapGridMode(DaggerfallAutomapWindow menuWindow)
        {
            miActionChangeAutomapGridMode?.Invoke(menuWindow, null);
        }
        private void SwitchFocusToNextBeacon(DaggerfallAutomapWindow menuWindow)
        {
            miActionSwitchFocusToNextBeaconObject?.Invoke(menuWindow, null);
        }
        private void RotateCameraOnYZUp(DaggerfallAutomapWindow menuWindow)
        {
            if (menuWindow == null || miActionrotateCameraOnCameraYZplaneAroundObject == null)
                return;

            float speed = GetCachedFloat(fiRotateCameraOnCameraYZplaneAroundObjectSpeedInView3D, 50.0f);
            miActionrotateCameraOnCameraYZplaneAroundObject.Invoke(menuWindow, new object[] { speed, true });
        }

        private void RotateCameraOnYZDown(DaggerfallAutomapWindow menuWindow)
        {
            if (menuWindow == null || miActionrotateCameraOnCameraYZplaneAroundObject == null)
                return;

            float speed = GetCachedFloat(fiRotateCameraOnCameraYZplaneAroundObjectSpeedInView3D, 50.0f);
            miActionrotateCameraOnCameraYZplaneAroundObject.Invoke(menuWindow, new object[] { -speed, true });
        }

        private void EnsureInitialized(DaggerfallAutomapWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "automapBinding");

            miActionMoveLeft = CacheMethod(type, "ActionMoveLeft");
            miActionMoveRight = CacheMethod(type, "ActionMoveRight");
            miActionMoveForward = CacheMethod(type, "ActionMoveForward");
            miActionMoveBackward = CacheMethod(type, "ActionMoveBackward");
            miActionZoomIn = CacheMethod(type, "ActionZoomIn");
            miActionZoomOut = CacheMethod(type, "ActionZoomOut");
            miActionMoveUpstairs = CacheMethod(type, "ActionMoveUpstairs");
            miActionMoveDownstairs = CacheMethod(type, "ActionMoveDownstairs");
            miActionRotateLeft = CacheMethod(type, "ActionRotateLeft");
            miActionRotateRight = CacheMethod(type, "ActionRotateRight");
            miActionResetView = CacheMethod(type, "ActionResetView");
            miActionChangeAutomapGridMode = CacheMethod(type, "ActionChangeAutomapGridMode");
            miActionSwitchFocusToNextBeaconObject = CacheMethod(type, "ActionSwitchFocusToNextBeaconObject");
            miActionrotateCameraOnCameraYZplaneAroundObject = CacheMethod(type, "ActionrotateCameraOnCameraYZplaneAroundObject");

            fiZoomSpeed = CacheField(type, "zoomSpeed");
            fiRotateCameraOnCameraYZplaneAroundObjectSpeedInView3D = CacheField(type, "rotateCameraOnCameraYZplaneAroundObjectSpeedInView3D");

            //fiPanelRenderWindow = CacheField(type, "panelRenderAutomap");
            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        private void EnsureLegendUI(DaggerfallAutomapWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

                legend.HeaderScale = 6.0f;
                legend.RowScale = 5.0f;
                legend.PadL = 18f;
                legend.PadT = 16f;
                legend.LineGap = 36f;
                legend.ColGap = 22f;
                legend.MarginX = 8f;
                legend.MarginFromBottom = 24f;
                legend.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("Right Stick", "Pan"),
                    new LegendOverlay.LegendRow("D-Pad Up/Down", "Zoom"),
                    new LegendOverlay.LegendRow("D-Pad Left/Right", "Rotate"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "Reset"),
                    new LegendOverlay.LegendRow(cm.Action2Name, "Grid mode"),
                    new LegendOverlay.LegendRow(cm.Action2Name + " Held + " + cm.Action1Name, "Next beacon"),
                    new LegendOverlay.LegendRow(cm.Action2Name + " Held + Right Stick Up/Down", "Z rotate"),
                    new LegendOverlay.LegendRow(cm.Action2Name + " Held + Right Stick Left/Right", "Up/Downstairs"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallAutomapWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                panelRenderWindow = current;
                legendVisible = false;
                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }
        private float GetCachedFloat(FieldInfo fi, float fallback)
        {
            if (fi == null)
                return fallback;

            try
            {
                if (fi.IsLiteral)
                    return System.Convert.ToSingle(fi.GetRawConstantValue());

                object value = fi.IsStatic ? fi.GetValue(null) : null;
                if (value == null)
                    return fallback;

                return System.Convert.ToSingle(value);
            }
            catch
            {
                return fallback;
            }
        }

        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
            return fi;
        }

        private void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
    }
}