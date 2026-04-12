using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class ItemMakerAssist : IMenuAssist
    {
        private const bool debugMODE = true;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private System.Type cachedReflectionType = null;

        // Reflected handlers
        private MethodInfo miWeaponsAndArmor_OnMouseClick;
        private MethodInfo miMagicItems_OnMouseClick;
        private MethodInfo miClothingAndMisc_OnMouseClick;
        private MethodInfo miIngredients_OnMouseClick;
        private MethodInfo miPowersButton_OnMouseClick;
        private MethodInfo miSideEffectsButton_OnMouseClick;
        private MethodInfo miEnchantButton_OnMouseClick;
        private MethodInfo miSelectedItemButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;
        private MethodInfo miNameItemButon_OnMouseClick;
        private MethodInfo miItemListScroller_OnItemClick;

        // Reflected fields
        private FieldInfo fiItemsListScroller;
        private FieldInfo fiPowersList;
        private FieldInfo fiSideEffectsList;
        private FieldInfo fiSelectedItem;
        private FieldInfo fiSelectedTabPage;
        private FieldInfo fiItemListScrollerRect;
        private FieldInfo fiSelectedItemRect;
        private FieldInfo fiPowersListRect;
        private FieldInfo fiSideEffectsListRect;

        // Item scroller internals
        private FieldInfo fiItemScroller_ItemButtons;
        private FieldInfo fiItemScroller_ScrollBar;
        private FieldInfo fiItemScroller_Items;
        private MethodInfo miItemScroller_GetScrollIndex;
        private PropertyInfo piItemScroller_Items;
        private PropertyInfo piScrollBar_ScrollIndex;

        // Item grid state
        private int itemGridColumn = 0;
        private int itemGridRow = 0;
        private const int ItemGridColumns = 2;
        private const int ItemGridRows = 8;

        // Enchantment panel state
        private int powersSelectedIndex = -1;
        private int sideEffectsSelectedIndex = -1;

        // Enchantment picker reflection
        private FieldInfo fiEnchantmentPanels;
        private FieldInfo fiEnchantmentScroller;
        private PropertyInfo piEnchantmentScroller_ScrollIndex;
        private PropertyInfo piEnchantmentScroller_TotalUnits;
        private PropertyInfo piEnchantmentScroller_DisplayUnits;
        private MethodInfo miEnchantmentPicker_RemoveEnchantment;

        // Enchantment panel reflection
        private PropertyInfo piEnchantmentPanel_TextColor;
        private PropertyInfo piEnchantmentPanel_HighlightedTextColor;
        private FieldInfo fiEnchantmentPanel_PrimaryLabel;
        private FieldInfo fiEnchantmentPanel_SecondaryLabel;

        // Colors
        private static readonly Color EnchantmentNormalColor = DaggerfallUI.DaggerfallDefaultTextColor;
        private static readonly Color EnchantmentSelectedColor = new Color(0.1f, 1f, 1f, 1f);

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private DefaultSelectorBoxHost selectorHost;

        const int ItemNameButton = 0;
        const int WeaponsButton = 1;
        const int MagicButton = 2;
        const int ClothingButton = 3;
        const int IngredientsButton = 4;
        const int EnchantButton = 5;
        const int ExitButton = 6;
        const int AddSideEffectsButton = 7;
        const int AddPowersButton = 8;
        const int PowersPanel = 9;
        const int SideEffectsPanel = 10;
        const int ItemPanel = 11;
        const int Grid00 = 9000;
        const int Grid01 = 9001;
        const int Grid03 = 9003;
        const int Grid07 = 9007;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(3.6f, 1.6f, 160.6f, 8.2f), S = PowersPanel, E = WeaponsButton },
            new SelectorButtonInfo { rect = new Rect(174.6f, 5.9f, 81.9f, 9.2f), S = MagicButton, E = Grid00, W = ItemNameButton },
            new SelectorButtonInfo { rect = new Rect(174.6f, 14.9f, 81.9f, 9.2f), N = WeaponsButton, S = ClothingButton, E = Grid00, W = ItemNameButton },
            new SelectorButtonInfo { rect = new Rect(174.6f, 24.2f, 81.9f, 9.2f), N = MagicButton, S = IngredientsButton, E = Grid00, W = ItemNameButton },
            new SelectorButtonInfo { rect = new Rect(174.6f, 32.8f, 81.9f, 9.2f), N = ClothingButton, S = ItemPanel, E = Grid00, W = ItemNameButton },
            new SelectorButtonInfo { rect = new Rect(200.0f, 114.5f, 43.5f, 15.6f), N = ItemPanel, S = ExitButton, E = Grid01, W = SideEffectsPanel },
            new SelectorButtonInfo { rect = new Rect(202.2f, 176.1f, 39.3f, 21.6f), N = EnchantButton, E = Grid07, W = AddSideEffectsButton },
            new SelectorButtonInfo { rect = new Rect(105.7f, 182.6f, 77.6f, 9.8f), N = SideEffectsPanel, E = ExitButton, W = AddPowersButton },
            new SelectorButtonInfo { rect = new Rect(7.6f, 182.6f, 77.6f, 9.8f), N = PowersPanel, E = AddSideEffectsButton },
            new SelectorButtonInfo { rect = new Rect(7.5f, 57.1f, 78.4f, 121.5f), N = ItemNameButton, S = AddPowersButton, E = SideEffectsPanel },
            new SelectorButtonInfo { rect = new Rect(105.2f, 57.1f, 78.4f, 121.5f), N = ItemNameButton, S = AddSideEffectsButton, E = ItemPanel, W = PowersPanel },
            new SelectorButtonInfo { rect = new Rect(196.6f, 68.5f, 49.2f, 35.9f), N = IngredientsButton, S = EnchantButton, E = Grid03, W = SideEffectsPanel },
        };

        public int buttonSelected = ItemPanel;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallItemMakerWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallItemMakerWindow menuWindow = top as DaggerfallItemMakerWindow;

            if (menuWindow == null)
            {
                if (wasOpen)
                {
                    OnClosed(cm);
                    wasOpen = false;
                }
                return;
            }

            if (!wasOpen)
            {
                wasOpen = true;
                OnOpened(menuWindow, cm);
            }

            OnTickOpen(menuWindow, cm);
        }

        public void ResetState()
        {
            wasOpen = false;

            DestroyLegend();
            DestroySelectorBox();

            legendVisible = false;
            panelRenderWindow = null;
            cachedReflectionType = null;
        }
    }
}