using CustomerPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Infrastructure;

/// <summary>
/// CustomerPortal's OWN DbContext over the shared LegacyRetailDB. Different entity
/// mappings than OrderDesk/FulfillmentAPI. Does not own schema (DbProvisioner does).
/// </summary>
public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options) { }

    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();
    public DbSet<ShopperProfile> Profiles => Set<ShopperProfile>();
    public DbSet<CustomerPreference> Preferences => Set<CustomerPreference>();
    public DbSet<CatalogProduct> Products => Set<CatalogProduct>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<StockLevel> Stock => Set<StockLevel>();
    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();
    public DbSet<ShoppingCartItem> CartItems => Set<ShoppingCartItem>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();
    public DbSet<CustomerOrderLine> OrderLines => Set<CustomerOrderLine>();
    public DbSet<CustomerReturn> Returns => Set<CustomerReturn>();
    public DbSet<CustomerReturnLine> ReturnLines => Set<CustomerReturnLine>();
    public DbSet<TrackedShipment> Shipments => Set<TrackedShipment>();
    public DbSet<TrackingEvent> CarrierEvents => Set<TrackingEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CustomerAccount>(e =>
        {
            e.ToTable("CustomerAccounts");
            e.HasKey(x => x.CustomerAccountId);
            // Portal links its login identity to the shared customer profile row.
            e.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.CustomerId)
                .HasPrincipalKey(p => p.CustomerId);
        });

        b.Entity<ShopperProfile>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(x => x.CustomerId);
        });

        b.Entity<CustomerPreference>(e =>
        {
            e.ToTable("CustomerPreferences");
            e.HasKey(x => x.CustomerPreferenceId);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("ProductCategories");
            e.HasKey(x => x.CategoryId);
        });

        b.Entity<CatalogProduct>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.ProductId);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        });

        b.Entity<StockLevel>(e =>
        {
            e.ToTable("Inventory");
            e.HasKey(x => x.ProductId);
            e.Ignore(x => x.Available);
        });

        b.Entity<ShoppingCart>(e =>
        {
            e.ToTable("ShoppingCarts");
            e.HasKey(x => x.ShoppingCartId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ShoppingCartId);
        });

        b.Entity<ShoppingCartItem>(e =>
        {
            e.ToTable("ShoppingCartItems");
            e.HasKey(x => x.ShoppingCartItemId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        b.Entity<Wishlist>(e =>
        {
            e.ToTable("Wishlists");
            e.HasKey(x => x.WishlistId);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.WishlistId);
        });

        b.Entity<WishlistItem>(e =>
        {
            e.ToTable("WishlistItems");
            e.HasKey(x => x.WishlistItemId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        b.Entity<Coupon>(e =>
        {
            e.ToTable("Coupons");
            e.HasKey(x => x.CouponId);
        });

        b.Entity<CouponRedemption>(e =>
        {
            e.ToTable("CouponRedemptions");
            e.HasKey(x => x.CouponRedemptionId);
        });

        b.Entity<SupportTicket>(e =>
        {
            e.ToTable("SupportTickets");
            e.HasKey(x => x.SupportTicketId);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.SupportTicketId);
        });

        b.Entity<SupportTicketMessage>(e =>
        {
            e.ToTable("SupportTicketMessages");
            e.HasKey(x => x.SupportTicketMessageId);
        });

        b.Entity<CustomerOrder>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(x => x.OrderId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId);
        });

        b.Entity<CustomerOrderLine>(e =>
        {
            e.ToTable("OrderLines");
            e.HasKey(x => x.OrderLineId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        b.Entity<CustomerReturn>(e =>
        {
            e.ToTable("Returns");
            e.HasKey(x => x.ReturnId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ReturnId);
        });

        b.Entity<CustomerReturnLine>(e =>
        {
            e.ToTable("ReturnLines");
            e.HasKey(x => x.ReturnLineId);
        });

        b.Entity<TrackedShipment>(e =>
        {
            e.ToTable("Shipments");
            e.HasKey(x => x.ShipmentId);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.ShipmentId);
        });

        b.Entity<TrackingEvent>(e =>
        {
            e.ToTable("CarrierEvents");
            e.HasKey(x => x.CarrierEventId);
        });
    }
}
