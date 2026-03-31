using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RoboStore.Services
{
    public class TelegramService
    {
        // ВСТАВЬ СЮДА СВОЙ ТОКЕН ОТ @BotFather
        private const string BotToken = "8625743321:AAF9L_989VApHpTNgRXulXfuLxgwYRtSaOA";
        
        public async Task<bool> SendCodeAsync(string chatId, string code)
        {
            try
            {
                // Формируем сообщение
                string message = $"🔐 RoboStoreMori\n\n" +
                                $"Ваш код подтверждения: {code}\n\n" +
                                $"⏰ Действителен 10 минут\n" +
                                $"Если вы не запрашивали код — проигнорируйте сообщение.";

                // Формируем URL для API Telegram
                string url = $"https://api.telegram.org/bot{BotToken}/sendMessage";

                // Создаём JSON
                string json = $"{{\"chat_id\":\"{chatId}\",\"text\":\"{message}\",\"parse_mode\":\"HTML\"}}";

                // Отправляем запрос
                using (var client = new HttpClient())
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(url, content);
                    
                    // Проверяем ответ
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"✅ Код {code} отправлен в Telegram пользователю {chatId}");
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
    }
}