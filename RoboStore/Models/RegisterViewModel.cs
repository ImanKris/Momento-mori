using System.ComponentModel.DataAnnotations;

namespace RoboStore.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Telegram ID обязателен")]
    [Display(Name = "Telegram Chat ID")]
    public string TelegramId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Логин обязателен")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Логин должен быть от 3 до 50 символов")]
    [Display(Name = "Логин")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть от 6 символов")]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "Пароли не совпадают")]
    [Display(Name = "Подтверждение пароля")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
