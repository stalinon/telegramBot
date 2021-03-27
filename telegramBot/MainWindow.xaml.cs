
using System;
using System.Threading;
using System.Windows;
using Telegram.Bot;
using ImageProcessor;
using ImageProcessor.Imaging;
using ImageProcessor.Imaging.Formats;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;

namespace telegramBot
{
    public partial class MainWindow : Window
    {
        static string token = "1772067882:AAFzkYj4WFXlUk-2r1Y5UJSmCQAVQLe6IcE";
        static TelegramBotClient botClient = new TelegramBotClient(token);
        public string message = "";
        public long chatId;
        public string filename = "";
        public List<string> sign = new List<string>();
        Random rand = new Random(DateTime.Now.GetHashCode());

        public MainWindow()
        {
            InitializeComponent();
            SignInitialization();
            botClient.OnMessage += Bot_OnMessage;
            Input.PreviewKeyDown += KeyDownHandler;
            botClient.StartReceiving();
        }

        private async void SignInitialization()
        {
            using (FileStream fs = new FileStream("sign.json", FileMode.OpenOrCreate))
            {
                if (new FileInfo("sign.json").Length != 0)
                {
                    sign = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                }
            }
        }
        
        private async void SignButton_Click(object sender, RoutedEventArgs e)
        {
            sign.Add(SignTextBox.Text);
            using (FileStream fs = new FileStream("sign.json", FileMode.OpenOrCreate))
            {
                await JsonSerializer.SerializeAsync(fs, sign);
            }
            SignTextBox.Text = "";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            token = TokenTextBox.Text;
            botClient = new TelegramBotClient(token);
            var me = botClient.GetMeAsync().Result;
            Output.Text = "Успешно подключено \n";
        }

        private void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            chatId = e.Message.Chat.Id;

            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
                Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Text += $"{e.Message.Chat.Username}\t {DateTime.Now}: {e.Message.Text}\n"; }));
            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Photo)
            {
                Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Text += $"{e.Message.Chat.Username}\t {DateTime.Now}: Photo\n"; }));
                string fileId = (e.Message.Photo[e.Message.Photo.Length - 1]).FileId;
                var path = @"C:\Users\aestriplex\source\repos\telegramBot\telegramBot\images\";
                var pathSend = @"C:\Users\aestriplex\source\repos\telegramBot\telegramBot\imagesSend\";
                
                filename = Path.GetRandomFileName().Remove(8,3);
                Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Text += filename + "\n"; }));
                DownloadFile(fileId, path);
                Thread.Sleep(1000);
                ImageProcessing(path, pathSend);
                SendPhoto(chatId.ToString(), pathSend + filename + ".jpg", token);
            }
        }

        private void ImageProcessing(string path, string pathSend)
        {
            byte[] photoBytes = File.ReadAllBytes(path + filename + ".jpg");
            FileStream fs = File.OpenWrite(pathSend + filename + ".jpg");
            ISupportedImageFormat format = new JpegFormat { Quality = 100 };

            var text = new TextLayer
            {
                FontColor = Color.White,
                FontFamily = new FontFamily("Lobster"),
                FontSize = 20,
                DropShadow = true,
                Text = sign[rand.Next(0,sign.Count)],
                Style = System.Drawing.FontStyle.Bold
            };
            using (MemoryStream inStream = new MemoryStream(photoBytes))
            {
                using (MemoryStream outStream = new MemoryStream())
                {

                    using (ImageFactory image = new ImageFactory(preserveExifData: true))
                    {
                        // Load, resize, set the format and quality and save an image.
                        image.Load(inStream);  // грузим картинку
                        text.Position = new System.Drawing.Point(image.Image.Width / 2, 9 * image.Image.Height / 10);
                        image.Watermark(text);
                        image.Format(format);
                        image.Save(outStream);
                        outStream.WriteTo(fs);
                        outStream.Close();
                    }
                    inStream.Close();
                    fs.Close();
                }
            }
        }

        private async void DownloadFile(string fileId, string path)
        {
            var file = await botClient.GetFileAsync(fileId);
            FileStream fs = new FileStream(path + filename + ".jpg", FileMode.Create);
            await botClient.DownloadFileAsync(file.FilePath, fs);
            fs.Close();
            fs.Dispose();
        }

        public async static Task SendPhoto(string chatId, string filePath, string token)
        {
            var url = string.Format("https://api.telegram.org/bot{0}/sendPhoto", token);
            var fileName = filePath.Split('\\').Last();

            using (var form = new MultipartFormDataContent())
            {
                form.Add(new StringContent(chatId.ToString(), Encoding.UTF8), "chat_id");

                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    form.Add(new StreamContent(fileStream), "photo", fileName);

                    using (var client = new HttpClient())
                    {
                        await client.PostAsync(url, form);
                    }
                }
            }
        }

        private void KeyDownHandler(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                message = Input.Text;
                if (message != "")
                {
                    botClient.SendTextMessageAsync(chatId, message);
                    Output.Text += $"You\t {DateTime.Now}: {message}\n";
                    message = "";
                    Input.Text = "";
                }
            }
        }
        
    }
}
