using System;
using System.Windows.Input;

using Xamarin.Forms;

namespace INGota.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        public AboutViewModel()
        {
            Title = "About";

            OpenWebCommand = new Command(() => Device.OpenUri(new Uri("http://www.ingchips.com")));
        }

        public ICommand OpenWebCommand { get; }
    }
}