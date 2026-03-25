using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class MessageBoxStatusLabelOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private readonly Panel parentPanel;

        private Panel root;

        private Panel headerRoot;
        private Button headerButton;

        private Panel talkRoot;
        private Button talkButton;

        private Panel infoRoot;
        private Button infoButton;

        private Panel grabRoot;
        private Button grabButton;

        private Panel stealRoot;
        private Button stealButton;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public MessageBoxStatusLabelOverlay(Panel parentPanel)
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

            headerRoot = null;
            headerButton = null;

            talkRoot = null;
            talkButton = null;

            infoRoot = null;
            infoButton = null;

            grabRoot = null;
            grabButton = null;

            stealRoot = null;
            stealButton = null;
        }

        public void Build()
        {
            if (parentPanel == null)
                return;

            Destroy();

            root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 64), parentPanel);
            root.BackgroundColor = Color.clear;
            root.Enabled = true;

            BuildBox(ref headerRoot, ref headerButton, "Interaction Mode");
            BuildBox(ref talkRoot, ref talkButton, "Talk");
            BuildBox(ref infoRoot, ref infoButton, "Info");
            BuildBox(ref grabRoot, ref grabButton, "Grab");
            BuildBox(ref stealRoot, ref stealButton, "Steal");

            SetLayout();
        }

        public void SetLayout()
        {
            if (root == null)
                return;

            root.Position = Vector2.zero;
            root.Size = parentPanel.Size;

            Rect headerRect = NativeToPanelRect(new Rect(124.9f, 148.8f, 70.0f, 6.2f));
            Rect talkRect = NativeToPanelRect(new Rect(95.2f, 161.2f, 14.7f, 6.2f));
            Rect infoRect = NativeToPanelRect(new Rect(133.4f, 161.2f, 14.7f, 6.2f));
            Rect grabRect = NativeToPanelRect(new Rect(171.7f, 161.2f, 14.7f, 6.2f));
            Rect stealRect = NativeToPanelRect(new Rect(209.9f, 161.2f, 14.7f, 6.2f));

            float uiScale = parentPanel.Size.x / NativeWidth;

            // Gentler than the current linear 1.0x curve.
            // Keeps low-res close to what you liked, but pulls high-res down a bit.
            float t = Mathf.InverseLerp(2.0f, 12.0f, uiScale);
            float buttonTextScale = Mathf.Lerp(2.0f, 8.2f, t);
            float headerTextScale = buttonTextScale * 1.18f;

            ApplyBoxRect(headerRoot, headerButton, headerRect, headerTextScale);
            ApplyBoxRect(talkRoot, talkButton, talkRect, buttonTextScale);
            ApplyBoxRect(infoRoot, infoButton, infoRect, buttonTextScale);
            ApplyBoxRect(grabRoot, grabButton, grabRect, buttonTextScale);
            ApplyBoxRect(stealRoot, stealButton, stealRect, buttonTextScale);
        }

        private void BuildBox(ref Panel panel, ref Button button, string text)
        {
            panel = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), root);
            panel.BackgroundColor = Color.black;
            panel.Enabled = true;

            button = DaggerfallUI.AddTextButton(new Rect(0, 0, 64, 16), text, panel);
            button.BackgroundColor = Color.clear;
            button.Outline.Enabled = false;

            // Keep enabled so label still renders.
            button.Enabled = true;

            button.Label.TextColor = Color.white;
            button.Label.ShadowColor = Color.clear;
            button.Label.ShadowPosition = Vector2.zero;
        }

        private void ApplyBoxRect(Panel panel, Button button, Rect rect, float textScale)
        {
            if (panel == null || button == null)
                return;

            panel.Position = new Vector2(rect.x, rect.y);
            panel.Size = new Vector2(rect.width, rect.height);

            button.Position = Vector2.zero;
            button.Size = new Vector2(rect.width, rect.height);

            button.Label.TextScale = textScale;

            // Button labels are horizontally centered already, but ride a bit high
            // once scaled up. Nudge them down in proportion to the text scale.
            float yNudge = Mathf.Lerp(0.75f, 2.5f, Mathf.InverseLerp(2.0f, 8.2f, textScale));
            button.Label.Position = new Vector2(0f, yNudge);
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