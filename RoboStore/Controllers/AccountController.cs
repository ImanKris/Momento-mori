using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoboStore.Data;
using RoboStore.Models;
using RoboStore.Services;
using System.Security.Claims;
<<<<<<< HEAD
=======
using System.Security.Cryptography;
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478
using System.Text;

namespace RoboStore.Controllers;

public class AccountController : Controller
{
    private readonly RoboStoreDbContext _context;
<<<<<<< HEAD
    private readonly TelegramAuthService _telegramAuth;

    public AccountController(RoboStoreDbContext context, TelegramAuthService telegramAuth)
    {
        _context = context;
        _telegramAuth = telegramAuth;
=======
    private readonly TelegramService _telegramService;

    public AccountController(RoboStoreDbContext context, TelegramService telegramService)
    {
        _context = context;
        _telegramService = telegramService;
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478
    }

    // GET: Главная страница с виджетом Telegram
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }
<<<<<<< HEAD
=======

    // POST: Обрабатывает вход
    [HttpPost]
    public async Task<IActionResult> Login(string login, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == login);
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478

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
<<<<<<< HEAD
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
=======
            ModelState.AddModelError("", "Аккаунт не верифицирован. Подтвердите через Telegram.");
            return View();
        }

>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478
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

<<<<<<< HEAD
        // Редирект в личный кабинет
        return RedirectToAction("Dashboard");
=======
        return user.Role switch
        {
            "Admin" => RedirectToAction("Index", "Admin"),
            "Manager" => RedirectToAction("Index", "Manager"),
            _ => RedirectToAction("Index", "Home")
        };
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478
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
<<<<<<< HEAD
        // Формируем строку для проверки
        // Telegram hash verification algorithm
        var dataToHash = new SortedDictionary<string, string>
        {
            ["auth_date"] = model.AuthDate.ToString(),
            ["first_name"] = model.FirstName ?? ""
=======
        return View();
    }

    // POST: Отправляет код верификации через Telegram
    [HttpPost]
    public async Task<IActionResult> SendCode([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.TelegramId) || string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return Json(new { success = false, message = "Заполните все поля" });
        }

        if (request.Password.Length < 6)
        {
            return Json(new { success = false, message = "Пароль должен быть не менее 6 символов" });
        }

        if (await _context.Users.AnyAsync(u => u.Login == request.Login))
        {
            return Json(new { success = false, message = "Логин уже занят" });
        }

        string code = new Random().Next(100000, 999999).ToString();

        TempData["TelegramId"] = request.TelegramId;
        TempData["Login"] = request.Login;
        TempData["Password"] = HashPassword(request.Password);
        TempData["VerificationCode"] = code;
        TempData["CodeExpires"] = DateTime.Now.AddMinutes(10).ToString();

        bool sent = await _telegramService.SendCodeAsync(request.TelegramId, code);

        if (!sent)
        {
            return Json(new { success = false, message = "Не удалось отправить код в Telegram. Проверьте Chat ID." });
        }

        return Json(new { success = true, message = $"Код отправлен в Telegram боту" });
    }

    // POST: Проверяет код и завершает регистрацию
    [HttpPost]
    public async Task<IActionResult> ConfirmRegistration([FromBody] ConfirmRequest request)
    {
        string? storedCode = TempData["VerificationCode"] as string;
        string? telegramId = TempData["TelegramId"] as string;
        string? login = TempData["Login"] as string;
        string? passwordHash = TempData["Password"] as string;

        if (string.IsNullOrEmpty(storedCode) || storedCode != request.Code)
        {
            return Json(new { success = false, message = "Неверный код" });
        }

        if (TempData["CodeExpires"] is string expiresStr && DateTime.TryParse(expiresStr, out var expires))
        {
            if (DateTime.Now > expires)
            {
                return Json(new { success = false, message = "Код устарел" });
            }
        }

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(passwordHash))
        {
            return Json(new { success = false, message = "Данные устарели. Начните регистрацию заново." });
        }

        var user = new User
        {
            Login = login,
            PasswordHash = passwordHash,
            Role = "User",
            IsVerified = true,
            CreatedAt = DateTime.Now
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478
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

<<<<<<< HEAD
        // Вычисляем hash = HMAC-SHA256(secret, data_check_string)
        var hash = ComputeHmacSha256(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var hashHex = Convert.ToHexString(hash).ToLower();

        return hashHex == model.Hash?.ToLower();
    }

    private static byte[] ComputeHmacSha256(byte[] key, byte[] data)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        return hmac.ComputeHash(data);
=======
        return Json(new { success = true, message = "Регистрация завершена" });
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        return passwordHash == HashPassword(password);
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
>>>>>>> 845866a2aa35226d43f74152fbeebe37cf99a478
    }
}
