using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoboStore.Data;
using RoboStore.Models;
using RoboStore.Services;
using System.Security.Claims;
using System.Text;

namespace RoboStore.Controllers;

public class AccountController : Controller
{
    private readonly RoboStoreDbContext _context;
    private readonly TelegramAuthService _telegramAuth;

    public AccountController(RoboStoreDbContext context, TelegramAuthService telegramAuth)
    {
        _context = context;
        _telegramAuth = telegramAuth;
    }

    // GET: Главная страница с виджетом Telegram
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // GET: Страница пользователя (личный кабинет)
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return RedirectToAction("Login");
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        return View(user);
    }

    // POST: Callback от Telegram Login Widget
    [HttpPost]
    public async Task<IActionResult> Callback([FromForm] TelegramLoginViewModel model)
    {
        // Проверяем hash
        if (!ModelState.IsValid || string.IsNullOrEmpty(model.Hash))
        {
            return BadRequest("Invalid data from Telegram");
        }

        // Проверяем hash для безопасности
        if (!ValidateData(model))
        {
            return Unauthorized("Invalid hash");
        }

        // Проверяем что данные не устарели (не старше 1 дня)
        var authDate = DateTimeOffset.FromUnixTimeSeconds(model.AuthDate);
        if (DateTimeOffset.Now - authDate > TimeSpan.FromDays(1))
        {
            return Unauthorized("Data expired");
        }

        // Создаем или обновляем пользователя
        var user = await _telegramAuth.CreateOrUpdateUserAsync(model);

        // Создаем claims для авторизации
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.TelegramUsername ?? user.FirstName ?? "User"),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString()),
            new Claim("TelegramId", user.TelegramId.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        // Вход в систему
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

        // Редирект в личный кабинет
        return RedirectToAction("Dashboard");
    }

    // POST: Выход
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // Вспомогательный метод для проверки hash (можно вынести в сервис)
    private bool ValidateData(TelegramLoginViewModel model)
    {
        // Формируем строку для проверки
        // Telegram hash verification algorithm
        var dataToHash = new SortedDictionary<string, string>
        {
            ["auth_date"] = model.AuthDate.ToString(),
            ["first_name"] = model.FirstName ?? ""
        };

        if (!string.IsNullOrEmpty(model.LastName))
            dataToHash["last_name"] = model.LastName;
        if (!string.IsNullOrEmpty(model.Username))
            dataToHash["username"] = model.Username;
        if (!string.IsNullOrEmpty(model.PhotoUrl))
            dataToHash["photo_url"] = model.PhotoUrl;
        if (model.Id > 0)
            dataToHash["id"] = model.Id.ToString();

        // Формируем строку "key=value\n" в алфавитном порядке
        var dataCheckString = string.Join("\n", dataToHash.Select(kv => $"{kv.Key}={kv.Value}")) + "\n";

        // Вычисляем secret = HMAC-SHA256(bot_token, "WebAppData")
        var secretKey = ComputeHmacSha256(Encoding.UTF8.GetBytes("8782323218:AAHmT7WLxWnmXLSWv3Bn30cbiqCW8REV-QE"), Encoding.UTF8.GetBytes("WebAppData"));

        // Вычисляем hash = HMAC-SHA256(secret, data_check_string)
        var hash = ComputeHmacSha256(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var hashHex = Convert.ToHexString(hash).ToLower();

        return hashHex == model.Hash?.ToLower();
    }

    private static byte[] ComputeHmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        return hmac.ComputeHash(data);
    }
}
