using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;
using System.Collections.Generic;

namespace gigantibyte.DFU.ControllerAssistant
{
    public enum OnScreenKeyboardKeyAction
    {
        InsertText,
        Shift,
        Toggle123,
        Space,
        Backspace,
        Ok,
    }

    public enum OnScreenKeyboardForm
    {
        Lower,
        Upper,
        Symbols,
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

            BuildRowsForCurrentForm();
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