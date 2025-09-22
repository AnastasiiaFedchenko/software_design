using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    public class HomeController : Controller
    {
        [Route("/Home/Error")]
        public IActionResult Error()
        {
            return View();
        }

        [Route("/Home/AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}