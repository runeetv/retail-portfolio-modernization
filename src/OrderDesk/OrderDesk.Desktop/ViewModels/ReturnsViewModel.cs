using System.Collections.ObjectModel;
using OrderDesk.Application;
using OrderDesk.Desktop.Mvvm;
using OrderDesk.Domain;

namespace OrderDesk.Desktop.ViewModels;

public sealed class ReturnsViewModel : ObservableObject
{
    private readonly ReturnReviewService _returns;
    private ReturnRequest? _selected;
    private string _status = "";
    private int _agentId = 1;

    public ObservableCollection<ReturnRequest> Returns { get; } = new();
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public int AgentId { get => _agentId; set => SetProperty(ref _agentId, value); }

    public ReturnRequest? Selected
    {
        get => _selected;
        set { SetProperty(ref _selected, value); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selected is not null;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand RejectCommand { get; }

    public ReturnsViewModel(ReturnReviewService returns)
    {
        _returns = returns;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        ApproveCommand = new AsyncRelayCommand(() => ReviewAsync(true), () => HasSelection);
        RejectCommand = new AsyncRelayCommand(() => ReviewAsync(false), () => HasSelection);
    }

    public async Task RefreshAsync()
    {
        var list = await _returns.ListAsync();
        Returns.Clear();
        foreach (var r in list) Returns.Add(r);
        Status = $"{Returns.Count} return(s).";
    }

    private async Task ReviewAsync(bool approve)
    {
        if (Selected is null) return;
        var result = await _returns.ReviewAsync(Selected.ReturnId, approve, AgentId);
        Status = result.Success ? (approve ? "Return approved." : "Return rejected.") : $"Error: {result.Error}";
        await RefreshAsync();
    }
}
