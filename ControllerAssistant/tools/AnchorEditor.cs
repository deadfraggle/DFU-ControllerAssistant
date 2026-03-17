using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class AnchorEditor
    {
        private Panel parent;
        private Panel root;
        private Panel top;
        private Panel bottom;
        private Panel left;
        private Panel right;

        private bool enabled = false;
        private bool built = false;
        private bool centeredOnce = false;

        private float x;
        private float y;
        private float w;
        private float h;

        private float borderThickness = 2f;
        private readonly Color borderColor = new Color(0.1f, 1f, 1f, 1f);

        private bool fineMode = false;

        public AnchorEditor(float startWidth, float startHeight)
        {
            w = startWidth;
            h = startHeight;
        }

        public void Toggle()
        {
            enabled = !enabled;

            if (!enabled)
                Hide();
            else
                Debug.Log("[AnchorEditor] Enabled");

            if (!enabled)
                Debug.Log("[AnchorEditor] Disabled");
        }

        public void Tick(Panel parentPanel)
        {
            if (!enabled || parentPanel == null)
                return;

            parent = parentPanel;

            EnsureBuilt();

            if (!centeredOnce)
            {
                x = (parent.Size.x - w) * 0.5f;
                y = (parent.Size.y - h) * 0.5f;
                centeredOnce = true;
            }

            HandleInput();
            RefreshVisual();
        }

        private void EnsureBuilt()
        {
            if (built || parent == null)
                return;

            root = new Panel();
            root.AutoSize = AutoSizeModes.None;
            root.BackgroundColor = Color.clear;

            top = CreateBorderPiece();
            bottom = CreateBorderPiece();
            left = CreateBorderPiece();
            right = CreateBorderPiece();

            root.Components.Add(top);
            root.Components.Add(bottom);
            root.Components.Add(left);
            root.Components.Add(right);

            parent.Components.Add(root);
            built = true;
        }

        private Panel CreateBorderPiece()
        {
            Panel p = new Panel();
            p.AutoSize = AutoSizeModes.None;
            p.BackgroundColor = borderColor;
            return p;
        }

        private float GetStep()
        {
            if (fineMode)
                return 0.1f;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                return 0.5f;

            return 1f;
        }

        private void HandleInput()
        {
            float step = GetStep();

            // Move
            if (Input.GetKey(KeyCode.Keypad8)) y -= step;
            if (Input.GetKey(KeyCode.Keypad2)) y += step;
            if (Input.GetKey(KeyCode.Keypad4)) x -= step;
            if (Input.GetKey(KeyCode.Keypad6)) x += step;

            // Resize
            if (Input.GetKey(KeyCode.Keypad7)) w -= step;
            if (Input.GetKey(KeyCode.Keypad9)) w += step;
            if (Input.GetKey(KeyCode.Keypad1)) h -= step;
            if (Input.GetKey(KeyCode.Keypad3)) h += step;
            if (Input.GetKeyDown(KeyCode.KeypadDivide))
            {
                fineMode = !fineMode;
                Debug.Log("[AnchorEditor] Fine mode: " + (fineMode ? "ON" : "OFF"));
            }

            // Clamp so it never collapses into nonsense
            w = Mathf.Max(2f, w);
            h = Mathf.Max(2f, h);

            // Dump to Player.log
            if (Input.GetKeyDown(KeyCode.Keypad5))
                PrintCurrentBox();

            // Re-center current box
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                x = (parent.Size.x - w) * 0.5f;
                y = (parent.Size.y - h) * 0.5f;
                Debug.Log("[AnchorEditor] Re-centered box");
            }
        }

        private void RefreshVisual()
        {
            if (!built || root == null)
                return;

            borderThickness = Mathf.Max(2f, GetUiScale() * 0.5f);

            root.Position = new Vector2(x, y);
            root.Size = new Vector2(w, h);

            top.Position = Vector2.zero;
            top.Size = new Vector2(w, borderThickness);

            bottom.Position = new Vector2(0f, h - borderThickness);
            bottom.Size = new Vector2(w, borderThickness);

            left.Position = Vector2.zero;
            left.Size = new Vector2(borderThickness, h);

            right.Position = new Vector2(w - borderThickness, 0f);
            right.Size = new Vector2(borderThickness, h);
        }
        private void PrintCurrentBox()
        {
            Rect nativeRect = GetNativeRectFromScaledRect(x, y, w, h);

            Debug.Log(
                $"[AnchorEditor] new Rect({nativeRect.x:F1}f, {nativeRect.y:F1}f, {nativeRect.width:F1}f, {nativeRect.height:F1}f)"
            );
        }

        private void Hide()
        {
            if (root != null)
                root.Enabled = false;
        }

        public void Destroy()
        {
            if (root != null && root.Parent != null)
            {
                Panel parentPanel = root.Parent as Panel;
                if (parentPanel != null)
                    parentPanel.Components.Remove(root);
            }

            root = null;
            top = null;
            bottom = null;
            left = null;
            right = null;

            built = false;
            centeredOnce = false;
        }
        private Rect GetNativeRectFromScaledRect(float scaledX, float scaledY, float scaledW, float scaledH)
        {
            if (parent == null)
                return new Rect(scaledX, scaledY, scaledW, scaledH);

            float nativeWidth = 320f;
            float nativeHeight = 200f;

            float parentWidth = parent.Size.x;
            float parentHeight = parent.Size.y;

            float scaleX = parentWidth / nativeWidth;
            float scaleY = parentHeight / nativeHeight;
            float scale = Mathf.Min(scaleX, scaleY);

            float scaledNativeWidth = nativeWidth * scale;
            float scaledNativeHeight = nativeHeight * scale;

            float offsetX = (parentWidth - scaledNativeWidth) * 0.5f;
            float offsetY = (parentHeight - scaledNativeHeight) * 0.5f;

            float nativeX = (scaledX - offsetX) / scale;
            float nativeY = (scaledY - offsetY) / scale;
            float nativeW = scaledW / scale;
            float nativeH = scaledH / scale;

            return new Rect(nativeX, nativeY, nativeW, nativeH);
        }
        private float GetUiScale()
        {
            if (parent == null)
                return 1f;

            float nativeWidth = 320f;
            float nativeHeight = 200f;

            float parentWidth = parent.Size.x;
            float parentHeight = parent.Size.y;

            float scaleX = parentWidth / nativeWidth;
            float scaleY = parentHeight / nativeHeight;

            return Mathf.Min(scaleX, scaleY);
        }
    }
}