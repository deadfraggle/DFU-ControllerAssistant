using System.Collections.Generic;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

/// <summary>
/// Generic legend overlay: a background Panel + aligned two-column TextLabels.
/// Caller chooses the parent Panel and when to show/hide.
/// </summary>

namespace gigantibyte.DFU.ControllerAssistant
{
    public class LegendOverlay
    {
        private Panel parent;
        private Panel box;

        private readonly List<TextLabel> left = new List<TextLabel>();
        private readonly List<TextLabel> right = new List<TextLabel>();

        // Layout knobs (tweak per-window or set once)
        public float HeaderScale { get; set; } = 6.0f;
        public float RowScale { get; set; } = 5.0f;
        public Color HeaderColor { get; set; } = new Color(1.0f, 0.78f, 0.20f, 1.0f); // warm gold
        public float HeaderExtraGap { get; set; } = 12f; // extra vertical space after header
        public float HeaderScaleBoost { get; set; } = 0.6f; // additional size beyond HeaderScale

        public float PadL { get; set; } = 18f;
        public float PadT { get; set; } = 16f;
        public float LineGap { get; set; } = 36f;
        public float ColGap { get; set; } = 22f;

        public float MarginX { get; set; } = 8f;
        public float MarginFromBottom { get; set; } = 24f;

        public Color BackgroundColor { get; set; } = new Color(0f, 0f, 0f, 0.60f);

        public bool IsBuilt => box != null;
        public bool IsEnabled => box != null && box.Enabled;

        public LegendOverlay(Panel parentPanel)
        {
            SetParent(parentPanel);
        }

        public void SetParent(Panel parentPanel)
        {
            parent = parentPanel;
        }

        public class LegendRow
        {
            public string Left;
            public string Right;

            public LegendRow(string left, string right)
            {
                Left = left;
                Right = right;
            }
        }

        public void Build(string header, List<LegendRow> rows)
        {
            if (parent == null)
                return;

            // If DFU cleared our parent components, we may need to rebuild from scratch.
            // Easiest/cleanest: just discard our refs and rebuild.
            box = null;
            left.Clear();
            right.Clear();

            box = new Panel();
            box.BackgroundColor = BackgroundColor;
            box.Enabled = false;
            parent.Components.Add(box);

            AddHeader(header);

            for (int i = 0; i < rows.Count; i++)
                AddRow(rows[i].Left, rows[i].Right);

            Layout();
            //PositionBottomLeft();
        }

        public void SetEnabled(bool enabled)
        {
            if (box != null)
                box.Enabled = enabled;

            for (int i = 0; i < left.Count; i++)
                left[i].Enabled = enabled;

            for (int i = 0; i < right.Count; i++)
                right[i].Enabled = enabled;
        }

        public void PositionBottomLeft()
        {
            if (parent == null || box == null)
                return;

            Rect r = parent.Rectangle;

            float x = MarginX;
            float y = r.height - box.Size.y - MarginFromBottom;

            box.Position = new Vector2(x, y);
        }
        public void PositionBottomRight()
        {
            if (parent == null || box == null)
                return;

            Rect r = parent.Rectangle;

            float x = r.width - box.Size.x;
            float y = r.height - box.Size.y - MarginFromBottom;

            box.Position = new Vector2(x, y);
        }

        public void PositionAt(float x, float y)
        {
            if (box == null)
                return;

            box.Position = new Vector2(x, y);
        }
        public void PositionTopRight()
        {
            if (parent == null || box == null)
                return;

            Rect r = parent.Rectangle;
            float x = r.width - box.Size.x - MarginX;
            float y = MarginFromBottom; // rename later if needed
            box.Position = new Vector2(x, y);
        }
        public enum LegendAnchor
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Center
        }
        public void PositionNormalized(float xNorm, float yNorm, LegendAnchor anchor)
        {
            if (parent == null || box == null)
                return;

            Rect r = parent.Rectangle;

            float x = 0.0f;
            float y = 0.0f;

            switch (anchor)
            {
                case LegendAnchor.TopRight:
                    x = (r.width * xNorm) - box.Size.x;
                    break;
                case LegendAnchor.BottomLeft:
                    y = (r.height * yNorm) - box.Size.y;
                    break;
                case LegendAnchor.BottomRight:
                    x = (r.width * xNorm) - box.Size.x;
                    y = (r.height * yNorm) - box.Size.y;
                    break;
                case LegendAnchor.Center:
                    x = (r.width * xNorm * 0.5f) - (box.Size.x * 0.5f);
                    y = (r.height * yNorm * 0.5f) - (box.Size.y * 0.5f);
                    break;
            }

            box.Position = new Vector2(x, y);
        }

        public bool IsAttached()
        {
            if (parent == null || box == null)
                return false;

            var comps = parent.Components;
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] == box)
                    return true;
            }

            return false;
        }

        private void AddHeader(string text)
        {
            var line = new TextLabel();
            line.Text = text;
            line.TextScale = HeaderScale + HeaderScaleBoost;
            line.TextColor = HeaderColor;
            line.Enabled = false;

            left.Add(line);
            box.Components.Add(line);
        }

        private void AddRow(string lText, string rText)
        {
            var l = new TextLabel();
            l.Text = lText;
            l.TextScale = RowScale;
            l.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
            l.Enabled = false;

            var r = new TextLabel();
            r.Text = rText;
            r.TextScale = RowScale;
            r.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
            r.Enabled = false;

            left.Add(l);
            right.Add(r);

            box.Components.Add(l);
            box.Components.Add(r);
        }

        private void Layout()
        {
            if (box == null)
                return;

            float y = PadT;

            // First pass: position left items + find widest left
            float leftW = 0f;
            for (int i = 0; i < left.Count; i++)
            {
                TextLabel l = left[i];
                l.Position = new Vector2(PadL, y);
                leftW = Mathf.Max(leftW, l.Rectangle.width);
                y += LineGap;
                // extra spacing after header
                if (i == 0)
                    y += HeaderExtraGap;
            }

            // Second pass: position right column rows (skip header = left[0])
            float rightX = PadL + leftW + ColGap;
            y = PadT + LineGap + HeaderExtraGap;

            for (int i = 0; i < right.Count; i++)
            {
                TextLabel r = right[i];
                r.Position = new Vector2(rightX, y);
                y += LineGap;
            }

            // Compute max width for box
            float maxW = leftW;
            for (int i = 0; i < right.Count; i++)
                maxW = Mathf.Max(maxW, (rightX - PadL) + right[i].Rectangle.width);

            box.Size = new Vector2(maxW + PadL * 2f, (PadT + LineGap * left.Count) + PadT);
        }
        public void ApplyScale(float scale)
        {
            HeaderScale *= scale;
            HeaderScaleBoost *= scale;
            RowScale *= scale;
            HeaderExtraGap *= scale;
            PadL *= scale;
            PadT *= scale;
            LineGap *= scale;
            ColGap *= scale;
            MarginX *= scale;
            MarginFromBottom *= scale;
        }
        public void Destroy()
        {
            if (parent != null && box != null && IsAttached())
                parent.Components.Remove(box);

            box = null;
            left.Clear();
            right.Clear();
        }
    }
}
