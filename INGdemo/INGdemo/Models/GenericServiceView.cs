using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;

using INGota.FOTA;

namespace INGdemo.Models
{
    class GenericServiceView : ContentPage
    {
        static public string SERVICE_NAME = "Unknown Service";
        static public string ICON_STR = "⁇";

        IService service;
        StackLayout stack;

        Label[] CreateItem(string title)
        {
            var r = new Label[] { new Label(), new Label() };
            r[0].Text = title;
            r[0].FontSize = Device.GetNamedSize(NamedSize.Large, r[0]);
            return r;
        }

        public void InitUI()
        {
            stack = new StackLayout();
            stack.Padding = 10;

            Content = new ScrollView
            {
                Content = stack
            };
            Title = SERVICE_NAME;
        }

        void ShowValue(Editor editor, byte[] data)
        {
            if (data == null) return;
            Device.BeginInvokeOnMainThread(() =>
            {
                editor.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:ms") + ":\n" +
                    Utils.PrintHexTable(data) + "\n\n" +
                    (editor.Text?.Length < 100 * 1024 ? editor.Text : "");
            });
        }

        View MakeReadView(ICharacteristic c)
        {
            var stack = new StackLayout();
            var bar = new StackLayout();
            var DataLog = new Editor
            {
                FontFamily = Utils.GetMonoFamily(),
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 200,
            };

            bar.Orientation = StackOrientation.Horizontal;
            bar.Padding = 10;
            if (c.CanRead)
            {
                var b = new Button();
                b.Text = "Read";
                b.Clicked += async (object sender, EventArgs e) =>
                {
                    var v = await c.ReadAsync();

                    ShowValue(DataLog, v);
                };
                bar.Children.Add(b);
            }
            c.ValueUpdated += (object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e) =>
            {
                ShowValue(DataLog, e.Characteristic.Value);
            };

            if (c.CanUpdate)
            {
                var b = new Button();
                b.Text = "Subscribe";
                
                b.Clicked += async (object sender, EventArgs e) =>
                {
                    if (b.Text == "Subscribe")
                    {                        
                        await c.StartUpdatesAsync();
                        b.Text = "Unsubscribe";
                    }
                    else
                    {
                        await c.StopUpdatesAsync();
                        b.Text = "Subscribe";
                    }
                };
                bar.Children.Add(b);
            }
            stack.Children.Add(DataLog);
            stack.Children.Add(bar);
            return stack;
        }

        View MakeWriteView(ICharacteristic c)
        {
            var stack = new StackLayout();
            var bar = new StackLayout();
            var DataLog = new Editor
            {
                FontFamily = Utils.GetMonoFamily(),
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 200,
                Text = "00 01 02"
            };

            bar.Orientation = StackOrientation.Horizontal;
            bar.Padding = 10;

            {
                var b = new Button();
                b.Text = "Write";
                b.Clicked += async (object sender, EventArgs e) =>
                {
                    if (DataLog.Text != null)
                    {
                        var val = Utils.ParseBytes(DataLog.Text);
                        if (val.Length > 0)
                            await c.WriteAsync(val);
                    }
                    
                };
                bar.Children.Add(b);
            }
            stack.Children.Add(DataLog);
            stack.Children.Add(bar);
            return stack;
        }

        string BoolToIcon(bool v)
        {
            return v ? "✔" : "✖";
        }

        View MakeView(ICharacteristic c)
        {
            var stack = new StackLayout();
            stack.Padding = 2;
            var label = new Label();
            label.Text = c.Uuid;
            label.Style = Device.Styles.ListItemTextStyle;
            var prop = new Label();
            prop.Text = "Properties: Read " + BoolToIcon(c.CanRead)
                + "    Update" + BoolToIcon(c.CanUpdate)
                + "    Write" + BoolToIcon(c.CanWrite);
            prop.Style = Device.Styles.ListItemDetailTextStyle;
            stack.Children.Add(label);
            stack.Children.Add(prop);
            if (c.CanRead || c.CanUpdate)
                stack.Children.Add(MakeReadView(c));
            if (c.CanWrite)
                stack.Children.Add(MakeWriteView(c));
            stack.Children.Add(label);
            return stack;
        }

        async void Read()
        {
            if (service == null)
                return;

            var chars = await service.GetCharacteristicsAsync();
            foreach (var c in chars) {
                stack.Children.Add(MakeView(c));
            }
        }

        public GenericServiceView(IDevice ADevice, IService service)
        {
            InitUI();
            this.service = service;
            Read();
        }
    }
}
