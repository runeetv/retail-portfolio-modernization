using System.Collections.ObjectModel;
using System.Windows;
using OrderDesk.Application;
using OrderDesk.Desktop.Mvvm;
using OrderDesk.Domain;

namespace OrderDesk.Desktop.ViewModels;

public sealed class OrdersViewModel : ObservableObject
{
    private readonly OrderService _orders;
    private Order? _selected;
    private string _status = "";
    private string _filterStatus = "";
    private string _cancelReason = "";
    private int _actingAgentId = 1;
    private bool _forceOverride;
    private string _overrideJustification = "";

    public ObservableCollection<Order> Orders { get; } = new();
    public ObservableCollection<OrderLine> SelectedLines { get; } = new();
    public ObservableCollection<ShipmentStatusView> Shipments { get; } = new();

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string FilterStatus { get => _filterStatus; set => SetProperty(ref _filterStatus, value); }
    public string CancelReason { get => _cancelReason; set => SetProperty(ref _cancelReason, value); }
    public int ActingAgentId { get => _actingAgentId; set => SetProperty(ref _actingAgentId, value); }
    public bool ForceOverride { get => _forceOverride; set => SetProperty(ref _forceOverride, value); }
    public string OverrideJustification { get => _overrideJustification; set => SetProperty(ref _overrideJustification, value); }

    public Order? Selected
    {
        get => _selected;
        set { SetProperty(ref _selected, value); OnPropertyChanged(nameof(HasSelection)); _ = LoadDetailAsync(); }
    }
    public bool HasSelection => _selected is not null;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public OrdersViewModel(OrderService orders)
    {
        _orders = orders;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => HasSelection);
    }

    public async Task RefreshAsync()
    {
        var list = await _orders.ListAsync(string.IsNullOrWhiteSpace(FilterStatus) ? null : FilterStatus);
        Orders.Clear();
        foreach (var o in list) Orders.Add(o);
        Status = $"{Orders.Count} order(s).";
    }

    private async Task LoadDetailAsync()
    {
        SelectedLines.Clear();
        Shipments.Clear();
        if (Selected is null) return;
        var full = await _orders.GetAsync(Selected.OrderId);
        if (full is not null)
            foreach (var l in full.Lines) SelectedLines.Add(l);
        foreach (var s in await _orders.GetShipmentsAsync(Selected.OrderId)) Shipments.Add(s);
    }

    private async Task CancelAsync()
    {
        if (Selected is null) return;
        var (eligible, reason) = await _orders.CheckCancellationEligibilityAsync(Selected.OrderId);
        if (!eligible && !ForceOverride)
        {
            Status = $"Not eligible: {reason}. Enable override + justification to force (authorized agents only).";
            return;
        }
        var result = await _orders.CancelOrderAsync(Selected.OrderId, ActingAgentId, CancelReason,
            forceOverride: ForceOverride, overrideJustification: OverrideJustification);
        Status = result.Success ? "Order cancelled." : $"Error: {result.Error}";
        if (!result.Success)
            MessageBox.Show(result.Error, "Cancellation failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        await RefreshAsync();
    }
}
