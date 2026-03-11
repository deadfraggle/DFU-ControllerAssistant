using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;

namespace gigantibyte.DFU.ControllerAssistant
{
    public class FavoritesWindow : DaggerfallPopupWindow
    {
        private Panel mainPanel = new Panel();
        private ListBox favoritesList = new ListBox();
        private TextLabel titleLabel = new TextLabel();

        public FavoritesWindow(IUserInterfaceManager uiManager)
            : base(uiManager)
        {
            ParentPanel.BackgroundColor = Color.clear;
            AllowCancel = true;
        }

        protected override void Setup()
        {
            if (IsSetup)
                return;

            base.Setup();

            // IMPORTANT: use NativePanel, like DFU popup windows do
            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.VerticalAlignment = VerticalAlignment.Middle;
            mainPanel.Position = new Vector2(0, 0);

            // Start with guild-popup scale, but larger
            mainPanel.Size = new Vector2(180, 90);
            mainPanel.BackgroundColor = new Color(0f, 0f, 0f, 0.9f);

            NativePanel.Components.Add(mainPanel);

            titleLabel.Text = "Favorites";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.Position = new Vector2(0, 4);
            mainPanel.Components.Add(titleLabel);

            favoritesList.Position = new Vector2(8, 16);
            favoritesList.Size = new Vector2(164, 64);
            favoritesList.RowsDisplayed = 6;
            mainPanel.Components.Add(favoritesList);

            favoritesList.AddItem("[No favorites yet]");
            favoritesList.SelectedIndex = 0;
        }

        public override void Update()
        {
            base.Update();

            if (InputManager.Instance.GetBackButtonDown())
                CloseWindow();
        }
    }
}