using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class PauseQuickButtonOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private readonly Panel parentPanel;
        private readonly Rect[] nativeRects;
        private readonly string[] texts;

        private Panel root;
        private Panel[] boxRoots;
        private Button[] boxButtons;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public PauseQuickButtonOverlay(Panel parentPanel, Rect[] nativeRects, string[] texts)
        {
            this.parentPanel = parentPanel;
            this.nativeRects = nativeRects;
            this.texts = texts;
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
            boxRoots = null;
            boxButtons = null;
        }

        public void Build()
        {
            if (parentPanel == null || nativeRects == null || texts == null)
                return;

            Destroy();

            root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 64), parentPanel);
            root.BackgroundColor = Color.clear;
            root.Enabled = true;

            boxRoots = new Panel[texts.Length];
            boxButtons = new Button[texts.Length];

            for (int i = 0; i < texts.Length; i++)
                BuildBox(i, texts[i]);

            SetLayout();
        }

        public void SetLayout()
        {
            if (root == null)
                return;

            root.Position = Vector2.zero;
            root.Size = parentPanel.Size;

            float uiScale = parentPanel.Size.x / NativeWidth;
            float t = Mathf.InverseLerp(2.0f, 12.0f, uiScale);
            float buttonTextScale = Mathf.Lerp(1.85f, 7.25f, t);

            for (int i = 0; i < nativeRects.Length; i++)
            {
                Rect rect = NativeToPanelRect(nativeRects[i]);
                ApplyBoxRect(boxRoots[i], boxButtons[i], rect, buttonTextScale);
            }
        }

        private void BuildBox(int index, string text)
        {
            Panel panel = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), root);
            panel.BackgroundColor = Color.black;
            panel.Enabled = true;

            Button button = DaggerfallUI.AddButton(new Rect(0, 0, 64, 16), panel);
            button.BackgroundColor = Color.clear;

            TextLabel label = DaggerfallUI.AddTextLabel(DaggerfallUI.DefaultFont, Vector2.zero, text, panel);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextColor = Color.white;
            label.ShadowColor = Color.clear;

            button.Tag = label;

            boxRoots[index] = panel;
            boxButtons[index] = button;
        }

        private void ApplyBoxRect(Panel panel, Button button, Rect rect, float textScale)
        {
            if (panel == null || button == null)
                return;

            panel.Position = new Vector2(rect.x, rect.y);
            panel.Size = new Vector2(rect.width, rect.height);

            button.Position = Vector2.zero;
            button.Size = new Vector2(rect.width, rect.height);

            TextLabel label = button.Tag as TextLabel;
            if (label == null)
                return;

            label.TextScale = textScale;

            float scaledFontHeight = 7f * textScale;
            float yOffset = (rect.height - scaledFontHeight) / 2f;
            label.Position = new Vector2(0, yOffset + (0.55f * textScale));
            label.HorizontalAlignment = HorizontalAlignment.Center;
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
