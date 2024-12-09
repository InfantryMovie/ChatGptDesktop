using ChatGptDesktop.Model;
using ChatGptDesktop.View;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ChatGptDesktop.ViewModel
{
    public class MainViewModel : BaseViewModel
    {
        private readonly ChatDbContext _dbContext;
        System.Collections.Generic.List<dynamic> messageHistory { get; set; }
        readonly string ApiKey;

        ListBox messageListBox;
        public ListBox MessageListBox
        {
            get => messageListBox;
            set => SetProperty(ref messageListBox, value);
        }
        // Коллекция сообщений для привязки к UI
        public ObservableCollection<ChatResponseDb> Messages { get; }
        private string _userInput;
        public string UserInput
        {
            get => _userInput;
            set => SetProperty(ref _userInput, value);
        }

        private string _lastMessageContent;
        public string LastMessageContent
        {
            get => _lastMessageContent;
            set => SetProperty(ref _lastMessageContent, value);
        }

        private int _totalTokens;
        public int TotalTokens
        {
            get => _totalTokens;
            set => SetProperty(ref _totalTokens, value);
        }

        private int? _currentProfileId;
        public int? CurrentProfileId
        {
            get => _currentProfileId;
            set => SetProperty(ref _currentProfileId, value);

        }
        public ICommand SendMessageCommand { get; }
        public ICommand ClearMessageContextCommand { get; }

        public MainViewModel()
        {
            _dbContext = new ChatDbContext();
            Messages = new ObservableCollection<ChatResponseDb>();
            SendMessageCommand = new RelayCommand(async () => await SendMessage());
            ClearMessageContextCommand = new RelayCommand(async () => await ClearMessageContext());
            messageHistory = new List<dynamic>();

            if(File.Exists("./appsettings.json"))
            {
                var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())  
                .AddJsonFile("appsettings.json")  
                .Build();

                ApiKey = config["ApiKey"] ?? ""; 
                if(string.IsNullOrEmpty(ApiKey))
                {
                    //MessageBox.Show("API ключ от OpenAI не заполнен.");
                }

            }
            else
            {
                File.WriteAllText("./appsettings.json", "{\n  \"ApiKey\":\"\" \n}");
                //MessageBox.Show("API ключ от OpenAI не заполнен.");
            }

            LoadMessagesFromDatabase();


        }

        

        async Task LoadMessagesFromDatabase()
        {
            // Загружаем все сообщения из базы данных
            var chatMessages = await _dbContext.ChatMessages.ToListAsync();

            // Добавляем их в ObservableCollection
            foreach (var message in chatMessages)
            {
                Messages.Add(message);
                AddMessage(message.Role == "ChatGPT" ? "assistant" : "user", message.Content);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(MessageListBox);
                scrollViewer?.ScrollToBottom();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(UserInput)) return;

            try
            {
                // Получаем текущий активный профиль или создаем новый
                var currentProfileId = await GetOrCreateProfileIdAsync(UserInput);

                // Устанавливаем текущий профиль
                SetCurrentProfile(currentProfileId);

                // получаем ответ от ChatGPT
                var chatResponse = await FetchResponseFromApi(UserInput, currentProfileId);

                if (chatResponse == null) return;

                // Берём первое сообщение от ассистента
                var choice = chatResponse.Choices.FirstOrDefault();
                if (choice == null) return;

                var chatResponseDb = new ChatResponseDb
                {
                    ResponseId = chatResponse.Id,
                    Object = chatResponse.Object,
                    Created = DateTimeOffset.FromUnixTimeSeconds(chatResponse.Created).DateTime,
                    Role = "ChatGPT",// choice.Message.Role
                    Content = choice.Message.Content,
                    PromptTokens = chatResponse.Usage.PromptTokens,
                    CompletionTokens = chatResponse.Usage.CompletionTokens,
                    TotalTokens = chatResponse.Usage.TotalTokens,
                    ProfileId = currentProfileId
                };

                // Сохраняем сообщение в базу данных
                _dbContext.ChatMessages.Add(chatResponseDb);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine("Сохраняем данные в базе данных.");

                // Добавляем сообщение в UI
                Messages.Add(chatResponseDb);
                ScrollToBottom();


                // Обновляем свойства для отображения
                LastMessageContent = chatResponseDb.Content;
                TotalTokens = chatResponseDb.TotalTokens;

                UserInput = string.Empty;

            }
            catch (Exception ex)
            {
                // Логгирование или уведомление об ошибке
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        

        private async Task<int> GenerateUniqueIdAsync()
        {
            Random random = new Random();
            int newId;

            do
            {
                newId = random.Next(1, int.MaxValue);
            }
            while (await _dbContext.ChatContexts.AnyAsync(c => c.Id == newId)); // Уникальность проверяется в БД

            return newId;
        }


        void SetCurrentProfile(int profileId)
        {
            CurrentProfileId = profileId;
        }

        private async Task<int> CreateNewContextAsync(string firstMessageContent)
        {
            // Генерация имени профиля (до 15 символов)
            string profileName = firstMessageContent.Length > 15
                ? firstMessageContent.Substring(0, 15)
                : firstMessageContent;

            // Генерация уникального ID
            int newId = await GenerateUniqueIdAsync();

            var newProfile = new ChatContextDb
            {
                Id = newId,
                Name = profileName
            };

            // Сохраняем профиль в базу
            await _dbContext.ChatContexts.AddAsync(newProfile);
            await _dbContext.SaveChangesAsync();

            return newProfile.Id; // Возвращаем ID нового профиля
        }


        private async Task<int> GetOrCreateProfileIdAsync(string firstMessageContent)
        {
            // Проверяем наличие существующего профиля
            var existingProfile = await _dbContext.ChatContexts.FirstOrDefaultAsync();

            if (existingProfile == null)
            {
                // Если профилей нет, создаем первый профиль и контекст
                Console.WriteLine("Создаем новый профиль...");
                return await CreateNewContextAsync(firstMessageContent);
            }

            // Если профиль уже есть, возвращаем его ID
            return existingProfile.Id;
        }


        async Task SelectProfileAsync(int profileId)
        {
            // Устанавливаем текущий профиль
            SetCurrentProfile(profileId);

            // Загружаем сообщения для выбранного профиля
            await LoadMessagesForProfile(profileId);
        }

        async Task LoadMessagesForProfile(int profileId)
        {
            // При выборе существующего профиля

            Messages.Clear();

            // Загружаем сообщения, относящиеся к новому профилю
            var messages = await _dbContext.ChatMessages
                .Where(m => m.ProfileId == profileId)
                .ToListAsync();

            // Добавляем их в ObservableCollection для отображения
            foreach (var message in messages)
            {
                Messages.Add(message);
            }
        }

        private async Task ClearMessageContext()
        {
            try
            {
                // Загружаем все сообщения из базы данных
                var allMessages = await _dbContext.ChatMessages.ToListAsync();

                // Удаляем их
                _dbContext.ChatMessages.RemoveRange(allMessages);

                // Сохраняем изменения
                await _dbContext.SaveChangesAsync();

                // Очищаем коллекцию сообщений в UI
                Messages.Clear();
                messageHistory.Clear();
                // Обновляем свойства
                LastMessageContent = string.Empty;
                TotalTokens = 0;

                Console.WriteLine("Контекст сообщений очищен.");

            }
            catch (Exception ex)
            {

                Console.WriteLine($"Произошла ошибка в процессе удаления контекста: {ex.Message}\n {ex.ToString()}");
            }
        }

        public void ScrollToBottom()
        {
            if (MessageListBox != null)
            {
                // Получаем ScrollViewer, который содержит ListBox
                var scrollViewer = FindVisualChild<ScrollViewer>(MessageListBox);
                scrollViewer?.ScrollToBottom();
            }
        }

        T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            T foundChild = default;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t)
                {
                    foundChild = t;
                    break;
                }
                else
                {
                    foundChild = FindVisualChild<T>(child);
                    if (foundChild != null)
                        break;
                }
            }

            return foundChild;
        }

        void AddMessage(string role, string content)
        {
            messageHistory.Add(new { role = role, content = content });
        }

        private async Task<ChatResponse> FetchResponseFromApi(string userInput, int currentProfileId)
        {
           if(string.IsNullOrEmpty(ApiKey))
           {
                MessageBox.Show("Api ключ от OpenAI не заполнен");
                return null;
           }

            AddMessage("user", userInput);
            Console.WriteLine("Добавлен в контекст запрос пользователя");

            var requestObject = new
            {
                model = "gpt-4o-mini",
                messages = messageHistory.ToArray(),
                temperature = 0.7
            };

            string requestData = JsonConvert.SerializeObject(requestObject);
            string? answer = null;

            using (var client = CreateHttpClientWithProxy())
            {
                Console.WriteLine("Отправляем запрос к ChatGPT...");
                var httpContent = new StringContent(requestData, Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);

                if (!response.StatusCode.HasFlag(HttpStatusCode.OK))
                {
                    string responseContentError = await response.Content.ReadAsStringAsync();
                    dynamic responseJson = JObject.Parse(responseContentError);
                    Console.WriteLine($"{responseJson.error}: {responseJson.error_description}");

                }
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(jsonResponse);
                    string answerGpt = chatResponse.Choices[0].Message.Content.ToString();
                    AddMessage("assistant", answerGpt);
                    Console.WriteLine("Получен ответ от ChatGPT.");

                    //сохраняем запрос пользователя
                    var userRequest = new UserRequestDb
                    {
                        UserInput = userInput,
                        ProfileId = currentProfileId
                    };

                    _dbContext.UserRequests.Add(userRequest);
                    await _dbContext.SaveChangesAsync();

                    return chatResponse;
                }
                else
                {
                    Console.WriteLine("\nError: " + response.StatusCode + "\n");
                }
            }

            return null;
        }

        public static HttpClient CreateHttpClientWithProxy()
        {
            var proxy = new WebProxy("http://185.49.124.204:1431", false)
            {
                Credentials = new NetworkCredential("user173254", "afcvj4") 
            };

            var httpClientHandler = new HttpClientHandler
            {
                Proxy = proxy,
                PreAuthenticate = true,
                UseDefaultCredentials = false,
            };

            return new HttpClient(handler: httpClientHandler, disposeHandler: true);
        }
    }

    public class WidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
                return width * 0.55;

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
