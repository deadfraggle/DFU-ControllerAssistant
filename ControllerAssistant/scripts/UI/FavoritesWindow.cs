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

            // Main panel
            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.VerticalAlignment = VerticalAlignment.Middle;
            mainPanel.Size = new Vector2(280, 180);
            mainPanel.Position = Vector2.zero;
            mainPanel.BackgroundColor = new Color(0f, 0f, 0f, 0.85f);

            ParentPanel.Components.Add(mainPanel);

            // Title
            titleLabel.Text = "Favorites";
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleLabel.Position = new Vector2(0, 6);
            mainPanel.Components.Add(titleLabel);

            // Empty list box for now
            favoritesList.Position = new Vector2(12, 24);
            favoritesList.Size = new Vector2(256, 140);
            favoritesList.RowsDisplayed = 10;
            mainPanel.Components.Add(favoritesList);

            favoritesList.AddItem("[No favorites yet]");
            favoritesList.SelectedIndex = 0;
        }

        public override void Update()
        {
            base.Update();

            if (InputManager.Instance.GetBackButtonDown())
            {
                CloseWindow();
                return;
            }
        }
    }
}