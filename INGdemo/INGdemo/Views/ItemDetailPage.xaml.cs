using System;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using INGota.Models;
using INGota.ViewModels;
using INGdemo.Models;

namespace INGota.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ItemDetailPage : ContentPage
    {
        ItemDetailViewModel viewModel;

        public ItemDetailPage(ItemDetailViewModel viewModel)
        {
            InitializeComponent();

            BindingContext = this.viewModel = viewModel;
        }

        public ItemDetailPage()
        {
            InitializeComponent();

            var item = new BLEDev(null);

            viewModel = new ItemDetailViewModel(item);
            BindingContext = viewModel;
        }
    }
}