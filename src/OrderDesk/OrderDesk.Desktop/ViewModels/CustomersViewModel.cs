using System.Collections.ObjectModel;
using System.Windows;
using OrderDesk.Application;
using OrderDesk.Desktop.Mvvm;
using OrderDesk.Domain;

namespace OrderDesk.Desktop.ViewModels;

public sealed class CustomersViewModel : ObservableObject
{
    private readonly CustomerService _service;
    private string _searchTerm = "";
    private Customer? _selected;
    private string _status = "";

    public ObservableCollection<Customer> Customers { get; } = new();

    public string SearchTerm { get => _searchTerm; set => SetProperty(ref _searchTerm, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public Customer? Selected
    {
        get => _selected;
        set { SetProperty(ref _selected, value); OnPropertyChanged(nameof(HasSelection)); }
    }
    public bool HasSelection => _selected is not null;

    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }

    public CustomersViewModel(CustomerService service)
    {
        _service = service;
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasSelection);
    }

    public async Task SearchAsync()
    {
        var list = await _service.SearchAsync(SearchTerm);
        Customers.Clear();
        foreach (var c in list) Customers.Add(c);
        Status = $"{Customers.Count} customer(s).";
    }

    private async Task SaveAsync()
    {
        if (Selected is null) return;
        var result = await _service.UpdateContactAsync(Selected);
        Status = result.Success ? "Saved." : $"Error: {result.Error}";
        if (!result.Success)
            MessageBox.Show(result.Error, "Save failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
