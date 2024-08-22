using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
// using System.Runtime.InteropServices.WindowsRuntime;
#if WINDOWS
using Microsoft.UI.Xaml.Media.Imaging; //The type or namespace name 'UI' does not exist in the namespace 'Microsoft' (are you missing an assembly reference?)CS0234
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Streams;
using Windows.Graphics.Display;
#endif
using System.Linq;

namespace CSimple.Pages;

public partial class ObservePage : ContentPage
{
        public Command ReadPCVisualCommand { get; }
        public Command ReadPCAudibleCommand { get; }
        public Command ReadUserVisualCommand { get; }
        public Command ReadUserAudibleCommand { get; }
        public Command ReadUserTouchCommand { get; }
        public ObservePage()
        {
            InitializeComponent();
            ReadPCVisualCommand = new Command(async () => InitializePCVisualOutput());
            ReadPCAudibleCommand = new Command(InitializePCAudibleOutput);
            ReadUserVisualCommand = new Command(InitializeUserVisualOutput);
            ReadUserAudibleCommand = new Command(InitializeUserAudibleOutput);
            ReadUserTouchCommand = new Command(InitializeUserTouchOutput);

            BindingContext = this;
        }
        private void InitializePCVisualOutput()
        {
            
        }
        private void InitializePCAudibleOutput()
        {
            DebugOutput("Starting PC Audible Output capture.");
        }

        private void InitializeUserVisualOutput()
        {
            DebugOutput("Starting User Visual Output capture.");
        }

        private void InitializeUserAudibleOutput()
        {
            DebugOutput("Starting User Audible Output capture.");
        }

        private void InitializeUserTouchOutput()
        {
            DebugOutput("Starting User Touch Output capture.");
        }

        private void DebugOutput(string message)
        {
            Debug.WriteLine(message);
            // Update a UI label or text area with the debug message if needed
        }
}
