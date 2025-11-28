using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize(Roles = "Admin")] // Tylko dla administratorów
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}