using OrderDesk.Application;
using OrderDesk.Infrastructure;

namespace OrderDesk.Desktop;

/// <summary>Composition root for OrderDesk desktop services (simple hand-rolled DI).</summary>
public sealed class AppServices
{
    public static AppServices Current { get; } = new();

    private readonly Func<OrderDeskDbContext> _ctxFactory;

    public CustomerService Customers { get; }
    public CatalogService Catalog { get; }
    public QuoteService Quotes { get; }
    public OrderService Orders { get; }
    public ReturnReviewService Returns { get; }
    public AgentTaskService AgentTasks { get; }

    private AppServices()
    {
        _ctxFactory = () => OrderDeskContextFactory.Create();
        Customers = new CustomerService(_ctxFactory);
        Catalog = new CatalogService(_ctxFactory);
        Quotes = new QuoteService(_ctxFactory);
        Orders = new OrderService(_ctxFactory);
        Returns = new ReturnReviewService(_ctxFactory);
        AgentTasks = new AgentTaskService(_ctxFactory);
    }
}
