using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using System.Security.Claims;

namespace RoboStore.Controllers;

public class AccountController : Controller
{
    private readonly RoboStoreDbContext _context;

    public AccountController(RoboStoreDbContext context)
    {
        _context = context;
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
            ModelState.AddModelError("", "Аккаунт не верифицирован. Проверьте почту или телефон.");
            return View();
        }

        // Создаём claims для авторизации
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

        // Перенаправляем в зависимости от роли
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

    // POST: Обрабатывает регистрацию
    [HttpPost]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Показываем форму для ввода кода верификации
        TempData["Login"] = model.Login;
        TempData["Email"] = model.Email;
        TempData["Phone"] = model.Phone;
        TempData["Password"] = model.Password;

        // Генерируем код
        string code = new Random().Next(100000, 999999).ToString();
        TempData["VerificationCode"] = code;
        TempData["VerificationCodeTime"] = DateTime.Now.ToString();

        // TODO: Отправить код на email или SMS

        return View("VerifyCode", (object)model.Email);
    }

    // GET: Показывает форму ввода кода
    [HttpGet]
    public IActionResult VerifyCode()
    {
        if (TempData["Login"] == null)
        {
            return RedirectToAction("Register");
        }
        return View();
    }

    // POST: Проверяет код и завершает регистрацию
    [HttpPost]
    public async Task<IActionResult> ConfirmRegistration(string code)
    {
        string? storedCode = TempData["VerificationCode"] as string;
        string? login = TempData["Login"] as string;
        string? email = TempData["Email"] as string;
        string? phone = TempData["Phone"] as string;
        string? password = TempData["Password"] as string;

        if (storedCode == null || storedCode != code)
        {
            ModelState.AddModelError("", "Неверный код");
            return View("VerifyCode", email);
        }

        // Проверяем срок действия кода (10 минут)
        if (TempData["VerificationCodeTime"] is string codeTimeStr &&
            DateTime.TryParse(codeTimeStr, out var codeTime) &&
            DateTime.Now - codeTime > TimeSpan.FromMinutes(10))
        {
            ModelState.AddModelError("", "Код устарел");
            return View("VerifyCode", email);
        }

        // Создаём пользователя
        var user = new User
        {
            Login = login!,
            Email = email,
            Phone = phone,
            PasswordHash = HashPassword(password!),
            Role = "User",
            IsVerified = true
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

        return RedirectToAction("Index", "Home");
    }

    // POST: Отправляет код верификации
    [HttpPost]
    public IActionResult SendCode(string contact, string contactType)
    {
        if (string.IsNullOrEmpty(contact))
        {
            return Json(new { success = false, message = "Укажите контакт для верификации" });
        }

        string code = new Random().Next(100000, 999999).ToString();

        // TODO: Отправить код (email или SMS в зависимости от contactType)
        // if (contactType == "email") { /* отправить на email */ }
        // else { /* отправить SMS */ }

        TempData["VerificationCode"] = code;
        TempData["Contact"] = contact;
        TempData["ContactType"] = contactType;

        return Json(new { success = true, message = $"Код отправлен на {contact}" });
    }

    // POST: Проверяет код верификации
    [HttpPost]
    public IActionResult VerifyCode(string code, string contact, string contactType, string login, string password)
    {
        string? storedCode = TempData["VerificationCode"] as string;
        string? storedContact = TempData["Contact"] as string;

        if (storedCode != code || storedContact != contact)
        {
            return Json(new { success = false, message = "Неверный код" });
        }

        // TODO: Создать пользователя в базе данных
        // TODO: Авторизовать пользователя

        return Json(new { success = true, message = "Регистрация завершена" });
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        // Простая проверка хеша (для production использовать BCrypt/Argon2)
        return passwordHash == HashPassword(password);
    }

    private static string HashPassword(string password)
    {
        // Простой хеш (для production использовать BCrypt/Argon2)
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
