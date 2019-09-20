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

using INGdemo.Lib;
using SkiaSharp.Views.Forms;
using SkiaSharp;

namespace INGdemo.Models
{
    class DirFindingViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("00000003-494e-4743-4849-505355554944");
        static public Guid GUID_CHAR_CFG = new Guid("10000001-494e-4743-4849-505355554944");
        static public Guid GUID_CHAR_IQ = new Guid("bf83f3f2-399a-414d-9035-ce64ceb3ff67");
        static public string SERVICE_NAME = "INGChips Direction Finding Service";
        static public string ICON_STR = "🧭";

        IService service;
        IDevice BleDevice;
        ICharacteristic charCfg;
        ICharacteristic charIQ;

        Label AntCfg;
        Label AngleLable;

        AngleEstimator Estimator;
        AngleIndicatorControl Indicator;

        public void InitUI()
        {
            var layout = new StackLayout
            {
                VerticalOptions = LayoutOptions.FillAndExpand,
                Padding = new Thickness(10, 10, 10, 10)
            };

            AntCfg = new Label
            {
                Text = "---",
                Style = Device.Styles.CaptionStyle,
                HorizontalTextAlignment = TextAlignment.Center
            };

            AngleLable = new Label
            {
                Text = "",
                Style = Device.Styles.SubtitleStyle,
                HorizontalTextAlignment = TextAlignment.Center
            };

            layout.Children.Add(AngleLable);
            layout.Children.Add(AntCfg);
            

            Indicator = new AngleIndicatorControl
            {
                VerticalOptions = LayoutOptions.FillAndExpand,
                HorizontalOptions = LayoutOptions.FillAndExpand
            };
            layout.Children.Add(Indicator);

            Content = layout; //  new ScrollView { Content = layout };
            Title = SERVICE_NAME;
        }

        async void Read()
        {
            var chars = await service.GetCharacteristicsAsync();
            charCfg = chars.FirstOrDefault((x) => x.Id.Equals(GUID_CHAR_CFG));
            charIQ = chars.FirstOrDefault((x) => x.Id.Equals(GUID_CHAR_IQ));
            charIQ.ValueUpdated += CharOutput_ValueUpdated;
            await charCfg.ReadAsync();
            if (charCfg.Value.Length > 0)
            {
                Estimator = AngleEstimatorFactory.CreateEstimator(charCfg.Value[0]);
                if (Estimator != null)
                {
                    AntCfg.Text = Estimator.Description;
                    await BleDevice.RequestMtuAsync(250);
                    await charIQ.StartUpdatesAsync();
                }
                else
                    AntCfg.Text = string.Format("invalide cfg: {0}", charCfg.Value[0]);
            }
            else
                AntCfg.Text = "Unknown Error";
        }

        private void CharOutput_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            var b = charIQ.Value;
            if (b?.Length < 17) return;

            double angle = Estimator.Estimate(b[0], b.Skip(1).Select(x => (sbyte)x).ToArray());
            Device.BeginInvokeOnMainThread(() =>
            {
                AngleLable.Text = string.Format("{0:F1} Deg", 360 * angle / (2 * Math.PI));
                Indicator.AngleRad = angle;
            });
        }

        public DirFindingViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        async protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (BleDevice.State == DeviceState.Connected) await charIQ.StopUpdatesAsync();
        }
    }

    internal class AngleIndicatorControl : SKCanvasView
    {
        double FAngle;

        enum TextPosition
        {
            Left,
            Right,
            Top,
            Bottom
        }

        void DrawText(SKCanvas canvas, SKPaint paint, string text, float x, float y, TextPosition pos)
        {
            var bounds = new SKRect();
            paint.MeasureText(text, ref bounds);
            float width = bounds.Width;
            float height = bounds.Height;
            switch (pos)
            {
                case TextPosition.Left:
                    canvas.DrawText(text, x - width, y + height / 2, paint);
                    break;
                case TextPosition.Right:
                    canvas.DrawText(text, x, y + height / 2, paint);
                    break;
                case TextPosition.Top:
                    canvas.DrawText(text, x - width / 2, y, paint);
                    break;
                case TextPosition.Bottom:
                    canvas.DrawText(text, x - width / 2, y + height, paint);
                    break;
            }
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var min = (float)Math.Min(CanvasSize.Width, CanvasSize.Height);
            float radius = min * 0.4f;
            float scaling = (float)(CanvasSize.Width / Width);
            var center = new SKPoint(CanvasSize.Width / 2, min * 0.1f + radius);
            SKPaint paint = new SKPaint
            {
                Color = Color.LightGray.ToSKColor(),
                StrokeWidth = 5 * scaling,
                Style = SKPaintStyle.StrokeAndFill,
                TextSize = 12 * scaling,
                IsAntialias = true
            };
            
            canvas.Clear();
            canvas.DrawCircle(center, radius, paint);

            // draw lines
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1 * scaling;
            paint.Color = Color.Gray.ToSKColor();
            canvas.DrawLine(center.X - radius, center.Y, center.X + radius, center.Y, paint);
            canvas.DrawLine(center.X, center.Y - radius, center.X, center.Y + radius, paint);

            // degree markers
            paint.Color = Color.Black.ToSKColor();
            DrawText(canvas, paint, "0", center.X + radius, center.Y, TextPosition.Left);
            DrawText(canvas, paint, "90",  center.X, center.Y - radius, TextPosition.Bottom);
            DrawText(canvas, paint, "180", center.X - radius, center.Y, TextPosition.Right);
            DrawText(canvas, paint, "270", center.X , center.Y + radius, TextPosition.Top);

            paint.PathEffect = SKPathEffect.CreateDash(new float[] {10,10}, 0);
            canvas.DrawCircle(center, radius / 3, paint);
            canvas.DrawCircle(center, radius * 2 / 3, paint);

            paint.PathEffect = null;
            paint.Color = Color.Orange.ToSKColor();
            paint.StrokeCap = SKStrokeCap.Round;
            paint.StrokeWidth = 5 * scaling;
            var p1 = new SKPoint((float)(center.X + radius * 0.9f * Math.Cos(FAngle)),
                                 (float)(center.Y - radius * 0.9f * Math.Sin(FAngle)));
            canvas.DrawLine(center, p1, paint);

            paint.Color = Color.Gray.ToSKColor();
            paint.Style = SKPaintStyle.StrokeAndFill;
            canvas.DrawCircle(center, 5 * scaling, paint);
        }

        internal double AngleRad
        {
            get => FAngle;
            set {
                FAngle = value;
                InvalidateSurface();
            }
        }
    }
}
