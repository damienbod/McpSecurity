using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ToolsLibrary.Data;
using ToolsLibrary.Models;

namespace ToolsLibrary.Tools;

[McpServerToolType]
public class SalesTools
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool]
    [Description("Returns all customers with their Id, Name, Industry, Tier, AccountManager and Email.")]
    public string GetAllCustomers(SalesDataStore store)
    {
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
    public string GetAllOrders(SalesDataStore store)
    {
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var orders = store.GetAllOrders().Select(o => MapOrder(o, customers));
        return JsonSerializer.Serialize(orders, _json);
    }

    [McpServerTool]
    [Description("Returns all orders for a specific customer by their customerId.")]
    public string GetCustomerOrders(SalesDataStore store, [Description("The customer Id, e.g. 'fabrikam-gmbh'")] string customerId)
    {
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var orders = store.GetOrdersByCustomer(customerId).Select(o => MapOrder(o, customers));
        return JsonSerializer.Serialize(orders, _json);
    }

    [McpServerTool]
    [Description("Returns only delayed orders. Includes how many days late each order is.")]
    public string GetDelayedOrders(SalesDataStore store)
    {
        var customers = store.GetAllCustomers().ToDictionary(c => c.Id, c => c.Name);
        var delayed = store.GetDelayedOrders().Select(o =>
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
