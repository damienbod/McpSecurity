using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using ToolsLibrary.Data;
using ToolsLibrary.Models;

namespace ToolsLibrary.Tools;

public sealed class SalesToolsOptions
{
    public bool RequireSalesScope { get; set; } = true;
}

[McpServerToolType]
public class SalesTools(
    SalesDataStore store,
    IHttpContextAccessor httpContextAccessor,
    SalesToolsOptions? options = null)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(ReadOnly = true)]
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

    [McpServerTool(ReadOnly = true)]
    [Description("Returns all orders across all customers including status (OnTime, Delayed, InProgress), dates and value.")]
    public string GetAllOrders()
    {
        EnsureSalesScope();
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var orders = store.GetAllOrders().Select(o => MapOrder(o, customers));
        return JsonSerializer.Serialize(orders, _json);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Returns all orders for a specific customer by their customerId.")]
    public string GetCustomerOrders([Description("The customer Id, e.g. 'fabrikam-gmbh'")] string customerId)
    {
        EnsureSalesScope();
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var orders = store.GetOrdersByCustomer(customerId).Select(o => MapOrder(o, customers));
        return JsonSerializer.Serialize(orders, _json);
    }

    [McpServerTool(ReadOnly = true)]
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
                : Math.Max(0, (int)(DateTime.UtcNow.Date - o.PromisedDeliveryDate.Date).TotalDays);
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

    [McpServerTool(ReadOnly = true)]
    [Description("Search customers by partial name, industry or account manager, and/or exact tier (A, B or C). Filters are case-insensitive and combined.")]
    public string SearchCustomers(
        [Description("Optional partial customer name.")] string? name = null,
        [Description("Optional partial industry.")] string? industry = null,
        [Description("Optional customer tier: A, B or C.")] string? tier = null,
        [Description("Optional partial account manager name.")] string? accountManager = null)
    {
        EnsureSalesScope();
        if (!string.IsNullOrWhiteSpace(tier) && !Enum.GetNames<CustomerTier>().Contains(tier, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Tier must be A, B or C.", nameof(tier));
        var customers = store.GetAllCustomers().Where(c =>
            Contains(c.Name, name) && Contains(c.Industry, industry) &&
            Contains(c.AccountManager, accountManager) &&
            (string.IsNullOrWhiteSpace(tier) || c.Tier.ToString().Equals(tier, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new { c.Id, c.Name, c.Industry, Tier = c.Tier.ToString(), c.AccountManager, c.Email });
        return JsonSerializer.Serialize(customers, _json);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Search orders by customer ID, status, inclusive order-date range and/or value range. Dates use YYYY-MM-DD and values are in CHF.")]
    public string SearchOrders(
        [Description("Optional exact customer ID.")] string? customerId = null,
        [Description("Optional status: OnTime, Delayed or InProgress.")] string? status = null,
        [Description("Optional inclusive first order date (YYYY-MM-DD).")] DateTime? fromDate = null,
        [Description("Optional inclusive last order date (YYYY-MM-DD).")] DateTime? toDate = null,
        [Description("Optional inclusive minimum order value in CHF.")] decimal? minValue = null,
        [Description("Optional inclusive maximum order value in CHF.")] decimal? maxValue = null)
    {
        EnsureSalesScope();
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            throw new ArgumentException("The first order date must not be after the last order date.");
        if (minValue < 0 || maxValue < 0 || (minValue.HasValue && maxValue.HasValue && minValue > maxValue))
            throw new ArgumentException("Order values must be non-negative and the minimum must not exceed the maximum.");
        if (!string.IsNullOrWhiteSpace(customerId) && store.FindCustomer(customerId) is null)
            throw new ArgumentException($"Customer '{customerId}' was not found.", nameof(customerId));
        OrderStatus? parsedStatus = string.IsNullOrWhiteSpace(status) ? null : ParseStatus(status);
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var orders = store.GetAllOrders().Where(o =>
            (string.IsNullOrWhiteSpace(customerId) || o.CustomerId == customerId) &&
            (!parsedStatus.HasValue || o.Status == parsedStatus) &&
            (!fromDate.HasValue || o.OrderDate.Date >= fromDate.Value.Date) &&
            (!toDate.HasValue || o.OrderDate.Date <= toDate.Value.Date) &&
            (!minValue.HasValue || o.Value >= minValue) &&
            (!maxValue.HasValue || o.Value <= maxValue))
            .Select(o => MapOrder(o, customers));
        return JsonSerializer.Serialize(orders, _json);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Summarize a customer's order value, status counts and average days late for delivered delayed orders. Undelivered delayed orders are counted separately.")]
    public string GetCustomerOverview([Description("Exact customer ID. Use search_customers to find it.")] string customerId)
    {
        EnsureSalesScope();
        var customer = store.FindCustomer(customerId)
            ?? throw new ArgumentException($"Customer '{customerId}' was not found.", nameof(customerId));
        var orders = store.GetOrdersByCustomer(customerId);
        var deliveredLate = orders.Where(o => o.Status == OrderStatus.Delayed && o.ActualDeliveryDate.HasValue).ToList();
        return JsonSerializer.Serialize(new
        {
            customer.Id, customer.Name, customer.Industry, Tier = customer.Tier.ToString(), customer.AccountManager,
            OrderCount = orders.Count,
            TotalValue = orders.Sum(o => o.Value),
            OnTimeCount = orders.Count(o => o.Status == OrderStatus.OnTime),
            DelayedCount = orders.Count(o => o.Status == OrderStatus.Delayed),
            InProgressCount = orders.Count(o => o.Status == OrderStatus.InProgress),
            UndeliveredDelayedCount = orders.Count(o => o.Status == OrderStatus.Delayed && !o.ActualDeliveryDate.HasValue),
            AverageDeliveredDelayDays = deliveredLate.Count == 0 ? (double?)null :
                deliveredLate.Average(o => (o.ActualDeliveryDate!.Value.Date - o.PromisedDeliveryDate.Date).TotalDays)
        }, _json);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("List the simulated follow-up notes for a customer by exact customer ID.")]
    public string GetCustomerFollowUpNotes([Description("Exact customer ID.")] string customerId)
    {
        EnsureSalesScope();
        return JsonSerializer.Serialize(store.GetNotes(customerId), _json);
    }

    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("Change a demo order's status. OnTime requires an actual delivery date on or before the promised date; Delayed accepts a late actual date or no date; InProgress requires no date. This changes shared in-memory demo data.")]
    public string UpdateOrderStatus(
        [Description("Exact order ID, for example ORD-011.")] string orderId,
        [Description("New status: OnTime, Delayed or InProgress.")] string status,
        [Description("Actual delivery date as YYYY-MM-DD, or omit for an undelivered order.")] DateTime? actualDeliveryDate = null)
    {
        EnsureSalesScope();
        var order = store.UpdateOrderStatus(orderId, ParseStatus(status), actualDeliveryDate);
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        return JsonSerializer.Serialize(MapOrder(order, customers), _json);
    }

    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Add a simulated follow-up note to a customer. This changes shared in-memory demo data; no message is sent.")]
    public string AddCustomerFollowUpNote(
        [Description("Exact customer ID.")] string customerId,
        [Description("Note text, between 1 and 1000 characters.")] string text)
    {
        EnsureSalesScope();
        return JsonSerializer.Serialize(store.AddNote(customerId, text), _json);
    }

    private static bool Contains(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static OrderStatus ParseStatus(string status)
    {
        if (!Enum.GetNames<OrderStatus>().Contains(status, StringComparer.OrdinalIgnoreCase) ||
            !Enum.TryParse<OrderStatus>(status, true, out var result))
            throw new ArgumentException("Status must be OnTime, Delayed or InProgress.", nameof(status));
        return result;
    }

    private void EnsureSalesScope()
    {
        if (options is { RequireSalesScope: false })
        {
            return;
        }

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
