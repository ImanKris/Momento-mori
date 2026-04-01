using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RoboStore.Controllers;

public class AccountController : Controller
{
    private readonly RoboStoreDbContext _context;

    public AccountController(RoboStoreDbContext context)
    {
        _context = context;
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
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
        {
            return Json(new { success = false, message = "Заполните все поля" });
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

        // Редирект в зависимости от роли
        string redirectUrl = user.Role switch
        {
            "Admin" => "/Admin",
            "Manager" => "/Manager",
            _ => "/User"
        };

        return await SignInUser(user, redirectUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Login) || string.IsNullOrEmpty(request.Password))
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

        var user = new User
        {
            Login = request.Login,
            PasswordHash = HashPassword(request.Password),
            Role = "User",
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Регистрация успешна!" });
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    private async Task<IActionResult> SignInUser(User user, string redirectUrl)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Login ?? "User"),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.Id.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

        // Синхронизируем сессию для совместимости с UserController
        HttpContext.Session.SetString("UserLogin", user.Login ?? "User");

        return Json(new { success = true, message = "Вход выполнен", redirectUrl });
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

public class RegisterRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
