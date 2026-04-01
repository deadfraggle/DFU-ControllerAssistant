using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    /// <summary>
    /// Generic selector box overlay for menu assists.
    /// Draws a simple rectangular outline on a DFU UI panel.
    /// Rects passed in are assumed to be native 320x200 UI coordinates.
    /// </summary>
    public class DefaultSelectorBoxOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private readonly Panel parentPanel;

        private Panel root;
        private Panel borderTop;
        private Panel borderBottom;
        private Panel borderLeft;
        private Panel borderRight;

        private Rect lastPanelRect;
        private float lastBorderThickness = 2f;
        private Color lastBorderColor = new Color(0.1f, 1f, 1f, 1f);

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public DefaultSelectorBoxOverlay(Panel parentPanel)
        {
            this.parentPanel = parentPanel;
        }

        public bool IsAttached()
        {
            return root != null && root.Parent == parentPanel;
        }

        public void Destroy()
        {
            if (root != null && root.Parent != null)
            {
                Panel parent = root.Parent as Panel;
                if (parent != null)
                    parent.Components.Remove(root);
            }

            root = null;
            borderTop = null;
            borderBottom = null;
            borderLeft = null;
            borderRight = null;
        }

        public void BuildFromNativeRect(Rect nativeRect, float borderThickness, Color borderColor)
        {
            if (parentPanel == null)
                return;

            Rect panelRect = NativeToPanelRect(nativeRect);

            lastPanelRect = panelRect;
            lastBorderThickness = borderThickness;
            lastBorderColor = borderColor;

            Rebuild(panelRect, borderThickness, borderColor);
        }
        public void BuildFromPanelRect(Rect panelRect, float borderThickness, Color borderColor)
        {
            if (parentPanel == null)
                return;

            lastPanelRect = panelRect;
            lastBorderThickness = borderThickness;
            lastBorderColor = borderColor;

            Rebuild(panelRect, borderThickness, borderColor);
        }

        public void MoveToNativeRect(Rect nativeRect)
        {
            if (parentPanel == null)
                return;

            Rect panelRect = NativeToPanelRect(nativeRect);
            MoveToPanelRect(panelRect);
        }

        public void MoveToPanelRect(Rect panelRect)
        {
            if (!IsBuilt)
            {
                Rebuild(panelRect, lastBorderThickness, lastBorderColor);
                return;
            }

            lastPanelRect = panelRect;

            float x = panelRect.x;
            float y = panelRect.y;
            float w = panelRect.width;
            float h = panelRect.height;
            float t = Mathf.Max(1f, lastBorderThickness);

            root.Position = new Vector2(x, y);
            root.Size = new Vector2(w, h);

            borderTop.Position = Vector2.zero;
            borderTop.Size = new Vector2(w, t);

            borderBottom.Position = new Vector2(0f, h - t);
            borderBottom.Size = new Vector2(w, t);

            borderLeft.Position = Vector2.zero;
            borderLeft.Size = new Vector2(t, h);

            borderRight.Position = new Vector2(w - t, 0f);
            borderRight.Size = new Vector2(t, h);
        }

        public void RebuildLast()
        {
            if (parentPanel == null)
                return;

            Rebuild(lastPanelRect, lastBorderThickness, lastBorderColor);
        }

        public float GetSuggestedBorderThickness()
        {
            if (parentPanel == null)
                return 2f;

            float scaleX = parentPanel.Size.x / NativeWidth;
            float scaleY = parentPanel.Size.y / NativeHeight;
            float scale = Mathf.Min(scaleX, scaleY);

            return Mathf.Max(2f, scale * 0.5f);
        }

        private void Rebuild(Rect panelRect, float borderThickness, Color borderColor)
        {
            Destroy();

            root = new Panel();
            root.AutoSize = AutoSizeModes.None;
            root.BackgroundColor = Color.clear;
            root.Position = new Vector2(panelRect.x, panelRect.y);
            root.Size = new Vector2(panelRect.width, panelRect.height);

            float w = panelRect.width;
            float h = panelRect.height;
            float t = Mathf.Max(1f, borderThickness);

            borderTop = BuildBorder(new Vector2(0f, 0f), new Vector2(w, t), borderColor);
            borderBottom = BuildBorder(new Vector2(0f, h - t), new Vector2(w, t), borderColor);
            borderLeft = BuildBorder(new Vector2(0f, 0f), new Vector2(t, h), borderColor);
            borderRight = BuildBorder(new Vector2(w - t, 0f), new Vector2(t, h), borderColor);

            root.Components.Add(borderTop);
            root.Components.Add(borderBottom);
            root.Components.Add(borderLeft);
            root.Components.Add(borderRight);

            parentPanel.Components.Add(root);
        }

        private Panel BuildBorder(Vector2 pos, Vector2 size, Color color)
        {
            Panel p = new Panel();
            p.AutoSize = AutoSizeModes.None;
            p.BackgroundColor = color;
            p.Position = pos;
            p.Size = size;
            return p;
        }

        private Rect NativeToPanelRect(Rect nativeRect)
        {
            if (parentPanel == null)
                return nativeRect;

            float parentWidth = parentPanel.Size.x;
            float parentHeight = parentPanel.Size.y;

            float scaleX = parentWidth / NativeWidth;
            float scaleY = parentHeight / NativeHeight;
            float scale = Mathf.Min(scaleX, scaleY);

            float scaledNativeWidth = NativeWidth * scale;
            float scaledNativeHeight = NativeHeight * scale;

            float offsetX = (parentWidth - scaledNativeWidth) * 0.5f;
            float offsetY = (parentHeight - scaledNativeHeight) * 0.5f;

            return new Rect(
                offsetX + nativeRect.x * scale,
                offsetY + nativeRect.y * scale,
                nativeRect.width * scale,
                nativeRect.height * scale
            );
        }
    }
}
