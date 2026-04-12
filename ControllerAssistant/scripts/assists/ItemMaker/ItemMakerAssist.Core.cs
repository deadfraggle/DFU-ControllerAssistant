using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class ItemMakerAssist
    {
        private void ActivateSelectedButton(DaggerfallItemMakerWindow menuWindow)
        {
            if (IsItemGridButton(buttonSelected))
            {
                ActivateSelectedGridItem(menuWindow);
                return;
            }

            if (buttonSelected == PowersPanel || buttonSelected == SideEffectsPanel)
            {
                object pickerObj = GetActiveEnchantmentPicker(menuWindow);
                IList<object> panels = GetEnchantmentPanels(pickerObj);
                if (pickerObj != null && panels != null && panels.Count > 0 && miEnchantmentPicker_RemoveEnchantment != null)
                {
                    int selected = GetSelectedEnchantmentIndex();
                    if (selected >= 0 && selected < panels.Count)
                    {
                        object selectedPanel = panels[selected];
                        if (selectedPanel != null)
                            miEnchantmentPicker_RemoveEnchantment.Invoke(pickerObj, new object[] { selectedPanel });
                    }
                }

                NormalizeSelectedEnchantmentIndex(menuWindow);
                return;
            }

            switch (buttonSelected)
            {
                case ItemNameButton: InvokeButtonHandler(menuWindow, miNameItemButon_OnMouseClick); break;
                case WeaponsButton: InvokeButtonHandler(menuWindow, miWeaponsAndArmor_OnMouseClick); break;
                case MagicButton: InvokeButtonHandler(menuWindow, miMagicItems_OnMouseClick); break;
                case ClothingButton: InvokeButtonHandler(menuWindow, miClothingAndMisc_OnMouseClick); break;
                case IngredientsButton: InvokeButtonHandler(menuWindow, miIngredients_OnMouseClick); break;
                case EnchantButton: InvokeButtonHandler(menuWindow, miEnchantButton_OnMouseClick); break;
                case ExitButton: InvokeButtonHandler(menuWindow, miExitButton_OnMouseClick); break;
                case AddPowersButton: InvokeButtonHandler(menuWindow, miPowersButton_OnMouseClick); break;
                case AddSideEffectsButton: InvokeButtonHandler(menuWindow, miSideEffectsButton_OnMouseClick); break;
                case ItemPanel: InvokeButtonHandler(menuWindow, miSelectedItemButton_OnMouseClick); break;
            }
        }

        private void OnTickOpen(DaggerfallItemMakerWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);
            RefreshItemScrollerInternals(menuWindow);
            RefreshEnchantmentPickerInternals(menuWindow);

            if (selectorHost == null)
            {
                RefreshSelectorToCurrentButton(menuWindow);
            }
            else
            {
                Panel currentPanel = GetCurrentRenderPanel(menuWindow);
                if (currentPanel != null)
                    RefreshSelectorToCurrentButton(menuWindow);
            }

            UpdateEnchantmentPanelColors(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (IsItemGridButton(buttonSelected))
            {
                if (cm.DPadUpPressed || cm.DPadUpHeldSlow)
                {
                    ScrollItemGridByRows(menuWindow, -1);
                    return;
                }

                if (cm.DPadDownPressed || cm.DPadDownHeldSlow)
                {
                    ScrollItemGridByRows(menuWindow, +1);
                    return;
                }
            }

            if (buttonSelected == PowersPanel || buttonSelected == SideEffectsPanel)
            {
                if (cm.DPadUpPressed || cm.DPadUpHeldSlow)
                {
                    object pickerObj = GetActiveEnchantmentPicker(menuWindow);
                    if (pickerObj != null)
                    {
                        int scroll = GetEnchantmentScrollIndex(pickerObj);
                        if (scroll > 0)
                            SetEnchantmentScrollIndex(pickerObj, Mathf.Max(0, scroll - 8));
                    }
                    return;
                }

                if (cm.DPadDownPressed || cm.DPadDownHeldSlow)
                {
                    object pickerObj = GetActiveEnchantmentPicker(menuWindow);
                    if (pickerObj != null)
                    {
                        int scroll = GetEnchantmentScrollIndex(pickerObj);
                        int totalUnits = GetEnchantmentTotalUnits(pickerObj);
                        int displayUnits = GetEnchantmentDisplayUnits(pickerObj);
                        int maxScroll = totalUnits - displayUnits;
                        if (maxScroll < 0)
                            maxScroll = 0;

                        if (scroll < maxScroll)
                            SetEnchantmentScrollIndex(pickerObj, Mathf.Min(maxScroll, scroll + 8));
                    }
                    return;
                }
            }

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            bool isAssisting = (cm.Action1Released || cm.LegendPressed);

            if (isAssisting)
            {
                if (cm.Action1Released)
                    ActivateSelectedButton(menuWindow);

                if (cm.LegendPressed)
                {
                    EnsureLegendUI(menuWindow, cm);
                    legendVisible = !legendVisible;

                    if (legend != null)
                        legend.SetEnabled(legendVisible);
                }
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }

        private void OnOpened(DaggerfallItemMakerWindow menuWindow, ControllerManager cm)
        {
            EnsureInitialized(menuWindow);
            RefreshItemScrollerInternals(menuWindow);
            RefreshEnchantmentPickerInternals(menuWindow);
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallItemMakerWindow closed");
        }
    }
}
