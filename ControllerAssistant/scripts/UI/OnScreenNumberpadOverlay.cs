using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using UnityEngine;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public enum OnScreenNumberpadKeyAction
    {
        InsertText,
        Backspace,
        Ok,
        InsertMax,
    }

    public struct OnScreenNumberpadActivation
    {
        public OnScreenNumberpadKeyAction Action;
        public string Text;
    }

    public class OnScreenNumberpadOverlay
    {
        private const float NativeWidth = 320f;
        private const float NativeHeight = 200f;

        private class KeyInfo
        {
            public string Label;
            public Rect NativeRect;
            public int Row;
            public float CenterX;
            public OnScreenNumberpadKeyAction Action;
            public string Text;
        }

        private readonly Panel parentPanel;

        private Panel root;
        private DefaultSelectorBoxHost selectorHost;
        private Panel formTexturePanel;

        private readonly List<Panel> keyRoots = new List<Panel>();
        private readonly List<Button> keyButtons = new List<Button>();
        private readonly List<KeyInfo> keys = new List<KeyInfo>();

        private Vector2 anchorNative;
        private float keySpacingX;
        private float keySpacingY;

        private const float KeyWidth = 16.0f;
        private const float KeyHeight = 10.0f;
        private const float OkWidth = 26.0f;
        private const float MaxWidth = 24.0f;
        private const float BackWidth = 24.0f;

        private const float AtlasScale = 8f;
        private static readonly Rect numberpadAtlasRect = new Rect(1200, 672, 432, 464);

        private Texture2D keyboardAtlas;
        private Texture2D numberpadTexture;

        private string maxValueText = "99";
        private string defaultSelectedLabel = "1";
        private int selectedKeyIndex = 0;

        private System.Action<OnScreenNumberpadActivation> onKeyClicked;

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public void SetLayout(Vector2 anchorNative, float keySpacingX, float keySpacingY)
        {
            this.anchorNative = anchorNative;
            this.keySpacingX = keySpacingX;
            this.keySpacingY = keySpacingY;

            RebuildIfBuilt();
        }

        public void SetDefaultSelectedLabel(string label)
        {
            defaultSelectedLabel = label;
            RebuildIfBuilt();
        }

        public void SetMaxValue(int value)
        {
            if (value < 0)
                value = 0;

            maxValueText = value.ToString();
            RebuildIfBuilt();
        }

        public void SetOnKeyClicked(System.Action<OnScreenNumberpadActivation> callback)
        {
            onKeyClicked = callback;
        }

        public OnScreenNumberpadOverlay(Panel parentPanel)
        {
            this.parentPanel = parentPanel;
            this.anchorNative = new Vector2(126f, 145f);
            this.keySpacingX = 3.0f;
            this.keySpacingY = 2.0f;
        }

        public OnScreenNumberpadOverlay(
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

            string previouslySelected = null;
            if (selectedKeyIndex >= 0 && selectedKeyIndex < keys.Count)
                previouslySelected = keys[selectedKeyIndex].Label;

            Destroy();

            root = DaggerfallUI.AddPanel(new Rect(0, 0, parentPanel.Size.x, parentPanel.Size.y), parentPanel);
            root.BackgroundColor = Color.clear;
            root.Enabled = true;

            BuildFormTextureOverlay();
            BuildKeys();
            ApplyFallbackVisualMode(formTexturePanel == null);
            SetRootLayout();

            if (!TrySelectByLabel(previouslySelected))
                TrySelectByLabel(defaultSelectedLabel);

            RefreshSelector();
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

        public OnScreenNumberpadActivation ActivateSelectedKey()
        {
            OnScreenNumberpadActivation result = new OnScreenNumberpadActivation();

            if (selectedKeyIndex < 0 || selectedKeyIndex >= keys.Count)
                return result;

            KeyInfo key = keys[selectedKeyIndex];
            result.Action = key.Action;
            result.Text = key.Text;
            return result;
        }

        private void RebuildIfBuilt()
        {
            if (IsBuilt)
                Build();
        }

        private void SetRootLayout()
        {
            if (root == null)
                return;

            root.Position = Vector2.zero;
            root.Size = parentPanel.Size;

            if (formTexturePanel != null)
            {
                Rect formRect = NativeToPanelRect(GetTextureNativeRect());
                formTexturePanel.Position = new Vector2(formRect.x, formRect.y);
                formTexturePanel.Size = new Vector2(formRect.width, formRect.height);
            }
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

        private bool TrySelectByLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return false;

            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].Label == label)
                {
                    selectedKeyIndex = i;
                    return true;
                }
            }

            if (keys.Count > 0)
            {
                selectedKeyIndex = 0;
                return false;
            }

            selectedKeyIndex = -1;
            return false;
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

        private void BuildFormTextureOverlay()
        {
            Texture2D formTexture = GetNumberpadTexture();
            if (formTexture == null || root == null)
                return;

            Rect formRect = NativeToPanelRect(GetTextureNativeRect());

            formTexturePanel = new Panel();
            formTexturePanel.Position = new Vector2(formRect.x, formRect.y);
            formTexturePanel.Size = new Vector2(formRect.width, formRect.height);
            formTexturePanel.BackgroundTexture = formTexture;
            formTexturePanel.BackgroundColor = Color.clear;

            root.Components.Add(formTexturePanel);
        }

        private Rect GetTextureNativeRect()
        {
            float width = 432f / AtlasScale;   // 54
            float height = 464f / AtlasScale;  // 58
            return new Rect(anchorNative.x, anchorNative.y, width, height);
        }

        private void BuildKeys()
        {
            float x = anchorNative.x;
            float y = anchorNative.y;

            BuildKey("7", new Rect(x, y, KeyWidth, KeyHeight), 0, OnScreenNumberpadKeyAction.InsertText, "7");
            x += KeyWidth + keySpacingX;
            BuildKey("8", new Rect(x, y, KeyWidth, KeyHeight), 0, OnScreenNumberpadKeyAction.InsertText, "8");
            x += KeyWidth + keySpacingX;
            BuildKey("9", new Rect(x, y, KeyWidth, KeyHeight), 0, OnScreenNumberpadKeyAction.InsertText, "9");

            x = anchorNative.x;
            y += KeyHeight + keySpacingY;
            BuildKey("4", new Rect(x, y, KeyWidth, KeyHeight), 1, OnScreenNumberpadKeyAction.InsertText, "4");
            x += KeyWidth + keySpacingX;
            BuildKey("5", new Rect(x, y, KeyWidth, KeyHeight), 1, OnScreenNumberpadKeyAction.InsertText, "5");
            x += KeyWidth + keySpacingX;
            BuildKey("6", new Rect(x, y, KeyWidth, KeyHeight), 1, OnScreenNumberpadKeyAction.InsertText, "6");

            x = anchorNative.x;
            y += KeyHeight + keySpacingY;
            BuildKey("1", new Rect(x, y, KeyWidth, KeyHeight), 2, OnScreenNumberpadKeyAction.InsertText, "1");
            x += KeyWidth + keySpacingX;
            BuildKey("2", new Rect(x, y, KeyWidth, KeyHeight), 2, OnScreenNumberpadKeyAction.InsertText, "2");
            x += KeyWidth + keySpacingX;
            BuildKey("3", new Rect(x, y, KeyWidth, KeyHeight), 2, OnScreenNumberpadKeyAction.InsertText, "3");

            x = anchorNative.x;
            y += KeyHeight + keySpacingY;
            BuildKey("0", new Rect(x, y, KeyWidth, KeyHeight), 3, OnScreenNumberpadKeyAction.InsertText, "0");
            x += KeyWidth + keySpacingX;
            BuildKey("[OK]", new Rect(x, y, OkWidth, KeyHeight), 3, OnScreenNumberpadKeyAction.Ok, null);

            x = anchorNative.x;
            y += KeyHeight + keySpacingY;
            BuildKey("[Max]", new Rect(x, y, MaxWidth, KeyHeight), 4, OnScreenNumberpadKeyAction.InsertMax, maxValueText);
            x += MaxWidth + keySpacingX;
            BuildKey("[Back]", new Rect(x, y, BackWidth, KeyHeight), 4, OnScreenNumberpadKeyAction.Backspace, null);
        }

        private void BuildKey(string text, Rect nativeRect, int row, OnScreenNumberpadKeyAction action, string value)
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

        private void ApplyFallbackVisualMode(bool showFallback)
        {
            for (int i = 0; i < keyRoots.Count && i < keys.Count; i++)
            {
                Panel keyRoot = keyRoots[i];
                Button keyButton = keyButtons[i];
                KeyInfo keyInfo = keys[i];

                if (keyRoot != null)
                    keyRoot.BackgroundColor = showFallback ? Color.black : Color.clear;

                if (keyButton != null && keyButton.Tag is TextLabel)
                {
                    TextLabel label = keyButton.Tag as TextLabel;
                    if (label != null)
                    {
                        label.Text = showFallback ? keyInfo.Label : string.Empty;
                        label.TextColor = Color.white;
                        label.ShadowColor = Color.clear;
                    }
                }
            }
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

                OnScreenNumberpadActivation activation = new OnScreenNumberpadActivation();
                activation.Action = key.Action;
                activation.Text = key.Text;

                onKeyClicked(activation);
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

        private Texture2D GetNumberpadTexture()
        {
            if (numberpadTexture == null)
                numberpadTexture = SliceAtlasRect(numberpadAtlasRect);

            return numberpadTexture;
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