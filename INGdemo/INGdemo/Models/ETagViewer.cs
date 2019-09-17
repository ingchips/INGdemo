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

using Plugin.Media;
using Plugin.Media.Abstractions;

using SkiaSharp;
using SkiaSharp.Views.Forms;
using System.IO;

using INGota.FOTA;

namespace INGdemo.Models
{
    class ETagViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("1145c2f0-6f36-45c5-8541-92f56728d5f3");
        static public Guid GUID_CHAR_CTRL = new Guid("1145c2f2-6f36-45c5-8541-92f56728d5f3");
        static public Guid GUID_CHAR_DATA = new Guid("1145c2f3-6f36-45c5-8541-92f56728d5f3");
        static public Guid GUID_CHAR_RESOLUTION = new Guid("1145c2f4-6f36-45c5-8541-92f56728d5f3");
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F3F7);

        static public string SERVICE_NAME = "INGChips E-Tag Service";

        LABColor[] LABMap;
        SKColor ThirdColor;

        // commands & status
        const int ETAG_CMD_IMG_CLEAR = 0;
        const int ETAG_CMD_IMG_USE_DEF1 = 1;
        const int ETAG_CMD_IMG_USE_DEF2 = 2;
        const int ETAG_CMD_IMG_START_WRITE = 10;
        const int ETAG_CMD_IMG_COMPLETE = 11;
        const int ETAG_STATUS_OK = 0;
        const int ETAG_STATUS_ERR = 1;

        private int IMAGE_WIDTH;
        private int IMAGE_HEIGHT;

        IService service;
        ICharacteristic charCtrl;
        ICharacteristic charData;
        SKBitmap image = null;
        SKCanvasView canvasView;

        SKBitmap imageProcessed = null;
        SKCanvasView canvasViewProcessed;

        IDevice BleDevice;
        WaitActivity Wait;

        Button einkWBR;
        Button einkWBY;

        public void InitUI()
        {
            var stack = new StackLayout();
            var toolbar = new StackLayout();
            toolbar.Orientation = StackOrientation.Horizontal;

            var clearImage = new Button();
            clearImage.Text = "Clear";
            clearImage.Pressed += ClearImage_Pressed;

            var defImage = new Button();
            defImage.Text = "Default 1";
            defImage.Pressed += DefImage_Pressed;

            var defImage2 = new Button();
            defImage2.Text = "Default 2";
            defImage2.Pressed += DefImage2_Pressed;

            var toolbar3 = new StackLayout();
            toolbar3.Orientation = StackOrientation.Horizontal;

            einkWBR = new Button();
            einkWBR.Text = " B/W/R ";
            einkWBR.BorderColor = Color.Red;
            einkWBR.BackgroundColor = new Color(1.0, 0.7, 0.7);
            einkWBR.Pressed += ColorBWR_Pressed;
            einkWBY = new Button();
            einkWBY.BorderColor = new Color(0.8, 0.8, 0.0);
            einkWBY.BackgroundColor = Color.LightYellow;
            einkWBY.Text = " B/W/Y ";
            einkWBY.Pressed += ColorBWY_Pressed;

            var toolbar2 = new StackLayout();
            toolbar2.Orientation = StackOrientation.Horizontal;

            var takeImage = new Button();
            takeImage.Text = "Take Photo";
            takeImage.Pressed += TakeImage_Pressed;

            var upload = new Button();
            upload.Text = "Upload";
            upload.Pressed += Upload_Pressed;

            toolbar.Children.Add(clearImage);
            toolbar.Children.Add(defImage);
            toolbar.Children.Add(defImage2);
            toolbar3.Children.Add(einkWBR);
            toolbar3.Children.Add(einkWBY);
            toolbar3.Children.Add(new Label()
            {
                Text = String.Format("RES: {0} x {1}", IMAGE_WIDTH, IMAGE_HEIGHT),
                VerticalTextAlignment = TextAlignment.Center
            });
            toolbar2.Children.Add(takeImage);
            toolbar2.Children.Add(upload);

            canvasView = new SKCanvasView();
            canvasViewProcessed = new SKCanvasView();
            canvasView.HorizontalOptions = LayoutOptions.Fill;
            canvasView.HeightRequest = 200;
            canvasViewProcessed.HorizontalOptions = LayoutOptions.Fill;
            canvasViewProcessed.HeightRequest = 200;

            stack.Padding = 10;
            stack.Children.Add(toolbar);
            stack.Children.Add(toolbar3);
            stack.Children.Add(toolbar2);
            stack.Children.Add(new BoxView { HeightRequest = 10 });
            stack.Children.Add(canvasView);
            stack.Children.Add(canvasViewProcessed);
            canvasView.PaintSurface += Canvas_PaintSurface;
            canvasViewProcessed.PaintSurface += CanvasViewProcessed_PaintSurface;

            Content = new ScrollView { Content = stack };
            Title = SERVICE_NAME;
        }

        async private void DefImage2_Pressed(object sender, EventArgs e)
        {
            await SendCmdAndWait(new byte[] { ETAG_CMD_IMG_USE_DEF2 });
        }

        void InitColors(SKColor[] ETAG_COLORS)
        {
            ThirdColor = ETAG_COLORS.Last();
            LAB2RGB.Clear();
            foreach (var c in ETAG_COLORS)
                LAB2RGB.Add(ToLAB(c), c);

            LABMap = ETAG_COLORS.Select((c) => ToLAB(c)).ToArray();
            if (image != null)
            {
                imageProcessed = null;
                canvasView.InvalidateSurface();
            }
        }

        private void ColorBWY_Pressed(object sender, EventArgs e)
        {
            einkWBR.BorderWidth = 0;
            einkWBY.BorderWidth = 3;
            InitColors(new SKColor[] { SKColors.White, SKColors.Black, SKColors.Yellow });
        }

        private void ColorBWR_Pressed(object sender, EventArgs e)
        {
            einkWBR.BorderWidth = 3;
            einkWBY.BorderWidth = 0;
            InitColors(new SKColor[] { SKColors.White, SKColors.Black, SKColors.Red });
        }

        private void CanvasViewProcessed_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKImageInfo info = e.Info;
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear();

            if (imageProcessed == null)
                return;
            SKRect dst = new SKRect(0, 0, info.Width, info.Height);

            if ((float)info.Width / info.Height > (float)IMAGE_WIDTH / IMAGE_HEIGHT)
                dst.Right = (float)info.Height * IMAGE_WIDTH / IMAGE_HEIGHT;
            else
                dst.Bottom = (float)info.Width / IMAGE_WIDTH * IMAGE_HEIGHT;

            canvas.DrawBitmap(imageProcessed, dst);
        }

        async private void Canvas_PaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            SKImageInfo info = e.Info;
            SKSurface surface = e.Surface;
            SKCanvas canvas = surface.Canvas;

            canvas.Clear();

            if (image == null)
                return;           

            SKRect src = new SKRect(0, 0, image.Width, image.Height);
            SKRect dst = new SKRect(0, 0, info.Width, info.Height);

            if ((float)image.Width / image.Height > (float)IMAGE_WIDTH / IMAGE_HEIGHT)
            {
                float w = (float)image.Height * IMAGE_WIDTH / IMAGE_HEIGHT;
                src.Left = (image.Width - w) / 2;
                src.Right = src.Left + w;
            }
            else
            {
                float h = (float)image.Width / IMAGE_WIDTH * IMAGE_HEIGHT;
                src.Top = (image.Height - h) / 2;
                src.Bottom = src.Top + h;
            }

            if ((float)info.Width / info.Height > (float)IMAGE_WIDTH / IMAGE_HEIGHT)
                dst.Right = (float)info.Height * IMAGE_WIDTH / IMAGE_HEIGHT;
            else
                dst.Bottom = (float)info.Width / IMAGE_WIDTH * IMAGE_HEIGHT;

            //float x = (info.Width - image.Width) / 2;
            //float y = (info.Height / 3 - image.Height) / 2;
            canvas.DrawBitmap(image, src, dst);
            
            if ((imageProcessed == null)
                && (src.Width > 10) && (src.Height > 10))
                await ImageProcess(image, src);
        }

        async Task ImageProcess(SKBitmap image, SKRect src)
        {
            await Task.Run(() =>
            {
                imageProcessed = new SKBitmap((int)src.Width, (int)src.Height);
                var c = new SKCanvas(imageProcessed);
                c.Clear();
                c.DrawBitmap(image, src, new SKRect(0, 0, imageProcessed.Width, imageProcessed.Height));
                imageProcessed = imageProcessed.Resize(new SKImageInfo(IMAGE_WIDTH, IMAGE_HEIGHT), SKFilterQuality.High);
                imageQuantify(imageProcessed);                
            });
            canvasViewProcessed.InvalidateSurface();
        }

        async private Task SendCmdAndWait(byte [] command)
        {
            await charCtrl.WriteAsync(command);
            image = null;
            imageProcessed = null;
            canvasView.InvalidateSurface();
            canvasViewProcessed.InvalidateSurface();
            await NotifyWaitMsg();
        }

        async private void ClearImage_Pressed(object sender, EventArgs e)
        {
            await SendCmdAndWait(new byte [] { ETAG_CMD_IMG_CLEAR });            
        }

        int TotalSize;

        void OnProgress(int progress)
        {
            if (Wait == null)
                return;
            Wait.Progress = progress * 100.0 / TotalSize;
        }

        async private void Upload_Pressed(object sender, EventArgs e)
        {
            if (imageProcessed == null)
            {
                await DisplayAlert("Warning", "Please take a phone firstly.", "OK");
                return;
            }
            byte[] buffer = DumpImageBitData(imageProcessed);
            if (buffer.Length != IMAGE_HEIGHT * IMAGE_WIDTH / 8 * 2)
                throw new Exception("internal error");
            TotalSize = buffer.Length;

            Wait = new WaitActivity();
            DeviceDisplay.KeepScreenOn = true;
            bool r = false;
            await Navigation.PushModalAsync(Wait);
            try
            {
                BleDevice.UpdateConnectionInterval(ConnectionInterval.High);
                int MtuSize = Math.Max(Utils.BLE_MIN_MTU_SIZE, await BleDevice.RequestMtuAsync(80) - 4);
                r = await Upload(buffer, MtuSize, OnProgress);
                if (!r && (MtuSize > Utils.BLE_MIN_MTU_SIZE))
                {
                    Wait.Message = "Your device's BLE subsystem seems broken, retry...";
                    r = await Upload(buffer, Utils.BLE_MIN_MTU_SIZE, OnProgress);                    
                }
            }
            finally
            {
                await Navigation.PopModalAsync();
                DeviceDisplay.KeepScreenOn = false;
                Wait = null;
            }

            if (!r)
                await DisplayAlert("Warning", "Upload failed.", "OK");
            else
                await NotifyWaitMsg();
        }

        async private void TakeImage_Pressed(object sender, EventArgs e)
        {
            image = null;
            imageProcessed = null;

            var photo = await CrossMedia.Current.TakePhotoAsync(
                new StoreCameraMediaOptions
                {
                    PhotoSize = PhotoSize.Small,
                    SaveToAlbum = false,
                    CompressionQuality = 30
                });

            if (photo != null)
            {
                var stream = photo.GetStream();
                using (MemoryStream memStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memStream);
                    memStream.Seek(0, SeekOrigin.Begin);

                    image = SKBitmap.Decode(memStream);
                    canvasView.InvalidateSurface();
                };
            }
        }

        async private void DefImage_Pressed(object sender, EventArgs e)
        {
            await SendCmdAndWait(new byte[] { ETAG_CMD_IMG_USE_DEF1 });
        }

        async private void Init()
        {
            await PrepareChars();

            image = new SKBitmap();
            imageProcessed = new SKBitmap(IMAGE_WIDTH, IMAGE_HEIGHT);
            InitUI();

            ColorBWR_Pressed(null, null);
        }

        public ETagViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            BleDevice = ADevice;
            service = services.First((s) => s.Id == GUID_SERVICE);
            Init();
        }

        async Task PrepareChars()
        {
            var chars = await service.GetCharacteristicsAsync();
            charCtrl = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_CTRL);
            charData = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_DATA);
            ICharacteristic charRes = chars.FirstOrDefault((c) => c.Id == GUID_CHAR_RESOLUTION);
            byte[] res = await charRes.ReadAsync();
            IMAGE_WIDTH = res[0] | (res[1] << 8);
            IMAGE_HEIGHT = res[2] | (res[3] << 8);
        }

        void imageQuantify(SKBitmap image)
        {
            for (int i = 0; i < image.Width; i++)
                for (int j = 0; j < image.Height; j++)
                {
                    var c = image.GetPixel(i, j);
                    image.SetPixel(i, j, colorQuantify(c));
                    //image.SetPixel(i, j, i % 2 == 0 ? SKColors.Black : SKColors.White);
                }
        }

        async Task NotifyWaitMsg()
        {
            await DisplayAlert("Wait...", "It takes about 30s before e-Ink display got updated.", "Got It");
        }

        byte []DumpImageBitData(SKBitmap image)
        {
            if (image.Height % 8 != 0)
                throw new Exception("height incorrect");
            byte[] r = new byte[(image.Height / 8) * image.Width * 2];

            int c = PickColorData(image, SKColors.Black, r, 0);
            PickColorData(image, ThirdColor, r, c);
            return r;
        }

        private static int PickColorData(SKBitmap image, SKColor TheColor, byte[] r, int c)
        {
            for (int i = 0; i < image.Width; i++)
            {
                byte v = 0;
                for (int j = 0; j < image.Height; j++)
                {
                    var color = image.GetPixel(image.Width - 1 - i, j);
                    v <<= 1;
                    if (!color.Equals(TheColor)) v |= 1;
                    if (j % 8 == 7)
                    {
                        r[c++] = v;
                        v = 0;
                    }
                }
            }
            return c;
        }

        struct LABColor
        {
            internal double l;
            internal double a;
            internal double b;
        };

        static double LABDistance(LABColor lab1, LABColor lab2)
        {
            var deltaL = lab1.l - lab2.l;
            var deltaA = lab1.a - lab2.a;
            var deltaB = lab1.b - lab2.b;
            return Math.Sqrt(deltaA * deltaA + deltaB * deltaB + deltaL * deltaL);
        }

        static double ColorDistance(SKColor c1, SKColor c2)
        {
            return LABDistance(ToLAB(c1), ToLAB(c2));
        }

        static LABColor ToLAB(SKColor c)
        {
            return ToLAB(c.Red, c.Green, c.Blue);
        }

        static LABColor ToLAB(int red, int green, int blue)
        {
            var r = red / 255.0;
            var g = green / 255.0;
            var b = blue / 255.0;
            double x, y, z;

            r = (r > 0.04045) ? Math.Pow((r + 0.055) / 1.055, 2.4) : r / 12.92;
            g = (g > 0.04045) ? Math.Pow((g + 0.055) / 1.055, 2.4) : g / 12.92;
            b = (b > 0.04045) ? Math.Pow((b + 0.055) / 1.055, 2.4) : b / 12.92;

            x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047;
            y = (r * 0.2126 + g * 0.7152 + b * 0.0722) / 1.00000;
            z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883;

            x = (x > 0.008856) ? Math.Pow(x, 1 / 3.0) : (7.787 * x) + 16 / 116.0;
            y = (y > 0.008856) ? Math.Pow(y, 1 / 3.0) : (7.787 * y) + 16 / 116.0;
            z = (z > 0.008856) ? Math.Pow(z, 1 / 3.0) : (7.787 * z) + 16 / 116.0;

            return new LABColor
            {
                l = (116 * y) - 16,
                a = 500 * (x - y),
                b = 200 * (y - z)
            };
        }

        Dictionary<LABColor, SKColor> LAB2RGB = new Dictionary<LABColor, SKColor>();

        SKColor colorQuantify(SKColor c)
        {
            LABColor l = ToLAB(c);
            return LAB2RGB[LABMap.OrderBy((x) => LABDistance(x, l)).First()];
        }

        async Task<bool> CheckStatus()
        {
            var b = await charCtrl.ReadAsync();
            return b?.Length > 0 ? b[0] == ETAG_STATUS_OK : false;
        }

        async Task<bool> WaitData(int current)
        {
            int cnt = 0;
            while (Utils.ParseLittleInt(await charData.ReadAsync()) < current)
            {
                await Task.Delay(300);
                cnt++;
                if (cnt > 20)
                    return false;
            }
            return true;
        }

        async Task<bool> Upload(byte[] page, int BLOCK_SIZE, Action<int> onProgress)
        {
            var cmd = new byte[] { ETAG_CMD_IMG_START_WRITE };
            if (!await charCtrl.WriteAsync(cmd)) return false;
            if (!await CheckStatus()) return false;

            int current = 0;
            int cnt = 0;
            while (current < page.Length)
            {
                onProgress(current);

                int size = Math.Min(BLOCK_SIZE, page.Length - current);
                var block = new byte[size];
                Array.Copy(page, current, block, 0, size);
                if (!await charData.WriteAsync(block)) return false;
                current += size;

                cnt++;
                if ((cnt % 4) == 0)
                    if (!await WaitData(current))
                        return false;
            }

            // wait for BLE stack
            if (!await WaitData(current))
                return false;

            cmd = new byte[] { ETAG_CMD_IMG_COMPLETE, 0, 0, 0, 0 };
            UInt32 sum = (UInt32)page.Sum((x) => x);
            Utils.WriteLittle(sum, cmd, 1);

            if (!await charCtrl.WriteAsync(cmd)) return false;
            await Task.Delay(10);
            return await CheckStatus();
        }
    }
}
