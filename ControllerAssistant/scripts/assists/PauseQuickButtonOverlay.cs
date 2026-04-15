using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using System;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class PauseQuickButtonOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private readonly Panel parentPanel;
        private readonly Rect[] nativeRects;
        private readonly Texture2D atlas;

        private readonly Action onLocalMapClick;
        private readonly Action onStatusClick;
        private readonly Action onTransportClick;
        private readonly Action onCharacterClick;
        private readonly Action onTravelMapClick;
        private readonly Action onSpellbookClick;
        private readonly Action onLogbookClick;
        private readonly Action onNotebookClick;
        private readonly Action onRestClick;
        private readonly Action onInventoryClick;
        private readonly Action onQuickSaveClick;
        private readonly Action onQuickLoadClick;
        private readonly Action onUseMagicItemClick;

        private Panel root;
        private Panel[] boxRoots;
        private Button[] boxButtons;
        private Texture2D[] cachedSlices;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public PauseQuickButtonOverlay(
            Panel parentPanel,
            Rect[] nativeRects,
            Texture2D atlas,
            Action onLocalMapClick,
            Action onStatusClick,
            Action onTransportClick,
            Action onCharacterClick,
            Action onTravelMapClick,
            Action onSpellbookClick,
            Action onLogbookClick,
            Action onNotebookClick,
            Action onRestClick,
            Action onInventoryClick,
            Action onQuickSaveClick,
            Action onQuickLoadClick,
            Action onUseMagicItemClick)
        {
            this.parentPanel = parentPanel;
            this.nativeRects = nativeRects;
            this.atlas = atlas;

            this.onLocalMapClick = onLocalMapClick;
            this.onStatusClick = onStatusClick;
            this.onTransportClick = onTransportClick;
            this.onCharacterClick = onCharacterClick;
            this.onTravelMapClick = onTravelMapClick;
            this.onSpellbookClick = onSpellbookClick;
            this.onLogbookClick = onLogbookClick;
            this.onNotebookClick = onNotebookClick;
            this.onRestClick = onRestClick;
            this.onInventoryClick = onInventoryClick;
            this.onQuickSaveClick = onQuickSaveClick;
            this.onQuickLoadClick = onQuickLoadClick;
            this.onUseMagicItemClick = onUseMagicItemClick;
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
                case 0: button.OnMouseClick += LocalMapButton_OnMouseClick; break;
                case 1: button.OnMouseClick += StatusButton_OnMouseClick; break;
                case 2: button.OnMouseClick += TransportButton_OnMouseClick; break;
                case 3: button.OnMouseClick += CharacterButton_OnMouseClick; break;
                case 4: button.OnMouseClick += TravelMapButton_OnMouseClick; break;
                case 5: button.OnMouseClick += SpellbookButton_OnMouseClick; break;
                case 6: button.OnMouseClick += LogbookButton_OnMouseClick; break;
                case 7: button.OnMouseClick += NotebookButton_OnMouseClick; break;
                case 8: button.OnMouseClick += RestButton_OnMouseClick; break;
                case 9: button.OnMouseClick += InventoryButton_OnMouseClick; break;
                case 10: button.OnMouseClick += QuickSaveButton_OnMouseClick; break;
                case 11: button.OnMouseClick += QuickLoadButton_OnMouseClick; break;
                case 12: button.OnMouseClick += UseMagicItemButton_OnMouseClick; break;
            }

            boxRoots[index] = panel;
            boxButtons[index] = button;
        }

        private void LocalMapButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onLocalMapClick != null)
                onLocalMapClick();
        }

        private void StatusButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onStatusClick != null)
                onStatusClick();
        }

        private void TransportButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onTransportClick != null)
                onTransportClick();
        }

        private void CharacterButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onCharacterClick != null)
                onCharacterClick();
        }

        private void TravelMapButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onTravelMapClick != null)
                onTravelMapClick();
        }

        private void SpellbookButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onSpellbookClick != null)
                onSpellbookClick();
        }

        private void LogbookButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onLogbookClick != null)
                onLogbookClick();
        }

        private void NotebookButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onNotebookClick != null)
                onNotebookClick();
        }

        private void RestButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onRestClick != null)
                onRestClick();
        }

        private void InventoryButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onInventoryClick != null)
                onInventoryClick();
        }

        private void QuickSaveButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onQuickSaveClick != null)
                onQuickSaveClick();
        }

        private void QuickLoadButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onQuickLoadClick != null)
                onQuickLoadClick();
        }

        private void UseMagicItemButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (onUseMagicItemClick != null)
                onUseMagicItemClick();
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
                case 0: slice = SlicePixelsTopLeft(32, 32, 280, 80); break;    // Local Map
                case 1: slice = SlicePixelsTopLeft(32, 128, 280, 80); break;   // Status
                case 2: slice = SlicePixelsTopLeft(32, 224, 280, 80); break;   // Transport
                case 3: slice = SlicePixelsTopLeft(32, 320, 280, 80); break;   // Character
                case 4: slice = SlicePixelsTopLeft(32, 416, 280, 80); break;   // Travel Map
                case 5: slice = SlicePixelsTopLeft(32, 512, 280, 80); break;   // Spellbook

                case 6: slice = SlicePixelsTopLeft(336, 32, 280, 80); break;   // Logbook
                case 7: slice = SlicePixelsTopLeft(336, 128, 280, 80); break;  // Notebook
                case 8: slice = SlicePixelsTopLeft(336, 224, 280, 80); break;  // Rest
                case 9: slice = SlicePixelsTopLeft(336, 320, 280, 80); break;  // Inventory
                case 10: slice = SlicePixelsTopLeft(336, 416, 280, 80); break; // QuickSave
                case 11: slice = SlicePixelsTopLeft(336, 512, 280, 80); break; // QuickLoad
                case 12: slice = SlicePixelsTopLeft(32, 1088, 592, 80); break; // Use Magic Item
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