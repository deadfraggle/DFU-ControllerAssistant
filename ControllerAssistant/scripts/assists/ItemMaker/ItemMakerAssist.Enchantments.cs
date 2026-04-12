using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class ItemMakerAssist
    {
        private void RefreshEnchantmentPickerInternals(DaggerfallItemMakerWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            object powersObj = (fiPowersList != null) ? fiPowersList.GetValue(menuWindow) : null;
            object sideEffectsObj = (fiSideEffectsList != null) ? fiSideEffectsList.GetValue(menuWindow) : null;

            object pickerObj = powersObj ?? sideEffectsObj;
            if (pickerObj == null)
                return;

            System.Type pickerType = pickerObj.GetType();

            fiEnchantmentPanels = pickerType.GetField("enchantmentPanels", BindingFlags.Instance | BindingFlags.NonPublic);
            fiEnchantmentScroller = pickerType.GetField("scroller", BindingFlags.Instance | BindingFlags.NonPublic);
            miEnchantmentPicker_RemoveEnchantment = pickerType.GetMethod("RemoveEnchantment", BindingFlags.Instance | BindingFlags.NonPublic);

            object scrollerObj = (fiEnchantmentScroller != null) ? fiEnchantmentScroller.GetValue(pickerObj) : null;
            if (scrollerObj != null)
            {
                System.Type scrollType = scrollerObj.GetType();
                piEnchantmentScroller_ScrollIndex = scrollType.GetProperty("ScrollIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                piEnchantmentScroller_TotalUnits = scrollType.GetProperty("TotalUnits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                piEnchantmentScroller_DisplayUnits = scrollType.GetProperty("DisplayUnits", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            IList<object> panels = GetEnchantmentPanels(powersObj);
            if ((panels == null || panels.Count == 0) && sideEffectsObj != null)
                panels = GetEnchantmentPanels(sideEffectsObj);

            if (panels != null && panels.Count > 0 && panels[0] != null)
            {
                System.Type panelType = panels[0].GetType();
                piEnchantmentPanel_TextColor = panelType.GetProperty("TextColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                piEnchantmentPanel_HighlightedTextColor = panelType.GetProperty("HighlightedTextColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                fiEnchantmentPanel_PrimaryLabel = panelType.GetField("primaryLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                fiEnchantmentPanel_SecondaryLabel = panelType.GetField("secondaryLabel", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }

        private IList<object> GetEnchantmentPanels(object pickerObj)
        {
            if (pickerObj == null || fiEnchantmentPanels == null)
                return null;

            object value = fiEnchantmentPanels.GetValue(pickerObj);
            if (value == null)
                return null;

            System.Collections.IList rawList = value as System.Collections.IList;
            if (rawList == null)
                return null;

            List<object> result = new List<object>();
            for (int i = 0; i < rawList.Count; i++)
                result.Add(rawList[i]);

            return result;
        }

        private object GetActiveEnchantmentPicker(DaggerfallItemMakerWindow menuWindow)
        {
            if (menuWindow == null)
                return null;

            if (buttonSelected == PowersPanel)
                return (fiPowersList != null) ? fiPowersList.GetValue(menuWindow) : null;

            if (buttonSelected == SideEffectsPanel)
                return (fiSideEffectsList != null) ? fiSideEffectsList.GetValue(menuWindow) : null;

            return null;
        }

        private int GetSelectedEnchantmentIndex()
        {
            if (buttonSelected == PowersPanel)
                return powersSelectedIndex;
            if (buttonSelected == SideEffectsPanel)
                return sideEffectsSelectedIndex;
            return 0;
        }

        private void SetSelectedEnchantmentIndex(int value)
        {
            if (buttonSelected == PowersPanel)
                powersSelectedIndex = value;
            else if (buttonSelected == SideEffectsPanel)
                sideEffectsSelectedIndex = value;
        }

        private int GetEnchantmentScrollIndex(object pickerObj)
        {
            if (pickerObj == null || fiEnchantmentScroller == null || piEnchantmentScroller_ScrollIndex == null)
                return 0;

            object scrollerObj = fiEnchantmentScroller.GetValue(pickerObj);
            if (scrollerObj == null)
                return 0;

            object value = piEnchantmentScroller_ScrollIndex.GetValue(scrollerObj, null);
            return (value != null) ? (int)value : 0;
        }

        private void SetEnchantmentScrollIndex(object pickerObj, int newIndex)
        {
            if (pickerObj == null || fiEnchantmentScroller == null || piEnchantmentScroller_ScrollIndex == null)
                return;

            object scrollerObj = fiEnchantmentScroller.GetValue(pickerObj);
            if (scrollerObj == null)
                return;

            piEnchantmentScroller_ScrollIndex.SetValue(scrollerObj, newIndex, null);
        }

        private int GetEnchantmentTotalUnits(object pickerObj)
        {
            if (pickerObj == null || fiEnchantmentScroller == null || piEnchantmentScroller_TotalUnits == null)
                return 0;

            object scrollerObj = fiEnchantmentScroller.GetValue(pickerObj);
            if (scrollerObj == null)
                return 0;

            object value = piEnchantmentScroller_TotalUnits.GetValue(scrollerObj, null);
            return (value != null) ? (int)value : 0;
        }

        private int GetEnchantmentDisplayUnits(object pickerObj)
        {
            if (pickerObj == null || fiEnchantmentScroller == null || piEnchantmentScroller_DisplayUnits == null)
                return 0;

            object scrollerObj = fiEnchantmentScroller.GetValue(pickerObj);
            if (scrollerObj == null)
                return 0;

            object value = piEnchantmentScroller_DisplayUnits.GetValue(scrollerObj, null);
            return (value != null) ? (int)value : 0;
        }

        private void NormalizeSelectedEnchantmentIndex(DaggerfallItemMakerWindow menuWindow)
        {
            object powersObj = (fiPowersList != null) ? fiPowersList.GetValue(menuWindow) : null;
            object sideEffectsObj = (fiSideEffectsList != null) ? fiSideEffectsList.GetValue(menuWindow) : null;

            int powersCount = GetEnchantmentCount(powersObj);
            int sideCount = GetEnchantmentCount(sideEffectsObj);

            if (powersCount <= 0)
                powersSelectedIndex = -1;
            else if (powersSelectedIndex < 0)
                powersSelectedIndex = 0;
            else if (powersSelectedIndex >= powersCount)
                powersSelectedIndex = powersCount - 1;

            if (sideCount <= 0)
                sideEffectsSelectedIndex = -1;
            else if (sideEffectsSelectedIndex < 0)
                sideEffectsSelectedIndex = 0;
            else if (sideEffectsSelectedIndex >= sideCount)
                sideEffectsSelectedIndex = sideCount - 1;
        }

        private void TryMoveWithinEnchantmentPanel(DaggerfallItemMakerWindow menuWindow, int direction)
        {
            object pickerObj = GetActiveEnchantmentPicker(menuWindow);
            if (pickerObj == null)
                return;

            int count = GetEnchantmentCount(pickerObj);

            // Empty panel: route immediately
            if (count <= 0)
            {
                if (direction < 0)
                    buttonSelected = ItemNameButton;
                else if (direction > 0)
                    buttonSelected = (buttonSelected == PowersPanel) ? AddPowersButton : AddSideEffectsButton;

                RefreshSelectorToCurrentButton(menuWindow);
                return;
            }

            int selected = GetSelectedEnchantmentIndex();
            if (selected < 0)
                selected = 0;
            if (selected >= count)
                selected = count - 1;

            if (direction < 0)
            {
                if (selected == 0)
                {
                    buttonSelected = ItemNameButton;
                    RefreshSelectorToCurrentButton(menuWindow);
                    return;
                }

                selected--;
                SetSelectedEnchantmentIndex(selected);
            }
            else if (direction > 0)
            {
                if (selected >= count - 1)
                {
                    buttonSelected = (buttonSelected == PowersPanel) ? AddPowersButton : AddSideEffectsButton;
                    RefreshSelectorToCurrentButton(menuWindow);
                    return;
                }

                selected++;
                SetSelectedEnchantmentIndex(selected);
            }

            // Keep selected item visible by scrolling as needed
            int scroll = GetEnchantmentScrollIndex(pickerObj);
            int displayUnits = GetEnchantmentDisplayUnits(pickerObj);

            if (selected < scroll)
                SetEnchantmentScrollIndex(pickerObj, selected);
            else if (selected >= scroll + displayUnits)
                SetEnchantmentScrollIndex(pickerObj, selected - displayUnits + 1);

            NormalizeSelectedEnchantmentIndex(menuWindow);
        }

        private void UpdateEnchantmentPanelColors(DaggerfallItemMakerWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            NormalizeSelectedEnchantmentIndex(menuWindow);

            UpdateOneEnchantmentPanelColors((fiPowersList != null) ? fiPowersList.GetValue(menuWindow) : null, powersSelectedIndex);
            UpdateOneEnchantmentPanelColors((fiSideEffectsList != null) ? fiSideEffectsList.GetValue(menuWindow) : null, sideEffectsSelectedIndex);
        }

        private void UpdateOneEnchantmentPanelColors(object pickerObj, int selectedIndex)
        {
            IList<object> panels = GetEnchantmentPanels(pickerObj);
            if (panels == null || panels.Count == 0 || piEnchantmentPanel_TextColor == null)
                return;

            for (int i = 0; i < panels.Count; i++)
            {
                object panel = panels[i];
                if (panel == null)
                    continue;

                Color color = (i == selectedIndex) ? EnchantmentSelectedColor : EnchantmentNormalColor;

                piEnchantmentPanel_TextColor.SetValue(panel, color, null);

                if (piEnchantmentPanel_HighlightedTextColor != null)
                    piEnchantmentPanel_HighlightedTextColor.SetValue(panel, color, null);

                TextLabel primary = (fiEnchantmentPanel_PrimaryLabel != null) ? fiEnchantmentPanel_PrimaryLabel.GetValue(panel) as TextLabel : null;
                TextLabel secondary = (fiEnchantmentPanel_SecondaryLabel != null) ? fiEnchantmentPanel_SecondaryLabel.GetValue(panel) as TextLabel : null;

                if (primary != null)
                    primary.TextColor = color;
                if (secondary != null)
                    secondary.TextColor = color;
            }
        }

        private int GetEnchantmentCount(object pickerObj)
        {
            IList<object> panels = GetEnchantmentPanels(pickerObj);
            return (panels != null) ? panels.Count : 0;
        }
    }
}
