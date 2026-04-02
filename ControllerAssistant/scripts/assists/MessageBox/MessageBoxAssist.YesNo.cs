using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/*
 * Yes/No MessageBox Assist
 * ------------------------------------------------------------
 * Problem:
 * DaggerfallMessageBox places Yes/No buttons dynamically depending
 * on the amount of prompt text. This causes their Y position to vary,
 * making hard-coded selector rectangles unreliable.
 *
 * Additionally, the rendered button rectangles do not perfectly match
 * the classic selector geometry used elsewhere in DFU UI, especially
 * in width and horizontal spacing.
 *
 * Solution:
 * - Extract live button positions at runtime via reflection.
 * - Convert button screen-space rectangles into native 320x200 space.
 * - Derive the vertical (Y) position directly from the live buttons.
 * - Compute a shared midpoint between Yes and No buttons.
 * - Reconstruct the classic DFU selector layout using:
 *     - Fixed selector size (32.7 x 16.9)
 *     - Fixed horizontal offset from midpoint (31.95)
 *
 * Result:
 * - Selector always aligns correctly regardless of prompt size.
 * - No need for hard-coded layouts or per-window special cases.
 * - Works across all Yes/No message boxes, including mod-added ones.
 *
 * This pattern can be reused for other dynamically positioned UI
 * elements that maintain a consistent layout relationship.
 */

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private sealed class YesNoHandler : IMessageBoxAssistHandler
        {
            private const int YesButton = 0;
            private const int NoButton = 1;

            private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private int selectedButton = NoButton;
            private DefaultSelectorBoxHost selectorHost;

            public bool CanHandle(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                return owner.HasExactButtons(
                    menuWindow,
                    DaggerfallMessageBox.MessageBoxButtons.Yes,
                    DaggerfallMessageBox.MessageBoxButtons.No);
            }

            public void OnOpen(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                selectedButton = NoButton;
                RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                RefreshSelectorAttachment(owner, menuWindow);

                bool moveLeft = (cm.RStickLeftPressed || cm.RStickLeftHeldSlow);
                bool moveRight = (cm.RStickRightPressed || cm.RStickRightHeldSlow);

                bool isAssisting =
                    moveLeft ||
                    moveRight ||
                    cm.DPadUpPressed ||
                    cm.DPadDownPressed ||
                    cm.Action1Released ||
                    cm.LegendPressed;

                if (!isAssisting)
                    return;

                if (cm.DPadUpPressed)
                {
                    owner.SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Yes);
                }

                if (cm.DPadDownPressed)
                {
                    owner.SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.No);
                }

                if (moveLeft || moveRight)
                    TryMoveSelector(owner, menuWindow);

                if (cm.Action1Released)
                {
                    ActivateSelectedButton(owner, menuWindow);
                    return;
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Yes / No",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("D-Pad Up", "Yes"),
                            new LegendOverlay.LegendRow("D-Pad Down", "No"),
                            new LegendOverlay.LegendRow("Right Stick Left/Right", "Move Selector"),
                            new LegendOverlay.LegendRow(cm.Action1Name, "Select Option"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                    //owner.ToggleAnchorEditor();
                }
            }

            public void OnClose(MessageBoxAssist owner, ControllerManager cm)
            {
                DestroySelectorBox();
            }

            private void TryMoveSelector(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                int previous = selectedButton;
                selectedButton = (selectedButton == YesButton) ? NoButton : YesButton;

                if (selectedButton != previous)
                    RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            private void ActivateSelectedButton(MessageBoxAssist owner, DaggerfallMessageBox menuWindow)
            {
                if (selectedButton == YesButton)
                    owner.SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.Yes);
                else
                    owner.SelectButton(menuWindow, DaggerfallMessageBox.MessageBoxButtons.No);
            }

            private void RefreshSelectorToCurrentButton(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow)
            {

                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                Rect yesRect;
                Rect noRect;
                if (!TryGetLiveButtonRects(owner, menuWindow, out yesRect, out noRect))
                    return; // fail silently instead of falling back

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.ShowAtNativeRect(
                    currentPanel,
                    selectedButton == YesButton ? yesRect : noRect,
                    new Color(0.1f, 1f, 1f, 1f));
            }

            private bool TryGetLiveButtonRects(
                MessageBoxAssist owner,
                DaggerfallMessageBox menuWindow,
                out Rect yesRect,
                out Rect noRect)
            {
                yesRect = Rect.zero;
                noRect = Rect.zero;

                if (menuWindow == null)
                    return false;

                Type type = menuWindow.GetType();
                FieldInfo fiButtons = type.GetField("buttons", BF);
                if (fiButtons == null)
                    return false;

                System.Collections.IList list = fiButtons.GetValue(menuWindow) as System.Collections.IList;
                if (list == null)
                    return false;

                Button yesButton = null;
                Button noButton = null;

                for (int i = 0; i < list.Count; i++)
                {
                    Button uiButton = list[i] as Button;
                    if (uiButton == null || !uiButton.Enabled)
                        continue;

                    if (!(uiButton.Tag is DaggerfallMessageBox.MessageBoxButtons))
                        continue;

                    DaggerfallMessageBox.MessageBoxButtons semantic =
                        (DaggerfallMessageBox.MessageBoxButtons)uiButton.Tag;

                    if (semantic == DaggerfallMessageBox.MessageBoxButtons.Yes)
                        yesButton = uiButton;
                    else if (semantic == DaggerfallMessageBox.MessageBoxButtons.No)
                        noButton = uiButton;
                }

                if (yesButton == null || noButton == null)
                    return false;

                Panel renderPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (renderPanel == null)
                    return false;

                Rect yesVisual = GetButtonVisualRectInNativeSpace(renderPanel, yesButton);
                Rect noVisual = GetButtonVisualRectInNativeSpace(renderPanel, noButton);

                GetClassicYesNoRectsFromLiveButtons(yesVisual, noVisual, out yesRect, out noRect);

                return true;
            }
            private void GetClassicYesNoRectsFromLiveButtons(
                Rect yesButtonRectNative,
                Rect noButtonRectNative,
                out Rect yesRect,
                out Rect noRect)
            {
                // Guard: ensure we have valid button data
                if (yesButtonRectNative.width <= 0 || noButtonRectNative.width <= 0)
                {
                    yesRect = Rect.zero;
                    noRect = Rect.zero;
                    return;
                }

                const float selectorW = 32.7f;
                const float selectorH = 16.9f;
                const float centerOffset = 31.95f;

                float yesCenterX = yesButtonRectNative.x + yesButtonRectNative.width * 0.5f;
                float noCenterX = noButtonRectNative.x + noButtonRectNative.width * 0.5f;
                float pairMidX = (yesCenterX + noCenterX) * 0.5f;

                float yesCenterY = yesButtonRectNative.y + yesButtonRectNative.height * 0.5f;
                float noCenterY = noButtonRectNative.y + noButtonRectNative.height * 0.5f;

                yesRect = new Rect(
                    pairMidX - centerOffset - selectorW * 0.5f,
                    yesCenterY - selectorH * 0.5f,
                    selectorW,
                    selectorH);

                noRect = new Rect(
                    pairMidX + centerOffset - selectorW * 0.5f,
                    noCenterY - selectorH * 0.5f,
                    selectorW,
                    selectorH);
            }

            private Rect GetButtonVisualRectInNativeSpace(Panel renderPanel, Button button)
            {
                if (renderPanel == null || button == null)
                    return Rect.zero;

                Rect visualScreenRect = GetOpaqueTextureBoundsInScreenSpace(button);
                return ScreenRectToNativeRect(renderPanel, visualScreenRect);
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
                    // Some textures may not be readable depending on import path.
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
        }
    }
}