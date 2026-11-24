using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ToolsLibrary.Data;
using ToolsLibrary.Models;
using ToolsLibrary.Tools;

namespace SalesDemo.Tests;

public class SalesToolsTests
{
    private readonly SalesDataStore _store = new();
    private SalesTools Tools => new(_store, new HttpContextAccessor(), new SalesToolsOptions { RequireSalesScope = false });

    [Fact]
    public void SearchCombinesFiltersAndIncludesBounds()
    {
        using var customers = JsonDocument.Parse(Tools.SearchCustomers(industry: "ret", tier: "a"));
        Assert.Equal("fabrikam-gmbh", Assert.Single(customers.RootElement.EnumerateArray()).GetProperty("id").GetString());
        using var orders = JsonDocument.Parse(Tools.SearchOrders(
            customerId: "fabrikam-gmbh", fromDate: new DateTime(2025, 3, 2),
            toDate: new DateTime(2025, 3, 2), minValue: 31500, maxValue: 31500));
        Assert.Equal("ORD-005", Assert.Single(orders.RootElement.EnumerateArray()).GetProperty("id").GetString());
        Assert.Throws<ArgumentException>(() => Tools.SearchOrders(minValue: 10, maxValue: 1));
        Assert.Throws<ArgumentException>(() => Tools.SearchCustomers(tier: "unknown"));
    }

    [Fact]
    public void OverviewReportsDeliveredAndUndeliveredDelaysSeparately()
    {
        using var overview = JsonDocument.Parse(Tools.GetCustomerOverview("fabrikam-gmbh"));
        Assert.Equal(140500, overview.RootElement.GetProperty("totalValue").GetDecimal());
        Assert.Equal(3, overview.RootElement.GetProperty("delayedCount").GetInt32());
        Assert.Equal(22.0 / 3, overview.RootElement.GetProperty("averageDeliveredDelayDays").GetDouble(), 8);
        Tools.UpdateOrderStatus("ORD-006", "Delayed");
        using var updated = JsonDocument.Parse(Tools.GetCustomerOverview("fabrikam-gmbh"));
        Assert.Equal(1, updated.RootElement.GetProperty("undeliveredDelayedCount").GetInt32());
        Assert.Equal(7, updated.RootElement.GetProperty("averageDeliveredDelayDays").GetDouble());
    }

    [Fact]
    public void ChangesAreValidatedSharedAndSnapshotsCannotMutateStore()
    {
        Assert.Throws<ArgumentException>(() => Tools.UpdateOrderStatus("ORD-011", "OnTime"));
        Assert.Throws<ArgumentException>(() => Tools.UpdateOrderStatus("ORD-011", "Delayed", new DateTime(2025, 5, 1)));
        Assert.Throws<ArgumentException>(() => Tools.UpdateOrderStatus("ORD-011", "3"));
        Assert.Throws<ArgumentException>(() => Tools.AddCustomerFollowUpNote("unknown", "Call"));
        Assert.Throws<ArgumentException>(() => Tools.AddCustomerFollowUpNote("fabrikam-gmbh", "  "));
        Tools.UpdateOrderStatus("ORD-011", "Delayed");
        Assert.Equal(OrderStatus.Delayed, _store.GetAllOrders().Single(o => o.Id == "ORD-011").Status);
        var snapshot = _store.GetAllOrders().Single(o => o.Id == "ORD-011");
        snapshot.Status = OrderStatus.OnTime;
        Assert.Equal(OrderStatus.Delayed, _store.GetAllOrders().Single(o => o.Id == "ORD-011").Status);
        Parallel.For(0, 50, i => _store.AddNote("fabrikam-gmbh", $"Note {i}"));
        Assert.Equal(50, _store.GetNotes("fabrikam-gmbh").Count);
        Assert.Empty(new SalesDataStore().GetNotes("fabrikam-gmbh"));
    }

    [Fact]
    public void SecureToolsRequireSalesScopeForReadsAndWrites()
    {
        var context = new DefaultHttpContext();
        var tools = new SalesTools(_store, new HttpContextAccessor { HttpContext = context });
        Assert.Throws<UnauthorizedAccessException>(() => tools.SearchCustomers());
        Assert.Throws<UnauthorizedAccessException>(() => tools.AddCustomerFollowUpNote("fabrikam-gmbh", "Call"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scp", "mcp:demo mcp:sales")]));
        Assert.NotEmpty(tools.SearchCustomers());
        Assert.NotEmpty(tools.AddCustomerFollowUpNote("fabrikam-gmbh", "Call"));
    }
}
