using CSimple.ViewModels;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage
    {
        private readonly IOnTrainModelClickedService _trainService;

        public OrientPage(IOnTrainModelClickedService trainService)
        {
            InitializeComponent();
            _trainService = trainService;
            BindingContext = new OrientViewModel();
        }

        private async void OnTrainModelClicked(object sender, EventArgs e)
        {
            await _trainService.HandleTrainModelAsync(BindingContext);
        }
    }
}
