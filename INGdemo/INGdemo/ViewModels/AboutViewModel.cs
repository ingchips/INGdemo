using System;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace INGota.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        public AboutViewModel()
        {
            Title = "About";

            OpenWebCommand = new Command(() => Launcher.TryOpenAsync(new Uri("http://www.ingchips.com")));
        }

        public ICommand OpenWebCommand { get; }
    }
}