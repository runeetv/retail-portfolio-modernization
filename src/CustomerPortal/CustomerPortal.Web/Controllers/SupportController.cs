using CustomerPortal.Application;
using CustomerPortal.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Web.Controllers;

[Authorize]
public class SupportController : Controller
{
    private readonly SupportTicketService _tickets;
    public SupportController(SupportTicketService tickets) => _tickets = tickets;

    [HttpGet]
    public async Task<IActionResult> Index()
        => View(await _tickets.GetOwnTicketsAsync(User.AccountId()));

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _tickets.GetOwnTicketAsync(User.AccountId(), id);
        if (ticket is null) return NotFound();
        return View(ticket);
    }

    [HttpGet]
    public IActionResult Create() => View(new SupportTicketCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupportTicketCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _tickets.CreateAsync(User.AccountId(), model.Subject, model.Body, model.RelatedOrderId);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMessage(int id, string body)
    {
        var result = await _tickets.AddMessageAsync(User.AccountId(), id, body);
        TempData["Message"] = result.Success ? "Message sent." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, string newStatus)
    {
        var result = await _tickets.ChangeStatusAsync(User.AccountId(), id, newStatus);
        TempData["Message"] = result.Success ? $"Ticket set to {newStatus}." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
