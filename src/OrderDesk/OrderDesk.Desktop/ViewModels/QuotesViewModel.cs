using System.Collections.ObjectModel;
using System.Windows;
using OrderDesk.Application;
using OrderDesk.Desktop.Mvvm;
using OrderDesk.Domain;

namespace OrderDesk.Desktop.ViewModels;

public sealed class QuotesViewModel : ObservableObject
{
    private readonly QuoteService _quotes;
    private readonly CustomerService _customers;
    private readonly AgentTaskService _agents;
    private readonly CatalogService _catalog;

    private SalesQuote? _selected;
    private string _status = "";
    private int _newCustomerId;
    private int _newAgentId;
    private int _lineProductId;
    private int _lineQuantity = 1;
    private decimal _discountPercent;

    public ObservableCollection<SalesQuote> Quotes { get; } = new();
    public ObservableCollection<SalesAgent> Agents { get; } = new();
    public SalesQuote? SelectedDetail { get; private set; }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public int NewCustomerId { get => _newCustomerId; set => SetProperty(ref _newCustomerId, value); }
    public int NewAgentId { get => _newAgentId; set => SetProperty(ref _newAgentId, value); }
    public int LineProductId { get => _lineProductId; set => SetProperty(ref _lineProductId, value); }
    public int LineQuantity { get => _lineQuantity; set => SetProperty(ref _lineQuantity, value); }
    public decimal DiscountPercent { get => _discountPercent; set => SetProperty(ref _discountPercent, value); }

    public SalesQuote? Selected
    {
        get => _selected;
        set { SetProperty(ref _selected, value); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selected is not null;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand CreateCommand { get; }
    public AsyncRelayCommand AddLineCommand { get; }
    public AsyncRelayCommand ApplyDiscountCommand { get; }
    public AsyncRelayCommand ConvertCommand { get; }

    public QuotesViewModel(QuoteService quotes, CustomerService customers, AgentTaskService agents, CatalogService catalog)
    {
        _quotes = quotes; _customers = customers; _agents = agents; _catalog = catalog;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateCommand = new AsyncRelayCommand(CreateAsync);
        AddLineCommand = new AsyncRelayCommand(AddLineAsync, () => HasSelection);
        ApplyDiscountCommand = new AsyncRelayCommand(ApplyDiscountAsync, () => HasSelection);
        ConvertCommand = new AsyncRelayCommand(ConvertAsync, () => HasSelection);
    }

    public async Task RefreshAsync()
    {
        var list = await _quotes.ListAsync();
        Quotes.Clear();
        foreach (var q in list) Quotes.Add(q);
        if (Agents.Count == 0)
            foreach (var a in await _agents.ListAgentsAsync()) Agents.Add(a);
        Status = $"{Quotes.Count} quote(s).";
    }

    private async Task CreateAsync()
    {
        if (NewCustomerId <= 0 || NewAgentId <= 0)
        {
            Status = "Enter a valid Customer Id and select an Agent.";
            return;
        }
        var result = await _quotes.CreateQuoteAsync(NewCustomerId, NewAgentId);
        if (!result.Success) { Status = $"Error: {result.Error}"; return; }
        Status = $"Created quote {result.Value!.QuoteNumber}.";
        await RefreshAsync();
        Selected = Quotes.FirstOrDefault(q => q.SalesQuoteId == result.Value!.SalesQuoteId);
    }

    private async Task AddLineAsync()
    {
        if (Selected is null) return;
        var result = await _quotes.AddLineAsync(Selected.SalesQuoteId, LineProductId, LineQuantity);
        Status = result.Success ? "Line added." : $"Error: {result.Error}";
        await RefreshAsync();
    }

    private async Task ApplyDiscountAsync()
    {
        if (Selected is null) return;
        var result = await _quotes.ApplyDiscountAsync(Selected.SalesQuoteId, DiscountPercent);
        Status = result.Success
            ? (DiscountPercent > OrderDeskRules.DiscountApprovalThresholdPercent
                ? "Discount applied - pending approval." : "Discount applied - quote approved.")
            : $"Error: {result.Error}";
        await RefreshAsync();
    }

    private async Task ConvertAsync()
    {
        if (Selected is null) return;
        var result = await _quotes.ConvertToOrderAsync(Selected.SalesQuoteId);
        if (result.Success)
        {
            Status = $"Converted to Order #{result.Value}.";
            MessageBox.Show($"Quote converted to Order #{result.Value}.", "OrderDesk", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else Status = $"Error: {result.Error}";
        await RefreshAsync();
    }
}
