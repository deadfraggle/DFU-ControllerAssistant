using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InventoryAssist
    {
        private class SelectorBoxOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private BaseScreenComponent top;
            private BaseScreenComponent bottom;
            private BaseScreenComponent left;
            private BaseScreenComponent right;
            private bool built = false;

            public SelectorBoxOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public bool IsAttached()
            {
                return built && root != null && root.Parent == parent;
            }

            public void BuildCenteredBox(float boxWidth, float boxHeight, float borderThickness, Color borderColor)
            {
                Destroy();

                if (parent == null)
                    return;

                root = new Panel();
                root.AutoSize = AutoSizeModes.None;
                root.Size = new Vector2(boxWidth, boxHeight);
                root.BackgroundColor = Color.clear;

                top = CreateBorderPiece(new Vector2(boxWidth, borderThickness), borderColor);
                top.Position = Vector2.zero;

                bottom = CreateBorderPiece(new Vector2(boxWidth, borderThickness), borderColor);
                bottom.Position = new Vector2(0f, boxHeight - borderThickness);

                left = CreateBorderPiece(new Vector2(borderThickness, boxHeight), borderColor);
                left.Position = Vector2.zero;

                right = CreateBorderPiece(new Vector2(borderThickness, boxHeight), borderColor);
                right.Position = new Vector2(boxWidth - borderThickness, 0f);

                root.Components.Add(top);
                root.Components.Add(bottom);
                root.Components.Add(left);
                root.Components.Add(right);

                parent.Components.Add(root);
                built = true;
            }

            public void SetPosition(Vector2 topLeft)
            {
                if (!built || root == null)
                    return;

                root.Position = topLeft;
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
            }

            private BaseScreenComponent CreateBorderPiece(Vector2 size, Color color)
            {
                Panel piece = new Panel();
                piece.AutoSize = AutoSizeModes.None;
                piece.Size = size;
                piece.BackgroundColor = color;
                return piece;
            }
        }

        private class DiamondIndicatorOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private DiamondPointOverlay top;
            private DiamondPointOverlay right;
            private DiamondPointOverlay bottom;
            private DiamondPointOverlay left;

            public DiamondIndicatorOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(float radius, float pointSize, Color faceColor)
            {
                if (parent == null)
                    return;

                Destroy();

                float size = radius * 2f + pointSize;

                root = DaggerfallUI.AddPanel(new Rect(0, 0, size, size), parent);
                root.BackgroundColor = new Color(0, 0, 0, 0);

                float center = size * 0.5f;
                float halfPoint = pointSize * 0.5f;

                top = new DiamondPointOverlay(root);
                top.Build(new Rect(center - halfPoint, 0, pointSize, pointSize), faceColor);

                right = new DiamondPointOverlay(root);
                right.Build(new Rect(size - pointSize, center - halfPoint, pointSize, pointSize), faceColor);

                bottom = new DiamondPointOverlay(root);
                bottom.Build(new Rect(center - halfPoint, size - pointSize, pointSize, pointSize), faceColor);

                left = new DiamondPointOverlay(root);
                left.Build(new Rect(0, center - halfPoint, pointSize, pointSize), faceColor);
            }

            public void SetCenter(Vector2 center)
            {
                if (root == null)
                    return;

                root.Position = new Vector2(
                    center.x - root.Size.x * 0.5f,
                    center.y - root.Size.y * 0.5f
                );
            }

            public void Destroy()
            {
                if (top != null) top.Destroy();
                if (right != null) right.Destroy();
                if (bottom != null) bottom.Destroy();
                if (left != null) left.Destroy();

                top = null;
                right = null;
                bottom = null;
                left = null;

                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                root = null;
            }

            private class DiamondPointOverlay
            {
                private readonly Panel parent;
                private Panel root;
                private Panel face;
                private Panel borderTop;
                private Panel borderLeft;
                private Panel borderRight;
                private Panel borderBottom;

                public DiamondPointOverlay(Panel parent)
                {
                    this.parent = parent;
                }

                public void Build(Rect rect, Color faceColor)
                {
                    if (parent == null)
                        return;

                    Destroy();

                    root = DaggerfallUI.AddPanel(rect, parent);
                    root.BackgroundColor = new Color(0, 0, 0, 0);

                    float w = rect.width;
                    float h = rect.height;

                    float thin = 1f;
                    float thick = 2f;

                    Color borderColor = new Color(0.08f, 0.16f, 0.45f, 1f);

                    face = DaggerfallUI.AddPanel(
                        new Rect(thin, thin, Mathf.Max(1f, w - thin - thick), Mathf.Max(1f, h - thin - thick)),
                        root);
                    face.BackgroundColor = faceColor;

                    borderTop = DaggerfallUI.AddPanel(
                        new Rect(0, 0, w, thin),
                        root);
                    borderTop.BackgroundColor = borderColor;

                    borderLeft = DaggerfallUI.AddPanel(
                        new Rect(0, 0, thin, h),
                        root);
                    borderLeft.BackgroundColor = borderColor;

                    borderRight = DaggerfallUI.AddPanel(
                        new Rect(w - thick, 0, thick, h),
                        root);
                    borderRight.BackgroundColor = borderColor;

                    borderBottom = DaggerfallUI.AddPanel(
                        new Rect(0, h - thick, w, thick),
                        root);
                    borderBottom.BackgroundColor = borderColor;
                }

                public void Destroy()
                {
                    if (root != null && root.Parent != null)
                    {
                        Panel parentPanel = root.Parent as Panel;
                        if (parentPanel != null)
                            parentPanel.Components.Remove(root);
                    }

                    face = null;
                    borderTop = null;
                    borderLeft = null;
                    borderRight = null;
                    borderBottom = null;
                    root = null;
                }
            }
        }

        private class PaperDollTargetListOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel[] labels;

            public PaperDollTargetListOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(string[] rows)
            {
                if (parent == null || rows == null || rows.Length == 0)
                    return;

                Destroy();

                root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 64), parent);
                root.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
                root.Enabled = true;

                labels = new TextLabel[rows.Length];

                for (int i = 0; i < rows.Length; i++)
                {
                    TextLabel label = new TextLabel();
                    label.Text = rows[i];
                    label.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
                    label.Enabled = true;
                    root.Components.Add(label);
                    labels[i] = label;
                }
            }

            public void SetRect(Rect rect)
            {
                if (root == null)
                    return;

                root.Position = new Vector2(rect.x, rect.y);
                root.Size = new Vector2(rect.width, rect.height);

                if (labels == null || labels.Length == 0)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                float scaleTo4K = rect.height / 540f;   // 50 native height becomes 540 px at 4K
                float topMargin = 14f * scaleTo4K;
                float bottomMargin = 5f * scaleTo4K;

                float usableHeight = rect.height - topMargin - bottomMargin;
                float rowHeight = usableHeight / labels.Length;

                float textScale = Mathf.Max(1.8f, rect.height / 62f);

                for (int i = 0; i < labels.Length; i++)
                {
                    float rowY = topMargin + i * rowHeight;

                    if (labels[i] != null)
                    {
                        float rowNudge = Mathf.Max(0.5f, rowHeight * 0.06f);

                        labels[i].Position = new Vector2(
                            padL,
                            rowY - rowNudge
                        );

                        labels[i].TextScale = textScale;
                    }
                }
            }

            public void SetSelectedIndex(int selectedIndex)
            {
                if (labels == null)
                    return;

                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null)
                    {
                        labels[i].TextColor = (i == selectedIndex)
                            ? new Color(1f, 0.9f, 0.2f, 1f)   // selected = yellow
                            : Color.white;                     // unselected = white
                    }
                }
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                labels = null;
                root = null;
            }
        }
        private class ClothingExpandOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel label;

            public ClothingExpandOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(string text)
            {
                if (parent == null)
                    return;

                Destroy();

                root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), parent);
                root.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
                root.Enabled = true;

                label = new TextLabel();
                label.Text = text;
                label.TextColor = Color.white;
                label.Enabled = true;
                root.Components.Add(label);
            }

            public void SetRect(Rect rect, float textScale)
            {
                if (root == null || label == null)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                float scaleTo4K = rect.height / 540f;   // 50 native height becomes 540 px at 4K
                float topMargin = 14f * scaleTo4K;
                float bottomMargin = 5f * scaleTo4K;

                // Match one row of the 9-row paper-doll list
                float referenceListHeight = rect.height;
                float referenceUsableHeight = referenceListHeight - topMargin - bottomMargin;
                float rowHeight = referenceUsableHeight / 9f;

                float rowNudge = Mathf.Max(0.5f, rowHeight * 0.06f);

                // Build a compact single-row panel instead of using the full 50-high rect
                float compactHeight = topMargin + rowHeight + bottomMargin;

                root.Position = new Vector2(rect.x, rect.y);
                root.Size = new Vector2(rect.width, compactHeight);

                label.Position = new Vector2(
                    padL,
                    topMargin - rowNudge
                );

                label.TextScale = textScale;
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                label = null;
                root = null;
            }
        }

        private class ClothingTargetListOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel[] labels;

            public ClothingTargetListOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(string[] rows)
            {
                if (parent == null || rows == null || rows.Length == 0)
                    return;

                Destroy();

                root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 64), parent);
                root.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
                root.Enabled = true;

                labels = new TextLabel[rows.Length];

                for (int i = 0; i < rows.Length; i++)
                {
                    TextLabel label = new TextLabel();
                    label.Text = rows[i];
                    label.TextColor = Color.white;
                    label.Enabled = true;
                    root.Components.Add(label);
                    labels[i] = label;
                }
            }

            public void SetRect(Rect rect, float textScale)
            {
                if (root == null)
                    return;

                root.Position = new Vector2(rect.x, rect.y);

                if (labels == null || labels.Length == 0)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                // Use the same vertical metrics as the paper-doll list:
                // reference rect = 50 native units high
                float referenceRectHeight = 50f * (rect.height / 78f);

                float scaleTo4K = referenceRectHeight / 540f;
                float topMargin = 14f * scaleTo4K;
                float bottomMargin = 5f * scaleTo4K;

                // Paper-doll uses 9 rows. Borrow its row height exactly.
                float referenceUsableHeight = referenceRectHeight - topMargin - bottomMargin;
                float referenceRowHeight = referenceUsableHeight / 9f;

                float rowNudge = Mathf.Max(0.5f, referenceRowHeight * 0.06f);

                // Build clothing panel height from 6 rows using paper-doll row spacing
                float compactHeight = topMargin + (referenceRowHeight * labels.Length) + bottomMargin;
                root.Size = new Vector2(rect.width, compactHeight);

                for (int i = 0; i < labels.Length; i++)
                {
                    float rowY = topMargin + i * referenceRowHeight;

                    if (labels[i] != null)
                    {
                        labels[i].Position = new Vector2(
                            padL,
                            rowY - rowNudge
                        );

                        labels[i].TextScale = textScale;
                    }
                }
            }

            public void SetSelectedIndex(int selectedIndex)
            {
                if (labels == null)
                    return;

                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] != null)
                    {
                        labels[i].TextColor = (i == selectedIndex)
                            ? new Color(1f, 0.9f, 0.2f, 1f)
                            : Color.white;
                    }
                }
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                labels = null;
                root = null;
            }
        }

        private class GearExpandOverlay
        {
            private readonly Panel parent;
            private Panel root;
            private TextLabel label;

            public GearExpandOverlay(Panel parent)
            {
                this.parent = parent;
            }

            public void Build(string text)
            {
                if (parent == null)
                    return;

                Destroy();

                root = DaggerfallUI.AddPanel(new Rect(0, 0, 64, 16), parent);
                root.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);
                root.Enabled = true;

                label = new TextLabel();
                label.Text = text;
                label.TextColor = Color.white;
                label.Enabled = true;
                root.Components.Add(label);
            }

            public void SetRect(Rect rect, float textScale)
            {
                if (root == null || label == null)
                    return;

                float padL = Mathf.Max(3f, rect.width * 0.08f);

                const float referenceRectHeightAt4K = 540f;
                const float referenceTopMarginAt4K = 14f;
                const float referenceBottomMarginAt4K = 5f;

                float scaleTo4K = rect.height / referenceRectHeightAt4K;
                float topMargin = referenceTopMarginAt4K * scaleTo4K;
                float bottomMargin = referenceBottomMarginAt4K * scaleTo4K;

                float usableHeight = rect.height - topMargin - bottomMargin;
                float rowHeight = usableHeight / 9f;
                float rowNudge = Mathf.Max(0.5f, rowHeight * 0.06f);
                float compactHeight = topMargin + rowHeight + bottomMargin;

                root.Position = new Vector2(rect.x, rect.y);
                root.Size = new Vector2(rect.width, compactHeight);

                label.Position = new Vector2(
                    padL,
                    topMargin - rowNudge
                );

                label.TextScale = textScale;
            }

            public void Destroy()
            {
                if (root != null && root.Parent != null)
                {
                    Panel parentPanel = root.Parent as Panel;
                    if (parentPanel != null)
                        parentPanel.Components.Remove(root);
                }

                label = null;
                root = null;
            }
        }

        private class InventoryGrid
        {
            public readonly float OriginX;
            public readonly float OriginY;
            public readonly int Columns;
            public readonly int Rows;
            public readonly float CellWidth;
            public readonly float CellHeight;

            public InventoryGrid(float originX, float originY, int columns, int rows, float cellWidth, float cellHeight)
            {
                OriginX = originX;
                OriginY = originY;
                Columns = columns;
                Rows = rows;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
            }

            public Rect GetCellRect(int column, int row)
            {
                float x = OriginX + (column * CellWidth);
                float y = OriginY + (row * CellHeight);
                return new Rect(x, y, CellWidth, CellHeight);
            }
        }

    }
}
