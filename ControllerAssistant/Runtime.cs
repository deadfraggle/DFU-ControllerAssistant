using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// ControllerAssistant routes controller input to context-specific “assist modules” based on the current DFU TopWindow.
// Only one assist owns input per frame. 
// Assists are responsible for detecting open/close edges and resetting state when released.

/*
===============================================================================
Controller Assistant – IMPORTANT ARCHITECTURE NOTE
===============================================================================

DO NOT refactor the assist modules back into a shared base class such as
MenuAssistModule<T> (deprecated) or similar lifecycle wrappers.

This was attempted during development and caused a compatibility conflict
with older DFU mods, most notably "Convenient Clock".

Problem:
--------
The MenuAssistModule<T> pattern centralized assist lifecycle logic
(OnOpened / OnTickOpen / OnClosed) through a shared generic base class.

Although functionally correct, this altered how DFU’s mod loader compiled
and invoked assist modules. Older mods that rely on DFU’s legacy runtime
assumptions (like Convenient Clock) would fail compilation and silently
disable themselves.

Symptom observed:
-----------------
Convenient Clock disappeared from the HUD and logged:

    [Convenient Clock] Compilation Error: Specified method is not supported

The failure occurred even when the assist modules were idle and no menus
were open.

Resolution:
-----------
All assist modules were converted to implement IMenuAssist directly.

Each module now explicitly provides:

    bool Claims(IUserInterfaceWindow top)
    void Tick(IUserInterfaceWindow top, ControllerManager cm)
    void ResetState()

The lifecycle (open detection, close detection, wasOpen state) is handled
inside each assist module instead of via a shared base class.

Why this must remain:
---------------------
Keeping modules as direct IMenuAssist implementations preserves compatibility
with legacy DFU mods and avoids triggering the mod loader behavior that
breaks older compiled mods.

The duplication across assists is intentional and acceptable.

If code reuse is desired in the future, use:
    - static helper utilities
    - small composition helpers

Do NOT introduce inheritance-based lifecycle wrappers again.

Short version:
--------------
Explicit modules = compatible with legacy mods.
Shared base class = breaks legacy mods.

Leave the assists as they are.
===============================================================================
*/

namespace gigantibyte.DFU.ControllerAssistant
{
    public class Runtime
    {
        static Mod my_mod;
        private static ModSettings settings;

        private static IMenuAssist current = null;
        private static ControllerManager cm;

        // ⚠️ IMPORTANT FOR AI / CODE REVIEWERS
        // Do NOT refactor assist modules into a shared base class (e.g., MenuAssistModule<T>).
        // This was previously attempted and broke compatibility with legacy DFU mods
        // such as "Convenient Clock". All assists must implement IMenuAssist directly.
        // The duplicated lifecycle code is intentional and required for mod compatibility.

        private static readonly SpellbookAssist spellbook = new SpellbookAssist();
        private static readonly ExtAutomapAssist ExtAutomap = new ExtAutomapAssist();
        private static readonly DefaultMenuAssist defaults = new DefaultMenuAssist();
        private static readonly InteriorAssist IntAutomap = new InteriorAssist();
        private static readonly InventoryAssist inventory = new InventoryAssist();
        private static readonly InputMessageBoxAssist inputbox = new InputMessageBoxAssist();
        private static readonly MessageBoxAssist messagebox = new MessageBoxAssist();
        private static readonly TransportAssist transport = new TransportAssist();
        private static readonly CAFavoritesWindowAssist favorites = new CAFavoritesWindowAssist();
        private static readonly PauseAssist pause = new PauseAssist();
        private static readonly TalkWindowAssist talk = new TalkWindowAssist();
        private static readonly SpellMakerAssist spellmaker = new SpellMakerAssist();
        private static readonly ListPickerAssist listpicker = new ListPickerAssist();
        private static readonly EffectSettingsAssist effectsettings = new EffectSettingsAssist();
        private static readonly SpellIconPickerAssist spelliconpicker = new SpellIconPickerAssist();
        private static readonly TravelMapAssist travelmap = new TravelMapAssist();
        private static readonly TravelPopUpAssist travelpopup = new TravelPopUpAssist();
        private static readonly RestAssist rest = new RestAssist();
        private static readonly CharacterSheetAssist character = new CharacterSheetAssist();
        private static readonly ItemMakerAssist itemmaker = new ItemMakerAssist();

        // Specialized modules (do NOT include defaults here)
        private static readonly IMenuAssist[] assists =
        {
            spellbook,
            ExtAutomap,
            IntAutomap,
            inventory,
            inputbox,
            messagebox,
            transport,
            favorites,
            pause,
            talk,
            spellmaker,
            listpicker,
            effectsettings,
            spelliconpicker,
            travelmap,
            travelpopup,
            rest,
            character,
            itemmaker,
        };

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void EarlyInit(InitParams initParams)
        {
            my_mod = initParams.Mod;
            my_mod.SaveDataInterface = FavoritesStore.Instance;
            my_mod.LoadSettingsCallback = LoadSettings;
            settings = my_mod.GetSettings();
        }

        [Invoke(StateManager.StateTypes.Game, 0)]
        public static void GameInit(InitParams initParams)
        {
            Debug.Log("Controller Assistant mod initialized!");
            cm = new ControllerManager();

            GameObject go = new GameObject("ControllerAssistant_Listener");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ControllerListener>();
        }

        private class ControllerListener : MonoBehaviour
        {
            private string lastWindowName = null;
            private string lastAssistName = null;

            void Update()
            {
                if (GameManager.Instance == null)
                    return;

                // Get assist keys once per frame (used by both cm and conflict detection)
                KeyCode a1 = GetAction1KeyCode();
                KeyCode a2 = GetAction2KeyCode();
                KeyCode lg = GetLegendKeyCode();

                // Poll DFU keybind file for:
                // 1) Forbidden DFU actions on DPad
                // 2) Assist keys conflicting with DFU actions
                ControllerAssistConflictDetector.Tick(2.0f, a1, a2, lg);

                var uiManager = DaggerfallUI.Instance != null ? DaggerfallUI.Instance.UserInterfaceManager : null;
                IUserInterfaceWindow top = uiManager != null ? uiManager.TopWindow : null;
                if (top == null)
                    return;

                // --- Diagnostic: print TopWindow only when it changes ---
                string currentName = top.GetType().FullName;
                if (currentName != lastWindowName)
                {
                    Debug.Log("TopWindow = " + currentName);
                    lastWindowName = currentName;
                }

                // HUD means "no menu open" for our purposes
                if (top is DaggerfallHUD)
                {
                    // Leaving menu system entirely → release active controller once
                    if (current != null)
                        current.ResetState();

                    current = null;
                    return;
                }

                // Find first assist that claims this TopWindow
                IMenuAssist active = null;
                for (int i = 0; i < assists.Length; i++)
                {
                    IMenuAssist a = assists[i];
                    if (a.Claims(top))
                    {
                        active = a;
                        break;
                    }
                }

                // Decide who should run this frame
                IMenuAssist next = active != null ? active : (IMenuAssist)defaults;

                string assistName = next.GetType().Name;
                if (assistName != lastAssistName)
                {
                    Debug.Log("Assist = " + assistName);
                    lastAssistName = assistName;
                }

                // If we switched controllers, reset the one we’re leaving ONCE
                if (!ReferenceEquals(next, current))
                {
                    if (current != null)
                        current.ResetState();

                    // If switching into a specialized assist, also reset defaults once (releases held keys)
                    if (active != null)
                        defaults.ResetState();

                    // If switching into defaults, reset all specialized assists once
                    if (active == null)
                    {
                        for (int i = 0; i < assists.Length; i++)
                            assists[i].ResetState();
                    }

                    current = next;
                }

                // Tick only the selected controller
                cm.SetAction1Key(a1);
                cm.SetAction2Key(a2);
                cm.SetLegendKey(lg);
                cm.Update();
                current.Tick(top, cm);
            }
        }

        private static void LoadSettings(ModSettings modSettings, ModSettingsChange change)
        {
            settings = modSettings;
            Debug.Log("Controller Assistant settings loaded. Change=" + change);
        }

        private static KeyCode GetAction1KeyCode()
        {
            // If settings haven't loaded yet, default to R3.
            if (settings == null)
                return KeyCode.JoystickButton9;

            int idx = settings.GetValue<int>("Controls", "AssistAction1");
            return MapChoiceIndexToKeyCode(idx);
        }

        private static KeyCode GetAction2KeyCode()
        {
            // If settings haven't loaded yet, default to L3.
            if (settings == null)
                return KeyCode.JoystickButton8;

            int idx = settings.GetValue<int>("Controls", "AssistAction2");
            return MapChoiceIndexToKeyCode(idx);
        }

        private static KeyCode GetLegendKeyCode()
        {
            // If settings haven't loaded yet, default to LB.
            if (settings == null)
                return KeyCode.JoystickButton4;

            int idx = settings.GetValue<int>("Controls", "AssistLegend");
            return MapChoiceIndexToKeyCode(idx);
        }

        // Options from modsettings.json (0-based index):
        // 0 A, 1 B, 2 X, 3 Y, 4 LB, 5 RB, 6 L3, 7 R3
        private static KeyCode MapChoiceIndexToKeyCode(int idx)
        {
            if (idx < 0 || idx > 7)
                return KeyCode.None;

            switch (idx)
            {
                case 0: return KeyCode.JoystickButton0; // A
                case 1: return KeyCode.JoystickButton1; // B
                case 2: return KeyCode.JoystickButton2; // X
                case 3: return KeyCode.JoystickButton3; // Y
                case 4: return KeyCode.JoystickButton4; // LB
                case 5: return KeyCode.JoystickButton5; // RB
                case 6: return KeyCode.JoystickButton8; // L3
                case 7: return KeyCode.JoystickButton9; // R3
                default: return KeyCode.None;
            }
        }

        internal static class ControllerAssistConflictDetector
        {
            // DPad "button-like" keycodes DFU writes into KeyBinds.txt
            private static readonly string[] DPadKeyNames =
            {
                "JoystickAxis6Button0", // Left
                "JoystickAxis6Button1", // Right
                "JoystickAxis7Button0", // Up
                "JoystickAxis7Button1", // Down
            };

            // DFU actions that MUST NOT be on the DPad,
            // and are also the ones we compare assist-keys against.
            private static readonly string[] ForbiddenActionOrder =
            {
                "Escape",
                "Back",
                "LeftClick",
                "RightClick",
            };

            private static readonly HashSet<string> ForbiddenActions = new HashSet<string>
            {
                "Escape",
                "Back",
                "LeftClick",
                "RightClick",
            };

            private static float nextPollTime = 0f;
            private static DateTime lastWriteUtc = DateTime.MinValue;

            // Separate signatures so messages are independent and non-spammy
            private static string lastDpadSignature = null;
            private static string lastAssistSignature = null;

            private static string lastAssistKeysSignature = null;
            private static Dictionary<string, string> cachedActionToKey = null;

            /// <summary>
            /// Polls KeyBinds.txt at most once per pollSeconds. If file changed, detects conflicts and emits warnings on change.
            /// </summary>
            public static void Tick(float pollSeconds, KeyCode action1, KeyCode action2, KeyCode legend)
            {
                if (Time.unscaledTime < nextPollTime)
                    return;

                nextPollTime = Time.unscaledTime + Mathf.Max(0.25f, pollSeconds);

                // NEW: detect assist-key changes (mod settings) even if KeyBinds didn't change
                string assistKeysSig = ((int)action1).ToString() + "|" + ((int)action2).ToString() + "|" + ((int)legend).ToString();
                bool assistKeysChanged = (assistKeysSig != lastAssistKeysSignature);
                if (assistKeysChanged)
                    lastAssistKeysSignature = assistKeysSig;

                string path = Path.Combine(Application.persistentDataPath, "KeyBinds.txt");
                if (!File.Exists(path))
                    return;

                DateTime writeUtc;
                try { writeUtc = File.GetLastWriteTimeUtc(path); }
                catch { return; }

                bool fileChanged = (writeUtc != lastWriteUtc);

                // CHANGED: only early-out if neither KeyBinds nor assist keys changed
                if (!fileChanged && !assistKeysChanged)
                    return;

                Dictionary<string, string> actionToKey = null;

                // NEW: if only assist keys changed, reuse cached DFU bindings (no file I/O)
                if (!fileChanged && assistKeysChanged && cachedActionToKey != null)
                {
                    actionToKey = cachedActionToKey;
                }
                else
                {
                    // Existing behavior: KeyBinds changed (or first run / cache missing) -> read and parse it
                    lastWriteUtc = writeUtc;

                    string text;
                    try { text = File.ReadAllText(path); }
                    catch { return; } // can be temporarily locked while DFU writes it

                    // Build a DFU map: action -> keyName  (for Escape/Back/LeftClick/RightClick)
                    actionToKey = FindForbiddenActionBindings(text);

                    // NEW: cache it so assist-only changes can be checked instantly
                    cachedActionToKey = actionToKey;
                }

                if (actionToKey == null)
                    return;

                // 1) DPad conflicts (DFU actions bound to DPad)
                // (Only worth recomputing when file changed, but it's cheap; keeping it simple.)
                Dictionary<string, string> dpadConflicts = FindDPadConflictsFromActionMap(actionToKey);
                string dpadSig = BuildDpadSignature(dpadConflicts);
                if (dpadSig != lastDpadSignature)
                {
                    lastDpadSignature = dpadSig;
                    if (dpadConflicts.Count > 0)
                    {
                        string msg = BuildDpadUserMessage(dpadConflicts);
                        Debug.Log("[ControllerAssistant] " + msg);
                        DaggerfallUI.AddHUDText(msg, 6f);
                    }
                }

                // 2) Assist-key conflicts (Action1/2/Legend share a binding with DFU Escape/Back/LeftClick/RightClick)
                Dictionary<string, string> assistConflicts = FindAssistConflicts(actionToKey, action1, action2, legend);
                string assistSig = BuildAssistSignature(assistConflicts);
                if (assistSig != lastAssistSignature)
                {
                    lastAssistSignature = assistSig;
                    if (assistConflicts.Count > 0)
                    {
                        string msg = BuildAssistUserMessage(assistConflicts);
                        Debug.Log("[ControllerAssistant] " + msg);
                        DaggerfallUI.AddHUDText(msg, 6f);
                    }
                }
            }

            // Reads all joystick bindings in KeyBinds, but only keeps entries for ForbiddenActions,
            // returning action -> keyName (e.g., "Escape" -> "JoystickAxis7Button0")
            private static Dictionary<string, string> FindForbiddenActionBindings(string keyBindsText)
            {
                var result = new Dictionary<string, string>();

                // Matches:
                // "JoystickButton4": "LeftClick"
                // "JoystickAxis7Button0": "Escape"
                // Group 1 = key name, Group 2 = action
                var rx = new Regex("\"(JoystickButton\\d+|JoystickAxis\\d+Button\\d+)\"\\s*:\\s*\"([^\"]+)\"",
                                   RegexOptions.Compiled);

                foreach (Match m in rx.Matches(keyBindsText))
                {
                    if (!m.Success || m.Groups.Count < 3)
                        continue;

                    string keyName = m.Groups[1].Value;
                    string action = m.Groups[2].Value;

                    if (!ForbiddenActions.Contains(action))
                        continue;

                    // If DFU ever writes duplicates (rare), last-one-wins is fine.
                    result[action] = keyName;
                }

                return result;
            }

            private static Dictionary<string, string> FindDPadConflictsFromActionMap(Dictionary<string, string> actionToKey)
            {
                var result = new Dictionary<string, string>();

                for (int i = 0; i < ForbiddenActionOrder.Length; i++)
                {
                    string action = ForbiddenActionOrder[i];

                    string keyName;
                    if (!actionToKey.TryGetValue(action, out keyName))
                        continue;

                    if (IsDPadKey(keyName))
                        result[action] = keyName;
                }

                return result;
            }

            private static Dictionary<string, string> FindAssistConflicts(
                Dictionary<string, string> actionToKey,
                KeyCode action1, KeyCode action2, KeyCode legend)
            {
                var result = new Dictionary<string, string>(); // assistName -> dfuAction

                string a1 = KeyCodeToKeyBindsName(action1);
                string a2 = KeyCodeToKeyBindsName(action2);
                string lg = KeyCodeToKeyBindsName(legend);

                if (string.IsNullOrEmpty(a1) && string.IsNullOrEmpty(a2) && string.IsNullOrEmpty(lg))
                    return result;

                for (int i = 0; i < ForbiddenActionOrder.Length; i++)
                {
                    string dfuAction = ForbiddenActionOrder[i];

                    string dfuKeyName;
                    if (!actionToKey.TryGetValue(dfuAction, out dfuKeyName))
                        continue;

                    if (!string.IsNullOrEmpty(a1) && string.Equals(dfuKeyName, a1, StringComparison.Ordinal))
                        result["Action1"] = dfuAction;

                    if (!string.IsNullOrEmpty(a2) && string.Equals(dfuKeyName, a2, StringComparison.Ordinal))
                        result["Action2"] = dfuAction;

                    if (!string.IsNullOrEmpty(lg) && string.Equals(dfuKeyName, lg, StringComparison.Ordinal))
                        result["Legend"] = dfuAction;
                }

                return result;
            }

            private static bool IsDPadKey(string keyName)
            {
                for (int i = 0; i < DPadKeyNames.Length; i++)
                {
                    if (string.Equals(DPadKeyNames[i], keyName, StringComparison.Ordinal))
                        return true;
                }
                return false;
            }

            private static string KeyCodeToKeyBindsName(KeyCode kc)
            {
                // Your assist options are joystick buttons (A/B/X/Y/LB/RB/L3/R3).
                // DFU KeyBinds uses "JoystickButtonN" naming for those.
                if (kc < KeyCode.JoystickButton0 || kc > KeyCode.JoystickButton19)
                    return null;

                int idx = (int)kc - (int)KeyCode.JoystickButton0;
                return "JoystickButton" + idx;
            }

            private static string BuildDpadSignature(Dictionary<string, string> conflicts)
            {
                if (conflicts == null || conflicts.Count == 0)
                    return string.Empty;

                var sb = new StringBuilder();
                for (int i = 0; i < ForbiddenActionOrder.Length; i++)
                {
                    string action = ForbiddenActionOrder[i];
                    string key;
                    if (conflicts.TryGetValue(action, out key))
                        sb.Append(action).Append("=").Append(key).Append(";");
                }
                return sb.ToString();
            }

            private static string BuildAssistSignature(Dictionary<string, string> conflicts)
            {
                if (conflicts == null || conflicts.Count == 0)
                    return string.Empty;

                // Stable ordering for signature
                string[] order = { "Action1", "Action2", "Legend" };
                var sb = new StringBuilder();

                for (int i = 0; i < order.Length; i++)
                {
                    string assist = order[i];
                    string dfuAction;
                    if (conflicts.TryGetValue(assist, out dfuAction))
                        sb.Append(assist).Append("=").Append(dfuAction).Append(";");
                }

                return sb.ToString();
            }

            private static string BuildDpadUserMessage(Dictionary<string, string> conflicts)
            {
                var bad = new List<string>();
                for (int i = 0; i < ForbiddenActionOrder.Length; i++)
                {
                    string action = ForbiddenActionOrder[i];
                    if (conflicts.ContainsKey(action))
                        bad.Add(action);
                }

                return "ControllerAssistant: Move " + string.Join("/", bad) +
                       " OFF the D-Pad (conflict detected in KeyBinds).";
            }

            private static string BuildAssistUserMessage(Dictionary<string, string> assistConflicts)
            {
                // Example: "Change assist keys: Action1 conflicts with LeftClick, Legend conflicts with Back."
                var parts = new List<string>();
                string[] order = { "Action1", "Action2", "Legend" };

                for (int i = 0; i < order.Length; i++)
                {
                    string assist = order[i];
                    string dfuAction;
                    if (assistConflicts.TryGetValue(assist, out dfuAction))
                        parts.Add(assist + " conflicts with " + dfuAction);
                }

                return "ControllerAssistant: Change assist keys (" + string.Join(", ", parts) +
                       "). Pick different Action1/Action2/Legend in mod settings.";
            }
        }
    }
}