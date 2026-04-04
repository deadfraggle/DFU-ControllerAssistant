using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private sealed class GenericButtonsHandler : IMessageBoxAssistHandler
        {
            private const bool DEBUG_GENERIC = true;

            private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private sealed class LiveButtonEntry
            {
                public Button button;
                public Rect nativeRect;
                public float centerX;
                public float centerY;
                public int left = -1;
                public int right = -1;
            }

            private readonly List<LiveButtonEntry> buttons = new List<LiveButtonEntry>();

            private int selectedIndex = 0;
            private DefaultSelectorBoxHost selectorHost;

            public bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (menuWindow == null)
                    return false;

                if (menuWindow.ClickAnywhereToClose)
                    return false;

                // Let more specific handlers take precedence.
                if (owner.HasExactButtons(
                    menuWindow,
                    DaggerfallMessageBox.MessageBoxButtons.Yes,
                    DaggerfallMessageBox.MessageBoxButtons.No))
                {
                    return false;
                }

                // Generic fallback: any enabled live buttons.
                List<Button> liveButtons;
                return TryGetLiveButtons(owner, menuWindow, out liveButtons) && liveButtons.Count > 0;
            }

            public void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                RebuildLiveButtons(owner, menuWindow);

                // Default to the second button when available.
                // Fall back to the first if there is only one.
                selectedIndex = (buttons.Count >= 2) ? 1 : 0;

                RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                RefreshSelectorAttachment(owner, menuWindow);

                // Rebuild if button list changed or panel got rebuilt
                if (!EnsureLiveButtonsStillValid(owner, menuWindow))
                    return;

                bool moveLeft = cm.RStickLeftPressed || cm.RStickLeftHeldSlow;
                bool moveRight = cm.RStickRightPressed || cm.RStickRightHeldSlow;

                bool isAssisting =
                    moveLeft ||
                    moveRight ||
                    cm.Action1Released ||
                    cm.LegendPressed;

                if (!isAssisting)
                    return;

                if (moveLeft)
                    TryMoveHorizontal(owner, menuWindow, false);
                else if (moveRight)
                    TryMoveHorizontal(owner, menuWindow, true);

                if (cm.Action1Released)
                {
                    ActivateSelectedButton(owner, menuWindow);
                    return;
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Options",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("Right Stick Left/Right", "Move Selector"),
                            new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                    //owner.ToggleAnchorEditor();
                }
            }

            public void OnClose(MessageBoxAssist owner, ControllerManager cm)
            {
                buttons.Clear();
                DestroySelectorBox();
            }

            private bool EnsureLiveButtonsStillValid(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (buttons.Count == 0)
                {
                    RebuildLiveButtons(owner, menuWindow);
                    RefreshSelectorToCurrentButton(owner, menuWindow);
                    return buttons.Count > 0;
                }

                for (int i = 0; i < buttons.Count; i++)
                {
                    if (buttons[i] == null || buttons[i].button == null)
                    {
                        RebuildLiveButtons(owner, menuWindow);
                        RefreshSelectorToCurrentButton(owner, menuWindow);
                        return buttons.Count > 0;
                    }
                }

                return true;
            }

            private void TryMoveHorizontal(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow,
                bool moveRight)
            {
                if (buttons.Count == 0 || selectedIndex < 0 || selectedIndex >= buttons.Count)
                    return;

                int previous = selectedIndex;
                LiveButtonEntry current = buttons[selectedIndex];

                int next = moveRight ? current.right : current.left;
                if (next > -1 && next < buttons.Count)
                    selectedIndex = next;

                if (selectedIndex != previous)
                    RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            private void ActivateSelectedButton(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (selectedIndex < 0 || selectedIndex >= buttons.Count)
                    return;

                Button uiButton = buttons[selectedIndex].button;
                if (uiButton == null)
                    return;

                owner.DestroyLegend();

                try
                {
                    FieldInfo fiOnMouseClick = typeof(BaseScreenComponent).GetField(
                        "OnMouseClick",
                        BF);

                    if (fiOnMouseClick != null)
                    {
                        object delObj = fiOnMouseClick.GetValue(uiButton);
                        Delegate del = delObj as Delegate;

                        if (del != null)
                        {
                            Delegate[] calls = del.GetInvocationList();
                            for (int i = 0; i < calls.Length; i++)
                                calls[i].DynamicInvoke(uiButton, Vector2.zero);

                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("[ControllerAssistant] GenericButtonsHandler activation failed: " + ex);
                }
            }

            private void RefreshSelectorToCurrentButton(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow)
            {
                if (selectedIndex < 0 || selectedIndex >= buttons.Count)
                    return;

                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                //Panel buttonParent = buttons[selectedIndex].button.Parent as Panel;
                //selectorHost.ShowAtNativeRect(
                //    buttonParent ?? currentPanel, // Fallback to main panel if parent is null
                //    buttons[selectedIndex].nativeRect,
                //    new Color(0.1f, 1f, 1f, 1f));

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.ShowAtNativeRect(
                    currentPanel,
                    buttons[selectedIndex].nativeRect,
                    new Color(0.1f, 1f, 1f, 1f));
            }

            private void RefreshSelectorAttachment(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.RefreshAttachment(currentPanel);
            }

            private void DestroySelectorBox()
            {
                if (selectorHost != null)
                    selectorHost.Destroy();
            }

            private void RebuildLiveButtons(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                buttons.Clear();

                List<Button> liveButtons;
                if (!TryGetLiveButtons(owner, menuWindow, out liveButtons))
                    return;

                Panel renderPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (renderPanel == null)
                    return;

                for (int i = 0; i < liveButtons.Count; i++)
                {
                    Button uiButton = liveButtons[i];
                    Rect screenRect = uiButton.Rectangle;
                    Rect nativeRect = GetButtonVisualRectInNativeSpace(renderPanel, uiButton);

                    if (DEBUG_GENERIC)
                    {
                        Debug.LogFormat("[GenericButtons] RAW screenRect: x={0:F2} y={1:F2} w={2:F2} h={3:F2}",
                            screenRect.x, screenRect.y, screenRect.width, screenRect.height);

                        Debug.LogFormat("[GenericButtons] Native BEFORE adjust: x={0:F2} y={1:F2} w={2:F2} h={3:F2}",
                            nativeRect.x, nativeRect.y, nativeRect.width, nativeRect.height);
                    }

                    if (nativeRect.width <= 0 || nativeRect.height <= 0)
                        continue;

                    float centerX = nativeRect.x + nativeRect.width * 0.5f;
                    float centerY = nativeRect.y + nativeRect.height * 0.5f;

                    if (DEBUG_GENERIC)
                    {
                        Debug.LogFormat("[GenericButtons] Center: ({0:F2}, {1:F2})", centerX, centerY);
                    }

                    float width = Mathf.Max(nativeRect.width + 6f, 28f);
                    float height = nativeRect.height;

                    nativeRect = new Rect(
                        centerX - width * 0.5f,
                        centerY - height * 0.5f,
                        width,
                        height);

                    if (DEBUG_GENERIC)
                    {
                        Debug.LogFormat("[GenericButtons] AFTER adjust: x={0:F2} y={1:F2} w={2:F2} h={3:F2}",
                            nativeRect.x, nativeRect.y, nativeRect.width, nativeRect.height);
                    }

                    LiveButtonEntry entry = new LiveButtonEntry();
                    entry.button = uiButton;
                    entry.nativeRect = nativeRect;
                    entry.centerX = centerX;
                    entry.centerY = centerY;

                    buttons.Add(entry);
                }

                if (buttons.Count == 0)
                    return;

                // Sort by row then by x. Most message boxes should be a single row,
                // but this keeps future oddballs reasonably sane.
                buttons.Sort((a, b) =>
                {
                    float dy = Mathf.Abs(a.centerY - b.centerY);
                    if (dy > 6f)
                        return a.centerY.CompareTo(b.centerY);

                    return a.centerX.CompareTo(b.centerX);
                });

                BuildHorizontalNeighbors();
                NormalizeRowButtonRects();

                if (selectedIndex >= buttons.Count)
                    selectedIndex = buttons.Count - 1;
                if (selectedIndex < 0)
                    selectedIndex = 0;
            }
            private void NormalizeRowButtonRects()
            {
                if (buttons.Count < 2)
                    return;

                const float rowTolerance = 6f;

                for (int i = 0; i < buttons.Count; i++)
                {
                    List<int> row = new List<int>();
                    row.Add(i);

                    for (int j = i + 1; j < buttons.Count; j++)
                    {
                        if (Mathf.Abs(buttons[j].centerY - buttons[i].centerY) <= rowTolerance)
                            row.Add(j);
                    }

                    if (row.Count < 2)
                        continue;

                    float avgCenterY = 0f;
                    float pairMidX = 0f;
                    float maxHeight = 0f;
                    float maxWidth = 0f;

                    for (int r = 0; r < row.Count; r++)
                    {
                        LiveButtonEntry e = buttons[row[r]];
                        avgCenterY += e.centerY;
                        pairMidX += e.centerX;

                        if (e.nativeRect.height > maxHeight)
                            maxHeight = e.nativeRect.height;

                        if (e.nativeRect.width > maxWidth)
                            maxWidth = e.nativeRect.width;
                    }

                    avgCenterY /= row.Count;
                    pairMidX /= row.Count;

                    // Special handling for exactly two buttons on one row:
                    // reconstruct a more DFU-like selector pair from the midpoint.
                    if (row.Count == 2)
                    {
                        // Sort left/right explicitly
                        int leftIndex = row[0];
                        int rightIndex = row[1];
                        if (buttons[leftIndex].centerX > buttons[rightIndex].centerX)
                        {
                            int tmp = leftIndex;
                            leftIndex = rightIndex;
                            rightIndex = tmp;
                        }

                        // Canonical-ish fallback size/spacing for paired popup buttons.
                        // Tuned to be close to your anchor measurements:
                        // Delete  ~ x=111.7 w=32.9
                        // Cancel  ~ x=175.7 w=32.9
                        const float selectorW = 32.9f;
                        const float selectorH = 16.0f;
                        const float centerOffset = 32.0f;

                        LiveButtonEntry left = buttons[leftIndex];
                        LiveButtonEntry right = buttons[rightIndex];

                        left.nativeRect = new Rect(
                            pairMidX - centerOffset - selectorW * 0.5f,
                            avgCenterY - selectorH * 0.5f,
                            selectorW,
                            selectorH);

                        right.nativeRect = new Rect(
                            pairMidX + centerOffset - selectorW * 0.5f,
                            avgCenterY - selectorH * 0.5f,
                            selectorW,
                            selectorH);

                        if (DEBUG_GENERIC)
                        {
                            Debug.LogFormat(
                                "[GenericButtons] Two-button reconstruct: pairMidX={0:F2} avgY={1:F2} offset={2:F2} w={3:F2} h={4:F2}",
                                pairMidX, avgCenterY, centerOffset, selectorW, selectorH);

                            Debug.LogFormat(
                                "[GenericButtons] FINAL rect LEFT : x={0:F2} y={1:F2} w={2:F2} h={3:F2}",
                                left.nativeRect.x, left.nativeRect.y, left.nativeRect.width, left.nativeRect.height);

                            Debug.LogFormat(
                                "[GenericButtons] FINAL rect RIGHT: x={0:F2} y={1:F2} w={2:F2} h={3:F2}",
                                right.nativeRect.x, right.nativeRect.y, right.nativeRect.width, right.nativeRect.height);
                        }

                        i = row[row.Count - 1];
                        continue;
                    }

                    // Generic normalization for 3+ button rows
                    maxWidth = Mathf.Max(maxWidth, 28f);

                    if (DEBUG_GENERIC)
                    {
                        Debug.LogFormat(
                            "[GenericButtons] Row normalize: count={0} maxWidth={1:F2} maxHeight={2:F2} avgY={3:F2}",
                            row.Count, maxWidth, maxHeight, avgCenterY);
                    }

                    for (int r = 0; r < row.Count; r++)
                    {
                        LiveButtonEntry e = buttons[row[r]];
                        e.nativeRect = new Rect(
                            e.centerX - maxWidth * 0.5f,
                            avgCenterY - maxHeight * 0.5f,
                            maxWidth,
                            maxHeight);

                        if (DEBUG_GENERIC)
                        {
                            Debug.LogFormat(
                                "[GenericButtons] FINAL rect: x={0:F2} y={1:F2} w={2:F2} h={3:F2}",
                                e.nativeRect.x,
                                e.nativeRect.y,
                                e.nativeRect.width,
                                e.nativeRect.height);
                        }
                    }

                    i = row[row.Count - 1];
                }
            }

            private void BuildHorizontalNeighbors()
            {
                if (buttons.Count == 0)
                    return;

                List<List<int>> rows = new List<List<int>>();
                const float rowTolerance = 6f;

                for (int i = 0; i < buttons.Count; i++)
                {
                    bool placed = false;

                    for (int r = 0; r < rows.Count; r++)
                    {
                        int probeIndex = rows[r][0];
                        if (Mathf.Abs(buttons[i].centerY - buttons[probeIndex].centerY) <= rowTolerance)
                        {
                            rows[r].Add(i);
                            placed = true;
                            break;
                        }
                    }

                    if (!placed)
                    {
                        List<int> newRow = new List<int>();
                        newRow.Add(i);
                        rows.Add(newRow);
                    }
                }

                for (int r = 0; r < rows.Count; r++)
                {
                    rows[r].Sort((ia, ib) => buttons[ia].centerX.CompareTo(buttons[ib].centerX));

                    for (int i = 0; i < rows[r].Count; i++)
                    {
                        int idx = rows[r][i];
                        buttons[idx].left = (i > 0) ? rows[r][i - 1] : -1;
                        buttons[idx].right = (i < rows[r].Count - 1) ? rows[r][i + 1] : -1;
                    }
                }
            }

            private bool TryGetLiveButtons(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow,
                out List<Button> liveButtons)
            {
                liveButtons = new List<Button>();

                if (menuWindow == null)
                    return false;

                FieldInfo fiButtons = menuWindow.GetType().GetField("buttons", BF);
                if (fiButtons == null)
                    return false;

                IList list = fiButtons.GetValue(menuWindow) as IList;
                if (list == null)
                    return false;

                for (int i = 0; i < list.Count; i++)
                {
                    Button uiButton = list[i] as Button;
                    if (uiButton == null)
                        continue;

                    if (!uiButton.Enabled)
                        continue;

                    if (uiButton.Rectangle.width <= 0 || uiButton.Rectangle.height <= 0)
                        continue;

                    liveButtons.Add(uiButton);
                }

                return liveButtons.Count > 0;
            }

            private Rect GetButtonVisualRectInNativeSpace(Panel renderPanel, Button button)
            {
                if (renderPanel == null || button == null)
                    return Rect.zero;

                // For generic fallback, use the full live button rectangle rather than
                // trimming to opaque texture pixels. This keeps the selector centered on
                // the actual clickable footprint and avoids inward bias on paired buttons.
                return ScreenRectToNativeRect(renderPanel, button.Rectangle);
            }

            private Rect ScreenRectToNativeRect(Panel renderPanel, Rect screenRect)
            {
                Rect panelRect = renderPanel.Rectangle;
                if (panelRect.width <= 0 || panelRect.height <= 0)
                    return Rect.zero;

                float localX = screenRect.x - panelRect.x;
                float localY = screenRect.y - panelRect.y;

                float nativeX = (localX / panelRect.width) * 320f;
                float nativeY = (localY / panelRect.height) * 200f;
                float nativeW = (screenRect.width / panelRect.width) * 320f;
                float nativeH = (screenRect.height / panelRect.height) * 200f;

                return new Rect(nativeX, nativeY, nativeW, nativeH);
            }

            private Rect GetOpaqueTextureBoundsInScreenSpace(Button button)
            {
                if (button == null)
                    return Rect.zero;

                Rect buttonScreenRect = button.Rectangle;
                Texture2D tex = button.BackgroundTexture;

                if (tex == null)
                    return buttonScreenRect;

                Color32[] pixels;
                try
                {
                    pixels = tex.GetPixels32();
                }
                catch
                {
                    return buttonScreenRect;
                }

                int texW = tex.width;
                int texH = tex.height;

                int minX = texW;
                int minY = texH;
                int maxX = -1;
                int maxY = -1;

                const byte alphaThreshold = 8;

                for (int y = 0; y < texH; y++)
                {
                    int row = y * texW;
                    for (int x = 0; x < texW; x++)
                    {
                        if (pixels[row + x].a > alphaThreshold)
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (maxX < minX || maxY < minY)
                    return buttonScreenRect;

                float sx = buttonScreenRect.width / texW;
                float sy = buttonScreenRect.height / texH;

                return new Rect(
                    buttonScreenRect.x + minX * sx,
                    buttonScreenRect.y + minY * sy,
                    (maxX - minX + 1) * sx,
                    (maxY - minY + 1) * sy);
            }
        }
    }
}
