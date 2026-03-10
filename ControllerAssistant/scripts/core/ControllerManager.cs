using UnityEngine;
using DaggerfallWorkshop.Game; // InputManager (DFU)
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class ControllerManager
    {
        private KeyCode action1Key = KeyCode.JoystickButton9; // default R3
        private KeyCode action2Key = KeyCode.JoystickButton8; // default L3
        private KeyCode legendKey = KeyCode.JoystickButton4;  // default LB

        // Values the old ExtAutomapAssist computed
        public int DPadH { get; private set; }     // -1 left, +1 right
        public int DPadV { get; private set; }     // +1 up,   -1 down
        public int RStickH { get; private set; }   // -1 left, +1 right
        public int RStickV { get; private set; }   // +1 up,   -1 down

        public bool Action1 { get; private set; }
        public bool Action2 { get; private set; }
        public bool Legend { get; private set; }
        public bool Action1Pressed { get; private set; }
        public bool Action2Pressed { get; private set; }
        public bool LegendPressed { get; private set; }

        // one-shot right-stick direction presses
        public bool RStickUpPressed { get; private set; }
        public bool RStickDownPressed { get; private set; }
        public bool RStickLeftPressed { get; private set; }
        public bool RStickRightPressed { get; private set; }

        // one-shot D-Pad direction presses
        public bool DPadLeftPressed { get; private set; }
        public bool DPadRightPressed { get; private set; }
        public bool DPadUpPressed { get; private set; }
        public bool DPadDownPressed { get; private set; }

        private bool prevAction1Held = false;
        private bool prevAction2Held = false;
        private bool prevLegendHeld = false;

        // latch/re-arm state for right stick & D-Pad
        private bool rStickReady = true;
        private bool dPadReady = true;

        // For closing (down-edge)
        public bool BackPressed { get; private set; }

        public bool DPadAny => DPadH != 0 || DPadV != 0;
        public bool RStickAny => RStickH != 0 || RStickV != 0;

        public void Update()
        {
            if (InputManager.Instance == null)
                return;

            // INPUT PROCESSING
            float axis7Value = Input.GetAxisRaw("Axis7"); // DPad V
            float axis6Value = Input.GetAxisRaw("Axis6"); // DPad H
            float axis4Value = Input.GetAxisRaw("Axis4"); // RStick H
            float axis5Value = Input.GetAxisRaw("Axis5"); // RStick V (+ up, - down)

            DPadH = axis6Value < -0.5f ? -1 : axis6Value > 0.5f ? 1 : 0;
            DPadV = axis7Value < -0.5f ? -1 : axis7Value > 0.5f ? 1 : 0;
            RStickH = axis4Value < -0.5f ? -1 : axis4Value > 0.5f ? 1 : 0;
            RStickV = axis5Value < -0.5f ? -1 : axis5Value > 0.5f ? 1 : 0;

            // Reset one-shot outputs every frame
            RStickUpPressed = false;
            RStickDownPressed = false;
            RStickLeftPressed = false;
            RStickRightPressed = false;
            DPadLeftPressed = false;
            DPadRightPressed = false;
            DPadUpPressed = false;
            DPadDownPressed = false;

            // Re-arm only when stick returns to center
            if (RStickH == 0 && RStickV == 0)
            {
                rStickReady = true;
            }
            else if (rStickReady)
            {
                // Fire exactly once per excursion from center
                if (Mathf.Abs(axis5Value) >= Mathf.Abs(axis4Value))
                {
                    if (RStickV == 1)
                        RStickUpPressed = true;
                    else if (RStickV == -1)
                        RStickDownPressed = true;
                }
                else
                {
                    if (RStickH == -1)
                        RStickLeftPressed = true;
                    else if (RStickH == 1)
                        RStickRightPressed = true;
                }

                rStickReady = false;
            }

            // Re-arm only when D-Pad returns to center
            if (DPadH == 0 && DPadV == 0)
            {
                dPadReady = true;
            }
            else if (dPadReady)
            {
                // Fire exactly once per excursion from center
                if (Mathf.Abs(axis7Value) >= Mathf.Abs(axis6Value))
                {
                    if (DPadV == 1)
                        DPadUpPressed = true;
                    else if (DPadV == -1)
                        DPadDownPressed = true;
                }
                else
                {
                    if (DPadH == -1)
                        DPadLeftPressed = true;
                    else if (DPadH == 1)
                        DPadRightPressed = true;
                }

                dPadReady = false;
            }

            // Action1
            bool action1Held = action1Key != KeyCode.None && Input.GetKey(action1Key);
            Action1Pressed = action1Held && !prevAction1Held;
            Action1 = action1Held;
            prevAction1Held = action1Held;

            // Action2
            bool action2Held = action2Key != KeyCode.None && Input.GetKey(action2Key);
            Action2Pressed = action2Held && !prevAction2Held;
            Action2 = action2Held;
            prevAction2Held = action2Held;

            // Legend
            bool legendHeld = legendKey != KeyCode.None && Input.GetKey(legendKey);
            LegendPressed = legendHeld && !prevLegendHeld;
            Legend = legendHeld;
            prevLegendHeld = legendHeld;

            // Back close
            BackPressed = InputManager.Instance != null &&
                          InputManager.Instance.GetBackButtonDown();
        }

        public ControllerManager()
        {
        }

        public void SetAction1Key(KeyCode key)
        {
            action1Key = key;
        }

        public void SetAction2Key(KeyCode key)
        {
            action2Key = key;
        }

        public void SetLegendKey(KeyCode key)
        {
            legendKey = key;
        }

        public string Action1Name
        {
            get { return GetButtonName(action1Key); }
        }

        public string Action2Name
        {
            get { return GetButtonName(action2Key); }
        }

        private string GetButtonName(KeyCode key)
        {
            if (key == KeyCode.JoystickButton0) return "A";
            if (key == KeyCode.JoystickButton1) return "B";
            if (key == KeyCode.JoystickButton2) return "X";
            if (key == KeyCode.JoystickButton3) return "Y";

            if (key == KeyCode.JoystickButton4) return "LB";
            if (key == KeyCode.JoystickButton5) return "RB";

            if (key == KeyCode.JoystickButton8) return "L3";
            if (key == KeyCode.JoystickButton9) return "R3";

            return key.ToString();
        }
    }
}