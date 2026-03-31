using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    private readonly TelegramService _telegramService;

    public AccountController(RoboStoreDbContext context, TelegramService telegramService)
    {
        _context = context;
        _telegramService = telegramService;
    }

    // GET: Показывает форму входа
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // POST: Обрабатывает вход
    [HttpPost]
    public async Task<IActionResult> Login(string login, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == login);

        if (user == null)
        {
            ModelState.AddModelError("", "Пользователь с таким логином не найден");
            return View();
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Неверный пароль");
            return View();
        }

        if (!user.IsVerified)
        {
            ModelState.AddModelError("", "Аккаунт не верифицирован. Подтвердите через Telegram.");
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString())
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

    // POST: Выход
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // GET: Показывает форму регистрации
    [HttpGet]
    public IActionResult Register()
    {
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
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Авторизуем
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

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
    }
}
