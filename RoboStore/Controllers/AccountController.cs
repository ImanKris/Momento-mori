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
// POST: Проверяет логин и пароль
[HttpPost]
public IActionResult Login(string login, string password)
{
    // Ищем пользователя в базе
    var user = _context.Users.FirstOrDefault(u => u.Login == login);
    
    // Если не нашли или пароль неверный
    if (user == null || user.PasswordHash != HashPassword(password))
    {
        ViewBag.Error = "Неверный логин или пароль";
        return View();
    }
    
    // Создаем сессию (cookie)
    HttpContext.Session.SetString("UserLogin", user.Login);
    HttpContext.Session.SetString("UserRole", user.Role);
    
    // Перенаправляем в зависимости от роли
    if (user.Role == "Admin")
        return RedirectToAction("Index", "Admin");
    else if (user.Role == "Manager")
        return RedirectToAction("Index", "Manager");
    else
        return RedirectToAction("Index", "User");
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
