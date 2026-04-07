using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class TransportQuickButtonOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private readonly Panel parentPanel;
        private readonly Rect[] nativeRects;
        private readonly Texture2D atlas;

        private readonly Action onAddFavoriteClick;
        private readonly Action onViewFavoritesClick;

        private Panel root;
        private Panel[] boxRoots;
        private Button[] boxButtons;
        private Texture2D[] cachedSlices;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public TransportQuickButtonOverlay(
            Panel parentPanel,
            Rect[] nativeRects,
            Texture2D atlas,
            Action onAddFavoriteClick,
            Action onViewFavoritesClick)
        {
            this.parentPanel = parentPanel;
            this.nativeRects = nativeRects;
            this.atlas = atlas;
            this.onAddFavoriteClick = onAddFavoriteClick;
            this.onViewFavoritesClick = onViewFavoritesClick;
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
            if (parentPanel == null || nativeRects == null || atlas == null)
                return;

            Destroy();

            root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 64), parentPanel);
            root.BackgroundColor = Color.clear;
            root.Enabled = true;

            boxRoots = new Panel[nativeRects.Length];
            boxButtons = new Button[nativeRects.Length];

            if (cachedSlices == null || cachedSlices.Length != nativeRects.Length)
                cachedSlices = new Texture2D[nativeRects.Length];

            for (int i = 0; i < nativeRects.Length; i++)
                BuildBox(i);

            SetLayout();
        }

        public void SetLayout()
        {
            if (root == null)
                return;

            root.Position = Vector2.zero;
            root.Size = parentPanel.Size;

            for (int i = 0; i < nativeRects.Length; i++)
            {
                Rect rect = NativeToPanelRect(nativeRects[i]);
                ApplyBoxRect(boxRoots[i], boxButtons[i], rect);
            }
        }

        private void BuildBox(int index)
        {
            Panel panel = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), root);
            panel.BackgroundColor = Color.clear;
            panel.Enabled = true;

            Texture2D sliced = GetButtonSlice(index);
            if (sliced != null)
                panel.BackgroundTexture = sliced;

            Button button = DaggerfallUI.AddButton(new Rect(0, 0, 64, 16), panel);
            button.BackgroundColor = Color.clear;

            switch (index)
            {
                case 0: button.OnMouseClick += AddFavoriteButton_OnMouseClick; break;
                case 1: button.OnMouseClick += ViewFavoritesButton_OnMouseClick; break;
            }

            boxRoots[index] = panel;
            boxButtons[index] = button;
        }

        private void AddFavoriteButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onAddFavoriteClick != null)
                onAddFavoriteClick();
        }

        private void ViewFavoritesButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onViewFavoritesClick != null)
                onViewFavoritesClick();
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
                case 0: slice = SlicePixelsTopLeft(32, 896, 560, 80); break; // Add Location to Favorites
                case 1: slice = SlicePixelsTopLeft(32, 992, 560, 80); break; // View Favorite Locations
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

        private void ApplyBoxRect(Panel panel, Button button, Rect rect)
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