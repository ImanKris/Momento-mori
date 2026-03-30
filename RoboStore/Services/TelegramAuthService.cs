using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;

namespace RoboStore.Services
{
    public class TelegramAuthService
    {
        private readonly RoboStoreDbContext _context;
        private const string BotToken = "8782323218:AAHmT7WLxWnmXLSWv3Bn30cbiqCW8REV-QE";

        public TelegramAuthService(RoboStoreDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Проверяет hash данных от Telegram Login Widget
        /// </summary>
        public bool ValidateTelegramHash(TelegramLoginViewModel model)
        {
            if (string.IsNullOrEmpty(model.Hash))
                return false;

            // Формируем строку данных для проверки
            // Данные от widget приходят в формате: field1=value1\nfield2=value2\n... (sorted by key)
            var dataCheckString = $"auth_date={model.AuthDate}\nfirst_name={model.FirstName ?? ""}\nid={model.Id}\n";

            if (!string.IsNullOrEmpty(model.LastName))
                dataCheckString += $"last_name={model.LastName}\n";
            if (!string.IsNullOrEmpty(model.Username))
                dataCheckString += $"username={model.Username}\n";
            if (!string.IsNullOrEmpty(model.PhotoUrl))
                dataCheckString += $"photo_url={model.PhotoUrl}\n";

            // Вычисляем secret key = HMAC-SHA256 of BotToken
            var secretKey = ComputeHmacSha256(Encoding.UTF8.GetBytes(BotToken), Encoding.UTF8.GetBytes("WebAppData"));

            // Вычисляем hash = HMAC-SHA256 of dataCheckString using secret key
            var hash = ComputeHmacSha256(secretKey, Encoding.UTF8.GetBytes(dataCheckString));

            // Конвертируем в hex строку
            var hashHex = Convert.ToHexString(hash).ToLower();

            // Сравниваем с полученным hash (также в нижнем регистре)
            return hashHex == model.Hash.ToLower();
        }

        /// <summary>
        /// Создает или обновляет пользователя из данных Telegram
        /// </summary>
        public async Task<User> CreateOrUpdateUserAsync(TelegramLoginViewModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramId == model.Id);

            if (user == null)
            {
                // Новый пользователь
                user = new User
                {
                    TelegramId = model.Id,
                    TelegramUsername = model.Username,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhotoUrl = model.PhotoUrl,
                    Role = "User",
                    IsVerified = true,
                    CreatedAt = DateTime.Now
                };
                _context.Users.Add(user);
            }
            else
            {
                // Обновляем существующего
                user.TelegramUsername = model.Username;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.PhotoUrl = model.PhotoUrl;
            }

            await _context.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Находит пользователя по TelegramId
        /// </summary>
        public async Task<User?> GetUserByTelegramIdAsync(long telegramId)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramId);
        }

        private static byte[] ComputeHmacSha256(byte[] key, byte[] data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(data);
        }
    }
}
