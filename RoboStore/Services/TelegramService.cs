using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RoboStore.Services
{
    public class TelegramService
    {
        private const string BotToken = "8625743321:AAF9L_989VApHpTNgRXulXfuLxgwYRtSaOA";

        /// <summary>
        /// Отправляет код подтверждения пользователю по его Telegram username
        /// </summary>
        public async Task<bool> SendCodeAsync(string telegramUsername, string code)
        {
            try
            {
                // Сначала получаем chat_id по username через getChat
                var chatId = await GetChatIdByUsernameAsync(telegramUsername);
                if (chatId == null)
                {
                    Console.WriteLine($"❌ Не найден chat_id для пользователя @{telegramUsername}");
                    return false;
                }

                // Формируем сообщение
                string message = $"🔐 RoboStore\n\n" +
                               $"Ваш код подтверждения: <b>{code}</b>\n\n" +
                               $"⏰ Действителен 10 минут\n" +
                               $"Если вы не запрашивали код — проигнорируйте сообщение.";

                // Отправляем сообщение
                string url = $"https://api.telegram.org/bot{BotToken}/sendMessage";
                string json = $"{{\"chat_id\":\"{chatId}\",\"text\":\"{message}\",\"parse_mode\":\"HTML\"}}";

                using (var client = new HttpClient())
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"✅ Код {code} отправлен пользователю @{telegramUsername}");
                        return true;
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"❌ Ошибка Telegram: {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Исключение: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получает chat_id по username через Telegram Bot API
        /// </summary>
        private async Task<string?> GetChatIdByUsernameAsync(string username)
        {
            try
            {
                // Убираем @ если есть
                username = username.TrimStart('@');

                string url = $"https://api.telegram.org/bot{BotToken}/getChat?chat_id=@{username}";

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        // Простой парсинг JSON для получения id
                        // {"ok":true,"result":{"id":123456789,"is_bot":false,"first_name":"...","username":"..."}}
                        if (json.Contains("\"ok\":true") && json.Contains("\"id\":"))
                        {
                            var start = json.IndexOf("\"id\":") + 5;
                            var end = json.IndexOf(",", start);
                            if (end == -1) end = json.IndexOf("}", start);
                            return json.Substring(start, end - start).Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка получения chat_id: {ex.Message}");
            }
            return null;
        }
    }
}
