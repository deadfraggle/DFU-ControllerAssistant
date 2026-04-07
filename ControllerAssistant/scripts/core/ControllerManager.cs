using UnityEngine;
using DaggerfallWorkshop.Game; // InputManager (DFU)
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class ControllerManager
    {
        public enum StickDir8 : int
        {
            None = 0,
            N,
            NE,
            E,
            SE,
            S,
            SW,
            W,
            NW,
        }

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
        public bool Action1Released { get; private set; }
        public bool Action2Released { get; private set; }
        public bool Action2Tapped { get; private set; }
        public bool Action2Held { get; private set; }

        // one-shot right-stick direction presses
        public bool RStickUpPressed { get; private set; }
        public bool RStickDownPressed { get; private set; }
        public bool RStickLeftPressed { get; private set; }
        public bool RStickRightPressed { get; private set; }

        // slow-repeat right-stick direction pulses
        public bool RStickUpHeldSlow { get; private set; }
        public bool RStickDownHeldSlow { get; private set; }
        public bool RStickLeftHeldSlow { get; private set; }
        public bool RStickRightHeldSlow { get; private set; }

        // 8-way right-stick direction outputs
        private int rStickDir8 = (int)StickDir8.None;
        private int rStickDir8Pressed = (int)StickDir8.None;
        private int rStickDir8HeldSlow = (int)StickDir8.None;

        public StickDir8 RStickDir8
        {
            get { return (StickDir8)rStickDir8; }
        }

        public StickDir8 RStickDir8Pressed
        {
            get { return (StickDir8)rStickDir8Pressed; }
        }

        public StickDir8 RStickDir8HeldSlow
        {
            get { return (StickDir8)rStickDir8HeldSlow; }
        }

        // one-shot D-Pad direction presses
        public bool DPadLeftPressed { get; private set; }
        public bool DPadRightPressed { get; private set; }
        public bool DPadUpPressed { get; private set; }
        public bool DPadDownPressed { get; private set; }
        public bool DPadReleased { get; private set; }
        public bool DPadLeftReleased { get; private set; }
        public bool DPadRightReleased { get; private set; }
        public bool DPadUpReleased { get; private set; }
        public bool DPadDownReleased { get; private set; }

        // slow-repeat D-Pad direction pulses
        public bool DPadUpHeldSlow { get; private set; }
        public bool DPadDownHeldSlow { get; private set; }
        public bool DPadLeftHeldSlow { get; private set; }
        public bool DPadRightHeldSlow { get; private set; }


        private bool prevAction1Held = false;
        private bool prevAction2Held = false;
        private bool prevLegendHeld = false;

        private float action2HoldTimer = 0f;
        private bool action2HoldTriggered = false;
        private float action2TapMaxDuration = 0.25f;
        private float action2HeldThreshold = 0.30f;

        // latch/re-arm state for right stick & D-Pad
        private bool rStickReady = true;
        private bool dPadReady = true;
        private bool dPadLeftReady = true;
        private bool dPadRightReady = true;
        private bool dPadUpReady = true;
        private bool dPadDownReady = true;

        // slow-repeat tuning for right stick
        private float rStickHeldSlowDelay = 0.35f;
        private float rStickHeldSlowInterval = 0.10f;

        private float rStickHeldSlowHoldTimer = 0f;
        private float rStickHeldSlowRepeatTimer = 0f;

        private int rStickHeldSlowX = 0;   // -1 left, +1 right
        private int rStickHeldSlowY = 0;   // -1 down, +1 up
        private int rStickHeldSlowDir8 = (int)StickDir8.None;

        // slow-repeat tuning for D-Pad
        private float dPadHeldSlowDelay = 0.35f;
        private float dPadHeldSlowInterval = 0.10f;

        private float dPadHeldSlowHoldTimer = 0f;
        private float dPadHeldSlowRepeatTimer = 0f;

        private int dPadHeldSlowX = 0;   // -1 left, +1 right
        private int dPadHeldSlowY = 0;   // -1 down, +1 up

        // For closing (down-edge)
        public bool BackPressed { get; private set; }

        public bool DPadAny => DPadH != 0 || DPadV != 0;
        public bool RStickAny => RStickH != 0 || RStickV != 0;

        private StickDir8 GetStickDir8(float axisH, float axisV, int stickH, int stickV)
        {
            if (stickH == 0 && stickV == 0)
                return StickDir8.None;

            // Require both axes to be meaningfully engaged for diagonal classification.
            // This prevents slight analog wobble from producing accidental diagonals.
            bool strongH = Mathf.Abs(axisH) > 0.5f;
            bool strongV = Mathf.Abs(axisV) > 0.5f;

            if (strongH && strongV)
            {
                if (stickV == 1 && stickH == 1) return StickDir8.NE;
                if (stickV == 1 && stickH == -1) return StickDir8.NW;
                if (stickV == -1 && stickH == 1) return StickDir8.SE;
                if (stickV == -1 && stickH == -1) return StickDir8.SW;
            }

            if (stickV == 1) return StickDir8.N;
            if (stickV == -1) return StickDir8.S;
            if (stickH == 1) return StickDir8.E;
            if (stickH == -1) return StickDir8.W;

            return StickDir8.None;
        }

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

            rStickDir8 = (int)GetStickDir8(axis4Value, axis5Value, RStickH, RStickV);

            // Reset one-shot outputs every frame
            RStickUpPressed = false;
            RStickDownPressed = false;
            RStickLeftPressed = false;
            RStickRightPressed = false;

            RStickUpHeldSlow = false;
            RStickDownHeldSlow = false;
            RStickLeftHeldSlow = false;
            RStickRightHeldSlow = false;

            DPadUpHeldSlow = false;
            DPadDownHeldSlow = false;
            DPadLeftHeldSlow = false;
            DPadRightHeldSlow = false;

            DPadLeftReleased = false;
            DPadRightReleased = false;
            DPadUpReleased = false;
            DPadDownReleased = false;
            DPadReleased = false;

            Action1Released = false;
            Action2Released = false;
            Action2Tapped = false;
            Action2Held = false;

            if (DPadUpPressed) dPadUpReady = false;
            else if (DPadRightPressed) dPadRightReady = false;
            else if (DPadDownPressed) dPadDownReady = false;
            else if (DPadLeftPressed) dPadLeftReady = false;

            DPadLeftPressed = false;
            DPadRightPressed = false;
            DPadUpPressed = false;
            DPadDownPressed = false;

            DPadReleased = false;

            rStickDir8Pressed = (int)StickDir8.None;
            rStickDir8HeldSlow = (int)StickDir8.None;

            // Re-arm only when stick returns to center
            if (RStickH == 0 && RStickV == 0)
            {
                rStickReady = true;
            }
            else if (rStickReady)
            {
                rStickDir8Pressed = rStickDir8;

                // Preserve existing dominant-axis behavior for current assists
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

            // Slow-repeat right stick pulses
            int currentSlowX = 0;
            int currentSlowY = 0;
            StickDir8 currentSlowDir8 = StickDir8.None;

            if (RStickH != 0 || RStickV != 0)
            {
                currentSlowDir8 = GetStickDir8(axis4Value, axis5Value, RStickH, RStickV);

                // Preserve existing dominant-axis behavior for current assists
                if (Mathf.Abs(axis5Value) >= Mathf.Abs(axis4Value))
                    currentSlowY = RStickV;   // +1 up, -1 down
                else
                    currentSlowX = RStickH;   // -1 left, +1 right
            }

            // returned to center
            if (currentSlowX == 0 && currentSlowY == 0)
            {
                rStickHeldSlowX = 0;
                rStickHeldSlowY = 0;
                rStickHeldSlowDir8 = (int)StickDir8.None;
                rStickHeldSlowHoldTimer = 0f;
                rStickHeldSlowRepeatTimer = 0f;
            }
            // changed direction (or fresh engage)
            else if (currentSlowX != rStickHeldSlowX || currentSlowY != rStickHeldSlowY || (int)currentSlowDir8 != rStickHeldSlowDir8)
            {
                rStickHeldSlowX = currentSlowX;
                rStickHeldSlowY = currentSlowY;
                rStickHeldSlowDir8 = (int)currentSlowDir8;
                rStickHeldSlowHoldTimer = 0f;
                rStickHeldSlowRepeatTimer = 0f;

                rStickDir8HeldSlow = rStickHeldSlowDir8;

                // Preserve existing cardinal held-slow pulses
                if (rStickHeldSlowY == 1)
                    RStickUpHeldSlow = true;
                else if (rStickHeldSlowY == -1)
                    RStickDownHeldSlow = true;
                else if (rStickHeldSlowX == -1)
                    RStickLeftHeldSlow = true;
                else if (rStickHeldSlowX == 1)
                    RStickRightHeldSlow = true;
            }
            // still holding same direction
            else
            {
                rStickHeldSlowHoldTimer += Time.unscaledDeltaTime;

                if (rStickHeldSlowHoldTimer >= rStickHeldSlowDelay)
                {
                    rStickHeldSlowRepeatTimer += Time.unscaledDeltaTime;

                    if (rStickHeldSlowRepeatTimer >= rStickHeldSlowInterval)
                    {
                        rStickHeldSlowRepeatTimer -= rStickHeldSlowInterval;

                        rStickDir8HeldSlow = rStickHeldSlowDir8;

                        if (rStickHeldSlowY == 1)
                            RStickUpHeldSlow = true;
                        else if (rStickHeldSlowY == -1)
                            RStickDownHeldSlow = true;
                        else if (rStickHeldSlowX == -1)
                            RStickLeftHeldSlow = true;
                        else if (rStickHeldSlowX == 1)
                            RStickRightHeldSlow = true;
                    }
                }
            }

            // Slow-repeat D-Pad pulses
            int currentDPadSlowX = 0;
            int currentDPadSlowY = 0;

            if (DPadH != 0 || DPadV != 0)
            {
                // Preserve existing dominant-axis behavior for current assists
                if (Mathf.Abs(axis7Value) >= Mathf.Abs(axis6Value))
                    currentDPadSlowY = DPadV;   // +1 up, -1 down
                else
                    currentDPadSlowX = DPadH;   // -1 left, +1 right
            }

            // returned to center
            if (currentDPadSlowX == 0 && currentDPadSlowY == 0)
            {
                dPadHeldSlowX = 0;
                dPadHeldSlowY = 0;
                dPadHeldSlowHoldTimer = 0f;
                dPadHeldSlowRepeatTimer = 0f;
            }
            // changed direction (or fresh engage)
            else if (currentDPadSlowX != dPadHeldSlowX || currentDPadSlowY != dPadHeldSlowY)
            {
                dPadHeldSlowX = currentDPadSlowX;
                dPadHeldSlowY = currentDPadSlowY;
                dPadHeldSlowHoldTimer = 0f;
                dPadHeldSlowRepeatTimer = 0f;

                if (dPadHeldSlowY == 1)
                    DPadUpHeldSlow = true;
                else if (dPadHeldSlowY == -1)
                    DPadDownHeldSlow = true;
                else if (dPadHeldSlowX == -1)
                    DPadLeftHeldSlow = true;
                else if (dPadHeldSlowX == 1)
                    DPadRightHeldSlow = true;
            }
            // still holding same direction
            else
            {
                dPadHeldSlowHoldTimer += Time.unscaledDeltaTime;

                if (dPadHeldSlowHoldTimer >= dPadHeldSlowDelay)
                {
                    dPadHeldSlowRepeatTimer += Time.unscaledDeltaTime;

                    if (dPadHeldSlowRepeatTimer >= dPadHeldSlowInterval)
                    {
                        dPadHeldSlowRepeatTimer -= dPadHeldSlowInterval;

                        if (dPadHeldSlowY == 1)
                            DPadUpHeldSlow = true;
                        else if (dPadHeldSlowY == -1)
                            DPadDownHeldSlow = true;
                        else if (dPadHeldSlowX == -1)
                            DPadLeftHeldSlow = true;
                        else if (dPadHeldSlowX == 1)
                            DPadRightHeldSlow = true;
                    }
                }
            }

            // Re-arm only when D-Pad returns to center
            if (DPadH == 0 && DPadV == 0)
            {
                if (dPadReady == false)
                    DPadReleased = true;

                if (!dPadUpReady) DPadUpReleased = true;
                else if (!dPadRightReady) DPadRightReleased = true;
                else if (!dPadDownReady) DPadDownReleased = true;
                else if (!dPadLeftReady) DPadLeftReleased = true;

                dPadReady = true;
                dPadUpReady = true;
                dPadRightReady = true;
                dPadDownReady = true;
                dPadLeftReady = true;

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
            Action1Released = !action1Held && prevAction1Held;

            Action1 = action1Held;
            prevAction1Held = action1Held;

            // Action2
            bool action2HeldNow = action2Key != KeyCode.None && Input.GetKey(action2Key);

            Action2Pressed = action2HeldNow && !prevAction2Held;
            Action2Released = !action2HeldNow && prevAction2Held;

            if (Action2Pressed)
            {
                action2HoldTimer = 0f;
                action2HoldTriggered = false;
            }

            if (action2HeldNow)
            {
                action2HoldTimer += Time.unscaledDeltaTime;

                if (!action2HoldTriggered && action2HoldTimer >= action2HeldThreshold)
                    action2HoldTriggered = true;

                if (action2HoldTriggered)
                    Action2Held = true;
            }

            if (Action2Released)
            {
                if (!action2HoldTriggered && action2HoldTimer <= action2TapMaxDuration)
                    Action2Tapped = true;

                action2HoldTimer = 0f;
                action2HoldTriggered = false;
            }

            Action2 = action2HeldNow;
            prevAction2Held = action2HeldNow;

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