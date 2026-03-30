using Microsoft.AspNetCore.Mvc;

namespace RoboStore.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
