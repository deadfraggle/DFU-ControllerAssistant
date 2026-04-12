using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class PotionMakerAssist : IMenuAssist
    {
        private const bool debugMODE = true;
        private bool wasOpen = false;

        private FieldInfo fiParentPanel;
        private Panel parentPanel;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private System.Type cachedReflectionType = null;

        // Reflected handlers
        private MethodInfo miRecipesButton_OnMouseClick;
        private MethodInfo miMixButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;
        private MethodInfo miIngredientsListScroller_OnItemClick;
        private MethodInfo miCauldronListScroller_OnItemClick;

        // Reflected fields
        private FieldInfo fiRecipesButton;
        private FieldInfo fiMixButton;
        private FieldInfo fiExitButton;
        private FieldInfo fiIngredientsListScroller;
        private FieldInfo fiCauldronListScroller;
        private FieldInfo fiIngredientsListScrollerRect;
        private FieldInfo fiCauldronListScrollerRect;
        private FieldInfo fiIngredientsListRect;
        private FieldInfo fiCauldronListRect;

        // ItemListScroller internals
        private FieldInfo fiItemScroller_ItemButtons;
        private FieldInfo fiItemScroller_ScrollBar;
        private FieldInfo fiItemScroller_Items;
        private MethodInfo miItemScroller_GetScrollIndex;
        private PropertyInfo piItemScroller_Items;
        private PropertyInfo piScrollBar_ScrollIndex;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private DefaultSelectorBoxHost selectorHost;

        // Regions
        private const int RecipesButton = 0;
        private const int MixButton = 1;
        private const int ExitButton = 2;
        private const int IngredientsGridAnchor = 9000;
        private const int CauldronGridAnchor = 9100;

        // Live grid ids
        private const int IngredientsGridBase = 1000; // 4x3 visible
        private const int CauldronGridBase = 2000;    // 4x2 visible

        private const int IngredientsGridColumns = 3;
        private const int IngredientsGridRows = 4;
        private const int CauldronGridColumns = 2;
        private const int CauldronGridRows = 4;

        private int ingredientsGridColumn = 0;
        private int ingredientsGridRow = 0;
        private int cauldronGridColumn = 0;
        private int cauldronGridRow = 0;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(169f, 26f, 36f, 16f), S = MixButton, W = IngredientsGridAnchor, E = CauldronGridAnchor }, // Recipes
            new SelectorButtonInfo { rect = new Rect(169f, 42f, 36f, 16f), N = RecipesButton, S = ExitButton, W = IngredientsGridAnchor, E = CauldronGridAnchor }, // Mix
            new SelectorButtonInfo { rect = new Rect(290f, 178f, 24f, 16f), N = MixButton, W = CauldronGridAnchor }, // Exit
        };

        public int buttonSelected = MixButton;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallPotionMakerWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallPotionMakerWindow menuWindow = top as DaggerfallPotionMakerWindow;

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
            parentPanel = null;
            cachedReflectionType = null;

            ingredientsGridColumn = 0;
            ingredientsGridRow = 0;
            cauldronGridColumn = 0;
            cauldronGridRow = 0;
            buttonSelected = MixButton;
        }
    }
}
