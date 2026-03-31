using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using RoboStore.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RoboStore.Controllers;

public class AccountController : Controller
{
    private readonly RoboStoreDbContext _context;
    private readonly TelegramAuthService _telegramAuth;
    private readonly TelegramService _telegramService;

    public AccountController(RoboStoreDbContext context, TelegramAuthService telegramAuth, TelegramService telegramService)
    {
        _context = context;
        _telegramAuth = telegramAuth;
        _telegramService = telegramService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SendCode([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return Json(new { success = false, message = "Заполните все поля" });
        }

        if (request.Password.Length < 4)
        {
            return Json(new { success = false, message = "Пароль должен быть не менее 4 символов" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == request.Login);

        if (user == null)
        {
            return Json(new { success = false, message = "Пользователь не найден" });
        }

        if (!VerifyPassword(request.Password, user.PasswordHash ?? ""))
        {
            return Json(new { success = false, message = "Неверный пароль" });
        }

        // Admin и Manager могут входить без кода верификации
        if (user.Role == "Admin" || user.Role == "Manager")
        {
            return await SignInUser(user, "/Admin");
        }

        // Генерируем код
        string code = new Random().Next(100000, 999999).ToString();

        HttpContext.Session.SetString("VerificationCode", code);
        HttpContext.Session.SetString("UserId", user.Id.ToString());
        HttpContext.Session.SetString("CodeExpires", DateTime.Now.AddMinutes(10).ToString());

        string? telegramLogin = user.TelegramUsername ?? request.Login;
        bool sent = await _telegramService.SendCodeAsync(telegramLogin, code);

        if (!sent)
        {
            return Json(new { success = true, message = $"Код для тестирования: {code}", debugMode = true });
        }

        return Json(new { success = true, message = "Код отправлен в Telegram боту", debugMode = false });
    }

    [HttpPost]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
    {
        string? storedCode = HttpContext.Session.GetString("VerificationCode");
        string? userIdStr = HttpContext.Session.GetString("UserId");
        string? expiresStr = HttpContext.Session.GetString("CodeExpires");

        if (string.IsNullOrEmpty(storedCode) || storedCode != request.Code)
        {
            return Json(new { success = false, message = "Неверный код" });
        }

        if (!string.IsNullOrEmpty(expiresStr) && DateTime.TryParse(expiresStr, out var expires))
        {
            if (DateTime.Now > expires)
            {
                return Json(new { success = false, message = "Код устарел. Получите новый код." });
            }
        }

        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Json(new { success = false, message = "Сессия устарела. Начните заново." });
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return Json(new { success = false, message = "Пользователь не найден" });
        }

        HttpContext.Session.Remove("VerificationCode");
        HttpContext.Session.Remove("UserId");
        HttpContext.Session.Remove("CodeExpires");

        return await SignInUser(user, "/");
    }

    [HttpPost]
    public async Task<IActionResult> TelegramCallback([FromForm] TelegramLoginViewModel model)
    {
        if (string.IsNullOrEmpty(model.Hash) || !_telegramAuth.ValidateTelegramHash(model))
        {
            return Unauthorized("Invalid hash");
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(model.AuthDate);
        if (DateTimeOffset.Now - authDate > TimeSpan.FromDays(1))
        {
            return Unauthorized("Data expired");
        }

        var user = await _telegramAuth.CreateOrUpdateUserAsync(model);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.TelegramUsername ?? user.FirstName ?? "User"),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString()),
            new Claim("TelegramId", user.TelegramId.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

        return user.Role switch
        {
            "Admin" => RedirectToAction("Index", "Admin"),
            "Manager" => RedirectToAction("Index", "Manager"),
            _ => RedirectToAction("Index", "Home")
        };
    }

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

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpPost]
    public async Task<IActionResult> RegisterSendCode([FromBody] RegisterSendCodeRequest request)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return Json(new { success = false, message = "Заполните все поля" });
        }

        if (request.Password.Length < 4)
        {
            return Json(new { success = false, message = "Пароль должен быть не менее 4 символов" });
        }

        if (request.Login.Length < 3)
        {
            return Json(new { success = false, message = "Логин должен быть не менее 3 символов" });
        }

        if (await _context.Users.AnyAsync(u => u.Login == request.Login))
        {
            return Json(new { success = false, message = "Логин уже занят" });
        }

        string code = new Random().Next(100000, 999999).ToString();

        HttpContext.Session.SetString("RegVerificationCode", code);
        HttpContext.Session.SetString("RegLogin", request.Login);
        HttpContext.Session.SetString("RegPassword", HashPassword(request.Password));
        HttpContext.Session.SetString("RegUsername", request.Username);
        HttpContext.Session.SetString("CodeExpires", DateTime.Now.AddMinutes(10).ToString());

        bool sent = await _telegramService.SendCodeAsync(request.Username, code);

        if (!sent)
        {
            return Json(new { success = true, message = $"Код для тестирования: {code}", debugMode = true });
        }

        return Json(new { success = true, message = "Код отправлен в Telegram боту", debugMode = false });
    }

    [HttpPost]
    public async Task<IActionResult> RegisterVerifyCode([FromBody] RegisterVerifyCodeRequest request)
    {
        string? storedCode = HttpContext.Session.GetString("RegVerificationCode");
        string? login = HttpContext.Session.GetString("RegLogin");
        string? passwordHash = HttpContext.Session.GetString("RegPassword");
        string? expiresStr = HttpContext.Session.GetString("CodeExpires");

        if (string.IsNullOrEmpty(storedCode) || storedCode != request.Code)
        {
            return Json(new { success = false, message = "Неверный код" });
        }

        if (!string.IsNullOrEmpty(expiresStr) && DateTime.TryParse(expiresStr, out var expires))
        {
            if (DateTime.Now > expires)
            {
                return Json(new { success = false, message = "Код устарел. Получите новый код." });
            }
        }

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(passwordHash))
        {
            return Json(new { success = false, message = "Сессия устарела. Начните заново." });
        }

        var user = new User
        {
            Login = login,
            PasswordHash = passwordHash,
            Role = "User",
            IsVerified = true,
            CreatedAt = DateTime.Now
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        HttpContext.Session.Remove("RegVerificationCode");
        HttpContext.Session.Remove("RegLogin");
        HttpContext.Session.Remove("RegPassword");
        HttpContext.Session.Remove("RegUsername");
        HttpContext.Session.Remove("CodeExpires");

        return Json(new { success = true, message = "Регистрация завершена!" });
    }

    private async Task<IActionResult> SignInUser(User user, string redirectUrl)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Login ?? "User"),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString())
        };

        if (user.TelegramId > 0)
        {
            claims.Add(new Claim("TelegramId", user.TelegramId.ToString()));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

        return Json(new { success = true, message = "Вход выполнен", redirectUrl, noCodeRequired = true });
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        return HashPassword(password) == passwordHash;
    }
}

public class LoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class VerifyCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class RegisterSendCodeRequest
{
    public string Username { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterVerifyCodeRequest
{
    public string Login { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
