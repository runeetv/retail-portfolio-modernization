using System.Collections.ObjectModel;
using OrderDesk.Application;
using OrderDesk.Desktop.Mvvm;
using OrderDesk.Domain;

namespace OrderDesk.Desktop.ViewModels;

public sealed class ApprovalsViewModel : ObservableObject
{
    private readonly QuoteService _quotes;
    private DiscountApproval? _selected;
    private string _status = "";
    private int _decidingAgentId = 1;
    private string _notes = "";

    public ObservableCollection<DiscountApproval> Pending { get; } = new();
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public int DecidingAgentId { get => _decidingAgentId; set => SetProperty(ref _decidingAgentId, value); }
    public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public DiscountApproval? Selected
    {
        get => _selected;
        set { SetProperty(ref _selected, value); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selected is not null;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }

    public ApprovalsViewModel(QuoteService quotes)
    {
        _quotes = quotes;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApproveCommand = new AsyncRelayCommand(() => DecideAsync(true), () => HasSelection);
        RejectCommand = new AsyncRelayCommand(() => DecideAsync(false), () => HasSelection);
    }

    public async Task RefreshAsync()
    {
        var list = await _quotes.ListPendingApprovalsAsync();
        Pending.Clear();
        foreach (var a in list) Pending.Add(a);
        Status = $"{Pending.Count} pending approval(s).";
    }

    private async Task DecideAsync(bool approve)
    {
        if (Selected is null) return;
        var result = await _quotes.DecideDiscountAsync(Selected.DiscountApprovalId, approve, DecidingAgentId, Notes);
        Status = result.Success ? (approve ? "Approved." : "Rejected.") : $"Error: {result.Error}";
        await RefreshAsync();
    }
}
