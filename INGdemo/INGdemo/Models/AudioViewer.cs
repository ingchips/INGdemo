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

    internal class TencentAiPlatform
    {

        static readonly string url_preffix = "https://api.ai.qq.com/fcgi-bin/";

        public static string GetMD5Hash(string str)
        {
            StringBuilder sb = new StringBuilder();
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                byte[] data = md5.ComputeHash(Encoding.UTF8.GetBytes(str));

                int length = data.Length;
                for (int i = 0; i < length; i++)
                    sb.Append(data[i].ToString("X2"));

            }
            return sb.ToString();
        }

        public static string EncodeParams(JObject Params)
        {
            var uri_str = "";
            var ks = Params.Properties().Select((x) => x.Name).ToList();
            foreach (var key in ks)
            {
                uri_str += string.Format("{0}={1}&", key, HttpUtility.UrlEncode(Params[key].ToString()));
            }
            return uri_str.TrimEnd(new char [] { '&' });
        }

        static string UrlEncode(string value)
        {
            int limit = 2000;

            StringBuilder sb = new StringBuilder();
            int loops = value.Length / limit;

            for (int i = 0; i <= loops; i++)
            {
                if (i < loops)
                {
                    sb.Append(Uri.EscapeDataString(value.Substring(limit * i, limit)));
                }
                else
                {
                    sb.Append(Uri.EscapeDataString(value.Substring(limit * i)));
                }
            }

            return sb.ToString();
        }

        public static string GenSignString(JObject Params)
        {
            var uri_str = "";
            var ks = Params.Properties().Select((x) => x.Name).ToList();
            ks.Sort();
            foreach (var key in ks)
            {
                if (key == "app_key")
                    continue;
                uri_str += string.Format("{0}={1}&", key, UrlEncode(Params[key].ToString()));
            }
            var sign_str = uri_str + "app_key=" + Params["app_key"].ToString();
            return GetMD5Hash(sign_str);
        }

        string app_id, app_key;

        public TencentAiPlatform(string app_id, string app_key)
        {
            this.app_id = app_id;
            this.app_key = app_key;
        }

        async Task<Stream> invoke(string url, JObject Params)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            string paraUrlCoded = EncodeParams(Params);
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(paraUrlCoded); 
            request.ContentLength = payload.Length;

            Stream writer;
            writer = await request.GetRequestStreamAsync();

            await writer.WriteAsync(payload, 0, payload.Length);
            writer.Close();

            HttpWebResponse response;
            response = (HttpWebResponse) await request.GetResponseAsync();

            return response.GetResponseStream();            
        }

        static public long GetTimeStamp()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        }

        public async Task<string> GetAaiWxAsrs(short []chunk, 
                                  string speech_id, 
                                  int end_flag, 
                                  int format_id, 
                                  int rate, int bits, int seq, int cont_res)
        {
            var Params = new JObject();

            int chunk_len = chunk.Length * 2;
            var bytes = new byte[chunk_len];
            Buffer.BlockCopy(chunk, 0, bytes, 0, bytes.Length);
            var speech_chunk = Convert.ToBase64String(bytes);

            Params["app_id"] = app_id;
            Params["app_key"] = app_key;
            Params["time_stamp"] = GetTimeStamp();
            Params["nonce_str"] = GetTimeStamp().ToString();
            Params["speech_chunk"] = speech_chunk;
            Params["speech_id"] = speech_id;
            Params["end"] = end_flag;
            Params["format"] = format_id;
            Params["rate"] = rate;
            Params["bits"] = bits;
            Params["seq"] = seq;
            Params["len"] = chunk_len;
            Params["cont_res"] = cont_res;
            Params["sign"] = GenSignString(Params);
            
            var s = await invoke(url_preffix + "aai/aai_wxasrs", Params);
            StreamReader reader = new StreamReader(s);
            var r = await reader.ReadToEndAsync();
            JObject o = JObject.Parse(r);
            if (o["msg"].ToString() == "ok")
                return o["data"]["speech_text"].ToString();
            else
                return r;
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

    class TecentAiRecognizer : ISpeechRecognition
    {
        // Generates a random string with a given size.    
        static public string RandomString(int size, bool lowerCase = false)
        {
            var builder = new StringBuilder(size);
            var rand = new Random();

            for (var i = 0; i < size; i++)
            {
                var @char = (char)rand.Next('a', 'a' + 26);
                builder.Append(@char);
            }

            return lowerCase ? builder.ToString() : builder.ToString().ToUpper();
        }

        async public Task<string> Recognize(short[] samples)
        {
            string app_key = Secrets.TecentAiPlatformSecrets_app_key;
            string app_id = Secrets.TecentAiPlatformSecrets_app_id;

            var api = new TencentAiPlatform(app_id, app_key);

            return await api.GetAaiWxAsrs(samples, RandomString(16),
                1, 1, SpeechRecognitionSettings.SAMPLE_RATE, 16, 0, 1);
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
        ADPCMDecoder Decoder;
        IPCMAudio Player;
        Slider Gain;
        Label GainInd;
        int CurrentGain = 0;
        Button BtnTalk;
        Picker EnginePicker;
        Picker SamplingRatePicker;
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
                Text = ICON_STR + "\nPress to Capture",               
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
                STTResult.Text = await engine.Recognize(samples);
            }
            catch (Exception ex)
            {
                STTResult.Text = "error: " + ex.Message;
            }
        }

        async private void BtnTalk_Pressed(object sender, EventArgs e)
        {
            int samplingRate = int.Parse(SamplingRatePicker.SelectedItem.ToString());
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
            AllSamples = new List<short>();
            Decoder = new ADPCMDecoder(32000 / 10);
            Player = DependencyService.Get<IPCMAudio>();
            BleDevice = ADevice;
            InitUI();
            service = services.First((s) => s.Id == GUID_SERVICE);
            Decoder.PCMOutput += Decoder_PCMOutput;            
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
    }
}
