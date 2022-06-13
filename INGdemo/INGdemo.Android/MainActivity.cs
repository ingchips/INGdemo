using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Support.V7.App;
using System.Threading.Tasks;
using Android;
using Android.Support.V4.Content;
using AlertDialog = Android.Support.V7.App.AlertDialog;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Xamarin.Essentials;
using Plugin.CurrentActivity;

namespace INGota.Droid
{
    [Activity(Label = "INGdemo", Icon = "@drawable/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);

            OxyPlot.Xamarin.Forms.Platform.Android.PlotViewRenderer.Init();

            LoadApplication(new App());

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            CrossCurrentActivity.Current.Init(this, savedInstanceState);

            GetAllPermissions();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            Plugin.Permissions.PermissionsImplementation.Current.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        void DoGetPermission(string[] items, int code)
        {
            RequestPermissions(items, code);
        }

        async void GetPermission(string[] items, int code, string prompt)
        {
            if (CheckSelfPermission(items[0]) == (int)Permission.Granted)
                return;

            if (prompt.Length > 0)
            {
                var contentView = Xamarin.Essentials.Platform.CurrentActivity?.FindViewById(Android.Resource.Id.Content);
                Snackbar.Make(contentView,
                       prompt,
                       Snackbar.LengthIndefinite)
                .SetAction("OK",
                            new Action<View>(delegate (View obj) {
                                DoGetPermission(items, code);
                            }
                        )
                ).Show();
            }
            else
            {
                DoGetPermission(items, code);
            }
        }

        void ShowToast(string text)
        {
            var context = Application.Context;
            ToastLength duration = ToastLength.Short;

            var toast = Toast.MakeText(context, text, duration);
            toast.Show();
        }

        void GetAllPermissions()
        {
            GetPermission(new string[] { Manifest.Permission.AccessCoarseLocation, Manifest.Permission.AccessFineLocation }, 1, "Location must be allowed to access BLE.");

        }
    }
}