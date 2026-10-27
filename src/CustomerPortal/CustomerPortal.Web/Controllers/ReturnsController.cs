using CustomerPortal.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

[Authorize]
public class ReturnsController : Controller
{
    private readonly ReturnService _returns;
    public ReturnsController(ReturnService returns) => _returns = returns;

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _returns.GetOwnReturnsAsync(User.CustomerId()));
}
