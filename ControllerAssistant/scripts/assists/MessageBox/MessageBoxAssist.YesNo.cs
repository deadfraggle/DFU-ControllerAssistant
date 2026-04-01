using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class MessageBoxAssist
    {
        private sealed class YesNoHandler : IMessageBoxAssistHandler
        {
            private const int YesButton = 0;
            private const int NoButton = 1;

            private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private enum YesNoLayout
            {
                Generic,
                SpellbookSort,
                TravelMapConfirm,
                MissingModsWarning,
            }

            // Old spellbook-aligned rects
            private static readonly Rect GenericYesRect = new Rect(111.7f, 97.1f, 32.7f, 16.9f);
            private static readonly Rect GenericNoRect = new Rect(175.6f, 97.1f, 32.7f, 16.9f);

            // Travel map specific rects
            private static readonly Rect TravelMapYesRect = new Rect(111.7f, 100.7f, 32.7f, 16.9f);
            private static readonly Rect TravelMapNoRect = new Rect(175.6f, 100.7f, 32.7f, 16.9f);

            //// Missing mod specific rects
            //private static readonly Rect MissingModsYesRect = new Rect(111.7f, 118.1f, 32.7f, 16.9f);
            //private static readonly Rect MissingModsNoRect = new Rect(175.6f, 118.1f, 32.7f, 16.9f);

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

                if (ShouldHideSelector(menuWindow))
                {
                    DestroySelectorBox();
                    return;
                }

                RefreshSelectorToCurrentButton(owner, menuWindow);
            }

            public void Tick(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, ControllerManager cm)
            {
                RefreshSelectorAttachment(owner, menuWindow);

                bool hideSelector = ShouldHideSelector(menuWindow);
                if (hideSelector)
                    DestroySelectorBox();

                bool moveLeft = !hideSelector && (cm.RStickLeftPressed || cm.RStickLeftHeldSlow);
                bool moveRight = !hideSelector && (cm.RStickRightPressed || cm.RStickRightHeldSlow);

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
            private bool ShouldHideSelector(DaggerfallMessageBox menuWindow)
            {
                return ResolveLayout(menuWindow) == YesNoLayout.MissingModsWarning;
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
                if (ShouldHideSelector(menuWindow))
                {
                    DestroySelectorBox();
                    return;
                }

                Panel currentPanel = owner.GetMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                Rect yesRect;
                Rect noRect;
                ResolveButtonRects(owner, menuWindow, out yesRect, out noRect);

                if (selectorHost == null)
                    selectorHost = new DefaultSelectorBoxHost();

                selectorHost.ShowAtNativeRect(
                    currentPanel,
                    selectedButton == YesButton ? yesRect : noRect,
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

            private void ResolveButtonRects(MessageBoxAssist owner, DaggerfallMessageBox menuWindow, out Rect yesRect, out Rect noRect)
            {
                YesNoLayout layout = ResolveLayout(menuWindow);

                switch (layout)
                {
                    case YesNoLayout.TravelMapConfirm:
                        yesRect = TravelMapYesRect;
                        noRect = TravelMapNoRect;
                        break;

                    case YesNoLayout.SpellbookSort:
                        yesRect = GenericYesRect;
                        noRect = GenericNoRect;
                        break;

                    //case YesNoLayout.MissingModsWarning:
                    //    yesRect = MissingModsYesRect;
                    //    noRect = MissingModsNoRect;
                    //    break;

                    default:
                        yesRect = GenericYesRect;
                        noRect = GenericNoRect;
                        break;
                }
            }


            private Rect GetAbsoluteRect(Panel rootPanel, BaseScreenComponent component)
            {
                float x = component.Position.x;
                float y = component.Position.y;

                BaseScreenComponent parent = component.Parent;
                while (parent != null && parent != rootPanel)
                {
                    x += parent.Position.x;
                    y += parent.Position.y;
                    parent = parent.Parent;
                }

                return new Rect(x, y, component.Size.x, component.Size.y);
            }

            private void CollectContentBottomRecursive(Panel rootPanel, BaseScreenComponent component, ref float maxY)
            {
                if (component == null || !component.Enabled)
                    return;

                // Ignore buttons (we don't want to include Yes/No themselves)
                if (component is Button)
                    return;

                Rect r = GetAbsoluteRect(rootPanel, component);

                float bottom = r.y + r.height;

                if (bottom > maxY)
                    maxY = bottom;

                Panel panel = component as Panel;
                if (panel == null || panel.Components == null)
                    return;

                for (int i = 0; i < panel.Components.Count; i++)
                    CollectContentBottomRecursive(rootPanel, panel.Components[i], ref maxY);
            }


            private YesNoLayout ResolveLayout(DaggerfallMessageBox menuWindow)
            {
                IUserInterfaceWindow previous = GetPreviousWindow(menuWindow);
                if (previous != null)
                {
                    Type previousType = previous.GetType();
                    string typeName = previousType.Name;

                    if (typeName == "DaggerfallTravelMapWindow")
                        return YesNoLayout.TravelMapConfirm;

                    if (typeName == "DaggerfallSpellBookWindow")
                        return YesNoLayout.SpellbookSort;

                    if (typeName == "DaggerfallUnitySaveGameWindow")
                        return YesNoLayout.MissingModsWarning;
                }

                string promptText = GetPromptText(menuWindow);
                if (!string.IsNullOrEmpty(promptText))
                {
                    string text = promptText.ToLowerInvariant();

                    Debug.Log("===== promptText =====");
                    Debug.Log(text);

                    if (text.Contains("do you wish to travel to"))
                        return YesNoLayout.TravelMapConfirm;

                    if (text.Contains("do you want to sort spells"))
                        return YesNoLayout.SpellbookSort;

                    if (text.Contains("currently used mods do not match") ||
                        text.Contains("mod is either not loaded or has been altered") ||
                        text.Contains("errors may occur during gameplay"))
                        return YesNoLayout.MissingModsWarning;
                }

                return YesNoLayout.Generic;
            }

            private IUserInterfaceWindow GetPreviousWindow(DaggerfallMessageBox menuWindow)
            {
                if (menuWindow == null)
                    return null;

                Type type = menuWindow.GetType();

                PropertyInfo pi = type.GetProperty("PreviousWindow", BF);
                if (pi != null)
                {
                    object value = pi.GetValue(menuWindow, null);
                    IUserInterfaceWindow previous = value as IUserInterfaceWindow;
                    if (previous != null)
                        return previous;
                }

                FieldInfo fi = type.GetField("previousWindow", BF);
                if (fi != null)
                {
                    object value = fi.GetValue(menuWindow);
                    IUserInterfaceWindow previous = value as IUserInterfaceWindow;
                    if (previous != null)
                        return previous;
                }

                return null;
            }

            private string GetPromptText(DaggerfallMessageBox menuWindow)
            {
                if (menuWindow == null)
                    return string.Empty;

                try
                {
                    Type type = menuWindow.GetType();

                    // Try common string-ish fields first
                    FieldInfo[] fields = type.GetFields(BF);
                    for (int i = 0; i < fields.Length; i++)
                    {
                        FieldInfo fi = fields[i];
                        if (fi.FieldType == typeof(string))
                        {
                            string value = fi.GetValue(menuWindow) as string;
                            if (!string.IsNullOrEmpty(value))
                                return value;
                        }
                    }

                    // Try TextLabel collections / fields containing text
                    foreach (FieldInfo fi in fields)
                    {
                        object fieldValue = fi.GetValue(menuWindow);
                        if (fieldValue == null)
                            continue;

                        string extracted = ExtractText(fieldValue);
                        if (!string.IsNullOrEmpty(extracted))
                            return extracted;
                    }

                    PropertyInfo[] props = type.GetProperties(BF);
                    for (int i = 0; i < props.Length; i++)
                    {
                        PropertyInfo pi = props[i];
                        if (!pi.CanRead)
                            continue;

                        object propValue = null;
                        try
                        {
                            propValue = pi.GetValue(menuWindow, null);
                        }
                        catch
                        {
                            continue;
                        }

                        if (propValue == null)
                            continue;

                        if (pi.PropertyType == typeof(string))
                        {
                            string value = propValue as string;
                            if (!string.IsNullOrEmpty(value))
                                return value;
                        }

                        string extracted = ExtractText(propValue);
                        if (!string.IsNullOrEmpty(extracted))
                            return extracted;
                    }
                }
                catch
                {
                }

                return string.Empty;
            }

            private string ExtractText(object obj)
            {
                if (obj == null)
                    return string.Empty;

                string s = obj as string;
                if (!string.IsNullOrEmpty(s))
                    return s;

                Type type = obj.GetType();

                // TextLabel.Text or similar
                PropertyInfo piText = type.GetProperty("Text", BF);
                if (piText != null && piText.CanRead)
                {
                    try
                    {
                        object value = piText.GetValue(obj, null);
                        string text = value as string;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                    catch
                    {
                    }
                }

                FieldInfo fiText = type.GetField("Text", BF);
                if (fiText != null)
                {
                    object value = fiText.GetValue(obj);
                    string text = value as string;
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }

                IEnumerable<object> enumerable = obj as IEnumerable<object>;
                if (enumerable != null)
                {
                    foreach (object item in enumerable)
                    {
                        string text = ExtractText(item);
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }

                System.Collections.IEnumerable weakEnumerable = obj as System.Collections.IEnumerable;
                if (weakEnumerable != null)
                {
                    foreach (object item in weakEnumerable)
                    {
                        string text = ExtractText(item);
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }

                return string.Empty;
            }

            private void DestroySelectorBox()
            {
                if (selectorHost != null)
                    selectorHost.Destroy();
            }
        }
    }
}