using System;
using Microsoft.Maui.Controls;

namespace CSimple.Components
{
    public partial class InputModifierPopup : ContentView
    {
        public event EventHandler<EventArgs> OkayClicked;

        public InputModifierPopup()
        {
            InitializeComponent();
        }

        public string Description
        {
            get => DescriptionEntry.Text;
            set => DescriptionEntry.Text = value;
        }

        public void Show()
        {
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        private void OnOkayClicked(object sender, EventArgs e)
        {
            OkayClicked?.Invoke(this, e);
            Hide();
        }
    }
}
