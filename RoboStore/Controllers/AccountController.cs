using Microsoft.AspNetCore.Mvc;

namespace RoboStore.Controllers;

public class AccountController : Controller
{
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
}
