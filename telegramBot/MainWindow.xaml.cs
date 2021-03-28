
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

        static string token;
        static TelegramBotClient botClient;

        long chatId; 
        //хранит id текущего чата, обновляется при получении ботом нового сообщения

        Dictionary<string, Telegram.Bot.Types.Chat> chats = new Dictionary<string, Telegram.Bot.Types.Chat>(); 
        //словарь, сохраняющий в виде ключа username каждого написавшего, а в качестве значения - информацию о чате

        string message = ""; 
        //хранит текст сообщения
        string filename = "";
        //хранит название файла изображения
        List<string> sign = new List<string>();
        //список с подписями к фото
        Random rand = new Random(DateTime.Now.GetHashCode());

        Thread thread;
        //поток, в котором выполняются операции, связанные с ботом (требовалось, чтобы преостановить запуск бота до ввода его токена)

        bool isSpecialSignNeeded = false;
        string specialSign = "";
        //спецподписи к изображениям

        public MainWindow()
        {
            InitializeComponent();
            Initialization();
            thread = new Thread(() => {
                botClient.OnMessage += Bot_OnMessage;
                botClient.StartReceiving();
            });
            thread.Interrupt();
            Input.PreviewKeyDown += KeyDownHandler;
        }

        //<summary>
        //Обрабатывает пришедшее от пользователя сообщение
        //</summary>
        private async void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            chatId = e.Message.Chat.Id;
            if (e.Message.Chat.Username == null) e.Message.Chat.Username = "X3";
            //добавляем в словарь chat с текущим пользователем, если его там нет
            if (!chats.ContainsKey(e.Message.Chat.Username))
            {
                chats.Add(e.Message.Chat.Username, e.Message.Chat);
                await Dispatcher.BeginInvoke(new ThreadStart(delegate {people.Items.Add(e.Message.Chat.Username); }));
                using (FileStream fs = new FileStream("users.json", FileMode.OpenOrCreate))
                {
                    await JsonSerializer.SerializeAsync(fs, chats);
                }
            }
            //если полученное сообщение - текстовое, выводим в консоль
            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                await Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Items.Add($"{e.Message.Chat.Username}\t {DateTime.Now}: {e.Message.Text}\n"); Output.ScrollIntoView(Output.Items[Output.Items.Count - 1]); }));
                
                if (e.Message.Text.Contains("/добавить "))
                {
                    specialSign = e.Message.Text.Substring(e.Message.Text.IndexOf(' ') + 1);
                    isSpecialSignNeeded = true;
                    sign.Add(specialSign);
                    using (FileStream fs = new FileStream("sign.json", FileMode.OpenOrCreate))
                    {
                        await JsonSerializer.SerializeAsync(fs, sign);
                    }
                    message = $"Добавлено: \n {sign[sign.Count-1]}";
                    await botClient.SendTextMessageAsync(chatId, message);
                    await Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Items.Add($"You\t {DateTime.Now}: {message}\n"); Output.ScrollIntoView(Output.Items[Output.Items.Count - 1]); }));
                }
                message = "";
            }
            //если полученное сообщение - фото, выводим в консоль "Photo" и название, под которым оно сохранится в images.
            //Затем обрабатываем и сохраняем в imagesSend, отправляем обратно текущему пользователю.
            if (e.Message.Type == Telegram.Bot.Types.Enums.MessageType.Photo)
            {
                await Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Items.Add($"{e.Message.Chat.Username}\t {DateTime.Now}: Photo\n"); Output.ScrollIntoView(Output.Items[Output.Items.Count - 1]); }));
                string fileId = (e.Message.Photo[e.Message.Photo.Length - 1]).FileId;
                var path = @"C:\Users\aestriplex\source\repos\telegramBot\telegramBot\images\";
                var pathSend = @"C:\Users\aestriplex\source\repos\telegramBot\telegramBot\imagesSend\";
                filename = Path.GetRandomFileName().Remove(8,3);
                await Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Items.Add(filename + "\n"); Output.ScrollIntoView(Output.Items[Output.Items.Count - 1]); }));
                DownloadFile(fileId, path);
                Thread.Sleep(1000);
                if (!isSpecialSignNeeded)
                    ImageProcessingSign(path, pathSend, sign[rand.Next(0, sign.Count)]);
                else
                {
                    ImageProcessingSign(path, pathSend, specialSign);
                    isSpecialSignNeeded = false;
                }
                await SendPhoto(chatId.ToString(), pathSend + filename + ".jpg", token);
                await Dispatcher.BeginInvoke(new ThreadStart(delegate { Output.Items.Add($"You\t {DateTime.Now}: Photo\n"); Output.ScrollIntoView(Output.Items[Output.Items.Count - 1]); }));
            }
        }

        //<summary>
        //Обрабатывает фотографию - добавляет на нее случайную подпись, 
        //десериализованную в список из файла "sign.json"
        //</summary>
        private void ImageProcessingSign(string path, string pathSend, string signPhoto)
        {
            byte[] photoBytes = File.ReadAllBytes(path + filename + ".jpg");
            FileStream fs = File.OpenWrite(pathSend + filename + ".jpg");
            ISupportedImageFormat format = new JpegFormat { Quality = 100 };

            var text = new TextLayer
            {
                FontColor = Color.White,
                FontFamily = new FontFamily("Lobster"),
                DropShadow = true,
                Text = signPhoto,
                Style = System.Drawing.FontStyle.Bold
            };
            using (MemoryStream inStream = new MemoryStream(photoBytes))
            {
                using (MemoryStream outStream = new MemoryStream())
                {

                    using (ImageFactory image = new ImageFactory(preserveExifData: true))
                    {
                        image.Load(inStream); 
                        text.Position = new System.Drawing.Point(image.Image.Width / 10, 7 * image.Image.Height / 10);
                        text.FontSize = image.Image.Height / 20;
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
        //<summary>
        //Скачивание файла по id
        //</summary>
        private async void DownloadFile(string fileId, string path)
        {
            var file = await botClient.GetFileAsync(fileId);
            FileStream fs = new FileStream(path + filename + ".jpg", FileMode.Create);
            await botClient.DownloadFileAsync(file.FilePath, fs);
            fs.Close();
            fs.Dispose();
        }
        //<summary>
        //Отправка фото по chat.id
        //</summary>
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
        //<summary>
        //Обрабатывает нажатие кнопки Enter при вводе в консоль.
        //Происходит отправка введенного текста в текущий чат.
        //</summary>
        private void KeyDownHandler(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                message = Input.Text;
                if (message != "")
                {
                    botClient.SendTextMessageAsync(chatId, message);
                    Output.Items.Add($"You\t {DateTime.Now}: {message}\n");
                    Output.ScrollIntoView(Output.Items[Output.Items.Count - 1]);
                    message = "";
                    Input.Text = "";
                }
            }
        }
        //<summary>
        //Десериализует подписи к фото из "sign.json"
        //Десериализует информацию о чате из "users.json"
        //</summary>
        private async void Initialization()
        {
            using (FileStream fs = new FileStream("sign.json", FileMode.OpenOrCreate))
            {
                if (new FileInfo("sign.json").Length != 0)
                {
                    sign = await JsonSerializer.DeserializeAsync<List<string>>(fs);
                }
            }

            using (FileStream fs = new FileStream("users.json", FileMode.OpenOrCreate))
            {
                if (new FileInfo("users.json").Length != 0)
                {
                    chats = await JsonSerializer.DeserializeAsync<Dictionary<string, Telegram.Bot.Types.Chat>>(fs);
                }
                foreach (var item in chats)
                {
                    people.Items.Add(item.Key);
                }
            }
        }
        //<summary>
        //Обрабатывает нажатие кнопки "Выбрать" при выборе чата в ComboBox
        //</summary>
        private void Choose_Click(object sender, RoutedEventArgs e)
        {
            Telegram.Bot.Types.Chat chat = new Telegram.Bot.Types.Chat();
            if (chats.TryGetValue(people.Text, out chat))
            {
                chatId = chat.Id;
                Output.Items.Add($"Успешная смена адресата на {chat.Username}\n");
            }
        }
        //<summary>
        //Обрабатывает нажатие кнопки, добавление введенной в TextBox подписи в соответствующий список
        //</summary>
        private async void SignButton_Click(object sender, RoutedEventArgs e)
        {
            sign.Add(SignTextBox.Text);
            using (FileStream fs = new FileStream("sign.json", FileMode.OpenOrCreate))
            {
                await JsonSerializer.SerializeAsync(fs, sign);
            }
            SignTextBox.Text = "";
        }
        //<summary>
        //Обрабатывает нажатие кнопки, подключает бота
        //</summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            token = TokenTextBox.Text;
            botClient = new TelegramBotClient(token);
            thread.Start();
            Output.Items.Add("Успешно подключено \n");
            Output.ScrollIntoView(Output.Items[Output.Items.Count - 1]);
        }
    }
}
