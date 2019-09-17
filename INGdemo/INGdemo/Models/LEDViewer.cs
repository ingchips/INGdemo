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

namespace INGdemo.Models
{
    class LEDViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("6a33a526-e004-4793-a084-8a1dc49b84fd");
        static public string SERVICE_NAME = "INGChips RGB Lighting Service";
        static Guid GUID_CHAR_RGB = new Guid("1c190e92-37dd-4ac4-8154-0444c69274c2");
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F4A1);

        IService service;
        ICharacteristic charRGB;

        BoxView ColorIndicator;

        Slider SliderRed;
        Slider SliderGreen;
        Slider SliderBlue;

        public View MakeSlider(string label, out Slider slider)
        {
            var layout = new StackLayout();
            layout.Orientation = StackOrientation.Horizontal;
            layout.HorizontalOptions = LayoutOptions.Fill;

            layout.Children.Add(new Label { Text = label, Style=Device.Styles.TitleStyle });
            slider = new Slider(0, 255, 255);
            slider.HorizontalOptions = LayoutOptions.FillAndExpand;
            layout.Children.Add(slider);
            return layout;
        }

        public void InitUI()
        {
            var layout = new StackLayout();
            
            layout.Margin = 20;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;

            ColorIndicator = new BoxView { Color = Color.White };
            layout.Children.Add(ColorIndicator);
            layout.Children.Add(MakeSlider("R", out SliderRed));
            layout.Children.Add(MakeSlider("G", out SliderGreen));
            layout.Children.Add(MakeSlider("B", out SliderBlue));
            Content = new ScrollView { Content = layout };
            Title = SERVICE_NAME;

            SliderRed.ValueChanged += SliderRed_ValueChanged;
            SliderGreen.ValueChanged += SliderGreen_ValueChanged;
            SliderBlue.ValueChanged += SliderBlue_ValueChanged;
        }

        async private void SliderBlue_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            await SetColor();
        }

        async private void SliderGreen_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            await SetColor();
        }

        async private void SliderRed_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            await SetColor();
        }

        async Task SetColor()
        {
            byte[] rgb = new byte[] { (byte)SliderRed.Value, (byte)SliderGreen.Value, (byte)SliderBlue.Value };
            await charRGB.WriteAsync(rgb);
            ColorIndicator.Color = new Color(rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0);
        }

        async Task Init()
        {
            var chars = await service.GetCharacteristicsAsync();
            charRGB = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_RGB);
        }

        public LEDViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Init();
        }
    }
}
