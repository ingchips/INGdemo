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

using INGdemo.Lib;
using INGota;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OxyPlot;
using System.Web;

using System.Security.Cryptography;
using System.Net;
using System.IO;
using INGdemo.Helpers;

namespace INGdemo.Models
{
    class SpeechRecognitionSettings
    {
        public static int SAMPLE_RATE = 16000;
    }

    class AlgorithmRecognitionSettings
    {
        public static string Algorithm_SBC = "SBC";
        public static string Algorithm_ADPCM = "ADPCM";
        public static byte AUDIO_CODEC_MODE = 2; //default
        readonly public static byte AUDIO_CODEC_ADPCM = 1;
        readonly public static byte AUDIO_CODEC_SBC = 2;
    }

    interface ISpeechRecognition
    {
        Task<string> Recognize(short[] samples);
    }

    class NullRecognizer : ISpeechRecognition
    {
        public Task<string> Recognize(short[] samples)
        {
            return Task.FromResult("");
        }
    }

    class GoogleRecognizer : ISpeechRecognition
    {

        string Lang;

        public GoogleRecognizer(string Language)
        {
            Lang = Language;
        }

        async public Task<string> Recognize(short[] samples)
        {
            string url = string.Format(
                "http://www.google.com/speech-api/v2/recognize?output=json&key={0}"
                     + "&lang={1}&client=chromium", // &pFilter=2
                Secrets.Google_app_key,
                Lang);

            uint numsamples = (uint)samples.Length;
            ushort samplelength = 2; // in bytes
            int samplerate = SpeechRecognitionSettings.SAMPLE_RATE;

            var payload = new MemoryStream();
            var wr = new BinaryWriter(payload);
            wr.Write(Encoding.ASCII.GetBytes("RIFF"));
            wr.Write(36 + numsamples * samplelength);
            wr.Write(Encoding.ASCII.GetBytes("WAVE"));

            wr.Write(Encoding.ASCII.GetBytes("fmt "));
            wr.Write(16);
            wr.Write((short)1); // Encoding
            wr.Write((short)1); // Channels
            wr.Write((int)(samplerate)); // Sample rate
            wr.Write((int)(samplerate * samplelength)); // Average bytes per second
            wr.Write((short)(samplelength)); // block align
            wr.Write((short)(8 * samplelength)); // bits per sample

            wr.Write(Encoding.ASCII.GetBytes("data"));
            wr.Write((short)(numsamples * samplelength)); // Extra size

            var bytes = new byte[numsamples * samplelength];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            wr.Write(bytes);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.Timeout = 10000;
            request.KeepAlive = true;
            request.SendChunked = true;
            request.UserAgent = "Mozilla/5.0";
            request.ContentType = "audio/l16; rate=16000";
            request.ContentLength = payload.Length;

            Stream writer;
            writer = await request.GetRequestStreamAsync();

            await writer.WriteAsync(payload.ToArray(), 0, (int)payload.Length);
            writer.Close();

            HttpWebResponse response;
            response = (HttpWebResponse)await request.GetResponseAsync();

            Stream result = response.GetResponseStream();
            StreamReader reader = new StreamReader(result);
            var r = await reader.ReadToEndAsync();
            var l = r.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            JObject o = JObject.Parse(l[l.Length - 1]);
            return o["result"][0]["alternative"][0]["transcript"].ToString();
        }
    }


    class AudioViewer : ContentPage
    {
        static public Guid GUID_SERVICE = new Guid("00000001-494e-4743-4849-505355554944");
        static public Guid GUID_CHAR_CTRL = new Guid("bf83f3f1-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_OUT = new Guid("bf83f3f2-399a-414d-9035-ce64ceb3ff67");
        static public Guid GUID_CHAR_INFO = new Guid("10000001-494e-4743-4849-505355554944");
        static public string SERVICE_NAME = "INGChips Voice Output Service";
        static public string ICON_STR = Char.ConvertFromUtf32(0x1F3A4);

        readonly byte CMD_DIGCMD_DIGITAL_GAIN = 0;
        readonly byte CMD_MIC_OPEN = 1;
        readonly byte CMD_MIC_CLOSE = 2;

        IDevice BleDevice;
        IService service;
        ICharacteristic charCtrl;
        ICharacteristic charOutput;

        Label label;
        Label labelInfo;
        BasicPCMDecoder Decoder;
        IPCMAudio Player;
        Slider Gain;
        Label GainInd;
        int CurrentGain = 0;
        Button BtnTalk;
        Picker EnginePicker;
        Picker SamplingRatePicker;
        Picker AlgorithmPicker;
        Label STTResult;

        List<short> AllSamples;

        public View MakeSlider(string label, out Slider slider)
        {
            var layout = new StackLayout();
            layout.Orientation = StackOrientation.Horizontal;
            layout.HorizontalOptions = LayoutOptions.Fill;

            layout.Children.Add(new Label { Text = label, Style = Device.Styles.TitleStyle });
            slider = new Slider(-5, 5, 0);
            slider.HorizontalOptions = LayoutOptions.FillAndExpand;
            layout.Children.Add(slider);

            GainInd = new Label();
            GainInd.Text = "0dB";
            GainInd.Style = Device.Styles.CaptionStyle;
            GainInd.HorizontalOptions = LayoutOptions.End;
            layout.Children.Add(GainInd);

            return layout;
        }

        void InitUI()
        {
            var layout = new StackLayout();
            label = new Label();

            labelInfo = new Label();
            labelInfo.Style = Device.Styles.CaptionStyle;
            labelInfo.HorizontalOptions = LayoutOptions.Center;

            BtnTalk = new Button
            {
                Text = "         " + ICON_STR + "\nPress to Capture",
                CornerRadius = 50,
                Style = Device.Styles.TitleStyle
            };

            BtnTalk.Pressed += BtnTalk_Pressed;
            BtnTalk.Released += BtnTalk_Released;

            EnginePicker = new Picker { Title = "Select" };
            EnginePicker.Items.Add("(Off)");
            EnginePicker.Items.Add("Google (普通话)");
            EnginePicker.Items.Add("Google (English)");
            //EnginePicker.Items.Add("Tencent AI Open Platform");

            AlgorithmPicker = new Picker { Title = "Select" };
            AlgorithmPicker.Items.Add("ADPCM");
            AlgorithmPicker.Items.Add("SBC");
            AlgorithmPicker.SelectedIndex = 1;
            AlgorithmPicker.SelectedIndexChanged += AlgorithmPicker_SelectedIndexChanged;

            SamplingRatePicker = new Picker { Title = "Select" };
            SamplingRatePicker.Items.Add("8000");
            SamplingRatePicker.Items.Add("16000");
            SamplingRatePicker.Items.Add("24000");
            SamplingRatePicker.Items.Add("32000");
            SamplingRatePicker.SelectedIndex = 1;
            SamplingRatePicker.SelectedIndexChanged += SamplingRatePicker_SelectedIndexChanged;

            STTResult = new Label();
            STTResult.Style = Device.Styles.BodyStyle;
            STTResult.HorizontalOptions = LayoutOptions.FillAndExpand;

            label.HorizontalOptions = LayoutOptions.Center;
            label.FontSize = 10;

            layout.Children.Add(BtnTalk);
            layout.Children.Add(labelInfo);
            layout.Children.Add(MakeSlider("Gain", out Gain));
            layout.Children.Add(label);
            layout.Children.Add(new Label { Text = "Algorithm", Style = Device.Styles.SubtitleStyle });
            layout.Children.Add(AlgorithmPicker);
            layout.Children.Add(label);
            layout.Children.Add(new Label { Text = "Sampling Rate", Style = Device.Styles.SubtitleStyle });
            layout.Children.Add(SamplingRatePicker);
            layout.Children.Add(label);
            layout.Children.Add(new Label { Text = "Speech Recognition Engine", Style = Device.Styles.SubtitleStyle });
            layout.Children.Add(EnginePicker);
            layout.Children.Add(STTResult);

            Gain.ValueChanged += Gain_ValueChanged;

            layout.Margin = 20;
            layout.Spacing = 10;
            layout.VerticalOptions = LayoutOptions.Fill;
            layout.HorizontalOptions = LayoutOptions.Fill;
            Content = new ScrollView { Content = layout };
            Title = SERVICE_NAME;
        }

        private void AlgorithmPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (AlgorithmPicker.SelectedItem.ToString() == AlgorithmRecognitionSettings.Algorithm_SBC)
            {
                AlgorithmRecognitionSettings.AUDIO_CODEC_MODE = 2;
                SetupDecoder();
            }
            else
            {
                AlgorithmRecognitionSettings.AUDIO_CODEC_MODE = 1;
                SetupDecoder();
            }
        }
        private void SamplingRatePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            EnginePicker.IsEnabled = int.Parse(SamplingRatePicker.SelectedItem.ToString()) == SpeechRecognitionSettings.SAMPLE_RATE;
        }

        async private void BtnTalk_Released(object sender, EventArgs e)
        {
            await charCtrl.WriteAsync(new byte[1] { CMD_MIC_CLOSE });
            Player.Stop();

            if (int.Parse(SamplingRatePicker.SelectedItem.ToString()) != SpeechRecognitionSettings.SAMPLE_RATE) return;

            ISpeechRecognition engine;

            switch (EnginePicker.SelectedIndex)
            {
                case 1:
                    engine = new GoogleRecognizer("cmn-Hans-CN");
                    break;
                case 2:
                    engine = new GoogleRecognizer("en-US");
                    break;
                default:
                    engine = new NullRecognizer();
                    break;
            }

            STTResult.Text = "Recognizing ...";
            var samples = AllSamples.ToArray();
            try
            {
                string s = await engine.Recognize(samples);
                STTResult.Text = s;
            }
            catch (Exception ex)
            {
                STTResult.Text = "error: " + ex.Message;
            }
        }

        async private void BtnTalk_Pressed(object sender, EventArgs e)
        {
            int samplingRate = int.Parse(SamplingRatePicker.SelectedItem.ToString());
            string audioCodec = AlgorithmPicker.SelectedItem.ToString();
            Decoder.Reset();
            Player.Play(samplingRate);
            AllSamples.Clear();
            await charCtrl.WriteAsync(new byte[1] { CMD_MIC_OPEN });
        }

        async private void Gain_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            int gain = (int)Math.Round(Gain.Value);
            Gain.Value = gain;

            if (CurrentGain != gain)
            {
                CurrentGain = gain;
                GainInd.Text = string.Format("{0}dB", 3 * gain);
                await charCtrl.WriteAsync(new byte[2] { CMD_DIGCMD_DIGITAL_GAIN, (byte)(gain & 0xff) });
            }
        }

        async void Read()
        {
            charCtrl = await service.GetCharacteristicAsync(GUID_CHAR_CTRL);
            charOutput = await service.GetCharacteristicAsync(GUID_CHAR_OUT);
            var charInfo = await service.GetCharacteristicAsync(GUID_CHAR_INFO);
            var info = await charInfo.ReadAsync();
            int size = info[0];
            labelInfo.Text = string.Format("BlockSize = {0} B", size);
            size = Utils.Att2MTUSize(size);
            var this_size = await BleDevice.RequestMtuAsync(size);
            if (this_size < size)
            {
                if (this_size > 0)
                {
                    var msg = string.Format("Your BLE subsystem can't support required block size ({0} B).", this_size);
                    await DisplayAlert("Error", msg, "OK");
                    return;
                }
                else
                {
                    await DisplayAlert("Warning", "Failed to request MTU exchange\nVoice data might be corrupted.", "OK");
                }
            }
            BleDevice.UpdateConnectionInterval(ConnectionInterval.High);
            charOutput.ValueUpdated += CharOutput_ValueUpdated;
            await charOutput.StartUpdatesAsync();
        }

        private void CharOutput_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            if (Decoder != null)
                Decoder.Decode(e.Characteristic.Value);

            Device.BeginInvokeOnMainThread(() =>
                label.Text = Utils.ByteArrayToString(e.Characteristic.Value)
            );
        }

        public AudioViewer(IDevice ADevice, IReadOnlyList<IService> services)
        {
            Player = DependencyService.Get<IPCMAudio>();
            SetupDecoder();
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Read();
        }

        private void Decoder_PCMOutput(object sender, short[] e)
        {
            Player.Write(e);
            AllSamples.AddRange(e);
        }

        async protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Player.Stop();
            if (BleDevice.State == DeviceState.Connected) await charOutput.StopUpdatesAsync();
        }

        private void SetupDecoder()
        {
            AllSamples = new List<short>();
            if (AlgorithmRecognitionSettings.AUDIO_CODEC_MODE == AlgorithmRecognitionSettings.AUDIO_CODEC_SBC)
            {
                Decoder = new SBCDecoder(3200);
            }
            else
            {
                Decoder = new ADPCMDecoder(3200);
            }
            Decoder.PCMOutput += Decoder_PCMOutput;
        }
    }
}
