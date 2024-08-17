namespace C_Simple
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(C_Simple.HomePage), typeof(C_Simple.HomePage));
            Routing.RegisterRoute(nameof(C_Simple.LoginPage), typeof(C_Simple.LoginPage));
            Routing.RegisterRoute(nameof(C_Simple.InputsPage), typeof(C_Simple.InputsPage));
            Routing.RegisterRoute(nameof(C_Simple.ContactPage), typeof(C_Simple.ContactPage));
            Routing.RegisterRoute(nameof(C_Simple.AboutPage), typeof(C_Simple.AboutPage));
        }
    }
}
