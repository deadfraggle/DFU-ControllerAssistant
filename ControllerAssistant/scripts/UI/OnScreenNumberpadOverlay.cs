using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
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

        private readonly List<KeyInfo> keys = new List<KeyInfo>();

        private Vector2 anchorNative;
        private float keySpacingX;
        private float keySpacingY;

        private const float KeyWidth = 16.0f;
        private const float KeyHeight = 10.0f;
        private const float OkWidth = 26.0f;
        private const float MaxWidth = 24.0f;
        private const float BackWidth = 24.0f;

        private string maxValueText = "99";

        // Default selection requested by user
        private string defaultSelectedLabel = "1";
        private int selectedKeyIndex = 0;

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

            BuildKeys();
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

        private void BuildKeys()
        {
            float x = anchorNative.x;
            float y = anchorNative.y;

            // Row 0
            BuildKey("7", new Rect(x, y, KeyWidth, KeyHeight), 0, OnScreenNumberpadKeyAction.InsertText, "7");
            x += KeyWidth + keySpacingX;
            BuildKey("8", new Rect(x, y, KeyWidth, KeyHeight), 0, OnScreenNumberpadKeyAction.InsertText, "8");
            x += KeyWidth + keySpacingX;
            BuildKey("9", new Rect(x, y, KeyWidth, KeyHeight), 0, OnScreenNumberpadKeyAction.InsertText, "9");

            // Row 1
            x = anchorNative.x;
            y += KeyHeight + keySpacingY;
            BuildKey("4", new Rect(x, y, KeyWidth, KeyHeight), 1, OnScreenNumberpadKeyAction.InsertText, "4");
            x += KeyWidth + keySpacingX;
            BuildKey("5", new Rect(x, y, KeyWidth, KeyHeight), 1, OnScreenNumberpadKeyAction.InsertText, "5");
            x += KeyWidth + keySpacingX;
            BuildKey("6", new Rect(x, y, KeyWidth, KeyHeight), 1, OnScreenNumberpadKeyAction.InsertText, "6");

            // Row 2
            x = anchorNative.x;
            y += KeyHeight + keySpacingY;
            BuildKey("1", new Rect(x, y, KeyWidth, KeyHeight), 2, OnScreenNumberpadKeyAction.InsertText, "1");
            x += KeyWidth + keySpacingX;
            BuildKey("2", new Rect(x, y, KeyWidth, KeyHeight), 2, OnScreenNumberpadKeyAction.InsertText, "2");
            x += KeyWidth + keySpacingX;
            BuildKey("3", new Rect(x, y, KeyWidth, KeyHeight), 2, OnScreenNumberpadKeyAction.InsertText, "3");

            // Row 3
            x = anchorNative.x;
            y += KeyHeight + keySpacingY;
            BuildKey("0", new Rect(x, y, KeyWidth, KeyHeight), 3, OnScreenNumberpadKeyAction.InsertText, "0");
            x += KeyWidth + keySpacingX;
            BuildKey("[OK]", new Rect(x, y, OkWidth, KeyHeight), 3, OnScreenNumberpadKeyAction.Ok, null);

            // Row 4
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

            KeyInfo key = new KeyInfo();
            key.Label = text;
            key.NativeRect = nativeRect;
            key.Row = row;
            key.CenterX = nativeRect.x + nativeRect.width * 0.5f;
            key.Action = action;
            key.Text = value;
            keys.Add(key);
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