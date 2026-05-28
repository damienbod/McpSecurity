using ToolsLibrary.Models;

namespace ToolsLibrary.Data;

public class SalesDataStore
{
    private readonly List<Customer> _customers;
    private readonly List<Order> _orders;

    public SalesDataStore()
    {
        _customers = SeedCustomers();
        _orders = SeedOrders();
    }

    public IReadOnlyList<Customer> GetAllCustomers() => _customers.AsReadOnly();

    public IReadOnlyList<Order> GetAllOrders() => _orders.AsReadOnly();

    public IReadOnlyList<Order> GetOrdersByCustomer(string customerId) =>
        _orders.Where(o => o.CustomerId == customerId).ToList().AsReadOnly();

    public IReadOnlyList<Order> GetDelayedOrders() =>
        _orders.Where(o => o.Status == OrderStatus.Delayed).ToList().AsReadOnly();

    public Customer? FindCustomer(string customerId) =>
        _customers.FirstOrDefault(c => c.Id == customerId);

    // -------------------------------------------------------------------------
    // Seed data
    // -------------------------------------------------------------------------

    private static List<Customer> SeedCustomers() =>
    [
        new() { Id = "contoso-ag",           Name = "Contoso AG",            Industry = "Manufacturing", Tier = CustomerTier.A, AccountManager = "Anna Bauer",    Email = "orders@contoso.ch" },
        new() { Id = "fabrikam-gmbh",        Name = "Fabrikam GmbH",         Industry = "Retail",        Tier = CustomerTier.A, AccountManager = "Marco Sutter",  Email = "orders@fabrikam.de" },
        new() { Id = "alpine-bikes",         Name = "Alpine Bikes",          Industry = "Sports",        Tier = CustomerTier.B, AccountManager = "Sandra Klein",  Email = "orders@alpinebikes.ch" },
        new() { Id = "helvetic-pharma",      Name = "Helvetic Pharma",       Industry = "Pharma",        Tier = CustomerTier.A, AccountManager = "David Meier",   Email = "orders@helveticpharma.ch" },
        new() { Id = "swisstech-solutions",  Name = "SwissTech Solutions",   Industry = "IT",            Tier = CustomerTier.B, AccountManager = "Lisa Vogel",    Email = "orders@swisstech.ch" },
        new() { Id = "local-shop-basel",     Name = "Local Shop Basel",      Industry = "Retail",        Tier = CustomerTier.C, AccountManager = "Hans Müller",   Email = "orders@localshop.ch" },
    ];

    private static List<Order> SeedOrders()
    {
        // Reference date: treat relative to a fixed anchor so data stays stable
        var anchor = new DateTime(2025, 5, 1);

        return
        [
            // ── Contoso AG – all OnTime ──────────────────────────────────────
            new()
            {
                Id = "ORD-001", CustomerId = "contoso-ag", ProductName = "Industrial Sensors Batch A",
                OrderDate = anchor.AddDays(-60), PromisedDeliveryDate = anchor.AddDays(-30),
                ActualDeliveryDate = anchor.AddDays(-32), Status = OrderStatus.OnTime, Value = 18_500m
            },
            new()
            {
                Id = "ORD-002", CustomerId = "contoso-ag", ProductName = "Control Units v3",
                OrderDate = anchor.AddDays(-45), PromisedDeliveryDate = anchor.AddDays(-15),
                ActualDeliveryDate = anchor.AddDays(-16), Status = OrderStatus.OnTime, Value = 24_000m
            },
            new()
            {
                Id = "ORD-003", CustomerId = "contoso-ag", ProductName = "Assembly Tools Kit",
                OrderDate = anchor.AddDays(-20), PromisedDeliveryDate = anchor.AddDays(10),
                ActualDeliveryDate = anchor.AddDays(9),  Status = OrderStatus.OnTime, Value = 9_200m
            },

            // ── Fabrikam GmbH – all Delayed 5-10 days, high value ────────────
            new()
            {
                Id = "ORD-004", CustomerId = "fabrikam-gmbh", ProductName = "Retail Display Units",
                OrderDate = anchor.AddDays(-90), PromisedDeliveryDate = anchor.AddDays(-50),
                ActualDeliveryDate = anchor.AddDays(-44), Status = OrderStatus.Delayed, Value = 42_000m
            },
            new()
            {
                Id = "ORD-005", CustomerId = "fabrikam-gmbh", ProductName = "POS Terminal Software Licenses",
                OrderDate = anchor.AddDays(-60), PromisedDeliveryDate = anchor.AddDays(-20),
                ActualDeliveryDate = anchor.AddDays(-12), Status = OrderStatus.Delayed, Value = 31_500m
            },
            new()
            {
                Id = "ORD-006", CustomerId = "fabrikam-gmbh", ProductName = "Warehouse Management System",
                OrderDate = anchor.AddDays(-35), PromisedDeliveryDate = anchor.AddDays(5),
                ActualDeliveryDate = anchor.AddDays(13), Status = OrderStatus.Delayed, Value = 67_000m
            },

            // ── Helvetic Pharma – 1 heavily delayed, 1 OnTime ───────────────
            new()
            {
                Id = "ORD-007", CustomerId = "helvetic-pharma", ProductName = "Lab Equipment Set",
                OrderDate = anchor.AddDays(-80), PromisedDeliveryDate = anchor.AddDays(-40),
                ActualDeliveryDate = anchor.AddDays(-25), Status = OrderStatus.Delayed, Value = 55_000m
            },
            new()
            {
                Id = "ORD-008", CustomerId = "helvetic-pharma", ProductName = "Compliance Documentation Package",
                OrderDate = anchor.AddDays(-30), PromisedDeliveryDate = anchor.AddDays(-5),
                ActualDeliveryDate = anchor.AddDays(-6),  Status = OrderStatus.OnTime,  Value = 8_000m
            },

            // ── SwissTech Solutions – 2 delayed 2 days, 1 InProgress ─────────
            new()
            {
                Id = "ORD-009", CustomerId = "swisstech-solutions", ProductName = "Cloud Infrastructure Setup",
                OrderDate = anchor.AddDays(-50), PromisedDeliveryDate = anchor.AddDays(-20),
                ActualDeliveryDate = anchor.AddDays(-18), Status = OrderStatus.Delayed, Value = 14_000m
            },
            new()
            {
                Id = "ORD-010", CustomerId = "swisstech-solutions", ProductName = "Security Audit Report",
                OrderDate = anchor.AddDays(-40), PromisedDeliveryDate = anchor.AddDays(-10),
                ActualDeliveryDate = anchor.AddDays(-8),  Status = OrderStatus.Delayed, Value = 11_000m
            },
            new()
            {
                Id = "ORD-011", CustomerId = "swisstech-solutions", ProductName = "DevOps Pipeline Implementation",
                OrderDate = anchor.AddDays(-15), PromisedDeliveryDate = anchor.AddDays(20),
                ActualDeliveryDate = null, Status = OrderStatus.InProgress, Value = 22_000m
            },

            // ── Alpine Bikes – 1 OnTime ──────────────────────────────────────
            new()
            {
                Id = "ORD-012", CustomerId = "alpine-bikes", ProductName = "E-Bike Firmware Update",
                OrderDate = anchor.AddDays(-25), PromisedDeliveryDate = anchor.AddDays(5),
                ActualDeliveryDate = anchor.AddDays(4),  Status = OrderStatus.OnTime, Value = 6_500m
            },

            // ── Local Shop Basel – 1 Delayed, low value ─────────────────────
            new()
            {
                Id = "ORD-013", CustomerId = "local-shop-basel", ProductName = "Point-of-Sale Labels",
                OrderDate = anchor.AddDays(-20), PromisedDeliveryDate = anchor.AddDays(-5),
                ActualDeliveryDate = anchor.AddDays(-1),  Status = OrderStatus.Delayed, Value = 850m
            },
        ];
    }
}
