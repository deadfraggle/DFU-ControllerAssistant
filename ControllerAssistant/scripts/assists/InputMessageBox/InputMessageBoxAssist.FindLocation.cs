using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallConnect.Arena2;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class InputMessageBoxAssist
    {
        private sealed class FindLocationHandler : IInputMessageBoxAssistHandler
        {
            private const float FindKeyboardAnchorX = 90f;
            private const float FindKeyboardAnchorY = 101f;

            private const float NavButtonWidth = 63f;
            private const float EntryButtonWidth = 128f;
            private const float EntryButtonHeight = 9f;
            private const float ExtraSpacingY = 2f;
            private const float NavGap = 2f;

            private OnScreenKeyboardOverlay keyboardOverlay;

            private readonly List<string> questLocations = new List<string>();
            private readonly List<string> favoriteLocations = new List<string>();

            private int questIndex = 0;
            private int favoriteIndex = 0;

            private string lastQuestLabel = string.Empty;
            private string lastFavoriteLabel = string.Empty;
            private int lastQuestCount = -1;
            private int lastFavoriteCount = -1;
            private int lastRegionIndex = -9999;

            public bool CanHandle(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                return owner.IsTravelMapFindPopup(menuWindow);
            }

            public void OnOpen(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                Panel panel = owner.GetInputMessageBoxRenderPanel(menuWindow);
                if (panel != null)
                {
                    keyboardOverlay = new OnScreenKeyboardOverlay(panel);
                    keyboardOverlay.SetLayout(new Vector2(FindKeyboardAnchorX, FindKeyboardAnchorY), 1.8f, 2.0f);
                    RebuildKeyboard(owner, menuWindow, true);
                }
            }

            public void Tick(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, ControllerManager cm)
            {
                if (menuWindow == null || menuWindow.TextBox == null)
                    return;

                RefreshKeyboardAttachment(owner, menuWindow);
                RebuildKeyboard(owner, menuWindow, false);

                ControllerManager.StickDir8 dir =
                    cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                    ? cm.RStickDir8Pressed
                    : cm.RStickDir8HeldSlow;

                if (dir != ControllerManager.StickDir8.None && keyboardOverlay != null)
                {
                    switch (dir)
                    {
                        case ControllerManager.StickDir8.W:
                        case ControllerManager.StickDir8.NW:
                        case ControllerManager.StickDir8.SW:
                            keyboardOverlay.MoveLeft();
                            break;

                        case ControllerManager.StickDir8.E:
                        case ControllerManager.StickDir8.NE:
                        case ControllerManager.StickDir8.SE:
                            keyboardOverlay.MoveRight();
                            break;

                        case ControllerManager.StickDir8.N:
                            keyboardOverlay.MoveUp();
                            break;

                        case ControllerManager.StickDir8.S:
                            keyboardOverlay.MoveDown();
                            break;
                    }
                }

                bool isAssisting =
                    (cm.DPadLeftPressed || cm.DPadLeftHeldSlow ||
                     cm.Action1Released || cm.Action2Pressed || cm.LegendPressed ||
                     cm.DPadUpPressed || cm.DPadDownPressed || cm.DPadRightReleased ||
                     dir != ControllerManager.StickDir8.None);

                if (keyboardOverlay != null)
                {
                    if (cm.DPadUpPressed)
                        keyboardOverlay.ToggleShift();

                    if (cm.DPadDownPressed)
                        keyboardOverlay.Toggle123();

                    if (cm.DPadRightReleased)
                    {
                        SubmitFindInput(owner, menuWindow);
                        return;
                    }
                }

                if (!isAssisting)
                    return;

                if (cm.DPadLeftPressed || cm.DPadLeftHeldSlow)
                    BackspaceText(menuWindow);

                if (cm.Action2Pressed)
                    menuWindow.TextBox.Text = string.Empty;

                if (cm.Action1Released && keyboardOverlay != null)
                {
                    OnScreenKeyboardActivation activation = keyboardOverlay.ActivateSelectedKey();

                    switch (activation.Action)
                    {
                        case OnScreenKeyboardKeyAction.InsertText:
                            if (!string.IsNullOrEmpty(activation.Text))
                                menuWindow.TextBox.Text += activation.Text;
                            break;

                        case OnScreenKeyboardKeyAction.ReplaceText:
                            if (!string.IsNullOrEmpty(activation.Text))
                                menuWindow.TextBox.Text = activation.Text;
                            break;

                        case OnScreenKeyboardKeyAction.Space:
                            menuWindow.TextBox.Text += " ";
                            break;

                        case OnScreenKeyboardKeyAction.Backspace:
                            BackspaceText(menuWindow);
                            break;

                        case OnScreenKeyboardKeyAction.Ok:
                            SubmitFindInput(owner, menuWindow);
                            return;

                        case OnScreenKeyboardKeyAction.Shift:
                            keyboardOverlay.ToggleShift();
                            break;

                        case OnScreenKeyboardKeyAction.Toggle123:
                            keyboardOverlay.Toggle123();
                            break;

                        case OnScreenKeyboardKeyAction.NextQuest:
                            if (questLocations.Count > 0)
                            {
                                questIndex++;
                                if (questIndex >= questLocations.Count)
                                    questIndex = 0;

                                RebuildKeyboard(owner, menuWindow, true);
                            }
                            break;

                        case OnScreenKeyboardKeyAction.PrevQuest:
                            if (questLocations.Count > 0)
                            {
                                questIndex--;
                                if (questIndex < 0)
                                    questIndex = questLocations.Count - 1;

                                RebuildKeyboard(owner, menuWindow, true);
                            }
                            break;

                        case OnScreenKeyboardKeyAction.NextFavorite:
                            if (favoriteLocations.Count > 0)
                            {
                                favoriteIndex++;
                                if (favoriteIndex >= favoriteLocations.Count)
                                    favoriteIndex = 0;

                                RebuildKeyboard(owner, menuWindow, true);
                            }
                            break;

                        case OnScreenKeyboardKeyAction.PrevFavorite:
                            if (favoriteLocations.Count > 0)
                            {
                                favoriteIndex--;
                                if (favoriteIndex < 0)
                                    favoriteIndex = favoriteLocations.Count - 1;

                                RebuildKeyboard(owner, menuWindow, true);
                            }
                            break;
                    }
                }

                if (cm.LegendPressed)
                {
                    owner.EnsureLegendUI(
                        menuWindow,
                        "Find Location",
                        new List<LegendOverlay.LegendRow>()
                        {
                            new LegendOverlay.LegendRow("Right Stick", "Move Selector"),
                            new LegendOverlay.LegendRow("D-Pad Up", "Shift"),
                            new LegendOverlay.LegendRow("D-Pad Down", "123 toggle"),
                            new LegendOverlay.LegendRow("D-Pad Right", "Submit"),
                            new LegendOverlay.LegendRow("D-Pad Left", "Backspace"),
                            new LegendOverlay.LegendRow(cm.Action1Name, "Activate Key"),
                            new LegendOverlay.LegendRow(cm.Action2Name, "Clear Text"),
                        });

                    owner.SetLegendVisible(!owner.GetLegendVisible());
                }
            }
            private void SubmitFindInput(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                if (owner == null || menuWindow == null || menuWindow.TextBox == null || owner.fiOnGotUserInput == null)
                    return;

                object value = owner.fiOnGotUserInput.GetValue(menuWindow);
                Delegate del = value as Delegate;
                if (del == null)
                    return;

                string text = menuWindow.TextBox.Text;

                owner.DestroyLegend();

                // Important: close first, then invoke.
                // Find-location behaves differently from spell naming when results may open another window.
                menuWindow.CloseWindow();

                try
                {
                    Delegate[] calls = del.GetInvocationList();
                    for (int i = 0; i < calls.Length; i++)
                        calls[i].DynamicInvoke(menuWindow, text);
                }
                catch (Exception ex)
                {
                    Debug.Log("[ControllerAssistant] SubmitFindInput failed: " + ex);
                }
            }

            public void OnClose(InputMessageBoxAssist owner, ControllerManager cm)
            {
                if (keyboardOverlay != null)
                {
                    keyboardOverlay.Destroy();
                    keyboardOverlay = null;
                }

                questLocations.Clear();
                favoriteLocations.Clear();
                questIndex = 0;
                favoriteIndex = 0;
                lastQuestLabel = string.Empty;
                lastFavoriteLabel = string.Empty;
                lastQuestCount = -1;
                lastFavoriteCount = -1;
                lastRegionIndex = -9999;
            }

            private void RefreshKeyboardAttachment(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                Panel currentPanel = owner.GetInputMessageBoxRenderPanel(menuWindow);
                if (currentPanel == null)
                    return;

                if (keyboardOverlay == null)
                {
                    keyboardOverlay = new OnScreenKeyboardOverlay(currentPanel);
                    keyboardOverlay.SetLayout(new Vector2(FindKeyboardAnchorX, FindKeyboardAnchorY), 1.8f, 2.0f);
                    RebuildKeyboard(owner, menuWindow, true);
                    return;
                }

                if (!keyboardOverlay.IsAttached())
                {
                    keyboardOverlay = new OnScreenKeyboardOverlay(currentPanel);
                    keyboardOverlay.SetLayout(new Vector2(FindKeyboardAnchorX, FindKeyboardAnchorY), 1.8f, 2.0f);
                    RebuildKeyboard(owner, menuWindow, true);
                    return;
                }

                keyboardOverlay.RefreshAttachment();
            }

            private void RebuildKeyboard(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow, bool force)
            {
                if (keyboardOverlay == null)
                    return;

                RebuildQuestLocations(owner, menuWindow);
                RebuildFavoriteLocations(owner, menuWindow);

                string questLabel = GetQuestButtonLabel();
                string favoriteLabel = GetFavoriteButtonLabel();
                int regionIndex = owner.GetTravelMapRegionIndex(menuWindow);

                bool changed =
                    force ||
                    regionIndex != lastRegionIndex ||
                    questLabel != lastQuestLabel ||
                    favoriteLabel != lastFavoriteLabel ||
                    questLocations.Count != lastQuestCount ||
                    favoriteLocations.Count != lastFavoriteCount;

                if (!changed)
                    return;

                lastRegionIndex = regionIndex;
                lastQuestLabel = questLabel;
                lastFavoriteLabel = favoriteLabel;
                lastQuestCount = questLocations.Count;
                lastFavoriteCount = favoriteLocations.Count;

                keyboardOverlay.ClearCustomKeys();

                float row4Y = FindKeyboardAnchorY + (EntryButtonHeight + ExtraSpacingY) * 4f;
                float row5Y = FindKeyboardAnchorY + (EntryButtonHeight + ExtraSpacingY) * 5f;
                float row6Y = FindKeyboardAnchorY + (EntryButtonHeight + ExtraSpacingY) * 6f;
                float row7Y = FindKeyboardAnchorY + (EntryButtonHeight + ExtraSpacingY) * 7f;

                float xLeft = FindKeyboardAnchorX;
                float xRight = FindKeyboardAnchorX + EntryButtonWidth - NavButtonWidth;

                keyboardOverlay.AddCustomKey("[Next Quest]", new Rect(xLeft, row4Y, NavButtonWidth, EntryButtonHeight), 4, OnScreenKeyboardKeyAction.NextQuest, null);
                keyboardOverlay.AddCustomKey("[Prev Quest]", new Rect(xRight, row4Y, NavButtonWidth, EntryButtonHeight), 4, OnScreenKeyboardKeyAction.PrevQuest, null);
                keyboardOverlay.AddCustomKey(questLabel, new Rect(FindKeyboardAnchorX, row5Y, EntryButtonWidth, EntryButtonHeight), 5, OnScreenKeyboardKeyAction.ReplaceText, GetCurrentQuestLocation());

                keyboardOverlay.AddCustomKey("[Next Favorite]", new Rect(xLeft, row6Y, NavButtonWidth, EntryButtonHeight), 6, OnScreenKeyboardKeyAction.NextFavorite, null);
                keyboardOverlay.AddCustomKey("[Prev Favorite]", new Rect(xRight, row6Y, NavButtonWidth, EntryButtonHeight), 6, OnScreenKeyboardKeyAction.PrevFavorite, null);
                keyboardOverlay.AddCustomKey(favoriteLabel, new Rect(FindKeyboardAnchorX, row7Y, EntryButtonWidth, EntryButtonHeight), 7, OnScreenKeyboardKeyAction.ReplaceText, GetCurrentFavoriteLocation());

                keyboardOverlay.Build();
            }

            private void RebuildQuestLocations(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                questLocations.Clear();

                string targetRegionName = owner.GetTravelMapRegionName(menuWindow);
                if (string.IsNullOrEmpty(targetRegionName) || QuestMachine.Instance == null)
                {
                    questIndex = 0;
                    return;
                }

                SiteDetails[] sites = QuestMachine.Instance.GetAllActiveQuestSites();
                if (sites == null || sites.Length == 0)
                {
                    questIndex = 0;
                    return;
                }

                for (int i = 0; i < sites.Length; i++)
                {
                    SiteDetails site = sites[i];

                    if (string.IsNullOrEmpty(site.regionName))
                        continue;

                    if (!string.Equals(site.regionName, targetRegionName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Inject locationName only.
                    string injectText = site.locationName;
                    if (string.IsNullOrEmpty(injectText))
                        continue;

                    if (!ContainsIgnoreCase(questLocations, injectText))
                        questLocations.Add(injectText);
                }

                if (questLocations.Count == 0)
                    questIndex = 0;
                else if (questIndex >= questLocations.Count)
                    questIndex = 0;
                else if (questIndex < 0)
                    questIndex = questLocations.Count - 1;
            }

            private void RebuildFavoriteLocations(InputMessageBoxAssist owner, DaggerfallInputMessageBox menuWindow)
            {
                favoriteLocations.Clear();

                int regionIndex = owner.GetTravelMapRegionIndex(menuWindow);
                if (regionIndex < 0)
                {
                    favoriteIndex = 0;
                    return;
                }

                List<FavoriteLocation> favorites = FavoritesStore.Favorites;
                if (favorites == null || favorites.Count == 0)
                {
                    favoriteIndex = 0;
                    return;
                }

                for (int i = 0; i < favorites.Count; i++)
                {
                    FavoriteLocation favorite = favorites[i];
                    if (favorite == null)
                        continue;

                    if (favorite.RegionIndex != regionIndex)
                        continue;

                    if (string.IsNullOrEmpty(favorite.LocationName))
                        continue;

                    if (!ContainsIgnoreCase(favoriteLocations, favorite.LocationName))
                        favoriteLocations.Add(favorite.LocationName);
                }

                if (favoriteLocations.Count == 0)
                    favoriteIndex = 0;
                else if (favoriteIndex >= favoriteLocations.Count)
                    favoriteIndex = 0;
                else if (favoriteIndex < 0)
                    favoriteIndex = favoriteLocations.Count - 1;
            }

            private string GetCurrentQuestLocation()
            {
                if (questLocations.Count == 0 || questIndex < 0 || questIndex >= questLocations.Count)
                    return null;

                return questLocations[questIndex];
            }

            private string GetCurrentFavoriteLocation()
            {
                if (favoriteLocations.Count == 0 || favoriteIndex < 0 || favoriteIndex >= favoriteLocations.Count)
                    return null;

                return favoriteLocations[favoriteIndex];
            }

            private string GetQuestButtonLabel()
            {
                string current = GetCurrentQuestLocation();
                if (string.IsNullOrEmpty(current))
                    return "[No Quest Locations]";

                return TrimToButtonText(current, 18);
            }

            private string GetFavoriteButtonLabel()
            {
                string current = GetCurrentFavoriteLocation();
                if (string.IsNullOrEmpty(current))
                    return "[No Favorites]";

                return TrimToButtonText(current, 24);
            }

            private string TrimToButtonText(string text, int maxLen)
            {
                if (string.IsNullOrEmpty(text))
                    return string.Empty;

                if (text.Length <= maxLen)
                    return text;

                if (maxLen <= 3)
                    return text.Substring(0, maxLen);

                return text.Substring(0, maxLen - 3) + "...";
            }

            private bool ContainsIgnoreCase(List<string> list, string value)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }

            private void BackspaceText(DaggerfallInputMessageBox menuWindow)
            {
                string text = menuWindow.TextBox.Text;

                if (string.IsNullOrEmpty(text))
                {
                    menuWindow.TextBox.Text = string.Empty;
                }
                else if (text.Length <= 1)
                {
                    menuWindow.TextBox.Text = string.Empty;
                }
                else
                {
                    menuWindow.TextBox.Text = text.Substring(0, text.Length - 1);
                }
            }
        }

        private bool IsTravelMapFindPopup(DaggerfallInputMessageBox menuWindow)
        {
            if (menuWindow == null || fiOnGotUserInput == null)
                return false;

            if (GetTravelMapWindow(menuWindow) == null)
                return false;

            object value = fiOnGotUserInput.GetValue(menuWindow);
            if (value == null)
                return false;

            Delegate del = value as Delegate;
            if (del == null)
                return false;

            Delegate[] calls = del.GetInvocationList();
            for (int i = 0; i < calls.Length; i++)
            {
                if (calls[i].Method.Name == "HandleLocationFindEvent")
                    return true;
            }

            return false;
        }

        private DaggerfallTravelMapWindow GetTravelMapWindow(DaggerfallInputMessageBox menuWindow)
        {
            IUserInterfaceWindow previous = GetPreviousWindow(menuWindow);
            return previous as DaggerfallTravelMapWindow;
        }

        private IUserInterfaceWindow GetPreviousWindow(object window)
        {
            if (window == null)
                return null;

            Type type = window.GetType();

            PropertyInfo pi = type.GetProperty("PreviousWindow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null)
            {
                object value = pi.GetValue(window, null);
                IUserInterfaceWindow prev = value as IUserInterfaceWindow;
                if (prev != null)
                    return prev;
            }

            FieldInfo fi = type.GetField("previousWindow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null)
            {
                object value = fi.GetValue(window);
                IUserInterfaceWindow prev = value as IUserInterfaceWindow;
                if (prev != null)
                    return prev;
            }

            return null;
        }

        private int GetTravelMapRegionIndex(DaggerfallInputMessageBox menuWindow)
        {
            DaggerfallTravelMapWindow travelMap = GetTravelMapWindow(menuWindow);
            if (travelMap == null)
                return -1;

            FieldInfo fi = travelMap.GetType().GetField("currentDFRegionIndex", BF);
            if (fi == null)
                return -1;

            object value = fi.GetValue(travelMap);
            if (value is int)
                return (int)value;

            return -1;
        }

        private string GetTravelMapRegionName(DaggerfallInputMessageBox menuWindow)
        {
            int regionIndex = GetTravelMapRegionIndex(menuWindow);
            if (regionIndex >= 0 &&
                DaggerfallUnity.Instance != null &&
                DaggerfallUnity.Instance.ContentReader != null &&
                DaggerfallUnity.Instance.ContentReader.MapFileReader != null)
            {
                return DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(regionIndex);
            }

            DaggerfallTravelMapWindow travelMap = GetTravelMapWindow(menuWindow);
            if (travelMap == null)
                return null;

            FieldInfo fi = travelMap.GetType().GetField("currentDFRegion", BF);
            if (fi == null)
                return null;

            object value = fi.GetValue(travelMap);
            if (value == null)
                return null;

            FieldInfo fiName = value.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fiName != null)
            {
                object regionName = fiName.GetValue(value);
                if (regionName is string)
                    return (string)regionName;
            }

            PropertyInfo piName = value.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (piName != null)
            {
                object regionName = piName.GetValue(value, null);
                if (regionName is string)
                    return (string)regionName;
            }

            return null;
        }
    }
}
