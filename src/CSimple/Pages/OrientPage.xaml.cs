using Microsoft.Maui.Controls;
using System;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage
    {
        public OrientPage()
        {
            InitializeComponent();
            // Simplified initialization without complex bindings initially
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Add any initialization that needs to happen when the page appears
        }

        private void OnTrainModelClicked(object sender, EventArgs e)
        {
            // Simple implementation that doesn't rely on bindings
            DisplayAlert("Training Model", "Model training would start here", "OK");
        }
    }
}
