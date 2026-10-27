using Microsoft.EntityFrameworkCore;
using OrderDesk.Domain;

namespace OrderDesk.Infrastructure;

/// <summary>
/// OrderDesk's OWN DbContext. It maps only the tables/columns OrderDesk needs
/// onto the shared LegacyRetailDB. It does NOT own schema (no migrations run at
/// runtime); the DbProvisioner owns schema creation. Other apps have their own
/// contexts with different mappings over the same physical tables.
/// </summary>
public class OrderDeskDbContext : DbContext
{
    public OrderDeskDbContext(DbContextOptions<OrderDeskDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<SalesAgent> SalesAgents => Set<SalesAgent>();
    public DbSet<SalesQuote> SalesQuotes => Set<SalesQuote>();
    public DbSet<SalesQuoteLine> SalesQuoteLines => Set<SalesQuoteLine>();
    public DbSet<DiscountApproval> DiscountApprovals => Set<DiscountApproval>();
    public DbSet<SalesCommission> SalesCommissions => Set<SalesCommission>();
    public DbSet<SalesAgentTask> SalesAgentTasks => Set<SalesAgentTask>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<CancellationOverride> CancellationOverrides => Set<CancellationOverride>();
    public DbSet<ReturnRequest> Returns => Set<ReturnRequest>();
    public DbSet<ShipmentStatusView> Shipments => Set<ShipmentStatusView>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(x => x.CustomerId);
            e.Ignore(x => x.FullName);
            e.Ignore(x => x.HasMandatoryOrderInfo);
        });

        b.Entity<ProductCategory>(e =>
        {
            e.ToTable("ProductCategories");
            e.HasKey(x => x.CategoryId);
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.ProductId);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.ToTable("Inventory");
            e.HasKey(x => x.ProductId);
            e.Ignore(x => x.QuantityAvailable);
        });

        b.Entity<SalesAgent>(e =>
        {
            e.ToTable("SalesAgents");
            e.HasKey(x => x.SalesAgentId);
        });

        b.Entity<SalesQuote>(e =>
        {
            e.ToTable("SalesQuotes");
            e.HasKey(x => x.SalesQuoteId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.SalesAgent).WithMany().HasForeignKey(x => x.SalesAgentId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SalesQuoteId);
        });

        b.Entity<SalesQuoteLine>(e =>
        {
            e.ToTable("SalesQuoteLines");
            e.HasKey(x => x.SalesQuoteLineId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            e.Ignore(x => x.LineTotal);
        });

        b.Entity<DiscountApproval>(e =>
        {
            e.ToTable("DiscountApprovals");
            e.HasKey(x => x.DiscountApprovalId);
            e.HasOne(x => x.SalesQuote).WithMany().HasForeignKey(x => x.SalesQuoteId);
        });

        b.Entity<SalesCommission>(e =>
        {
            e.ToTable("SalesCommissions");
            e.HasKey(x => x.SalesCommissionId);
        });

        b.Entity<SalesAgentTask>(e =>
        {
            e.ToTable("SalesAgentTasks");
            e.HasKey(x => x.SalesAgentTaskId);
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(x => x.OrderId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId);
        });

        b.Entity<OrderLine>(e =>
        {
            e.ToTable("OrderLines");
            e.HasKey(x => x.OrderLineId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        b.Entity<CancellationOverride>(e =>
        {
            e.ToTable("CancellationOverrides");
            e.HasKey(x => x.CancellationOverrideId);
        });

        b.Entity<ReturnRequest>(e =>
        {
            e.ToTable("Returns");
            e.HasKey(x => x.ReturnId);
        });

        b.Entity<ShipmentStatusView>(e =>
        {
            e.ToTable("Shipments");
            e.HasKey(x => x.ShipmentId);
        });
    }
}
