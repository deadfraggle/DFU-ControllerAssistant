using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class SpellbookAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private bool reflectionCached = false;
        private bool wasOpen = false;
        private bool closeDeferred = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private bool lastLegendBuyMode = false;

        // Reflection cache
        private FieldInfo fiWindowBinding;
        private FieldInfo fiBuyMode;
        private FieldInfo fiSpellsListBox;

        private MethodInfo miActionMoveSpell;
        private FieldInfo fiUpButton;
        private FieldInfo fiDownButton;

        private MethodInfo miActionSort;
        private FieldInfo fiSortButton;

        private MethodInfo miActionDelete;
        private FieldInfo fiDeleteButton;

        private MethodInfo miActionBuy;
        private FieldInfo fiBuyButton;

        private MethodInfo miActionExit;
        private FieldInfo fiExitButton;

        private MethodInfo miActionIconPicker;
        private FieldInfo fiSpellIconPanel;

        private MethodInfo miActionEffectPanelClick;
        private FieldInfo fiSpellEffectPanels;
        private FieldInfo fiOfferedSpells;

        // Button & selector setup
        private DefaultSelectorBoxHost selectorHost;

        const int IconButton = 0;
        const int SpellList = 1;
        const int DeleteButton = 2;
        const int UpButton = 3;
        const int SortButton = 4;
        const int DownButton = 5;
        const int ExitusButton = 6;

        const int Effect1 = 7;
        const int Effect2 = 8;
        const int Effect3 = 9;

        const int BuySpellList = 10;
        const int BuyButton = 11;
        const int ExitButton = 12;
        const int BuyEffect1 = 13;
        const int BuyEffect2 = 14;
        const int BuyEffect3 = 15;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(176.6f, 28.7f, 22.2f, 22.5f), N = -1, E = -1, S = Effect1, W = SpellList }, // IconButton
            new SelectorButtonInfo { rect = new Rect(34.6f, 25.6f, 114.2f, 142.8f), N = -1, E = IconButton, S = DeleteButton, W = ExitusButton }, // SpellList
            new SelectorButtonInfo { rect = new Rect(31.5f, 167.9f, 43.0f, 13.1f), N = SpellList, E = UpButton, S = -1, W = -1 }, // DeleteButton
            new SelectorButtonInfo { rect = new Rect(76.6f, 167.9f, 43.0f, 13.1f), N = SpellList, E = SortButton, S = -1, W = DeleteButton }, // UpButton
            new SelectorButtonInfo { rect = new Rect(118.0f, 167.9f, 43.0f, 13.1f), N = SpellList, E = DownButton, S = -1, W = UpButton }, // SortButton
            new SelectorButtonInfo { rect = new Rect(159.6f, 167.9f, 43.0f, 13.1f), N = Effect3, E = ExitusButton, S = -1, W = SortButton }, // DownButton
            new SelectorButtonInfo { rect = new Rect(245.8f, 166.5f, 44.2f, 15.8f), N = Effect3, E = -1, S = -1, W = DownButton }, // ExitusButton
            new SelectorButtonInfo { rect = new Rect(167.8f, 57.4f, 118.3f, 29.2f), N = IconButton, E = -1, S = Effect2, W = SpellList }, // Effect1
            new SelectorButtonInfo { rect = new Rect(167.8f, 95.3f, 118.3f, 29.2f), N = Effect1, E = -1, S =  Effect3, W = SpellList }, // Effect2
            new SelectorButtonInfo { rect = new Rect(167.8f, 133.2f, 118.3f, 29.2f), N = Effect2, E = -1, S = ExitusButton, W = SpellList }, // Effect3

            new SelectorButtonInfo { rect = new Rect(33.7f, 29.0f, 113.6f, 133.5f), N = -1, E = BuyEffect1, S = BuyButton, W = ExitButton }, // BuySpellList
            new SelectorButtonInfo { rect = new Rect(30.7f, 166.7f, 43.0f, 15.2f), N = BuySpellList, E = ExitButton, S = -1, W = -1 }, // BuyButton
            new SelectorButtonInfo { rect = new Rect(246.7f, 166.7f, 43.0f, 15.2f), N = BuyEffect3, E = -1, S = -1, W = BuyButton }, // ExitButton
            new SelectorButtonInfo { rect = new Rect(167.8f, 57.4f, 118.3f, 29.2f), N = -1, E = -1, S = BuyEffect2, W = BuySpellList }, // BuyEffect1
            new SelectorButtonInfo { rect = new Rect(167.8f, 95.3f, 118.3f, 29.2f), N = BuyEffect1, E = -1, S = BuyEffect3, W = BuySpellList }, // BuyEffect2
            new SelectorButtonInfo { rect = new Rect(167.8f, 133.2f, 118.3f, 29.2f), N = BuyEffect2, E = -1, S = ExitButton, W = BuySpellList }, // BuyEffect3
        };


        private int selectorIndex = SpellList;
        private AnchorEditor editor;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallSpellBookWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallSpellBookWindow menuWindow = top as DaggerfallSpellBookWindow;

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
            closeDeferred = false;
            selectorIndex = SpellList;

            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }

            if (selectorHost != null)
                selectorHost.Destroy();

            legendVisible = false;
            panelRenderWindow = null;
        }

        private void OnTickOpen(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
        {
            KeyCode windowBinding = InputManager.Instance.GetBinding(InputManager.Actions.CastSpell);

            RefreshLegendAttachment(menuWindow);

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            if (fiWindowBinding != null)
                fiWindowBinding.SetValue(menuWindow, KeyCode.None);

            bool buyMode = IsBuyMode(menuWindow);

            // Anchor Editor
            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow != null)
                editor.Tick(panelRenderWindow);

            if (buyMode)
                TickBuying(menuWindow, cm);
            else
                TickCasting(menuWindow, cm);

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;
                if (legend != null)
                    legend.SetEnabled(legendVisible);
            }

            if (cm.BackPressed)
            {
                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                if (selectorHost != null)
                    selectorHost.Destroy();

                return;
            }

            bool isAssisting =
                (cm.DPadUpPressed || cm.DPadDownPressed ||
                 cm.RStickUpPressed || cm.RStickDownPressed ||
                 cm.RStickLeftPressed || cm.RStickRightPressed ||
                 cm.RStickUpHeldSlow || cm.RStickDownHeldSlow ||
                 cm.Action1Pressed || cm.Action2Pressed || cm.LegendPressed);

            if (!isAssisting && InputManager.Instance.GetKeyDown(windowBinding))
                closeDeferred = true;

            if (closeDeferred && InputManager.Instance.GetKeyUp(windowBinding))
            {
                closeDeferred = false;

                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                if (selectorHost != null)
                    selectorHost.Destroy();

                menuWindow.CloseWindow();
                return;
            }
        }

        private void OnOpened(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE) DumpWindowMembers(menuWindow);
            EnsureInitialized(menuWindow);

            selectorIndex = IsBuyMode(menuWindow) ? BuySpellList : SpellList;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            if (editor == null)
            {
                // Match Inventory's default selector size: 25 x 19 native-ish feel
                editor = new AnchorEditor(25f, 19f);
            }
            
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();
            if (debugMODE) DaggerfallUI.AddHUDText("DaggerfallSpellBookWindow closed");
        }

        private void ActionMoveSpellUp(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeMoveSpell(menuWindow, fiUpButton);
        }

        private void ActionMoveSpellDown(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeMoveSpell(menuWindow, fiDownButton);
        }

        private void InvokeMoveSpell(DaggerfallSpellBookWindow menuWindow, FieldInfo buttonField)
        {
            if (menuWindow == null || miActionMoveSpell == null || buttonField == null)
                return;

            object sender = buttonField.GetValue(menuWindow);
            object[] args = new object[]
            {
                sender,
                Vector2.zero,
            };

            miActionMoveSpell.Invoke(menuWindow, args);
        }

        private void ActionSort(DaggerfallSpellBookWindow menuWindow)
        {
            if (menuWindow == null || miActionSort == null)
                return;

            object sender = fiSortButton != null ? fiSortButton.GetValue(menuWindow) : null;

            object[] args = new object[]
            {
                sender,
                Vector2.zero,
            };

            miActionSort.Invoke(menuWindow, args);
        }

        private void EnsureInitialized(DaggerfallSpellBookWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiWindowBinding = CacheField(type, "toggleClosedBinding");
            fiBuyMode = CacheField(type, "buyMode");
            fiSpellsListBox = CacheField(type, "spellsListBox");

            miActionMoveSpell = CacheMethod(type, "SwapButton_OnMouseClick");
            fiUpButton = CacheField(type, "upButton");
            fiDownButton = CacheField(type, "downButton");

            miActionSort = CacheMethod(type, "SortButton_OnMouseClick");
            fiSortButton = CacheField(type, "sortButton");

            miActionDelete = CacheMethod(type, "DeleteButton_OnMouseClick");
            fiDeleteButton = CacheField(type, "deleteButton");

            miActionBuy = CacheMethod(type, "BuyButton_OnMouseClick");
            fiBuyButton = CacheField(type, "buyButton");

            miActionExit = CacheMethod(type, "ExitButton_OnMouseClick");
            fiExitButton = CacheField(type, "exitButton");

            miActionIconPicker = CacheMethod(type, "SpellIconPanel_OnMouseClick");
            fiSpellIconPanel = CacheField(type, "spellIconPanel");

            miActionEffectPanelClick = CacheMethod(type, "SpellEffectPanelClick");
            fiSpellEffectPanels = CacheField(type, "spellEffectPanels");
            fiOfferedSpells = CacheField(type, "offeredSpells");

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            reflectionCached = true;
        }

        private void EnsureLegendUI(DaggerfallSpellBookWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            bool buyMode = IsBuyMode(menuWindow);

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

            if (legend != null && lastLegendBuyMode != buyMode)
            {
                legend.Destroy();
                legend = null;
                legendVisible = false;
            }

            if (legend == null)
            {
                legend = new LegendOverlay(panelRenderWindow);

                legend.HeaderScale = 6.0f;
                legend.RowScale = 5.0f;
                legend.PadL = 18f;
                legend.PadT = 16f;
                legend.LineGap = 36f;
                legend.ColGap = 22f;
                legend.MarginX = 8f;
                legend.MarginFromBottom = 24f;
                legend.BackgroundColor = new Color(0f, 0f, 0f, 0.60f);

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>();

                if (!buyMode)
                {
                    rows.Add(new LegendOverlay.LegendRow("Version", "2.5"));
                    rows.Add(new LegendOverlay.LegendRow("Right Stick", "Move selector"));
                    rows.Add(new LegendOverlay.LegendRow("D-Pad Up", "Move spell up"));
                    rows.Add(new LegendOverlay.LegendRow("D-Pad Down", "Move spell down"));
                    rows.Add(new LegendOverlay.LegendRow(cm.Action1Name, "Activate"));
                    rows.Add(new LegendOverlay.LegendRow(cm.Action2Name, "Sort"));
                }
                else
                {
                    rows.Add(new LegendOverlay.LegendRow("Version", "2.5"));
                    rows.Add(new LegendOverlay.LegendRow("Right Stick", "Move selector"));
                    rows.Add(new LegendOverlay.LegendRow(cm.Action1Name, "Activate"));
                }

                legend.Build("Legend", rows);
                lastLegendBuyMode = buyMode;
            }
        }

        private void RefreshLegendAttachment(DaggerfallSpellBookWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                panelRenderWindow = current;
                legendVisible = false;

                if (legend != null)
                {
                    legend.Destroy();
                    legend = null;
                }

                if (selectorHost != null)
                    selectorHost.RefreshAttachment(current);

                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }

            if (selectorHost != null)
                selectorHost.RefreshAttachment(current);
        }
        private bool IsBuyMode(DaggerfallSpellBookWindow menuWindow)
        {
            if (menuWindow == null || fiBuyMode == null)
                return false;

            object value = fiBuyMode.GetValue(menuWindow);
            return value is bool && (bool)value;
        }

        private ListBox GetSpellsListBox(DaggerfallSpellBookWindow menuWindow)
        {
            if (menuWindow == null || fiSpellsListBox == null)
                return null;

            return fiSpellsListBox.GetValue(menuWindow) as ListBox;
        }

        private bool CanMoveListUp(DaggerfallSpellBookWindow menuWindow)
        {
            ListBox list = GetSpellsListBox(menuWindow);
            if (list == null || list.Count <= 0)
                return false;

            return list.SelectedIndex > 0;
        }

        private bool CanMoveListDown(DaggerfallSpellBookWindow menuWindow)
        {
            ListBox list = GetSpellsListBox(menuWindow);
            if (list == null || list.Count <= 0)
                return false;

            return list.SelectedIndex >= 0 && list.SelectedIndex < list.Count - 1;
        }

        private void SelectListUp()
        {
            TapKey(KeyCode.UpArrow);
        }

        private void SelectListDown()
        {
            TapKey(KeyCode.DownArrow);
        }

        private void InvokeClick(MethodInfo method, DaggerfallSpellBookWindow menuWindow, FieldInfo buttonField)
        {
            if (method == null || menuWindow == null)
                return;

            object sender = buttonField != null ? buttonField.GetValue(menuWindow) : null;
            object[] args = new object[]
            {
                sender,
                Vector2.zero,
            };

            method.Invoke(menuWindow, args);
        }

        private void ActivateExit(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeClick(miActionExit, menuWindow, fiExitButton);
        }

        private void ActivateDelete(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeClick(miActionDelete, menuWindow, fiDeleteButton);
        }

        private void ActivateBuy(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeClick(miActionBuy, menuWindow, fiBuyButton);
        }

        private void ActivateIconPicker(DaggerfallSpellBookWindow menuWindow)
        {
            InvokeClick(miActionIconPicker, menuWindow, fiSpellIconPanel);
        }

        private bool GetSelectedSpellSettings(DaggerfallSpellBookWindow menuWindow, out EffectBundleSettings spellSettings)
        {
            spellSettings = default(EffectBundleSettings);

            if (menuWindow == null)
                return false;

            ListBox list = GetSpellsListBox(menuWindow);
            if (list == null || list.SelectedIndex < 0)
                return false;

            if (IsBuyMode(menuWindow))
            {
                if (fiOfferedSpells == null)
                    return false;

                List<EffectBundleSettings> offered = fiOfferedSpells.GetValue(menuWindow) as List<EffectBundleSettings>;
                if (offered == null || list.SelectedIndex >= offered.Count)
                    return false;

                spellSettings = offered[list.SelectedIndex];
                return true;
            }
            else
            {
                return GameManager.Instance.PlayerEntity.GetSpell(list.SelectedIndex, out spellSettings);
            }
        }

        private int GetEffectCount(DaggerfallSpellBookWindow menuWindow)
        {
            EffectBundleSettings spellSettings;
            if (!GetSelectedSpellSettings(menuWindow, out spellSettings))
                return 0;

            if (spellSettings.Effects == null)
                return 0;

            return spellSettings.Effects.Length;
        }

        private bool EffectSlotExists(DaggerfallSpellBookWindow menuWindow, int zeroBasedSlot)
        {
            return GetEffectCount(menuWindow) > zeroBasedSlot;
        }

        private bool IsCastingEffectButton(int index)
        {
            return index == Effect1 || index == Effect2 || index == Effect3;
        }

        private bool IsBuyingEffectButton(int index)
        {
            return index == BuyEffect1 || index == BuyEffect2 || index == BuyEffect3;
        }

        private int GetFirstCastingEffectOrFallback(DaggerfallSpellBookWindow menuWindow, int fallback)
        {
            if (EffectSlotExists(menuWindow, 0)) return Effect1;
            if (EffectSlotExists(menuWindow, 1)) return Effect2;
            if (EffectSlotExists(menuWindow, 2)) return Effect3;
            return fallback;
        }

        private int GetLastCastingEffectOrFallback(DaggerfallSpellBookWindow menuWindow, int fallback)
        {
            if (EffectSlotExists(menuWindow, 2)) return Effect3;
            if (EffectSlotExists(menuWindow, 1)) return Effect2;
            if (EffectSlotExists(menuWindow, 0)) return Effect1;
            return fallback;
        }

        private int GetFirstBuyingEffectOrFallback(DaggerfallSpellBookWindow menuWindow, int fallback)
        {
            if (EffectSlotExists(menuWindow, 0)) return BuyEffect1;
            if (EffectSlotExists(menuWindow, 1)) return BuyEffect2;
            if (EffectSlotExists(menuWindow, 2)) return BuyEffect3;
            return fallback;
        }

        private int GetLastBuyingEffectOrFallback(DaggerfallSpellBookWindow menuWindow, int fallback)
        {
            if (EffectSlotExists(menuWindow, 2)) return BuyEffect3;
            if (EffectSlotExists(menuWindow, 1)) return BuyEffect2;
            if (EffectSlotExists(menuWindow, 0)) return BuyEffect1;
            return fallback;
        }

        private int ResolveCastingTarget(DaggerfallSpellBookWindow menuWindow, int current, int next)
        {
            if (next < 0)
                return -1;

            if (current == IconButton && next == Effect1)
                return GetFirstCastingEffectOrFallback(menuWindow, ExitusButton);

            if ((current == DownButton || current == ExitusButton) && next == menuButton[current].N)
                return GetLastCastingEffectOrFallback(menuWindow, IconButton);

            if (current == Effect1 && next == Effect2 && !EffectSlotExists(menuWindow, 1))
                return ExitusButton;

            if (current == Effect2 && next == Effect3 && !EffectSlotExists(menuWindow, 2))
                return ExitusButton;

            if (current == Effect3 && next == Effect2 && !EffectSlotExists(menuWindow, 1))
                return Effect1;

            if (current == Effect2 && next == Effect1 && !EffectSlotExists(menuWindow, 0))
                return -1;

            if (IsCastingEffectButton(next))
            {
                if (next == Effect1 && !EffectSlotExists(menuWindow, 0))
                    return -1;
                if (next == Effect2 && !EffectSlotExists(menuWindow, 1))
                    return EffectSlotExists(menuWindow, 0) ? Effect1 : -1;
                if (next == Effect3 && !EffectSlotExists(menuWindow, 2))
                    return EffectSlotExists(menuWindow, 1) ? Effect2 :
                           EffectSlotExists(menuWindow, 0) ? Effect1 : -1;
            }

            return next;
        }

        private int ResolveBuyingTarget(DaggerfallSpellBookWindow menuWindow, int current, int next)
        {
            if (next < 0)
                return -1;

            if (current == BuySpellList && next == BuyEffect1)
                return GetFirstBuyingEffectOrFallback(menuWindow, ExitButton);

            if (current == ExitButton && next == BuyEffect3)
                return GetLastBuyingEffectOrFallback(menuWindow, -1);

            if (current == BuyEffect1 && next == BuyEffect2 && !EffectSlotExists(menuWindow, 1))
                return ExitButton;

            if (current == BuyEffect2 && next == BuyEffect3 && !EffectSlotExists(menuWindow, 2))
                return ExitButton;

            if (current == BuyEffect3 && next == BuyEffect2 && !EffectSlotExists(menuWindow, 1))
                return BuyEffect1;

            if (IsBuyingEffectButton(next))
            {
                if (next == BuyEffect1 && !EffectSlotExists(menuWindow, 0))
                    return -1;
                if (next == BuyEffect2 && !EffectSlotExists(menuWindow, 1))
                    return EffectSlotExists(menuWindow, 0) ? BuyEffect1 : -1;
                if (next == BuyEffect3 && !EffectSlotExists(menuWindow, 2))
                    return EffectSlotExists(menuWindow, 1) ? BuyEffect2 :
                           EffectSlotExists(menuWindow, 0) ? BuyEffect1 : -1;
            }

            return next;
        }

        private void ActivateEffectPanel(DaggerfallSpellBookWindow menuWindow, int zeroBasedSlot)
        {
            if (!EffectSlotExists(menuWindow, zeroBasedSlot))
                return;

            if (miActionEffectPanelClick == null || fiSpellEffectPanels == null)
                return;

            Panel[] panels = fiSpellEffectPanels.GetValue(menuWindow) as Panel[];
            if (panels == null || zeroBasedSlot < 0 || zeroBasedSlot >= panels.Length)
                return;

            object[] args = new object[]
            {
                panels[zeroBasedSlot],
                Vector2.zero,
            };

            miActionEffectPanelClick.Invoke(menuWindow, args);
        }

        private void RefreshSelectorToCurrentRegion(DaggerfallSpellBookWindow menuWindow)
        {
            if (panelRenderWindow == null || selectorHost == null)
                return;

            if (selectorIndex < 0 || selectorIndex >= menuButton.Length)
                return;

            Color borderColor = new Color(0.10f, 1.00f, 1.00f, 1.00f);
            float scaleX = panelRenderWindow.Size.x / 320f;
            float scaleY = panelRenderWindow.Size.y / 200f;
            float suggestedThickness = Mathf.Max(2f, Mathf.Min(scaleX, scaleY) * 0.5f);
            float borderThickness = suggestedThickness;

            bool softenSpellList =
                !IsBuyMode(menuWindow) && selectorIndex == SpellList;

            if (softenSpellList)
            {
                borderThickness *= 0.5f;
                borderColor.a = 0.5f;
            }

            selectorHost.ShowAtNativeRect(
                panelRenderWindow,
                menuButton[selectorIndex].rect,
                borderThickness,
                borderColor);
        }

        private void MoveSelector(int nextIndex)
        {
            if (nextIndex < 0 || nextIndex >= menuButton.Length)
                return;

            selectorIndex = nextIndex;
        }

        private static void TapKey(KeyCode key)
        {
            DaggerfallUI.Instance.OnKeyPress(key, true);
            DaggerfallUI.Instance.OnKeyPress(key, false);
        }

        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null && debugMODE)
                Debug.Log("[ControllerAssistant] Missing method: " + name + " on " + type.Name);
            return mi;
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null && debugMODE)
                Debug.Log("[ControllerAssistant] Missing field: " + name + " on " + type.Name);
            return fi;
        }

        private void DumpWindowMembers(object window)
        {
            var type = window.GetType();

            Debug.Log("===== METHODS =====");
            foreach (var m in type.GetMethods(BF))
                Debug.Log(m.Name);

            Debug.Log("===== FIELDS =====");
            foreach (var f in type.GetFields(BF))
                Debug.Log(f.Name);
        }
        internal void ToggleAnchorEditor()
        {
            if (editor != null)
                editor.Toggle();
        }
    }
}