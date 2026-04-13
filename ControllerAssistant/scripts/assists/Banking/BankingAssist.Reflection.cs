using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterface;
using System.Reflection;
using UnityEngine;

namespace gigantibyte.DFU.ControllerAssistant
{
    public partial class BankingAssist
    {
        private void EnsureInitialized(DaggerfallBankingWindow menuWindow)
        {
            if (menuWindow == null)
                return;

            var type = menuWindow.GetType();

            if (cachedReflectionType == type)
                return;

            fiMainPanel = CacheField(type, "parentPanel");
            fiTransactionInput = CacheField(type, "transactionInput");
            fiTransactionType = CacheField(type, "transactionType");
            fiBuyHouseButton = CacheField(type, "buyHouseButton");
            fiBuyShipButton = CacheField(type, "buyShipButton");

            miDepoGoldButton_OnMouseClick = CacheMethod(type, "DepoGoldButton_OnMouseClick");
            miDrawGoldButton_OnMouseClick = CacheMethod(type, "DrawGoldButton_OnMouseClick");
            miDepoLOCButton_OnMouseClick = CacheMethod(type, "DepoLOCButton_OnMouseClick");
            miDrawLOCButton_OnMouseClick = CacheMethod(type, "DrawLOCButton_OnMouseClick");
            miLoanRepayButton_OnMouseClick = CacheMethod(type, "LoanRepayButton_OnMouseClick");
            miLoanBorrowButton_OnMouseClick = CacheMethod(type, "LoanBorrowButton_OnMouseClick");
            miBuyHouseButton_OnMouseClick = CacheMethod(type, "BuyHouseButton_OnMouseClick");
            miSellHouseButton_OnMouseClick = CacheMethod(type, "SellHouseButton_OnMouseClick");
            miBuyShipButton_OnMouseClick = CacheMethod(type, "BuyShipButton_OnMouseClick");
            miSellShipButton_OnMouseClick = CacheMethod(type, "SellShipButton_OnMouseClick");
            miExitButton_OnMouseClick = CacheMethod(type, "ExitButton_OnMouseClick");

            miHandleTransactionInput = CacheMethod(type, "HandleTransactionInput");
            miToggleTransactionInput = CacheMethod(type, "ToggleTransactionInput");

            cachedReflectionType = type;
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

        private void InvokeButtonHandler(DaggerfallBankingWindow menuWindow, MethodInfo mi)
        {
            if (menuWindow == null || mi == null)
                return;

            mi.Invoke(menuWindow, new object[] { null, Vector2.zero });
        }

        private void TriggerReflectedButtonClick(DaggerfallBankingWindow menuWindow, FieldInfo fiButton)
        {
            if (menuWindow == null || fiButton == null)
                return;

            Button button = fiButton.GetValue(menuWindow) as Button;
            if (button == null)
                return;

            button.TriggerMouseClick();
        }
        
    }
}