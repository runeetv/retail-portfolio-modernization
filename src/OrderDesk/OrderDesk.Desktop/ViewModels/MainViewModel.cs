using OrderDesk.Desktop.Mvvm;

namespace OrderDesk.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public CustomersViewModel Customers { get; }
    public ProductsViewModel Products { get; }
    public QuotesViewModel Quotes { get; }
    public OrdersViewModel Orders { get; }
    public ApprovalsViewModel Approvals { get; }
    public ReturnsViewModel Returns { get; }
    public TasksViewModel Tasks { get; }

    public MainViewModel()
    {
        var s = AppServices.Current;
        Customers = new CustomersViewModel(s.Customers);
        Products = new ProductsViewModel(s.Catalog);
        Quotes = new QuotesViewModel(s.Quotes, s.Customers, s.AgentTasks, s.Catalog);
        Orders = new OrdersViewModel(s.Orders);
        Approvals = new ApprovalsViewModel(s.Quotes);
        Returns = new ReturnsViewModel(s.Returns);
        Tasks = new TasksViewModel(s.AgentTasks);
    }

    public async Task InitializeAsync()
    {
        await Customers.SearchAsync();
        await Products.LoadAsync();
        await Quotes.RefreshAsync();
        await Orders.RefreshAsync();
        await Approvals.RefreshAsync();
        await Returns.RefreshAsync();
        await Tasks.RefreshAsync();
    }
}
