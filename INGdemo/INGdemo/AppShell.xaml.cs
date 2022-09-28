using INGota.ViewModels;
using INGota.Views;
using System;
using System.Collections.Generic;
using Xamarin.Forms;

namespace INGota
{
    public partial class AppShell : Xamarin.Forms.Shell
    {
        public AppShell()
        {
            InitializeComponent();
            //Routing.RegisterRoute(nameof(FilesPage), typeof(ItemsPage));
        }
    }
}
