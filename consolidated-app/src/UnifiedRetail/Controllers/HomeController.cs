using Microsoft.AspNetCore.Mvc;

namespace UnifiedRetail.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    [Route("Home/Error")]
    public IActionResult Error() => View();
}
