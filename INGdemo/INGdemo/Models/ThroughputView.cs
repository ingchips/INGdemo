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

using INGota.FOTA;
using OxyPlot.Xamarin.Forms;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace INGdemo.Models
{
    class ThroughputViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("2445314a-a1d4-4874-b4d1-fdfb6f501485");
        static public Guid GUID_CHAR_IN = new Guid("bf83f3f1-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_OUT = new Guid("bf83f3f2-399a-414d-9035-ce64ceb3ff67");
        static public string SERVICE_NAME = "INGChips Throughput Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x2301);

        Label CurM2STpt;
        Label CurS2MTpt;
        Label MtuInfo;
        Entry MtuInput;
        Button BtnStart;
        Button BtnStop;
        Button BtnS2MStart;
        Button BtnS2MStop;
        Label Status;
        IService service;
        IDevice BleDevice;
        ICharacteristic charInput;
        ICharacteristic charOutput;
        PlotView TptView;
        LineSeries TptM2SSeries;
        LineSeries TptS2MSeries;
        int S2MByteCounter;
        bool S2MRunning;
        long StartByteNumber;

        int MtuSize = 23;
        bool FTesting = false;
        bool Testing { get { return FTesting; } set
            {
                FTesting = value;
                Device.BeginInvokeOnMainThread(() =>
                    Status.Text = value ? "Testing" : "");
            }
        }
        CancellationTokenSource source;

        Label CreateSpeedLabel(Style style, string Text)
        {
            var r = new Label();
            r.Style = style;
            r.VerticalTextAlignment = TextAlignment.End;
            r.Text = Text;
            return r;
        }
        View InitSpeed()
        {
            CurM2STpt = CreateSpeedLabel(Device.Styles.TitleStyle, "");
            CurS2MTpt = CreateSpeedLabel(Device.Styles.TitleStyle, "");

            var toolbar2 = new StackLayout();
            toolbar2.Orientation = StackOrientation.Horizontal;

            toolbar2.Children.Add(CreateSpeedLabel(Device.Styles.CaptionStyle, "M->S: "));
            toolbar2.Children.Add(CurM2STpt);
            toolbar2.Children.Add(new BoxView { WidthRequest = 10 });
            toolbar2.Children.Add(CreateSpeedLabel(Device.Styles.CaptionStyle, "S->M: "));
            toolbar2.Children.Add(CurS2MTpt);

            return toolbar2;
        }

        void InitUI()
        {
            var layout = new StackLayout();
            MtuInfo = new Label();
            
            var toolbar2 = new StackLayout();
            toolbar2.Orientation = StackOrientation.Horizontal;
            var toolbar3 = new StackLayout();
            toolbar3.Orientation = StackOrientation.Horizontal;

            BtnStart = new Button();
            BtnStop = new Button();
            BtnS2MStart = new Button();
            BtnS2MStop = new Button();
            Status = new Label();
            Status.VerticalTextAlignment = TextAlignment.Center;
            BtnStart.Text = "M->S Start";
            BtnStart.Clicked += BtnStart_Clicked;
            BtnStop.Text = "M->S Stop";
            BtnStop.Clicked += BtnStop_Clicked;

            BtnS2MStart.Text = "S->M Start";
            BtnS2MStart.Clicked += BtnS2MStart_Clicked;
            BtnS2MStop.Text = "S->M Stop";
            BtnS2MStop.Clicked += BtnS2MStop_Clicked;

            toolbar2.Margin = 10;
            toolbar2.Spacing = 20;
            toolbar2.Children.Add(BtnStart);
            toolbar2.Children.Add(BtnStop);
            toolbar2.Children.Add(Status);

            toolbar3.Margin = 10;
            toolbar3.Spacing = 20;
            toolbar3.Children.Add(BtnS2MStart);
            toolbar3.Children.Add(BtnS2MStop);

            layout.Children.Add(InitSpeed());

            switch (Device.RuntimePlatform)
            {
                case Device.UWP:
                    var container = new StackLayout();
                    var label = new Label();
                    label.Text = "MTU (Bytes): ";
                    label.Style = Device.Styles.CaptionStyle;
                    label.VerticalTextAlignment = TextAlignment.Center;
                    MtuInput = new Entry();
                    MtuInput.Text = "200";
                    container.Orientation = StackOrientation.Horizontal;
                    container.Margin = 10;
                    container.Children.Add(label);
                    container.Children.Add(MtuInput);
                    layout.Children.Add(container);
                    break;
                default:
                    MtuInfo.Style = Device.Styles.CaptionStyle;
                    MtuInfo.HorizontalOptions = LayoutOptions.Center;
                    layout.Children.Add(MtuInfo);
                    break;
            }

            layout.Children.Add(toolbar2);
            layout.Children.Add(toolbar3);
            layout.Children.Add(new Label
            {
                Text = "To test on both directions: 1. S->M Start; 2. M->S Start; ...; 3. M->S Stop; 4. S->M Stop.",
                LineBreakMode = LineBreakMode.WordWrap
            });

            var model = new PlotModel { Title = "BLE Throughput" };
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                IntervalType = DateTimeIntervalType.Seconds,
                StringFormat = "HH:mm:ss",
                Title = "Time"
            });
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Throughput (kbps)"
            });
            TptM2SSeries = new LineSeries
            {
                StrokeThickness = 2,
                Title = "M->S"
            };
            TptS2MSeries = new LineSeries
            {
                StrokeThickness = 2,
                Title = "S->M"
            };

            TptView = new PlotView
            {
                Model = model,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 300
            };
            model.Series.Add(TptM2SSeries);
            model.Series.Add(TptS2MSeries);
            layout.Children.Add(TptView);

            layout.Margin = 20;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;
            Content = new ScrollView { Content = layout };
            Title = SERVICE_NAME;
        }

        async private void BtnS2MStop_Clicked(object sender, EventArgs e)
        {
            S2MRunning = false;
            if (charOutput != null)
            {
                try { await charOutput.StopUpdatesAsync(); } catch (Exception) { }
                CurS2MTpt.Text = "Stopped";
            }
        }

        async private void BtnS2MStart_Clicked(object sender, EventArgs e)
        {
            if (S2MRunning) return;

            if (charOutput != null)
            {
                TptS2MSeries.Points.Clear();
                TptView.Model.ResetAllAxes();
                TptView.Model.InvalidatePlot(true);
                S2MByteCounter = 0;
                S2MRunning = true;
                await charOutput.StartUpdatesAsync();

                Device.StartTimer(TimeSpan.FromSeconds(1), () =>
                {
                    if (!S2MRunning) return false;
                    SaveSpeed(CurS2MTpt, TptS2MSeries, S2MByteCounter * 8.0 / 1000);
                    S2MByteCounter = 0;
                    return true; // True = Repeat again, False = Stop the timer
                });
            }
        }

        private void BtnStop_Clicked(object sender, EventArgs e)
        {
            if (source != null)
            {
                source.Cancel();
                source = null;
            }
        }

        async private void BtnStart_Clicked(object sender, EventArgs e)
        {
            if (!Testing)
            {
                TptM2SSeries.Points.Clear();                
                TptView.Model.ResetAllAxes();
                TptView.Model.InvalidatePlot(true);                

                Testing = true;
                StartByteNumber = await GetTotalBytes();
                source = new CancellationTokenSource();

                var t = new Task(() => Run(source.Token));
                t.Start();
            }
        }

        async Task<long> GetTotalBytes()
        {
            var buf = await charInput.ReadAsync();
            return buf?.Length >= 4 ? Utils.ParseLittleInt(buf) : 0;
        }

        async private void Run(CancellationToken token)
        {
            var buf = new byte[MtuSize];
            var now = DateTime.Now;
            long total = 0;
            long dev_total = 0;
            long my_total = 0;
            var error = false;
            int cnt = 0;
            while (!error && !token.IsCancellationRequested)
            {
                try
                {
                    if (my_total - dev_total < 2000)
                    {
                        if (await charInput.WriteAsync(buf))
                        {
                            cnt++;
                            total += MtuSize;
                            my_total += MtuSize;
                        }
                    }
                    else
                        await Task.Delay(200);
                }
                catch (Exception) { error = true; }

                if (cnt >= 5)
                {
                    // verify data has been written into device
                    dev_total = await GetTotalBytes() - StartByteNumber;
                    if (my_total - dev_total < 10)
                        cnt = 0;
                }

                var temp = DateTime.Now;
                double span = (temp - now).TotalMilliseconds;
                if (span > 1000.0)
                {
                    SaveSpeed(CurM2STpt, TptM2SSeries, total * 8.0 / span);
                    now = temp;
                    total = 0;
                }
            }
            Testing = false;
            Device.BeginInvokeOnMainThread(() => CurM2STpt.Text = "Stopped");
        }

        private void SaveSpeed(Label label, LineSeries Series, double kbps)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                label.Text = string.Format("{0:F2} kbps", kbps);
                Series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), kbps));
                if (Series.Points.Count > 50)
                    Series.Points.RemoveAt(0);
                TptView.Model.InvalidatePlot(true);
            }
            );
        }

        async void Read()
        {
            charInput = await service.GetCharacteristicAsync(GUID_CHAR_IN);
            charOutput = await service.GetCharacteristicAsync(GUID_CHAR_OUT);
            if (charOutput != null)
            {
                charOutput.ValueUpdated += CharOutput_ValueUpdated;
            }

            switch (Device.RuntimePlatform)
            {
                case Device.UWP:
                    MtuSize = int.Parse(MtuInput.Text);
                    break;
                default:
                    MtuSize = Math.Max(Utils.BLE_MIN_MTU_SIZE, await BleDevice.RequestMtuAsync(200) - 3);
                    MtuInfo.Text = string.Format("MTU = {0} B {1}", MtuSize);
                    break;
            }

            BleDevice.UpdateConnectionInterval(ConnectionInterval.High);
        }

        private void CharOutput_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            S2MByteCounter += (int)charOutput.Value?.Length;
        }

        public ThroughputViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (Testing)
            {
                BtnStop_Clicked(null, null);
            }
            BtnS2MStop_Clicked(null, null);
        }
    }
}