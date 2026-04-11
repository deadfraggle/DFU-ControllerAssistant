using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using UnityEngine;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public enum OnScreenKeyboardKeyAction
    {
        InsertText,
        ReplaceText,
        Shift,
        Toggle123,
        Space,
        Backspace,
        Ok,
        NextQuest,
        PrevQuest,
        NextFavorite,
        PrevFavorite,
    }

    public enum OnScreenKeyboardForm
    {
        Lower,
        Upper,
        Symbols,
    }

    public enum OnScreenKeyboardVisualVariant
    {
        Default,
        Find,
    }

    public struct OnScreenKeyboardActivation
    {
        public OnScreenKeyboardKeyAction Action;
        public string Text;
    }

    public class OnScreenKeyboardOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private class KeyInfo
        {
            public string Label;
            public Rect NativeRect;
            public int Row;
            public float CenterX;
            public OnScreenKeyboardKeyAction Action;
            public string Text;
        }
        private class CustomKeyDefinition
        {
            public string Label;
            public Rect NativeRect;
            public int Row;
            public OnScreenKeyboardKeyAction Action;
            public string Text;
        }

        private readonly List<CustomKeyDefinition> customKeys = new List<CustomKeyDefinition>();

        private readonly Panel parentPanel;

        private Panel root;
        private DefaultSelectorBoxHost selectorHost;

        private Vector2 anchorNative;

        private const float KeyWidth = 12.0f;
        private const float KeyHeight = 9.0f;

        private float keySpacingX;
        private float keySpacingY;

        private const float ShiftWidth = 28.0f;
        private const float NumWidth = 22.0f;
        private const float SpaceWidth = 42.0f;
        private const float BackWidth = 24.0f;
        private const float OkWidth = 18.0f;

        private readonly List<Panel> keyRoots = new List<Panel>();
        private readonly List<Button> keyButtons = new List<Button>();
        private readonly List<KeyInfo> keys = new List<KeyInfo>();

        private int selectedKeyIndex = 0;
        private OnScreenKeyboardForm currentForm = OnScreenKeyboardForm.Lower;

        private static readonly string[] lowerRow1 = { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
        private static readonly string[] lowerRow2 = { "k", "l", "m", "n", "o", "p", "q", "r", "s", "t" };
        private static readonly string[] lowerRow3 = { "u", "v", "w", "x", "y", "z", "'", "-", ".", "," };

        private static readonly string[] upperRow1 = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
        private static readonly string[] upperRow2 = { "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T" };
        private static readonly string[] upperRow3 = { "U", "V", "W", "X", "Y", "Z", "'", "-", ".", "," };

        private static readonly string[] symbolRow1 = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
        private static readonly string[] symbolRow2 = { "!", "?", "@", "#", "$", "%", "&", "*", "(", ")" };
        private static readonly string[] symbolRow3 = { "+", "=", "/", "\\", ":", ";", "\"", "_", "[", "]" };

        private Panel formTexturePanel;
        private Texture2D keyboardAtlas;

        private Texture2D defaultLowerFormTexture;
        private Texture2D defaultUpperFormTexture;
        private Texture2D defaultSymbolsFormTexture;

        private Texture2D findLowerFormTexture;
        private Texture2D findUpperFormTexture;
        private Texture2D findSymbolsFormTexture;

        // Atlas slices read from keyboardatlas.png
        // These are cropped from the uploaded atlas image and can be tweaked later if desired.
        private static readonly Rect lowerFormAtlasRect = new Rect(32, 32, 1160, 336);
        private static readonly Rect upperFormAtlasRect = new Rect(32, 672, 1160, 336);
        private static readonly Rect symbolsFormAtlasRect = new Rect(1200, 32, 1160, 336);

        private static readonly Rect lowerFindAtlasRect = new Rect(32, 32, 1160, 600);
        private static readonly Rect upperFindAtlasRect = new Rect(32, 672, 1160, 600);
        private static readonly Rect symbolsFindAtlasRect = new Rect(1200, 32, 1160, 600);

        private const float AtlasScale = 8f;

        private OnScreenKeyboardVisualVariant visualVariant = OnScreenKeyboardVisualVariant.Default;

        private System.Action<OnScreenKeyboardActivation> onKeyClicked;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public Vector2 AnchorNative
        {
            get { return anchorNative; }
            set
            {
                anchorNative = value;
                RebuildIfBuilt();
            }
        }

        public float KeySpacingX
        {
            get { return keySpacingX; }
            set
            {
                keySpacingX = value;
                RebuildIfBuilt();
            }
        }

        public float KeySpacingY
        {
            get { return keySpacingY; }
            set
            {
                keySpacingY = value;
                RebuildIfBuilt();
            }
        }

        public OnScreenKeyboardForm CurrentForm
        {
            get { return currentForm; }
        }

        public void SetLayout(Vector2 anchorNative, float keySpacingX, float keySpacingY)
        {
            this.anchorNative = anchorNative;
            this.keySpacingX = keySpacingX;
            this.keySpacingY = keySpacingY;

            RebuildIfBuilt();
        }

        public OnScreenKeyboardOverlay(Panel parentPanel)
        {
            this.parentPanel = parentPanel;
            this.anchorNative = new Vector2(90f, 145f);
            this.keySpacingX = 1.8f;
            this.keySpacingY = 2.0f;
        }

        public OnScreenKeyboardOverlay(
            Panel parentPanel,
            Vector2 anchorNative,
            float keySpacingX,
            float keySpacingY)
        {
            this.parentPanel = parentPanel;
            this.anchorNative = anchorNative;
            this.keySpacingX = keySpacingX;
            this.keySpacingY = keySpacingY;
        }

        public bool IsAttached()
        {
            return root != null && root.Parent == parentPanel;
        }

        public void Destroy()
        {
            if (selectorHost != null)
                selectorHost.Destroy();

            if (root != null && root.Parent != null)
            {
                Panel parent = root.Parent as Panel;
                if (parent != null)
                    parent.Components.Remove(root);
            }

            root = null;
            formTexturePanel = null;
            keyRoots.Clear();
            keyButtons.Clear();
            keys.Clear();
        }

        public void Build()
        {
            if (parentPanel == null)
                return;

            int previousSelected = selectedKeyIndex;

            Destroy();

            root = DaggerfallUI.AddPanel(new Rect(0, 0, parentPanel.Size.x, parentPanel.Size.y), parentPanel);
            root.BackgroundColor = Color.clear;
            root.Enabled = true;

            // Build the textured keyboard form first so buttons sit above it.
            BuildFormTextureOverlay();

            BuildRowsForCurrentForm();

            // If texture exists, hide fallback visuals but keep buttons alive.
            ApplyFallbackVisualMode(formTexturePanel == null);

            SetLayout();

            if (keys.Count == 0)
                selectedKeyIndex = 0;
            else if (previousSelected >= 0 && previousSelected < keys.Count)
                selectedKeyIndex = previousSelected;
            else
                selectedKeyIndex = 0;

            RefreshSelector();
        }

        public void SetLayout()
        {
            if (root == null)
                return;

            root.Position = Vector2.zero;
            root.Size = parentPanel.Size;

            if (formTexturePanel != null)
            {
                Rect formRect = NativeToPanelRect(GetCurrentTextureNativeRect());
                formTexturePanel.Position = new Vector2(formRect.x, formRect.y);
                formTexturePanel.Size = new Vector2(formRect.width, formRect.height);
            }
        }

        public void RefreshAttachment()
        {
            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.RefreshAttachment(parentPanel);
        }

        public void MoveLeft()
        {
            if (keys.Count == 0)
                return;

            int target = FindHorizontalNeighbor(-1);
            if (target != selectedKeyIndex)
            {
                selectedKeyIndex = target;
                RefreshSelector();
            }
        }

        public void MoveRight()
        {
            if (keys.Count == 0)
                return;

            int target = FindHorizontalNeighbor(1);
            if (target != selectedKeyIndex)
            {
                selectedKeyIndex = target;
                RefreshSelector();
            }
        }

        public void MoveUp()
        {
            if (keys.Count == 0)
                return;

            int target = FindVerticalNeighbor(-1);
            if (target != selectedKeyIndex)
            {
                selectedKeyIndex = target;
                RefreshSelector();
            }
        }

        public void MoveDown()
        {
            if (keys.Count == 0)
                return;

            int target = FindVerticalNeighbor(1);
            if (target != selectedKeyIndex)
            {
                selectedKeyIndex = target;
                RefreshSelector();
            }
        }

        public OnScreenKeyboardActivation ActivateSelectedKey()
        {
            OnScreenKeyboardActivation result = new OnScreenKeyboardActivation();

            if (selectedKeyIndex < 0 || selectedKeyIndex >= keys.Count)
                return result;

            KeyInfo key = keys[selectedKeyIndex];
            result.Action = key.Action;
            result.Text = key.Text;
            return result;
        }

        public void ToggleShift()
        {
            if (currentForm == OnScreenKeyboardForm.Symbols)
                return;

            currentForm = (currentForm == OnScreenKeyboardForm.Lower)
                ? OnScreenKeyboardForm.Upper
                : OnScreenKeyboardForm.Lower;

            Build();
        }

        public void Toggle123()
        {
            if (currentForm == OnScreenKeyboardForm.Symbols)
                currentForm = OnScreenKeyboardForm.Lower;
            else
                currentForm = OnScreenKeyboardForm.Symbols;

            Build();
        }

        private void RebuildIfBuilt()
        {
            if (IsBuilt)
                Build();
        }

        private void RefreshSelector()
        {
            if (selectedKeyIndex < 0 || selectedKeyIndex >= keys.Count)
                return;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                parentPanel,
                keys[selectedKeyIndex].NativeRect,
                new Color(0.1f, 1f, 1f, 1f));
        }

        private int FindHorizontalNeighbor(int direction)
        {
            KeyInfo current = keys[selectedKeyIndex];
            int best = selectedKeyIndex;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < keys.Count; i++)
            {
                if (i == selectedKeyIndex)
                    continue;

                KeyInfo candidate = keys[i];
                if (candidate.Row != current.Row)
                    continue;

                float delta = candidate.CenterX - current.CenterX;

                if (direction < 0 && delta >= 0)
                    continue;

                if (direction > 0 && delta <= 0)
                    continue;

                float distance = Mathf.Abs(delta);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private int FindVerticalNeighbor(int direction)
        {
            KeyInfo current = keys[selectedKeyIndex];
            int targetRow = current.Row + direction;

            int best = selectedKeyIndex;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < keys.Count; i++)
            {
                if (i == selectedKeyIndex)
                    continue;

                KeyInfo candidate = keys[i];
                if (candidate.Row != targetRow)
                    continue;

                float distance = Mathf.Abs(candidate.CenterX - current.CenterX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private void BuildRowsForCurrentForm()
        {
            string[] row1;
            string[] row2;
            string[] row3;

            if (currentForm == OnScreenKeyboardForm.Upper)
            {
                row1 = upperRow1;
                row2 = upperRow2;
                row3 = upperRow3;
            }
            else if (currentForm == OnScreenKeyboardForm.Symbols)
            {
                row1 = symbolRow1;
                row2 = symbolRow2;
                row3 = symbolRow3;
            }
            else
            {
                row1 = lowerRow1;
                row2 = lowerRow2;
                row3 = lowerRow3;
            }

            BuildAlphaRow(row1, 0);
            BuildAlphaRow(row2, 1);
            BuildAlphaRow(row3, 2);

            float y = anchorNative.y + (KeyHeight + keySpacingY) * 3f;
            float x = anchorNative.x;

            string shiftLabel = (currentForm == OnScreenKeyboardForm.Symbols) ? "[Shift]" : "[Shift]";
            string formLabel = (currentForm == OnScreenKeyboardForm.Symbols) ? "[ABC]" : "[123]";

            BuildKey(shiftLabel, new Rect(x, y, ShiftWidth, KeyHeight), 3, OnScreenKeyboardKeyAction.Shift, null);
            x += ShiftWidth + keySpacingX;

            BuildKey(formLabel, new Rect(x, y, NumWidth, KeyHeight), 3, OnScreenKeyboardKeyAction.Toggle123, null);
            x += NumWidth + keySpacingX;

            BuildKey("[Space]", new Rect(x, y, SpaceWidth, KeyHeight), 3, OnScreenKeyboardKeyAction.Space, " ");
            x += SpaceWidth + keySpacingX;

            BuildKey("[Back]", new Rect(x, y, BackWidth, KeyHeight), 3, OnScreenKeyboardKeyAction.Backspace, null);
            x += BackWidth + keySpacingX;

            BuildKey("[OK]", new Rect(x, y, OkWidth, KeyHeight), 3, OnScreenKeyboardKeyAction.Ok, null);

            for (int i = 0; i < customKeys.Count; i++)
            {
                CustomKeyDefinition key = customKeys[i];
                BuildKey(key.Label, key.NativeRect, key.Row, key.Action, key.Text);
            }
        }

        private void BuildFormTextureOverlay()
        {
            Texture2D formTexture = GetCurrentFormTexture();
            if (formTexture == null || root == null)
                return;

            Rect formRect = NativeToPanelRect(GetCurrentTextureNativeRect());

            formTexturePanel = new Panel();
            formTexturePanel.Position = new Vector2(formRect.x, formRect.y);
            formTexturePanel.Size = new Vector2(formRect.width, formRect.height);
            formTexturePanel.BackgroundTexture = formTexture;
            formTexturePanel.BackgroundColor = Color.clear;

            root.Components.Add(formTexturePanel);
        }

        private Rect GetCurrentTextureNativeRect()
        {
            float width = 1160f / AtlasScale;   // 145

            float height;
            if (visualVariant == OnScreenKeyboardVisualVariant.Find)
                height = 600f / AtlasScale;     // 75
            else
                height = 336f / AtlasScale;     // 42

            return new Rect(anchorNative.x, anchorNative.y, width, height);
        }

        private Texture2D GetCurrentFormTexture()
        {
            if (visualVariant == OnScreenKeyboardVisualVariant.Find)
            {
                switch (currentForm)
                {
                    case OnScreenKeyboardForm.Upper:
                        if (findUpperFormTexture == null)
                            findUpperFormTexture = SliceAtlasRect(upperFindAtlasRect);
                        return findUpperFormTexture;

                    case OnScreenKeyboardForm.Symbols:
                        if (findSymbolsFormTexture == null)
                            findSymbolsFormTexture = SliceAtlasRect(symbolsFindAtlasRect);
                        return findSymbolsFormTexture;

                    case OnScreenKeyboardForm.Lower:
                    default:
                        if (findLowerFormTexture == null)
                            findLowerFormTexture = SliceAtlasRect(lowerFindAtlasRect);
                        return findLowerFormTexture;
                }
            }
            else
            {
                switch (currentForm)
                {
                    case OnScreenKeyboardForm.Upper:
                        if (defaultUpperFormTexture == null)
                            defaultUpperFormTexture = SliceAtlasRect(upperFormAtlasRect);
                        return defaultUpperFormTexture;

                    case OnScreenKeyboardForm.Symbols:
                        if (defaultSymbolsFormTexture == null)
                            defaultSymbolsFormTexture = SliceAtlasRect(symbolsFormAtlasRect);
                        return defaultSymbolsFormTexture;

                    case OnScreenKeyboardForm.Lower:
                    default:
                        if (defaultLowerFormTexture == null)
                            defaultLowerFormTexture = SliceAtlasRect(lowerFormAtlasRect);
                        return defaultLowerFormTexture;
                }
            }
        }

        private Texture2D LoadKeyboardAtlas()
        {
            if (keyboardAtlas != null)
                return keyboardAtlas;

            Mod mod = ModManager.Instance.GetMod("ControllerAssistant");
            if (mod == null)
                return null;

            Texture2D tex = mod.GetAsset<Texture2D>("keyboardatlas");
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Point;
            }

            keyboardAtlas = tex;
            return keyboardAtlas;
        }

        private Texture2D SliceAtlasRect(Rect atlasRect)
        {
            Texture2D atlas = LoadKeyboardAtlas();
            if (atlas == null)
                return null;

            int x = Mathf.RoundToInt(atlasRect.x);
            int yTop = Mathf.RoundToInt(atlasRect.y);
            int w = Mathf.RoundToInt(atlasRect.width);
            int h = Mathf.RoundToInt(atlasRect.height);

            int yBottom = atlas.height - yTop - h;

            if (x < 0 || yBottom < 0 || x + w > atlas.width || yBottom + h > atlas.height)
                return null;

            Texture2D tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            tex.SetPixels(atlas.GetPixels(x, yBottom, w, h));
            tex.Apply(false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private void BuildAlphaRow(string[] labels, int rowIndex)
        {
            float y = anchorNative.y + (KeyHeight + keySpacingY) * rowIndex;
            float x = anchorNative.x;

            for (int i = 0; i < labels.Length; i++)
            {
                BuildKey(
                    labels[i],
                    new Rect(x, y, KeyWidth, KeyHeight),
                    rowIndex,
                    OnScreenKeyboardKeyAction.InsertText,
                    labels[i]);

                x += KeyWidth + keySpacingX;
            }
        }

        private void BuildKey(string text, Rect nativeRect, int row, OnScreenKeyboardKeyAction action, string value)
        {
            Rect rect = NativeToPanelRect(nativeRect);

            Panel keyRoot = DaggerfallUI.AddPanel(rect, root);
            keyRoot.BackgroundColor = Color.black;
            keyRoot.Enabled = true;

            Button keyButton = DaggerfallUI.AddButton(new Rect(0, 0, rect.width, rect.height), keyRoot);
            keyButton.BackgroundColor = Color.clear;

            TextLabel label = DaggerfallUI.AddTextLabel(DaggerfallUI.DefaultFont, Vector2.zero, text, keyRoot);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextColor = Color.white;
            label.ShadowColor = Color.clear;

            float uiScale = parentPanel.Size.x / NativeWidth;
            float t = Mathf.InverseLerp(2.0f, 12.0f, uiScale);
            float textScale = Mathf.Lerp(2.0f, 6.6f, t);

            label.TextScale = textScale;

            float scaledFontHeight = 7f * textScale;
            float yOffset = (rect.height - scaledFontHeight) / 2f;
            label.Position = new Vector2(0, yOffset + (0.6f * textScale));

            keyButton.Tag = label;

            keyRoots.Add(keyRoot);
            keyButtons.Add(keyButton);

            KeyInfo key = new KeyInfo();
            key.Label = text;
            key.NativeRect = nativeRect;
            key.Row = row;
            key.CenterX = nativeRect.x + nativeRect.width * 0.5f;
            key.Action = action;
            key.Text = value;
            keys.Add(key);

            int keyIndex = keys.Count - 1;
            keyButton.OnMouseClick += delegate (BaseScreenComponent sender, Vector2 position)
            {
                OnKeyMouseClick(keyIndex);
            };
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

        public void ClearCustomKeys()
        {
            customKeys.Clear();
        }

        public void AddCustomKey(string text, Rect nativeRect, int row, OnScreenKeyboardKeyAction action, string value)
        {
            CustomKeyDefinition key = new CustomKeyDefinition();
            key.Label = text;
            key.NativeRect = nativeRect;
            key.Row = row;
            key.Action = action;
            key.Text = value;
            customKeys.Add(key);
        }
        private void ApplyFallbackVisualMode(bool showFallback)
        {
            for (int i = 0; i < keyRoots.Count && i < keys.Count; i++)
            {
                Panel keyRoot = keyRoots[i];
                Button keyButton = keyButtons[i];
                KeyInfo keyInfo = keys[i];

                bool shouldShowThisFallback = showFallback || ShouldShowFallbackForRow(keyInfo.Row);

                if (keyRoot != null)
                    keyRoot.BackgroundColor = shouldShowThisFallback ? Color.black : Color.clear;

                if (keyButton != null && keyButton.Tag is TextLabel)
                {
                    TextLabel label = keyButton.Tag as TextLabel;
                    if (label != null)
                    {
                        label.Text = shouldShowThisFallback ? keyInfo.Label : string.Empty;
                        label.TextColor = Color.white;
                        label.ShadowColor = Color.clear;
                    }
                }
            }
        }

        private bool ShouldShowFallbackForRow(int row)
        {
            if (visualVariant == OnScreenKeyboardVisualVariant.Find)
            {
                // Find texture covers rows 0-4 and 6.
                // Dynamic entry rows 5 and 7 should remain visible.
                return row == 5 || row == 7;
            }

            // Default texture only covers rows 0-3.
            // Anything else should remain visible.
            return row >= 4;
        }
        public void SetVisualVariant(OnScreenKeyboardVisualVariant variant)
        {
            if (visualVariant == variant)
                return;

            visualVariant = variant;
            RebuildIfBuilt();
        }
        public void SetOnKeyClicked(System.Action<OnScreenKeyboardActivation> callback)
        {
            onKeyClicked = callback;
        }

        private void OnKeyMouseClick(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= keys.Count)
                return;

            selectedKeyIndex = keyIndex;
            RefreshSelector();

            if (onKeyClicked != null)
            {
                KeyInfo key = keys[keyIndex];

                OnScreenKeyboardActivation activation = new OnScreenKeyboardActivation();
                activation.Action = key.Action;
                activation.Text = key.Text;

                onKeyClicked(activation);
            }
        }
    }
}