using System.Collections.ObjectModel;
using OrderDesk.Application;
using OrderDesk.Desktop.Mvvm;
using OrderDesk.Domain;

namespace OrderDesk.Desktop.ViewModels;

public sealed class ProductRow
{
    public int ProductId { get; init; }
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal UnitPrice { get; init; }
    public int Available { get; set; }
}

public sealed class ProductsViewModel : ObservableObject
{
    private readonly CatalogService _catalog;
    private string _searchTerm = "";
    private string _status = "";

    public ObservableCollection<ProductRow> Products { get; } = new();
    public string SearchTerm { get => _searchTerm; set => SetProperty(ref _searchTerm, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public AsyncRelayCommand SearchCommand { get; }

    public ProductsViewModel(CatalogService catalog)
    {
        _catalog = catalog;
        SearchCommand = new AsyncRelayCommand(LoadAsync);
    }

    public async Task LoadAsync()
    {
        var items = await _catalog.BrowseAsync(SearchTerm);
        Products.Clear();
        foreach (var p in items)
        {
            var avail = await _catalog.AvailableQuantityAsync(p.ProductId);
            Products.Add(new ProductRow
            {
                ProductId = p.ProductId,
                Sku = p.Sku,
                Name = p.Name,
                Category = p.Category?.Name ?? "",
                UnitPrice = p.UnitPrice,
                Available = avail
            });
        }
        Status = $"{Products.Count} product(s).";
    }
}
