using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace RoboStoreBot;

class Program
{
    // Токен бота RoboStoreMoriBot
    private const string BotToken = "8625743321:AAF9L_989VApHpTNgRXulXfuLxgwYRtSaOA";
    private const string ApiUrl = "https://api.telegram.org/bot" + BotToken;

    static async Task Main(string[] args)
    {
        Console.WriteLine("RoboStore Bot started...");
        Console.WriteLine("Bot token: " + BotToken);

        // Проверим что бот работает
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.GetAsync($"{ApiUrl}/getMe");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Bot info: " + content);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error checking bot: " + ex.Message);
        }

        long offset = 0;

        while (true)
        {
            try
            {
                var updates = await GetUpdatesAsync(offset);

                foreach (var update in updates)
                {
                    Console.WriteLine($"Update received: {update.UpdateId}");

                    if (update.Message != null)
                    {
                        var chatId = update.Message.Chat.Id;
                        var text = update.Message.Text ?? "";
                        var firstName = update.Message.Chat.FirstName ?? "User";

                        Console.WriteLine($"  ChatId: {chatId}, Text: {text}, From: {firstName}");

                        // Команда /start
                        if (text == "/start")
                        {
                            var response = $"Привет, {firstName}! 👋\n\n" +
                                         $"Ваш Chat ID: <code>{chatId}</code>\n\n" +
                                         $"Используйте этот ID для регистрации на сайте RoboStore.";

                            await SendMessageAsync(chatId, response);
                            Console.WriteLine($"  Sent welcome message to {chatId}");
                        }
                        // Команда /help
                        else if (text == "/help")
                        {
                            var response = "📖 <b>Справка RoboStore Bot</b>\n\n" +
                                         "После регистрации на сайте, бот будет отправлять вам коды подтверждения.\n\n" +
                                         "Команды:\n" +
                                         "/start - Показать ваш Chat ID\n" +
                                         "/help - Эта справка";

                            await SendMessageAsync(chatId, response);
                            Console.WriteLine($"  Sent help message to {chatId}");
                        }
                    }

                    offset = update.UpdateId + 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Polling error: {ex.Message}");
            }

            Thread.Sleep(5000); // Пауза между запросами
        }
    }

    static async Task<List<Update>> GetUpdatesAsync(long offset)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);

        var url = $"{ApiUrl}/getUpdates?offset={offset}&timeout=0";
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"API Error: {content}");
            return new List<Update>();
        }

        var result = JsonSerializer.Deserialize<UpdatesResponse>(content);
        return result?.Updates ?? new List<Update>();
    }

    static async Task SendMessageAsync(long chatId, string text)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(10);

        var json = JsonSerializer.Serialize(new { chat_id = chatId, text = text, parse_mode = "HTML" });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{ApiUrl}/sendMessage", content);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Send error: {result}");
        }
    }
}

class UpdatesResponse
{
    public bool Ok { get; set; }
    public List<Update>? Updates { get; set; }
}

class Update
{
    public long UpdateId { get; set; }
    public Message? Message { get; set; }
}

class Message
{
    public Chat Chat { get; set; } = new();
    public string? Text { get; set; }
}

class Chat
{
    public long Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
