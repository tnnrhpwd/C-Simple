using Microsoft.Maui.Controls;

namespace CSimple.Components
{
    public partial class CapturePreviewCard : ContentView
    {
        public CapturePreviewCard()
        {
            InitializeComponent();
        }

        public string ButtonLabelText
        {
            get => ButtonLabel.Text;
            set => ButtonLabel.Text = value;
        }
    }
}
