using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using ToolsLibrary.Data;
using ToolsLibrary.Models;

namespace ToolsLibrary.Tools;

[McpServerToolType]
public class SalesTools(SalesDataStore store, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool]
    [Description("Returns all customers with their Id, Name, Industry, Tier, AccountManager and Email.")]
    public string GetAllCustomers()
    {
        EnsureSalesScope();
        var customers = store.GetAllCustomers().Select(c => new
        {
            c.Id, c.Name, c.Industry,
            Tier = c.Tier.ToString(),
            c.AccountManager, c.Email
        });
        return JsonSerializer.Serialize(customers, _json);
    }

    [McpServerTool]
    [Description("Returns all orders across all customers including status (OnTime, Delayed, InProgress), dates and value.")]
    public string GetAllOrders()
    {
        EnsureSalesScope();
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var orders = store.GetAllOrders().Select(o => MapOrder(o, customers));
        return JsonSerializer.Serialize(orders, _json);
    }

    [McpServerTool]
    [Description("Returns all orders for a specific customer by their customerId.")]
    public string GetCustomerOrders([Description("The customer Id, e.g. 'fabrikam-gmbh'")] string customerId)
    {
        EnsureSalesScope();
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var orders = store.GetOrdersByCustomer(customerId).Select(o => MapOrder(o, customers));
        return JsonSerializer.Serialize(orders, _json);
    }

    [McpServerTool]
    [Description("Returns only delayed orders. Includes how many days late each order is. Optionally filters by customer ID.")]
    public string GetDelayedOrders(
        [Description("Optional customer ID to filter delayed orders for a specific customer.")] string? customerId = null)
    {
        EnsureSalesScope();
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var delayedOrders = store.GetDelayedOrders();
        if (!string.IsNullOrEmpty(customerId))
        {
            delayedOrders = delayedOrders.Where(o => o.CustomerId == customerId).ToList().AsReadOnly();
        }
        var delayed = delayedOrders.Select(o =>
        {
            var mapped = MapOrder(o, customers);
            var daysLate = o.ActualDeliveryDate.HasValue
                ? (int)(o.ActualDeliveryDate.Value - o.PromisedDeliveryDate).TotalDays
                : 0;
            return new
            {
                mapped.Id, mapped.CustomerId, mapped.CustomerName, mapped.ProductName,
                mapped.Status, mapped.OrderDate, mapped.PromisedDeliveryDate,
                mapped.ActualDeliveryDate, mapped.Value,
                DaysLate = daysLate
            };
        });
        return JsonSerializer.Serialize(delayed, _json);
    }

    private void EnsureSalesScope()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (!HasScope(user, "mcp:sales"))
        {
            throw new UnauthorizedAccessException("The mcp:sales scope is required to call sales tools.");
        }
    }

    private static bool HasScope(ClaimsPrincipal? user, string requiredScope)
    {
        if (user is null)
        {
            return false;
        }

        var scopeClaimValues = user
            .FindAll("http://schemas.microsoft.com/identity/claims/scope")
            .Select(c => c.Value)
            .Concat(user.FindAll("scp").Select(c => c.Value));

        return scopeClaimValues
            .SelectMany(v => v.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(s => string.Equals(s, requiredScope, StringComparison.Ordinal));
    }

    private static OrderView MapOrder(Order o, Dictionary<string, string> customerNames) => new(
        o.Id,
        o.CustomerId,
        customerNames.GetValueOrDefault(o.CustomerId, "Unknown"),
        o.ProductName,
        o.Status.ToString(),
        o.OrderDate,
        o.PromisedDeliveryDate,
        o.ActualDeliveryDate,
        o.Value
    );

    private record OrderView(
        string Id,
        string CustomerId,
        string CustomerName,
        string ProductName,
        string Status,
        DateTime OrderDate,
        DateTime PromisedDeliveryDate,
        DateTime? ActualDeliveryDate,
        decimal Value
    );
}
