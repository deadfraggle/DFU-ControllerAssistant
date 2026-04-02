using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    /// <summary>
    /// Generic helper that owns a DefaultSelectorBoxOverlay and keeps it
    /// attached to the current panel. Assist modules only provide the panel
    /// and the current native rect to show.
    /// </summary>
    public class DefaultSelectorBoxHost
    {
        private DefaultSelectorBoxOverlay selectorBox;
        private Panel attachedPanel;
        private bool hasShownOnce = false;

        public bool IsBuilt
        {
            get { return selectorBox != null && selectorBox.IsBuilt; }
        }

        public void Destroy()
        {
            if (selectorBox != null)
            {
                selectorBox.Destroy();
                selectorBox = null;
            }

            attachedPanel = null;
            hasShownOnce = false;
        }

        public void RefreshAttachment(Panel currentPanel)
        {
            if (currentPanel == null)
                return;

            if (attachedPanel != currentPanel)
            {
                Destroy();
                attachedPanel = currentPanel;
                return;
            }

            if (selectorBox != null && !selectorBox.IsAttached())
            {
                selectorBox = null;
                hasShownOnce = false;
            }
        }

        public void ShowAtNativeRect(Panel currentPanel, Rect nativeRect, Color borderColor)
        {
            if (currentPanel == null)
                return;

            RefreshAttachment(currentPanel);

            if (selectorBox == null)
                selectorBox = new DefaultSelectorBoxOverlay(currentPanel);

            float thickness = selectorBox.GetSuggestedBorderThickness();

            if (!hasShownOnce || !selectorBox.IsBuilt)
            {
                selectorBox.BuildFromNativeRect(nativeRect, thickness, borderColor);
                hasShownOnce = true;
            }
            else
            {
                selectorBox.MoveToNativeRect(nativeRect);
            }
        }
        public void ShowAtNativeRect(Panel currentPanel, Rect nativeRect, float borderThickness, Color borderColor)
        {
            if (currentPanel == null)
                return;

            RefreshAttachment(currentPanel);

            if (selectorBox == null)
                selectorBox = new DefaultSelectorBoxOverlay(currentPanel);

            if (!hasShownOnce || !selectorBox.IsBuilt)
            {
                selectorBox.BuildFromNativeRect(nativeRect, borderThickness, borderColor);
                hasShownOnce = true;
            }
            else
            {
                selectorBox.BuildFromNativeRect(nativeRect, borderThickness, borderColor);
            }
        }

        public void ShowAtPanelRect(Panel currentPanel, Rect panelRect, Color borderColor)
        {
            if (currentPanel == null)
                return;

            RefreshAttachment(currentPanel);

            if (selectorBox == null)
                selectorBox = new DefaultSelectorBoxOverlay(currentPanel);

            float thickness = selectorBox.GetSuggestedBorderThickness();

            if (!hasShownOnce || !selectorBox.IsBuilt)
            {
                selectorBox.BuildFromPanelRect(panelRect, thickness, borderColor);
                hasShownOnce = true;
            }
            else
            {
                selectorBox.MoveToPanelRect(panelRect);
            }
        }
    }
}