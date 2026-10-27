using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderDesk.Application;
using OrderDesk.Domain;
using UnifiedRetail.Platform;

namespace UnifiedRetail.Areas.Staff.Controllers;

/// <summary>
/// Staff area (OrderDesk functionality migrated from WPF to web). All controllers require a
/// staff role (StaffOnly policy). Cancellation override additionally requires the
/// CanOverrideCancellation policy (SalesManager). Services are reused verbatim from
/// OrderDesk.Application, preserving staff-specific rules (eligibility proc, override audit,
/// quote conversion proc, mandatory-info guard).
/// </summary>
[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class HomeController : Controller
{
    public IActionResult Index() => View();
}

[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class CustomersController : Controller
{
    private readonly CustomerService _customers;
    public CustomersController(CustomerService customers) => _customers = customers;

    [HttpGet]
    public async Task<IActionResult> Index(string? term, CancellationToken ct)
    {
        ViewBag.Term = term;
        return View(await _customers.SearchAsync(term, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var c = await _customers.GetAsync(id, ct);
        if (c is null) { TempData["Err"] = "Customer not found."; return RedirectToAction(nameof(Index)); }
        return View(c);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Customer model, CancellationToken ct)
    {
        var r = await _customers.UpdateContactAsync(model, ct); // staff full-field edit (OD-CAP-002)
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Customer updated." : r.Error;
        return RedirectToAction(nameof(Edit), new { id = model.CustomerId });
    }
}

[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class CatalogController : Controller
{
    private readonly CatalogService _catalog;
    public CatalogController(CatalogService catalog) => _catalog = catalog;

    [HttpGet]
    public async Task<IActionResult> Index(string? term, CancellationToken ct)
    {
        ViewBag.Term = term;
        return View(await _catalog.BrowseAsync(term, ct));
    }
}

[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class QuotesController : Controller
{
    private readonly QuoteService _quotes;
    public QuotesController(QuoteService quotes) => _quotes = quotes;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken ct)
        => View(await _quotes.ListAsync(status, ct));

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var q = await _quotes.GetAsync(id, ct);
        if (q is null) { TempData["Err"] = "Quote not found."; return RedirectToAction(nameof(Index)); }
        return View(q);
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int customerId, int salesAgentId, int validityDays, CancellationToken ct)
    {
        var r = await _quotes.CreateQuoteAsync(customerId, salesAgentId <= 0 ? User.AgentId() : salesAgentId,
            validityDays <= 0 ? OrderDeskRules.DefaultQuoteValidityDays : validityDays, ct);
        if (!r.Success || r.Value is null) { TempData["Err"] = r.Error; return View(); }
        TempData["Ok"] = $"Quote {r.Value.QuoteNumber} created.";
        return RedirectToAction(nameof(Details), new { id = r.Value.SalesQuoteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(int id, int productId, int quantity, CancellationToken ct)
    {
        var r = await _quotes.AddLineAsync(id, productId, quantity, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Line added." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyDiscount(int id, decimal discountPercent, CancellationToken ct)
    {
        var r = await _quotes.ApplyDiscountAsync(id, discountPercent, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Discount applied." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id, CancellationToken ct)
    {
        var r = await _quotes.ConvertToOrderAsync(id, ct); // usp_ConvertQuoteToOrder (OD-CAP-009)
        if (!r.Success) { TempData["Err"] = r.Error; return RedirectToAction(nameof(Details), new { id }); }
        TempData["Ok"] = $"Converted to order #{r.Value}.";
        return RedirectToAction("Details", "Orders", new { id = r.Value });
    }
}

[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class ApprovalsController : Controller
{
    private readonly QuoteService _quotes;
    public ApprovalsController(QuoteService quotes) => _quotes = quotes;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _quotes.ListPendingApprovalsAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int id, bool approve, string? notes, CancellationToken ct)
    {
        var r = await _quotes.DecideDiscountAsync(id, approve, User.AgentId(), notes, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? (approve ? "Approved." : "Rejected.") : r.Error;
        return RedirectToAction(nameof(Index));
    }
}

[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class OrdersController : Controller
{
    private readonly OrderService _orders;
    public OrdersController(OrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken ct)
        => View(await _orders.ListAsync(status, ct)); // unrestricted staff read (OD-CAP-011)

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var o = await _orders.GetAsync(id, ct);
        if (o is null) { TempData["Err"] = "Order not found."; return RedirectToAction(nameof(Index)); }
        ViewBag.Shipments = await _orders.GetShipmentsAsync(id, ct); // read-only shipment lookup (OD-CAP-016)
        var (eligible, reason) = await _orders.CheckCancellationEligibilityAsync(id, ct);
        ViewBag.Eligible = eligible;
        ViewBag.EligibilityReason = reason;
        return View(o);
    }

    [HttpGet]
    public IActionResult Create() => View();

    // Staff-assisted order create (OD-CAP-010) — NEW web entry point (GAP-UI-1).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int customerId, int productId, int quantity, CancellationToken ct)
    {
        var lines = new List<OrderService.NewOrderLine> { new(productId, quantity <= 0 ? 1 : quantity) };
        var r = await _orders.CreateStaffOrderAsync(customerId, User.AgentId(), lines, ct);
        if (!r.Success) { TempData["Err"] = r.Error; return View(); }
        TempData["Ok"] = $"Order #{r.Value} created.";
        return RedirectToAction(nameof(Details), new { id = r.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, CancellationToken ct)
    {
        // Normal staff cancellation (eligibility via usp_EvaluateCancellationEligibility).
        var r = await _orders.CancelOrderAsync(id, User.AgentId(), reason, forceOverride: false, ct: ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Order cancelled." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // Override cancellation requires SalesManager (CanOverrideCancellation policy) AND an
    // authorized agent flag + justification (enforced again in the service). Writes an audit row.
    [HttpPost]
    [Authorize(Policy = "CanOverrideCancellation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Override(int id, string justification, string? reason, CancellationToken ct)
    {
        var r = await _orders.CancelOrderAsync(id, User.AgentId(), reason, forceOverride: true, overrideJustification: justification, ct: ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Order cancelled with override (audited)." : r.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}

[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class ReturnsController : Controller
{
    private readonly ReturnReviewService _returns;
    public ReturnsController(ReturnReviewService returns) => _returns = returns;

    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken ct)
        => View(await _returns.ListAsync(status, ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, bool approve, CancellationToken ct)
    {
        var r = await _returns.ReviewAsync(id, approve, User.AgentId(), ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? (approve ? "Return approved." : "Return rejected.") : r.Error;
        return RedirectToAction(nameof(Index));
    }
}

[Area("Staff")]
[Authorize(Policy = "StaffOnly")]
public class AgentsController : Controller
{
    private readonly AgentTaskService _agents;
    public AgentsController(AgentTaskService agents) => _agents = agents;

    [HttpGet]
    public async Task<IActionResult> Index(int? agentId, CancellationToken ct)
    {
        var agents = await _agents.ListAgentsAsync(ct);
        ViewBag.Agents = agents;
        var selected = agentId ?? (User.AgentId() > 0 ? User.AgentId() : agents.FirstOrDefault()?.SalesAgentId ?? 0);
        ViewBag.SelectedAgentId = selected;
        ViewBag.Tasks = selected > 0 ? await _agents.ListTasksAsync(selected, ct) : new();
        ViewBag.Commissions = selected > 0 ? await _agents.ListCommissionsAsync(selected, ct) : new();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTask(int taskId, string status, int agentId, CancellationToken ct)
    {
        var r = await _agents.UpdateStatusAsync(taskId, status, ct);
        TempData[r.Success ? "Ok" : "Err"] = r.Success ? "Task updated." : r.Error;
        return RedirectToAction(nameof(Index), new { agentId });
    }
}
