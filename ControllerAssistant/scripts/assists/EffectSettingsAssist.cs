using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class EffectSettingsAssist : IMenuAssist
    {
        private const bool debugMODE = false;
        private bool reflectionCached = false;
        private bool wasOpen = false;

        // Legend
        private FieldInfo fiPanelRenderWindow;
        private Panel panelRenderWindow;
        private LegendOverlay legend;
        private bool legendVisible = false;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private DefaultSelectorBoxHost selectorHost;
        private int buttonSelected = 0;
        private const bool skipUnavailableButtons = true;

        // Reflected controls for enabled-state checks
        private FieldInfo fiDurationBaseSpinner;
        private FieldInfo fiDurationPlusSpinner;
        private FieldInfo fiDurationPerLevelSpinner;
        private FieldInfo fiChanceBaseSpinner;
        private FieldInfo fiChancePlusSpinner;
        private FieldInfo fiChancePerLevelSpinner;
        private FieldInfo fiMagnitudeBaseMinSpinner;
        private FieldInfo fiMagnitudeBaseMaxSpinner;
        private FieldInfo fiMagnitudePlusMinSpinner;
        private FieldInfo fiMagnitudePlusMaxSpinner;
        private FieldInfo fiMagnitudePerLevelSpinner;

        private bool selectorInitializedThisOpen = false;
        private int selectorInitStableTicks = 0;
        private float selectorInitLastWidth = -1;
        private float selectorInitLastHeight = -1;

        private const int DurationBaseUp = 0;
        private const int DurationBaseDown = 1;
        private const int DurationPlusUp = 2;
        private const int DurationPlusDown = 3;
        private const int DurationPerLevelUp = 4;
        private const int DurationPerLevelDown = 5;

        private const int ChanceBaseUp = 6;
        private const int ChanceBaseDown = 7;
        private const int ChancePlusUp = 8;
        private const int ChancePlusDown = 9;
        private const int ChancePerLevelUp = 10;
        private const int ChancePerLevelDown = 11;

        private const int MagnitudeBaseMinUp = 12;
        private const int MagnitudeBaseMinDown = 13;
        private const int MagnitudeBaseMaxUp = 14;
        private const int MagnitudeBaseMaxDown = 15;
        private const int MagnitudePlusMinUp = 16;
        private const int MagnitudePlusMinDown = 17;
        private const int MagnitudePlusMaxUp = 18;
        private const int MagnitudePlusMaxDown = 19;
        private const int MagnitudePerLevelUp = 20;
        private const int MagnitudePerLevelDown = 21;

        private const int ExitButton = 22;

        // Native rects derived from DaggerfallEffectSettingsEditorWindow source.
        private readonly SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(64, 94, 24, 5) },   // DurationBaseUp
            new SelectorButtonInfo { rect = new Rect(64, 105, 24, 5) },  // DurationBaseDown
            new SelectorButtonInfo { rect = new Rect(104, 94, 24, 5) },  // DurationPlusUp
            new SelectorButtonInfo { rect = new Rect(104, 105, 24, 5) }, // DurationPlusDown
            new SelectorButtonInfo { rect = new Rect(160, 94, 24, 5) },  // DurationPerLevelUp
            new SelectorButtonInfo { rect = new Rect(160, 105, 24, 5) }, // DurationPerLevelDown

            new SelectorButtonInfo { rect = new Rect(64, 114, 24, 5) },  // ChanceBaseUp
            new SelectorButtonInfo { rect = new Rect(64, 125, 24, 5) },  // ChanceBaseDown
            new SelectorButtonInfo { rect = new Rect(104, 114, 24, 5) }, // ChancePlusUp
            new SelectorButtonInfo { rect = new Rect(104, 125, 24, 5) }, // ChancePlusDown
            new SelectorButtonInfo { rect = new Rect(160, 114, 24, 5) }, // ChancePerLevelUp
            new SelectorButtonInfo { rect = new Rect(160, 125, 24, 5) }, // ChancePerLevelDown

            new SelectorButtonInfo { rect = new Rect(64, 134, 24, 5) },  // MagnitudeBaseMinUp
            new SelectorButtonInfo { rect = new Rect(64, 145, 24, 5) },  // MagnitudeBaseMinDown
            new SelectorButtonInfo { rect = new Rect(104, 134, 24, 5) }, // MagnitudeBaseMaxUp
            new SelectorButtonInfo { rect = new Rect(104, 145, 24, 5) }, // MagnitudeBaseMaxDown
            new SelectorButtonInfo { rect = new Rect(144, 134, 24, 5) }, // MagnitudePlusMinUp
            new SelectorButtonInfo { rect = new Rect(144, 145, 24, 5) }, // MagnitudePlusMinDown
            new SelectorButtonInfo { rect = new Rect(184, 134, 24, 5) }, // MagnitudePlusMaxUp
            new SelectorButtonInfo { rect = new Rect(184, 145, 24, 5) }, // MagnitudePlusMaxDown
            new SelectorButtonInfo { rect = new Rect(235, 134, 24, 5) }, // MagnitudePerLevelUp
            new SelectorButtonInfo { rect = new Rect(235, 145, 24, 5) }, // MagnitudePerLevelDown

            new SelectorButtonInfo { rect = new Rect(281, 94, 24, 16) }, // ExitButton
        };

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallEffectSettingsEditorWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallEffectSettingsEditorWindow menuWindow = top as DaggerfallEffectSettingsEditorWindow;

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
            buttonSelected = 0;

            selectorInitializedThisOpen = false;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;
        }

        private void OnTickOpen(DaggerfallEffectSettingsEditorWindow menuWindow, ControllerManager cm)
        {
            RefreshLegendAttachment(menuWindow);
            RefreshSelectorAttachment(menuWindow);

            // One-time delayed selector attach for windows whose panel is not ready in OnOpened().
            if (!selectorInitializedThisOpen)
            {
                Panel currentPanel = GetCurrentRenderPanel(menuWindow);
                if (currentPanel != null)
                {
                    float w = currentPanel.Rectangle.width;
                    float h = currentPanel.Rectangle.height;

                    if (w > 0 && h > 0)
                    {
                        if (w == selectorInitLastWidth && h == selectorInitLastHeight)
                            selectorInitStableTicks++;
                        else
                            selectorInitStableTicks = 1;

                        selectorInitLastWidth = w;
                        selectorInitLastHeight = h;

                        if (selectorInitStableTicks >= 2)
                        {
                            RefreshSelectorToCurrentButton(menuWindow);
                            selectorInitializedThisOpen = true;
                        }
                    }
                }
            }

            if (selectorInitializedThisOpen)
            {
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
            }

            if (legend != null && legend.IsBuilt)
                legend.PositionBottomLeft();

            ControllerManager.StickDir8 dir =
                cm.RStickDir8Pressed != ControllerManager.StickDir8.None
                ? cm.RStickDir8Pressed
                : cm.RStickDir8HeldSlow;

            if (dir != ControllerManager.StickDir8.None)
            {
                TryMoveSelector(menuWindow, dir);
                return;
            }

            if (cm.Action1Released)
            {
                ActivateSelected(menuWindow);
                return;
            }

            if (cm.LegendPressed)
            {
                EnsureLegendUI(menuWindow, cm);
                legendVisible = !legendVisible;

                if (legend != null)
                    legend.SetEnabled(legendVisible);
            }

            if (cm.BackPressed)
            {
                DestroyLegend();
                return;
            }
        }

        private void OnOpened(DaggerfallEffectSettingsEditorWindow menuWindow, ControllerManager cm)
        {
            if (debugMODE)
                DumpWindowMembers(menuWindow);

            EnsureInitialized(menuWindow);
            buttonSelected = ExitButton;
            //if (!IsSelectable(menuWindow, buttonSelected))
            //    buttonSelected = FindFirstSelectable(menuWindow);

            selectorInitializedThisOpen = false;
            selectorInitStableTicks = 0;
            selectorInitLastWidth = -1;
            selectorInitLastHeight = -1;
        }

        private void OnClosed(ControllerManager cm)
        {
            ResetState();

            if (debugMODE)
                DaggerfallUI.AddHUDText("DaggerfallEffectSettingsEditorWindow closed");
        }

        private void EnsureInitialized(DaggerfallEffectSettingsEditorWindow menuWindow)
        {
            if (reflectionCached) return;
            if (menuWindow == null) return;

            var type = menuWindow.GetType();

            fiPanelRenderWindow = CacheField(type, "parentPanel");

            fiDurationBaseSpinner = CacheField(type, "durationBaseSpinner");
            fiDurationPlusSpinner = CacheField(type, "durationPlusSpinner");
            fiDurationPerLevelSpinner = CacheField(type, "durationPerLevelSpinner");
            fiChanceBaseSpinner = CacheField(type, "chanceBaseSpinner");
            fiChancePlusSpinner = CacheField(type, "chancePlusSpinner");
            fiChancePerLevelSpinner = CacheField(type, "chancePerLevelSpinner");
            fiMagnitudeBaseMinSpinner = CacheField(type, "magnitudeBaseMinSpinner");
            fiMagnitudeBaseMaxSpinner = CacheField(type, "magnitudeBaseMaxSpinner");
            fiMagnitudePlusMinSpinner = CacheField(type, "magnitudePlusMinSpinner");
            fiMagnitudePlusMaxSpinner = CacheField(type, "magnitudePlusMaxSpinner");
            fiMagnitudePerLevelSpinner = CacheField(type, "magnitudePerLevelSpinner");

            reflectionCached = true;

            if (debugMODE)
            {
                Debug.Log("[EffectSettingsAssist] durationBaseSpinner field found = " + (fiDurationBaseSpinner != null));
                Debug.Log("[EffectSettingsAssist] durationPlusSpinner field found = " + (fiDurationPlusSpinner != null));
                Debug.Log("[EffectSettingsAssist] chanceBaseSpinner field found = " + (fiChanceBaseSpinner != null));
                Debug.Log("[EffectSettingsAssist] magnitudeBaseMinSpinner field found = " + (fiMagnitudeBaseMinSpinner != null));
            }
        }

        private void TryMoveSelector(DaggerfallEffectSettingsEditorWindow menuWindow, ControllerManager.StickDir8 dir)
        {
            if (dir == ControllerManager.StickDir8.None)
                return;

            int previous = buttonSelected;
            int next = GetDirectionalTarget(previous, dir);

            if (next < 0)
                return;

            if (skipUnavailableButtons)
                next = ResolveSelectableTarget(menuWindow, next, dir);

            if (next < 0)
                return;

            buttonSelected = next;

            if (buttonSelected != previous)
                RefreshSelectorToCurrentButton(menuWindow);
        }

        private void ActivateSelected(DaggerfallEffectSettingsEditorWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            if (buttonSelected == ExitButton)
            {
                DestroyLegend();
                menuWindow.CloseWindow();
                return;
            }

            UpDownSpinner spinner = GetSpinnerForButton(menuWindow, buttonSelected);
            if (spinner == null || !spinner.Enabled)
                return;

            switch (buttonSelected)
            {
                case DurationBaseUp:
                case DurationPlusUp:
                case DurationPerLevelUp:
                case ChanceBaseUp:
                case ChancePlusUp:
                case ChancePerLevelUp:
                case MagnitudeBaseMinUp:
                case MagnitudeBaseMaxUp:
                case MagnitudePlusMinUp:
                case MagnitudePlusMaxUp:
                case MagnitudePerLevelUp:
                    StepSpinner(spinner, +1);
                    break;

                case DurationBaseDown:
                case DurationPlusDown:
                case DurationPerLevelDown:
                case ChanceBaseDown:
                case ChancePlusDown:
                case ChancePerLevelDown:
                case MagnitudeBaseMinDown:
                case MagnitudeBaseMaxDown:
                case MagnitudePlusMinDown:
                case MagnitudePlusMaxDown:
                case MagnitudePerLevelDown:
                    StepSpinner(spinner, -1);
                    break;
            }
        }

        private void StepSpinner(UpDownSpinner spinner, int delta)
        {
            if (spinner == null || delta == 0)
                return;

            spinner.Value += delta;
        }

        private int GetDirectionalTarget(int current, ControllerManager.StickDir8 dir)
        {
            switch (current)
            {
                case DurationBaseUp:
                    return TargetFromRect(dir,
                        MagnitudeBaseMinDown,  // N
                        MagnitudeBaseMaxDown,  // NE
                        DurationPlusUp,        // E
                        DurationPlusDown,      // SE
                        DurationBaseDown,      // S
                        -1,                    // SW
                        ExitButton,            // W
                        -1);                   // NW

                case DurationBaseDown:
                    return TargetFromRect(dir,
                        DurationBaseUp,        // N
                        -1,                    // NE
                        DurationPlusDown,      // E
                        -1,                    // SE
                        ChanceBaseUp,          // S
                        -1,                    // SW
                        ExitButton,            // W
                        -1);                   // NW

                case DurationPlusUp:
                    return TargetFromRect(dir,
                        MagnitudeBaseMaxDown,  // N
                        -1,                    // NE
                        DurationPerLevelUp,    // E
                        -1,                    // SE
                        DurationPlusDown,      // S
                        -1,                    // SW
                        DurationBaseUp,        // W
                        -1);                   // NW

                case DurationPlusDown:
                    return TargetFromRect(dir,
                        DurationPlusUp,        // N
                        -1,                    // NE
                        DurationPerLevelDown,  // E
                        -1,                    // SE
                        ChancePlusUp,          // S
                        -1,                    // SW
                        DurationBaseDown,      // W
                        -1);                   // NW

                case DurationPerLevelUp:
                    return TargetFromRect(dir,
                        MagnitudePlusMinDown,  // N
                        -1,                    // NE
                        ExitButton,            // E
                        -1,                    // SE
                        DurationPerLevelDown,  // S
                        -1,                    // SW
                        DurationPlusUp,        // W
                        -1);                   // NW

                case DurationPerLevelDown:
                    return TargetFromRect(dir,
                        DurationPerLevelUp,    // N
                        ExitButton,            // NE
                        ExitButton,            // E
                        -1,                    // SE
                        ChancePerLevelUp,      // S
                        -1,                    // SW
                        DurationPlusDown,      // W
                        -1);                   // NW

                case ChanceBaseUp:
                    return TargetFromRect(dir,
                        DurationBaseDown,      // N
                        -1,                    // NE
                        ChancePlusUp,          // E
                        -1,                    // SE
                        ChanceBaseDown,        // S
                        -1,                    // SW
                        ExitButton,            // W
                        -1);                   // NW

                case ChanceBaseDown:
                    return TargetFromRect(dir,
                        ChanceBaseUp,          // N
                        -1,                    // NE
                        ChancePlusDown,        // E
                        -1,                    // SE
                        MagnitudeBaseMinUp,    // S
                        -1,                    // SW
                        ExitButton,            // W
                        -1);                   // NW

                case ChancePlusUp:
                    return TargetFromRect(dir,
                        DurationPlusDown,        // N
                        -1,                    // NE
                        ChancePerLevelUp,      // E
                        -1,                    // SE
                        ChancePlusDown,        // S
                        -1,                    // SW
                        ChanceBaseUp,          // W
                        -1);                   // NW

                case ChancePlusDown:
                    return TargetFromRect(dir,
                        ChancePlusUp,          // N
                        -1,                    // NE
                        ChancePerLevelDown,    // E
                        MagnitudePlusMaxUp,    // SE
                        MagnitudeBaseMaxUp,    // S
                        MagnitudePlusMinUp,    // SW
                        ChanceBaseDown,        // W
                        -1);                   // NW

                case ChancePerLevelUp:
                    return TargetFromRect(dir,
                        DurationPerLevelUp,    // N
                        -1,                    // NE
                        ExitButton,            // E
                        -1,                    // SE
                        ChancePerLevelDown,    // S
                        -1,                    // SW
                        ChancePlusUp,          // W
                        -1);                   // NW

                case ChancePerLevelDown:
                    return TargetFromRect(dir,
                        ChancePerLevelUp,      // N
                        ExitButton,            // NE
                        ExitButton,            // E
                        -1,                    // SE
                        MagnitudePlusMinUp,    // S
                        -1,                    // SW
                        ChancePlusDown,        // W
                        -1);                   // NW

                case MagnitudeBaseMinUp:
                    return TargetFromRect(dir,
                        ChanceBaseDown,        // N
                        -1,                    // NE
                        MagnitudeBaseMaxUp,    // E
                        -1,                    // SE
                        MagnitudeBaseMinDown,  // S
                        -1,                    // SW
                        MagnitudePerLevelUp,   // W
                        -1);                   // NW

                case MagnitudeBaseMinDown:
                    return TargetFromRect(dir,
                        MagnitudeBaseMinUp,    // N
                        -1,                    // NE
                        MagnitudeBaseMaxDown,  // E
                        -1,                    // SE
                        DurationBaseUp,        // S
                        -1,                    // SW
                        MagnitudePerLevelDown, // W
                        -1);                   // NW

                case MagnitudeBaseMaxUp:
                    return TargetFromRect(dir,
                        ChancePlusDown,        // N
                        -1,                    // NE
                        MagnitudePlusMinUp,    // E
                        -1,                    // SE
                        MagnitudeBaseMaxDown,  // S
                        -1,                    // SW
                        MagnitudeBaseMinUp,    // W
                        -1);                   // NW

                case MagnitudeBaseMaxDown:
                    return TargetFromRect(dir,
                        MagnitudeBaseMaxUp,    // N
                        -1,                    // NE
                        MagnitudePlusMinDown,  // E
                        -1,                    // SE
                        DurationPlusUp,        // S
                        -1,                    // SW
                        MagnitudeBaseMinDown,  // W
                        -1);                   // NW

                case MagnitudePlusMinUp:
                    return TargetFromRect(dir,
                        ChancePerLevelDown,    // N
                        ChancePerLevelDown,    // NE
                        MagnitudePlusMaxUp,    // E
                        -1,                    // SE
                        MagnitudePlusMinDown,  // S
                        -1,                    // SW
                        MagnitudeBaseMaxUp,    // W
                        -1);                   // NW

                case MagnitudePlusMinDown:
                    return TargetFromRect(dir,
                        MagnitudePlusMinUp,    // N
                        -1,                    // NE
                        MagnitudePlusMaxDown,  // E
                        -1,                    // SE
                        DurationPerLevelUp,    // S
                        -1,                    // SW
                        MagnitudeBaseMaxDown,  // W
                        -1);                   // NW

                case MagnitudePlusMaxUp:
                    return TargetFromRect(dir,
                        ChancePerLevelDown,    // N
                        ExitButton,            // NE
                        MagnitudePerLevelUp,   // E
                        -1,                    // SE
                        MagnitudePlusMaxDown,  // S
                        -1,                    // SW
                        MagnitudePlusMinUp,    // W
                        ChancePerLevelDown);   // NW

                case MagnitudePlusMaxDown:
                    return TargetFromRect(dir,
                        MagnitudePlusMaxUp,    // N
                        -1,                    // NE
                        MagnitudePerLevelDown, // E
                        -1,                    // SE
                        DurationPerLevelUp,    // S
                        -1,                    // SW
                        MagnitudePlusMinDown,  // W
                        -1);                   // NW

                case MagnitudePerLevelUp:
                    return TargetFromRect(dir,
                        ChancePerLevelDown,    // N
                        ExitButton,            // NE
                        ExitButton,            // E
                        -1,                    // SE
                        MagnitudePerLevelDown, // S
                        -1,                    // SW
                        MagnitudePlusMaxUp,    // W
                        ChancePerLevelDown);   // NW

                case MagnitudePerLevelDown:
                    return TargetFromRect(dir,
                        MagnitudePerLevelUp,   // N
                        ExitButton,            // NE
                        ExitButton,            // E
                        -1,                    // SE
                        DurationPerLevelUp,    // S
                        -1,                    // SW
                        MagnitudePlusMaxDown,  // W
                        -1);                   // NW

                case ExitButton:
                    return TargetFromRect(dir,
                        -1,                    // N
                        -1,                    // NE
                        DurationBaseUp,        // E
                        -1,                    // SE
                        MagnitudePerLevelUp,   // S
                        MagnitudePerLevelUp,   // SW
                        DurationPerLevelDown,  // W
                        ChancePerLevelDown);   // NW
            }

            return -1;
        }

        private int TargetFromRect(
            ControllerManager.StickDir8 dir,
            int n, int ne, int e, int se, int s, int sw, int w, int nw)
        {
            switch (dir)
            {
                case ControllerManager.StickDir8.N: return n;
                case ControllerManager.StickDir8.NE: return ne;
                case ControllerManager.StickDir8.E: return e;
                case ControllerManager.StickDir8.SE: return se;
                case ControllerManager.StickDir8.S: return s;
                case ControllerManager.StickDir8.SW: return sw;
                case ControllerManager.StickDir8.W: return w;
                case ControllerManager.StickDir8.NW: return nw;
                default: return -1;
            }
        }

        private int ResolveSelectableTarget(DaggerfallEffectSettingsEditorWindow menuWindow, int target, ControllerManager.StickDir8 dir)
        {
            if (target < 0)
                return -1;

            if (IsSelectable(menuWindow, target))
                return target;

            // Special case:
            // From Exit, moving West should prefer the rightmost available section
            // without letting the generic walk bounce back to Exit.
            if (buttonSelected == ExitButton && dir == ControllerManager.StickDir8.W)
            {
                if (IsSelectable(menuWindow, DurationPerLevelDown))
                    return DurationPerLevelDown;

                if (IsSelectable(menuWindow, ChancePerLevelUp))
                    return ChancePerLevelUp;

                if (IsSelectable(menuWindow, MagnitudePerLevelDown))
                    return MagnitudePerLevelDown;

                return -1;
            }

            // Special case:
            // From Exit, moving East should prefer the leftmost available section
            // without changing the default graph.
            if (buttonSelected == ExitButton && dir == ControllerManager.StickDir8.E)
            {
                if (IsSelectable(menuWindow, DurationBaseUp))
                    return DurationBaseUp;

                if (IsSelectable(menuWindow, ChanceBaseUp))
                    return ChanceBaseUp;

                if (IsSelectable(menuWindow, MagnitudeBaseMinUp))
                    return MagnitudeBaseMinUp;

                return -1;
            }

            // Special case:
            // When moving north from the right-side Magnitude up buttons and Chance is unavailable,
            // prefer DurationPerLevelDown if Duration exists, otherwise pair within Magnitude.
            if (dir == ControllerManager.StickDir8.N)
            {
                if (buttonSelected == MagnitudePlusMaxUp)
                {
                    if (IsSelectable(menuWindow, ChancePerLevelDown))
                        return ChancePerLevelDown;

                    if (IsSelectable(menuWindow, DurationPerLevelDown))
                        return DurationPerLevelDown;

                    if (IsSelectable(menuWindow, MagnitudePlusMaxDown))
                        return MagnitudePlusMaxDown;

                    return -1;
                }

                if (buttonSelected == MagnitudePerLevelUp)
                {
                    if (IsSelectable(menuWindow, ChancePerLevelDown))
                        return ChancePerLevelDown;

                    if (IsSelectable(menuWindow, DurationPerLevelDown))
                        return DurationPerLevelDown;

                    if (IsSelectable(menuWindow, MagnitudePerLevelDown))
                        return MagnitudePerLevelDown;

                    return -1;
                }
            }

            // Special case:
            // When moving south from the right-side Magnitude down buttons and Chance is unavailable,
            // pair within Magnitude.
            if (dir == ControllerManager.StickDir8.S)
            {
                if (buttonSelected == MagnitudePlusMaxDown)
                {
                    if (IsSelectable(menuWindow, DurationPerLevelUp))
                        return DurationPerLevelUp;

                    if (IsSelectable(menuWindow, MagnitudePlusMaxUp))
                        return MagnitudePlusMaxUp;

                    return -1;
                }

                if (buttonSelected == MagnitudePerLevelDown)
                {
                    if (IsSelectable(menuWindow, DurationPerLevelUp))
                        return DurationPerLevelUp;

                    if (IsSelectable(menuWindow, MagnitudePerLevelUp))
                        return MagnitudePerLevelUp;

                    return -1;
                }
            }

            int current = target;
            for (int i = 0; i < 8; i++)
            {
                current = GetDirectionalTarget(current, dir);
                if (current < 0)
                    break;

                if (IsSelectable(menuWindow, current))
                    return current;
            }

            return -1;
        }

        private int FindFirstSelectable(DaggerfallEffectSettingsEditorWindow menuWindow)
        {
            for (int i = 0; i < menuButton.Length; i++)
            {
                if (IsSelectable(menuWindow, i))
                    return i;
            }

            return ExitButton;
        }

        private bool IsSelectable(DaggerfallEffectSettingsEditorWindow menuWindow, int button)
        {
            if (button == ExitButton)
                return true;

            UpDownSpinner spinner = GetSpinnerForButton(menuWindow, button);
            if (spinner == null)
                return false;

            return spinner.Enabled;
        }

        private UpDownSpinner GetSpinnerForButton(DaggerfallEffectSettingsEditorWindow menuWindow, int button)
        {
            FieldInfo fi = null;

            switch (button)
            {
                case DurationBaseUp:
                case DurationBaseDown:
                    fi = fiDurationBaseSpinner;
                    break;

                case DurationPlusUp:
                case DurationPlusDown:
                    fi = fiDurationPlusSpinner;
                    break;

                case DurationPerLevelUp:
                case DurationPerLevelDown:
                    fi = fiDurationPerLevelSpinner;
                    break;

                case ChanceBaseUp:
                case ChanceBaseDown:
                    fi = fiChanceBaseSpinner;
                    break;

                case ChancePlusUp:
                case ChancePlusDown:
                    fi = fiChancePlusSpinner;
                    break;

                case ChancePerLevelUp:
                case ChancePerLevelDown:
                    fi = fiChancePerLevelSpinner;
                    break;

                case MagnitudeBaseMinUp:
                case MagnitudeBaseMinDown:
                    fi = fiMagnitudeBaseMinSpinner;
                    break;

                case MagnitudeBaseMaxUp:
                case MagnitudeBaseMaxDown:
                    fi = fiMagnitudeBaseMaxSpinner;
                    break;

                case MagnitudePlusMinUp:
                case MagnitudePlusMinDown:
                    fi = fiMagnitudePlusMinSpinner;
                    break;

                case MagnitudePlusMaxUp:
                case MagnitudePlusMaxDown:
                    fi = fiMagnitudePlusMaxSpinner;
                    break;

                case MagnitudePerLevelUp:
                case MagnitudePerLevelDown:
                    fi = fiMagnitudePerLevelSpinner;
                    break;
            }

            if (fi == null || menuWindow == null)
                return null;

            return fi.GetValue(menuWindow) as UpDownSpinner;
        }

        private Panel GetCurrentRenderPanel(DaggerfallEffectSettingsEditorWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return null;

            return fiPanelRenderWindow.GetValue(menuWindow) as Panel;
        }

        private void RefreshSelectorToCurrentButton(DaggerfallEffectSettingsEditorWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.ShowAtNativeRect(
                currentPanel,
                menuButton[buttonSelected].rect,
                new Color(0.1f, 1f, 1f, 1f)
            );
        }

        private void RefreshSelectorAttachment(DaggerfallEffectSettingsEditorWindow menuWindow)
        {
            Panel currentPanel = GetCurrentRenderPanel(menuWindow);
            if (currentPanel == null)
                return;

            panelRenderWindow = currentPanel;

            if (selectorHost == null)
                selectorHost = new DefaultSelectorBoxHost();

            selectorHost.RefreshAttachment(currentPanel);
        }

        private void DestroySelectorBox()
        {
            if (selectorHost != null)
                selectorHost.Destroy();
        }

        private void EnsureLegendUI(DaggerfallEffectSettingsEditorWindow menuWindow, ControllerManager cm)
        {
            if (menuWindow == null) return;

            if (panelRenderWindow == null && fiPanelRenderWindow != null)
                panelRenderWindow = fiPanelRenderWindow.GetValue(menuWindow) as Panel;

            if (panelRenderWindow == null) return;

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

                List<LegendOverlay.LegendRow> rows = new List<LegendOverlay.LegendRow>()
                {
                    new LegendOverlay.LegendRow("Version", "7"),
                    new LegendOverlay.LegendRow("Right Stick", "move selector"),
                    new LegendOverlay.LegendRow(cm.Action1Name, "change value / exit"),
                };

                legend.Build("Legend", rows);
            }
        }

        private void RefreshLegendAttachment(DaggerfallEffectSettingsEditorWindow menuWindow)
        {
            if (menuWindow == null || fiPanelRenderWindow == null)
                return;

            Panel current = fiPanelRenderWindow.GetValue(menuWindow) as Panel;
            if (current == null)
                return;

            if (panelRenderWindow != current)
            {
                DestroyLegend();
                panelRenderWindow = current;
                legendVisible = false;
                return;
            }

            if (legend != null && !legend.IsAttached())
            {
                legendVisible = false;
                legend = null;
            }
        }

        private void DestroyLegend()
        {
            if (legend != null)
            {
                legend.Destroy();
                legend = null;
            }
        }

        private MethodInfo CacheMethod(System.Type type, string name)
        {
            MethodInfo mi = type.GetMethod(name, BF);
            if (mi == null)
                Debug.Log("[ControllerAssistant] Missing method: " + name);
            return mi;
        }

        private FieldInfo CacheField(System.Type type, string name)
        {
            FieldInfo fi = type.GetField(name, BF);
            if (fi == null)
                Debug.Log("[ControllerAssistant] Missing field: " + name);
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
    }
}
