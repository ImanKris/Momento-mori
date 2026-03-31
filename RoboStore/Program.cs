using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RoboStore.Data;
using RoboStore.Models;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<RoboStoreDbContext>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

// Seed users
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RoboStoreDbContext>();
    db.Database.EnsureCreated();

    string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    var manager1 = db.Users.FirstOrDefault(u => u.Login == "manager1");
    if (manager1 == null)
    {
        db.Users.Add(new User { Login = "manager1", PasswordHash = HashPassword("ManagerMori"), Role = "Manager", IsVerified = true });
    }
    else
    {
        manager1.PasswordHash = HashPassword("ManagerMori");
        manager1.Role = "Manager";
        manager1.IsVerified = true;
    }

    var admin1 = db.Users.FirstOrDefault(u => u.Login == "admin1");
    if (admin1 == null)
    {
        db.Users.Add(new User { Login = "admin1", PasswordHash = HashPassword("AdminMori"), Role = "Admin", IsVerified = true });
    }
    else
    {
        admin1.PasswordHash = HashPassword("AdminMori");
        admin1.Role = "Admin";
        admin1.IsVerified = true;
    }

    db.SaveChanges();
}

app.UseSession();
app.UseAuthentication();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
