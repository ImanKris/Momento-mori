using System.ComponentModel.DataAnnotations;

namespace RoboStore.Models;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Логин обязателен")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Логин должен быть от 3 до 50 символов")]
    [Display(Name = "Логин")]
    public string Login { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Некорректный email")]
    [Display(Name = "Введите почту")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Некорректный номер телефона")]
    [Display(Name = "Введите номер телефона")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Пароль обязателен")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть от 6 символов")]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "Пароли не совпадают")]
    [Display(Name = "Подтверждение пароля")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
