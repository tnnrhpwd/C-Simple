using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage
    {
        public OrientPage()
        {
            InitializeComponent();
            BindingContext = new OrientPageViewModel();
        }

        private async void OnTrainModelClicked(object sender, EventArgs e)
        {
            var viewModel = BindingContext as OrientPageViewModel;
            if (viewModel != null)
            {
                await viewModel.TrainModelAsync();
            }
        }
    }
}
