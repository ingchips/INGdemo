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
    class PianoViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("00000002-494e-4743-4849-505355554944");
        static public Guid GUID_CHAR_KEY = new Guid("bf83f3f1-399a-414d-9035-ce64ceb3ff67");
        static public string SERVICE_NAME = "INGChips Piano Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F3B9);

        IService service;
        ICharacteristic charKey;

        struct KeyInfo
        {
            public string Key;
            public double Freq;
        };

        KeyInfo[] Keys = new KeyInfo[] {
            new KeyInfo{ Key = "F3", Freq = 174.61},
            new KeyInfo{ Key = "G3", Freq = 196.00},
            new KeyInfo{ Key = "A3", Freq = 220.00},
            new KeyInfo{ Key = "B3", Freq = 246.94},
            new KeyInfo{ Key = "C4", Freq = 261.63},
            new KeyInfo{ Key = "D4", Freq = 293.66},
            new KeyInfo{ Key = "E4", Freq = 329.63},
            new KeyInfo{ Key = "F4", Freq = 349.23},
            new KeyInfo{ Key = "G4", Freq = 392.00},
            new KeyInfo{ Key = "A4", Freq = 440},
            new KeyInfo{ Key = "B4", Freq = 493.88},
            new KeyInfo{ Key = "C5", Freq = 523.25}
        };

        Dictionary<Button, double> BtnDict = new Dictionary<Button, double>();

        void InitUI()
        {
            var layout = new RelativeLayout();

            layout.Margin = 20;
            layout.HorizontalOptions = LayoutOptions.Fill;

            var total = Keys.Length;

            for (int local_i = 0; local_i < total; local_i++)
            {
                var btn = new Button();
                btn.HeightRequest = 200;
                btn.HorizontalOptions = LayoutOptions.FillAndExpand;
                btn.BackgroundColor = Color.White;
                btn.BorderWidth = 1;
                btn.BorderColor = Color.Gray;

                BtnDict.Add(btn, Keys[local_i].Freq);

                int i = local_i;

                layout.Children.Add(btn, Constraint.RelativeToParent((parent) =>
                {
                    return parent.Width / total * i;
                }),
                Constraint.RelativeToParent((parent) =>
                {
                    return 0;
                }),
                Constraint.RelativeToParent((parent) =>
                {
                    return parent.Width / total;
                }),
                Constraint.RelativeToParent((parent) =>
                {
                    return 200;
                }));

                var label = new Label();
                label.Style = Device.Styles.CaptionStyle;
                label.Text = Keys[local_i].Key;
                label.HorizontalTextAlignment = TextAlignment.Center;

                layout.Children.Add(label, Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return btn.X;
                    }),
                    Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return btn.Y + btn.Height + 5;
                    }),
                    Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return btn.Width;
                    }),
                    Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return 30;
                    }));

                if ((label.Text[0] != 'C') && (label.Text[0] != 'F'))
                {
                    var blackBtn = new Button();
                    blackBtn.HorizontalOptions = LayoutOptions.FillAndExpand;
                    blackBtn.BackgroundColor = Color.Black;
                    btn.BorderWidth = 1;
                    blackBtn.BorderColor = Color.Gray;

                    BtnDict.Add(blackBtn, Keys[local_i].Freq / 1.05946);

                    layout.Children.Add(blackBtn, Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return btn.X - 0.4 * btn.Width;
                    }),
                    Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return btn.Y;
                    }),
                    Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return btn.Width * 0.8;
                    }),
                    Constraint.RelativeToView(btn, (_layout, v) =>
                    {
                        return btn.Height * 0.7;
                    }));
                }
            }

            var tapGestureRecognizer = new TapGestureRecognizer();

            foreach (var b in BtnDict.Keys)
            {
                b.Pressed += Key_Pressed;
                b.Released += Key_Released;
            }

            Content = layout;
            Title = SERVICE_NAME;
        }

        async private Task SetFreq(double freq)
        {
            uint f = (uint)Math.Round(freq);
            byte[] cmd = new byte[2] { (byte)(f & 0xff), (byte)(f >> 8) };

            await charKey.WriteAsync(cmd);
        }
        async private void Key_Released(object sender, EventArgs e)
        {
            await SetFreq(0);
        }

        async private void Key_Pressed(object sender, EventArgs e)
        {
            await SetFreq(BtnDict[sender as Button]);
        }

        async Task Init()
        {
            var chars = await service.GetCharacteristicsAsync();
            charKey = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_KEY);
        }

        public PianoViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Init();
        }
    }
}
