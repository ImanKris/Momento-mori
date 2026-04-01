using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using System.Text.Json;

namespace RoboStore.Controllers;

/// <summary>
/// Контроллер магазина роботов
/// </summary>
public class UserController : Controller
{
    private readonly RoboStoreDbContext _context;
    private const string CartSessionKey = "Cart";

    public UserController(RoboStoreDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Главная страница магазина — список всех роботов с фильтрацией
    /// </summary>
    public IActionResult Index(string? search, int? typeId, string? inStock)
    {
        // Проверка авторизации через User.Identity (cookie-based)
        if (User?.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        var robots = _context.Robots.Include(r => r.RobotType).AsQueryable();

        // Фильтр по названию (поиск по подстроке)
        if (!string.IsNullOrWhiteSpace(search))
        {
            robots = robots.Where(r => r.Model.Contains(search) || (r.Description != null && r.Description.Contains(search)));
            ViewBag.Search = search;
        }

        // Фильтр по типу робота
        if (typeId.HasValue && typeId.Value > 0)
        {
            robots = robots.Where(r => r.TypeId == typeId.Value);
            ViewBag.SelectedTypeId = typeId.Value;
        }

        // Фильтр по наличию
        if (inStock == "yes")
        {
            robots = robots.Where(r => r.Stock > 0);
            ViewBag.InStock = "yes";
        }
        else if (inStock == "no")
        {
            robots = robots.Where(r => r.Stock <= 0);
            ViewBag.InStock = "no";
        }

        var robotList = robots.ToList();
        ViewBag.RobotTypes = _context.RobotTypes.OrderBy(t => t.Name).ToList();

        return View(robotList);
    }

    /// <summary>
    /// Страница деталей робота
    /// </summary>
    public IActionResult Details(int id)
    {
        var robot = _context.Robots.Find(id);

        if (robot == null)
        {
            return NotFound();
        }

        // Если нет в наличии — передаём сообщение
        if (robot.Stock == 0)
        {
            ViewBag.Message = "Нет в наличии";
        }

        return View(robot);
    }

    /// <summary>
    /// Добавить робота в корзину
    /// </summary>
    public IActionResult AddToCart(int id)
    {
        // Проверка авторизации
        if (User?.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        // Проверяем существование робота и наличие на складе
        var robot = _context.Robots.Find(id);
        if (robot == null || robot.Stock <= 0)
        {
            return RedirectToAction("Index");
        }

        // Читаем корзину из сессии
        var cart = GetCartFromSession();

        // Добавляем ID робота в корзину (если ещё нет)
        if (!cart.Contains(id))
        {
            cart.Add(id);
        }

        // Сохраняем корзину обратно в сессию
        SaveCartToSession(cart);

        return RedirectToAction("Cart");
    }

    /// <summary>
    /// Удалить робота из корзины
    /// </summary>
    public IActionResult RemoveFromCart(int id)
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        var cart = GetCartFromSession();
        cart.Remove(id);
        SaveCartToSession(cart);

        return RedirectToAction("Cart");
    }

    /// <summary>
    /// Полностью очистить корзину
    /// </summary>
    public IActionResult ClearCart()
    {
        HttpContext.Session.Remove(CartSessionKey);
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Показать корзину
    /// </summary>
    public IActionResult Cart()
    {
        // Проверка авторизации
        if (User?.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        // Читаем корзину из сессии
        var cart = GetCartFromSession();

        // Получаем роботов по ID из корзины
        var robots = _context.Robots.Where(r => cart.Contains(r.Id)).ToList();

        // Считаем общую сумму
        ViewBag.Total = robots.Sum(r => r.Price);

        return View(robots);
    }

    /// <summary>
    /// Оформить заказ
    /// </summary>
    public IActionResult Checkout()
    {
        // Проверка авторизации
        if (User?.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        // Читаем корзину
        var cart = GetCartFromSession();

        // Если корзина пуста — редирект
        if (cart.Count == 0)
        {
            TempData["Message"] = "Корзина пуста";
            return RedirectToAction("Index");
        }

        // Получаем UserId текущего пользователя
        var userLogin = User.Identity?.Name;
        if (string.IsNullOrEmpty(userLogin))
        {
            return RedirectToAction("Login", "Account");
        }

        var user = _context.Users.FirstOrDefault(u => u.Login == userLogin);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Создаём заказы для каждого робота в корзине
        foreach (var robotId in cart)
        {
            var robot = _context.Robots.Find(robotId);

            // Пропускаем если робот не найден или нет в наличии
            if (robot == null || robot.Stock <= 0)
            {
                continue;
            }

            // Создаём заказ
            var order = new Order
            {
                UserId = user.Id,
                RobotId = robotId,
                OrderDate = DateTime.Now,
                Status = "В обработке"
            };
            _context.Orders.Add(order);

            // Уменьшаем остаток на складе
            robot.Stock -= 1;
        }

        // Сохраняем все изменения
        _context.SaveChanges();

        // Очищаем корзину
        HttpContext.Session.Remove(CartSessionKey);

        TempData["Message"] = "Заказ оформлен";
        return RedirectToAction("Orders");
    }

    /// <summary>
    /// История заказов текущего пользователя
    /// </summary>
    public IActionResult Orders()
    {
        // Проверка авторизации
        if (User?.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        // Получаем пользователя
        var userLogin = User.Identity?.Name;
        if (string.IsNullOrEmpty(userLogin))
        {
            return RedirectToAction("Login", "Account");
        }

        var user = _context.Users.FirstOrDefault(u => u.Login == userLogin);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Получаем заказы пользователя с данными о роботах
        var orders = _context.Orders
            .Include(o => o.Robot)
            .Where(o => o.UserId == user.Id)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View(orders);
    }

    #region Вспомогательные методы для работы с корзиной

    /// <summary>
    /// Читает корзину из сессии (JSON → List<int>)
    /// </summary>
    private List<int> GetCartFromSession()
    {
        var data = HttpContext.Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(data))
        {
            return new List<int>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<int>>(data) ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }

    /// <summary>
    /// Сохраняет корзину в сессию (List<int> → JSON)
    /// </summary>
    private void SaveCartToSession(List<int> cart)
    {
        var json = JsonSerializer.Serialize(cart);
        HttpContext.Session.SetString(CartSessionKey, json);
    }

    #endregion
}
