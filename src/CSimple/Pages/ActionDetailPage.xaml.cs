using Microsoft.Maui.Controls;

namespace CSimple.Pages
{
    public partial class ActionDetailPage : ContentPage
    {
        public ActionDetailPage(ActionGroup actionGroup)
        {
            InitializeComponent();
            BindingContext = new ActionDetailViewModel(actionGroup);
        }
    }

    public class ActionDetailViewModel
    {
        public string ActionName { get; set; }
        public string ActionArrayFormatted { get; set; }
        public Command CloseCommand { get; set; }

        public ActionDetailViewModel(ActionGroup actionGroup)
        {
            ActionName = actionGroup.ActionName;
            ActionArrayFormatted = string.Join(", ", actionGroup.ActionArray);
            CloseCommand = new Command(() => Application.Current.MainPage.Navigation.PopModalAsync());
        }
    }
}
