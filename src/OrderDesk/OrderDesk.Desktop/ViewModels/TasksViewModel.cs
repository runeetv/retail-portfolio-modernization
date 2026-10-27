using System.Collections.ObjectModel;
using OrderDesk.Application;
using OrderDesk.Desktop.Mvvm;
using OrderDesk.Domain;

namespace OrderDesk.Desktop.ViewModels;

public sealed class TasksViewModel : ObservableObject
{
    private readonly AgentTaskService _service;
    private SalesAgent? _selectedAgent;
    private SalesAgentTask? _selectedTask;
    private string _status = "";

    public ObservableCollection<SalesAgent> Agents { get; } = new();
    public ObservableCollection<SalesAgentTask> Tasks { get; } = new();
    public ObservableCollection<SalesCommission> Commissions { get; } = new();

    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public SalesAgent? SelectedAgent
    {
        get => _selectedAgent;
        set { SetProperty(ref _selectedAgent, value); _ = LoadForAgentAsync(); }
    }

    public SalesAgentTask? SelectedTask
    {
        get => _selectedTask;
        set { SetProperty(ref _selectedTask, value); OnPropertyChanged(nameof(HasTask)); }
    }
    public bool HasTask => _selectedTask is not null;

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand MarkInProgressCommand { get; }
    public AsyncRelayCommand MarkDoneCommand { get; }

    public TasksViewModel(AgentTaskService service)
    {
        _service = service;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        MarkInProgressCommand = new AsyncRelayCommand(() => SetStatusAsync("InProgress"), () => HasTask);
        MarkDoneCommand = new AsyncRelayCommand(() => SetStatusAsync("Done"), () => HasTask);
    }

    public async Task RefreshAsync()
    {
        var agents = await _service.ListAgentsAsync();
        Agents.Clear();
        foreach (var a in agents) Agents.Add(a);
        SelectedAgent = Agents.FirstOrDefault();
    }

    private async Task LoadForAgentAsync()
    {
        Tasks.Clear();
        Commissions.Clear();
        if (SelectedAgent is null) return;
        foreach (var t in await _service.ListTasksAsync(SelectedAgent.SalesAgentId)) Tasks.Add(t);
        foreach (var c in await _service.ListCommissionsAsync(SelectedAgent.SalesAgentId)) Commissions.Add(c);
        Status = $"{Tasks.Count} task(s), {Commissions.Count} commission(s) for {SelectedAgent.FullName}.";
    }

    private async Task SetStatusAsync(string status)
    {
        if (SelectedTask is null) return;
        var result = await _service.UpdateStatusAsync(SelectedTask.SalesAgentTaskId, status);
        Status = result.Success ? $"Task set to {status}." : $"Error: {result.Error}";
        await LoadForAgentAsync();
    }
}
