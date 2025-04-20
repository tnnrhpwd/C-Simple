using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Add this namespace for Color and Colors
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CSimple.Services.AppModeService;
using CSimple.ViewModels;
using System.Diagnostics;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage
    {
        private readonly OrientPageViewModel _viewModel;

        public OrientPage(OrientPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Wire up UI interaction delegates if needed (similar to NetPage)
            _viewModel.ShowAlert = (title, message, cancel) => DisplayAlert(title, message, cancel);
            _viewModel.NavigateTo = async (route) => await Shell.Current.GoToAsync(route);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // ViewModel's ModelId property setter handles loading data via [QueryProperty]
            Debug.WriteLine($"OrientPage Appearing. Current Model ID in VM: {_viewModel.ModelId}");
            // You might force a refresh or check loading state here if needed
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            // The [QueryProperty] attribute on the ViewModel handles the modelId automatically.
            // You could add extra logic here if needed after navigation completes.
            Debug.WriteLine($"OrientPage NavigatedTo. Current Model ID in VM: {_viewModel.ModelId}");
        }
    }
}
