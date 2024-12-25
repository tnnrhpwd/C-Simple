using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using CSimple.ViewModels;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage
    {
        public OrientPage()
        {
            InitializeComponent();
            BindingContext = new OrientViewModel();
            Console.WriteLine("OrientPage initialized.");
            BindingContext = this;

        }

        private async void OnTrainModelClicked(object sender, EventArgs e)
        {
            var viewModel = BindingContext as OrientViewModel;
            if (viewModel != null)
            {
                await viewModel.TrainModelAsync();
            }
            Console.WriteLine("Model trained.");
        }
    }
}
