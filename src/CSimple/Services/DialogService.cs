using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public class DialogService
    {
        public async Task ShowErrorDialog(string title, string content)
        {
            // Use MAUI's built-in alert dialog instead of WinUI ContentDialog
            await Application.Current.MainPage.DisplayAlert(title, content, "OK");
        }
    }
}
