using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;

using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;

using Microcharts;
using Microcharts.Forms;

using SkiaSharp;
using SkiaSharp.Views.Forms;

using INGota.FOTA;

namespace INGdemo.Models
{
    internal class LightControl
    {
        Color[] borderColors = new Color[] { Color.Black, Color.Red, Color.Green, Color.Blue, Color.White };
        Color[] lightColors = new Color[] { Color.DarkGray, Color.Pink, Color.LightGreen, Color.LightBlue, Color.LightGray };

        static int BTN_SIZE = 30;
        List<Button> btns;

        Button MakeBtn(int i)
        {
            Color c = lightColors[i];
            var btn = new Button();
            btn.BackgroundColor = c;
            btn.HeightRequest = BTN_SIZE;
            btn.WidthRequest = BTN_SIZE;
            btn.CornerRadius = BTN_SIZE / 2;
            btn.BorderWidth = 2;
            btn.BorderColor = btn.BackgroundColor;
            btn.Margin = new Thickness(BTN_SIZE / 4, 0, 0, 0);
            btn.Clicked += (object sender, EventArgs e) =>
            {
                Color x = borderColors[i];
                byte[] rgb = new byte[] { (byte)(x.R * 255), (byte)(x.G * 255), (byte)(x.B * 255) };
                ColorChanged.Invoke(btn, rgb);
            };
            return btn;
        }

        public LightControl()
        {
            btns = new List<Button>();

            for (var i = 0; i < lightColors.Length; i++)
                btns.Add(MakeBtn(i));

            var stack = new StackLayout
            {
                Orientation = StackOrientation.Horizontal                
            };

            foreach (var b in btns)
                stack.Children.Add(b);
            view = stack;
        }

        public View view;

        public event EventHandler<byte[]> ColorChanged;

        public void SetCurrentRGB(byte r, byte g, byte b)
        {
            Color x = Color.FromRgb(r, g, b);
            for (var i = 0; i < borderColors.Length; i++)
            {
                var c = borderColors[i];
                var btn = btns[i];
                btn.BackgroundColor = c == x ? c : lightColors[i];
            }
        }
    }

    internal class Room
    {
        Label temperature;
        Label offlineInd;
        ChartView chartView;
        LightControl lightCtrl;
        View viewTemp;
        View viewLight;
        Frame frame;
        bool hasLight;
        Microcharts.Entry[] tempEntries;
        int id;

        static int TEMP_SIZE = 60;
        readonly Color TEXT_COLOR = Color.LightGray;
        readonly Color BK_COLOR = Color.DarkSlateBlue;
        readonly Color BK_COLOR_OFFLINE = Color.DarkGray;

        View MakeTempInd()
        {
            var stack = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            tempEntries = new Microcharts.Entry[20];
            for (var i = 0; i < tempEntries.Length; i++)
                tempEntries[i] = new Microcharts.Entry(10);

            var chart = new LineChart
            { 
                Entries = tempEntries,
                BackgroundColor = BK_COLOR.ToSKColor()
            };
            chartView = new ChartView
            {
                Chart = chart,                
                HorizontalOptions = LayoutOptions.FillAndExpand
            };

            temperature = new Label
            {
                Style = Device.Styles.CaptionStyle,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.End,
                Text = "°C",
                WidthRequest = TEMP_SIZE,
                TextColor = TEXT_COLOR
            };
            
            stack.Children.Add(new Label
            {
                Text = Char.ConvertFromUtf32(0x1F321),
                FontSize = 20,
                VerticalTextAlignment = TextAlignment.Center
            });
            stack.Children.Add(chartView);
            stack.Children.Add(temperature);
            return stack;
        }

        View MakeLights()
        {
            var stack = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.StartAndExpand,
                VerticalOptions = LayoutOptions.End
            };
            stack.Children.Add(new Label
            {
                Text = Char.ConvertFromUtf32(0x1F4A1),
                FontSize = 20,
                VerticalTextAlignment = TextAlignment.Center
            });
            stack.Children.Add(lightCtrl.view);
            return stack;
        }
        public Room(string caption, int id)
        {
            this.id = id;
            lightCtrl = new LightControl();
            lightCtrl.ColorChanged += LightCtrl_ColorChanged;

            var stack = new StackLayout();

            var labelStack = new StackLayout
            {
                Orientation = StackOrientation.Horizontal
            };

            var label = new Label
            {
                Style = Device.Styles.SubtitleStyle,
                HorizontalTextAlignment = TextAlignment.Start,
                VerticalTextAlignment = TextAlignment.Center,
                Text = caption,
                TextColor = TEXT_COLOR
            };
            offlineInd = new Label
            {
                Style = Device.Styles.ListItemTextStyle,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.End,
                Text = "offline",
                TextColor = TEXT_COLOR
            };
            labelStack.Children.Add(label);
            labelStack.Children.Add(offlineInd);

            // stack.Children.Add(new BoxView() { Color = Color.Gray, HeightRequest = 1, Opacity = 0.5 });
            stack.Children.Add(labelStack);
            viewTemp = MakeTempInd();
            viewLight = MakeLights();
            stack.Children.Add(viewTemp);
            stack.Children.Add(viewLight);

            frame = new Frame
            {
                CornerRadius = 10,
                Padding = 10,
                Content = stack,
                BackgroundColor = BK_COLOR_OFFLINE
            };

            view = frame;
            SetDevStatus(0);
        }

        private void LightCtrl_ColorChanged(object sender, byte[] e)
        {
            var arg = new RoomLightCtrlEvt { id = this.id, rgb = e };
            LightColorChanged.Invoke(this, arg);
        }

        public View view;

        public struct RoomLightCtrlEvt
        {
            public int id;
            public byte[] rgb;
        }

        public event EventHandler<RoomLightCtrlEvt> LightColorChanged;

        public void SetCurrentRGB(byte r, byte g, byte b)
        {
            lightCtrl.SetCurrentRGB(r, g, b);
        }

        SKColor TemperatureColor(float v)
        {
            if (v > 35) v = 35;
            else if (v < 15) v = 15;
            var hue = 270 * (35 - v) / 20;
            return SKColor.FromHsl(hue, 100, 50);
        }

        public void RecordTemp(string value)
        {            
            temperature.Text = value + "°C";
            try
            {
                float v = float.Parse(value);
                for (var i = 0; i < tempEntries.Length - 1; i++)
                    tempEntries[i] = tempEntries[i + 1];
                tempEntries[tempEntries.Length - 1] = new Microcharts.Entry(v)
                {
                    Color = TemperatureColor(v)
                };
                chartView.InvalidateSurface();
            }
            catch
            {
            }
        }

        public void SetDevStatus(byte status)
        {
            hasLight = (status & 2) != 0;
            viewTemp.IsVisible = (status & 1) != 0;
            viewLight.IsVisible = hasLight;

            offlineInd.IsVisible = status == 0;
            frame.BackgroundColor = status == 0 ? BK_COLOR_OFFLINE : BK_COLOR;
        }

        public int Id { get => id; }
        public bool HasLight { get => hasLight; }
    }

    class SmartHomeViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("00000004-494e-4743-4849-505355554944");
        static public Guid GUID_CHAR_CTRL = new Guid("bf83f3f1-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_STATUS = new Guid("bf83f3f2-399a-414d-9035-ce64ceb3ff67");
        static public string SERVICE_NAME = "INGChips Smart Home Hub Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F3E0);

        IService service;
        IDevice BleDevice;
        ICharacteristic charCtrl;
        ICharacteristic charStatus;

        List<Room> rooms = new List<Room>();
        HomeTitle homeTitle;

        enum CmdCode
        {
            DeviceStatus, // Packet format: CmdCode, DEV ID, set[StartHomeDevice]  
            TemperatureReport, // Packet format: CmdCode, Dev ID, same as temperature measurement char
            RGB // Packet format: CmdCode, Dev ID, R, G, B
        };    

        internal class HomeTitle
        {
            LightControl lightCtrl;

            public HomeTitle(string caption)
            {
                lightCtrl = new LightControl();
                lightCtrl.ColorChanged += LightCtrl_ColorChanged;

                var stack = new StackLayout();
                var label = new Label
                {
                    Style = Device.Styles.TitleStyle,
                    VerticalTextAlignment = TextAlignment.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Text = caption
                };
                stack.Children.Add(label);
                lightCtrl.view.HorizontalOptions = LayoutOptions.Center;
                stack.Children.Add(lightCtrl.view);
                view = stack;
            }

            private void LightCtrl_ColorChanged(object sender, byte[] e)
            {
                LightColorChanged.Invoke(this, e);
            }

            public View view;

            public event EventHandler<byte[]> LightColorChanged;
        }

        void InitUI()
        {
            var grid = new Grid
            {
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(0.5, GridUnitType.Star) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };

            homeTitle = new HomeTitle("Smart Home Dashboard");

            grid.Children.Add(homeTitle.view, 0, 1, 0, 1);

            for (var i = 0; i <= 3; i++)
            {
                rooms.Add(new Room("Room " + (i + 1).ToString(), i));
                rooms[i].LightColorChanged += async (sender, evt) => await RoomLightChanged(evt.id, evt.rgb);
            }

            homeTitle.LightColorChanged += HomeTitle_LightColorChanged;

            grid.Children.Add(rooms[0].view, 0, 1);
            grid.Children.Add(rooms[1].view, 0, 2);
            grid.Children.Add(rooms[2].view, 0, 3);
            grid.Children.Add(rooms[3].view, 0, 4);

            grid.Margin = 5;

            Content = new ScrollView { Content = grid };
            Title = SERVICE_NAME;
        }

        private async void HomeTitle_LightColorChanged(object sender, byte[] rgb)
        {
            foreach (var room in rooms)
            {
                if (!room.HasLight) continue;
                await RoomLightChanged(room.Id, rgb);
            }
        }


        async Task<bool> RoomLightChanged(int id, byte[] rgb)
        {
            byte[] cmd = new byte[1 + 1 + 3] { (byte)CmdCode.RGB, (byte)id, rgb[0], rgb[1], rgb[2] };
            return await charCtrl.WriteAsync(cmd);
        }

        async void Read()
        {
            charCtrl = await service.GetCharacteristicAsync(GUID_CHAR_CTRL);
            charStatus = await service.GetCharacteristicAsync(GUID_CHAR_STATUS);            

            await BleDevice.RequestMtuAsync(50);
            BleDevice.UpdateConnectionInterval(ConnectionInterval.Normal);
            if (charStatus != null)
            {
                charStatus.ValueUpdated += CharStatus_ValueUpdated;
                await charStatus.StartUpdatesAsync();
            }
        }

        private void CharStatus_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                var v = e.Characteristic.Value;
                int id;
                if (v.Length < 2) return;
                
                switch ((CmdCode)v[0])
                {
                    case CmdCode.DeviceStatus:
                        {
                            id = v[1];
                            if (id >= rooms.Count) return;
                            for (var j = 0; j < (v.Length - 1) / 2; j++)
                            {
                                rooms[v[1 + 2 * j]].SetDevStatus(v[1 + 2 * j + 1]);
                            }
                        }
                        break;
                    case CmdCode.TemperatureReport:
                        id = v[1];
                        if (id >= rooms.Count) return;
                        if (v.Length != 2 + 4) return;
                        var t = new byte[4];
                        for (var i = 0; i < 4; i++)
                            t[i] = v[2 + i];
                        rooms[id].RecordTemp(Utils.float_ieee_11073_val_to_repr((UInt32)Utils.ParseLittleInt(t)));
                        break;
                    case CmdCode.RGB:
                        for (int k = 0; k < (v.Length - 1) / 3; k++)
                        {
                            if (k >= rooms.Count) return;
                            rooms[k].SetCurrentRGB(v[1 + 3 * k + 0], v[1 + 3 * k + 1], v[1 + 3 * k + 2]);
                        }
                        break;
                }
            });
        }

        public SmartHomeViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        protected override void OnDisappearing()
        {
            if (charStatus != null)
            {
                charStatus.StopUpdatesAsync();
            }
            base.OnDisappearing();
        }
    }
}