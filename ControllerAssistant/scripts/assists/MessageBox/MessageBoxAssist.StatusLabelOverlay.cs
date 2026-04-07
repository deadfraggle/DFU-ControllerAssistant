using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class MessageBoxStatusLabelOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private readonly Panel parentPanel;
        private readonly Texture2D atlas;

        private readonly Action onTalkClick;
        private readonly Action onInfoClick;
        private readonly Action onGrabClick;
        private readonly Action onStealClick;

        private Panel root;

        private Panel headerRoot;

        private Panel talkRoot;
        private Button talkButton;

        private Panel infoRoot;
        private Button infoButton;

        private Panel grabRoot;
        private Button grabButton;

        private Panel stealRoot;
        private Button stealButton;

        private Texture2D[] cachedSlices;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public MessageBoxStatusLabelOverlay(
            Panel parentPanel,
            Texture2D atlas,
            Action onTalkClick,
            Action onInfoClick,
            Action onGrabClick,
            Action onStealClick)
        {
            this.parentPanel = parentPanel;
            this.atlas = atlas;
            this.onTalkClick = onTalkClick;
            this.onInfoClick = onInfoClick;
            this.onGrabClick = onGrabClick;
            this.onStealClick = onStealClick;
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
            if (parentPanel == null || atlas == null)
                return;

            Destroy();

            root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 64), parentPanel);
            root.BackgroundColor = Color.clear;
            root.Enabled = true;

            if (cachedSlices == null || cachedSlices.Length != 5)
                cachedSlices = new Texture2D[5];

            headerRoot = BuildHeaderBox(0);

            talkRoot = BuildClickableBox(1, out talkButton);
            infoRoot = BuildClickableBox(2, out infoButton);
            grabRoot = BuildClickableBox(3, out grabButton);
            stealRoot = BuildClickableBox(4, out stealButton);

            if (talkButton != null)
                talkButton.OnMouseClick += TalkButton_OnMouseClick;
            if (infoButton != null)
                infoButton.OnMouseClick += InfoButton_OnMouseClick;
            if (grabButton != null)
                grabButton.OnMouseClick += GrabButton_OnMouseClick;
            if (stealButton != null)
                stealButton.OnMouseClick += StealButton_OnMouseClick;

            SetLayout();
        }

        public void SetLayout()
        {
            if (root == null)
                return;

            root.Position = Vector2.zero;
            root.Size = parentPanel.Size;

            Rect headerRect = NativeToPanelRect(new Rect(124.9f, 146.9f, 70.0f, 10.0f));
            Rect talkRect = NativeToPanelRect(new Rect(85.1f, 159.3f, 35.0f, 10.0f));
            Rect infoRect = NativeToPanelRect(new Rect(123.3f, 159.3f, 35.0f, 10.0f));
            Rect grabRect = NativeToPanelRect(new Rect(161.6f, 159.3f, 35.0f, 10.0f));
            Rect stealRect = NativeToPanelRect(new Rect(199.8f, 159.3f, 35.0f, 10.0f));

            ApplyPanelRect(headerRoot, headerRect);
            ApplyButtonRect(talkRoot, talkButton, talkRect);
            ApplyButtonRect(infoRoot, infoButton, infoRect);
            ApplyButtonRect(grabRoot, grabButton, grabRect);
            ApplyButtonRect(stealRoot, stealButton, stealRect);
        }

        private Panel BuildHeaderBox(int index)
        {
            Panel panel = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), root);
            panel.BackgroundColor = Color.clear;
            panel.Enabled = true;

            Texture2D sliced = GetButtonSlice(index);
            if (sliced != null)
                panel.BackgroundTexture = sliced;

            return panel;
        }

        private Panel BuildClickableBox(int index, out Button button)
        {
            Panel panel = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), root);
            panel.BackgroundColor = Color.clear;
            panel.Enabled = true;

            Texture2D sliced = GetButtonSlice(index);
            if (sliced != null)
                panel.BackgroundTexture = sliced;

            button = DaggerfallUI.AddButton(new Rect(0, 0, 64, 16), panel);
            button.BackgroundColor = Color.clear;

            return panel;
        }

        private void TalkButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onTalkClick != null)
                onTalkClick();
        }

        private void InfoButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onInfoClick != null)
                onInfoClick();
        }

        private void GrabButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onGrabClick != null)
                onGrabClick();
        }

        private void StealButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onStealClick != null)
                onStealClick();
        }

        private Texture2D GetButtonSlice(int index)
        {
            if (cachedSlices != null &&
                index >= 0 &&
                index < cachedSlices.Length &&
                cachedSlices[index] != null)
            {
                return cachedSlices[index];
            }

            Texture2D slice = null;

            switch (index)
            {
                case 0: slice = SlicePixelsTopLeft(32, 608, 560, 80); break;  // Interaction Mode
                case 1: slice = SlicePixelsTopLeft(32, 704, 280, 80); break;  // Talk
                case 2: slice = SlicePixelsTopLeft(32, 800, 280, 80); break;  // Info
                case 3: slice = SlicePixelsTopLeft(336, 704, 280, 80); break; // Grab
                case 4: slice = SlicePixelsTopLeft(336, 800, 280, 80); break; // Steal
            }

            if (slice != null)
            {
                slice.filterMode = FilterMode.Point;
                slice.wrapMode = TextureWrapMode.Clamp;

                if (cachedSlices != null &&
                    index >= 0 &&
                    index < cachedSlices.Length)
                {
                    cachedSlices[index] = slice;
                }
            }

            return slice;
        }

        private Texture2D SlicePixelsTopLeft(int xLeft, int yTop, int w, int h)
        {
            if (atlas == null)
                return null;

            int yBottom = atlas.height - yTop - h;
            return SlicePixelsBottomLeft(xLeft, yBottom, w, h);
        }

        private Texture2D SlicePixelsBottomLeft(int x, int y, int w, int h)
        {
            if (atlas == null)
                return null;

            if (x < 0 || y < 0 || x + w > atlas.width || y + h > atlas.height)
                return null;

            Texture2D tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            tex.SetPixels(atlas.GetPixels(x, y, w, h));
            tex.Apply(false, true);
            return tex;
        }

        private void ApplyPanelRect(Panel panel, Rect rect)
        {
            if (panel == null)
                return;

            panel.Position = new Vector2(rect.x, rect.y);
            panel.Size = new Vector2(rect.width, rect.height);
        }

        private void ApplyButtonRect(Panel panel, Button button, Rect rect)
        {
            if (panel == null || button == null)
                return;

            panel.Position = new Vector2(rect.x, rect.y);
            panel.Size = new Vector2(rect.width, rect.height);

            button.Position = Vector2.zero;
            button.Size = new Vector2(rect.width, rect.height);
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