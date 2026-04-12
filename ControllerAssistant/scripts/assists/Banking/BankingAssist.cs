using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class BankingAssist : IMenuAssist
    {
        private const bool debugMODE = true;
        private bool wasOpen = false;

        private FieldInfo fiMainPanel;
        private Panel mainPanel;
        private LegendOverlay legend;
        private bool legendVisible = false;
        private System.Type cachedReflectionType = null;

        // Reflected button handlers
        private MethodInfo miDepoGoldButton_OnMouseClick;
        private MethodInfo miDrawGoldButton_OnMouseClick;
        private MethodInfo miDepoLOCButton_OnMouseClick;
        private MethodInfo miDrawLOCButton_OnMouseClick;
        private MethodInfo miLoanRepayButton_OnMouseClick;
        private MethodInfo miLoanBorrowButton_OnMouseClick;
        private MethodInfo miBuyHouseButton_OnMouseClick;
        private MethodInfo miSellHouseButton_OnMouseClick;
        private MethodInfo miBuyShipButton_OnMouseClick;
        private MethodInfo miSellShipButton_OnMouseClick;
        private MethodInfo miExitButton_OnMouseClick;
        private MethodInfo miHandleTransactionInput;
        private MethodInfo miToggleTransactionInput;


        // Reflected fields
        private FieldInfo fiTransactionInput;
        private FieldInfo fiTransactionType;
        private FieldInfo fiBuyHouseButton;
        private FieldInfo fiBuyShipButton;

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        private DefaultSelectorBoxHost selectorHost;
        private OnScreenNumberpadOverlay numberpadOverlay;

        // Buttons
        private const int DepoGoldButton = 0;
        private const int DrawGoldButton = 1;
        private const int DepoLOCButton = 2;
        private const int DrawLOCButton = 3;
        private const int LoanRepayButton = 4;
        private const int LoanBorrowButton = 5;
        private const int BuyHouseButton = 6;
        private const int SellHouseButton = 7;
        private const int BuyShipButton = 8;
        private const int SellShipButton = 9;
        private const int ExitButton = 10;

        public SelectorButtonInfo[] menuButton = new SelectorButtonInfo[]
        {
            new SelectorButtonInfo { rect = new Rect(120f, 58f, 45f, 8f), E = DrawGoldButton, S = DepoLOCButton },   // Deposit gold
            new SelectorButtonInfo { rect = new Rect(172f, 58f, 45f, 8f), W = DepoGoldButton, S = DrawLOCButton },   // Withdraw gold
            new SelectorButtonInfo { rect = new Rect(120f, 76f, 45f, 8f), N = DepoGoldButton, E = DrawLOCButton, S = LoanRepayButton }, // Deposit LOC
            new SelectorButtonInfo { rect = new Rect(172f, 76f, 45f, 8f), N = DrawGoldButton, W = DepoLOCButton, S = LoanBorrowButton }, // Withdraw LOC
            new SelectorButtonInfo { rect = new Rect(120f, 94f, 45f, 8f), N = DepoLOCButton, E = LoanBorrowButton, S = BuyHouseButton }, // Repay
            new SelectorButtonInfo { rect = new Rect(172f, 94f, 45f, 8f), N = DrawLOCButton, W = LoanRepayButton, S = SellHouseButton }, // Borrow
            new SelectorButtonInfo { rect = new Rect(120f, 112f, 45f, 8f), N = LoanRepayButton, E = SellHouseButton, S = BuyShipButton }, // Buy house
            new SelectorButtonInfo { rect = new Rect(172f, 112f, 45f, 8f), N = LoanBorrowButton, W = BuyHouseButton, S = SellShipButton }, // Sell house
            new SelectorButtonInfo { rect = new Rect(120f, 130f, 45f, 8f), N = BuyHouseButton, E = SellShipButton, S = ExitButton }, // Buy ship
            new SelectorButtonInfo { rect = new Rect(172f, 130f, 45f, 8f), N = SellHouseButton, W = BuyShipButton, S = ExitButton }, // Sell ship
            new SelectorButtonInfo { rect = new Rect(92f, 159f, 40f, 19f), N = BuyShipButton }, // Exit
        };

        public int buttonSelected = DepoGoldButton;

        public bool Claims(IUserInterfaceWindow top)
        {
            return top is DaggerfallBankingWindow;
        }

        public void Tick(IUserInterfaceWindow top, ControllerManager cm)
        {
            DaggerfallBankingWindow menuWindow = top as DaggerfallBankingWindow;

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
            DestroyNumberpad();

            legendVisible = false;
            mainPanel = null;
            cachedReflectionType = null;
        }
    }
}