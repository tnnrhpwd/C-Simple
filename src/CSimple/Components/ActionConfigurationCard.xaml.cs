using System;
using Microsoft.Maui.Controls;

namespace CSimple.Components
{
    public partial class ActionConfigurationCard : ContentView
    {
        public event EventHandler<EventArgs> InputModifierClicked;

        public ActionConfigurationCard()
        {
            InitializeComponent();
        }

        public string ActionName
        {
            get => ActionNameInput.Text;
            set => ActionNameInput.Text = value;
        }

        public string ModifierName
        {
            get => ModifierNameEntry.Text;
            set => ModifierNameEntry.Text = value;
        }

        public string Priority
        {
            get => PriorityEntry.Text;
            set => PriorityEntry.Text = value;
        }

        private void OnInputModifierClicked(object sender, EventArgs e)
        {
            InputModifierClicked?.Invoke(this, e);
        }
    }
}
