namespace ToolsLibrary.Models;

public enum OrderStatus { OnTime, Delayed, InProgress }

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime PromisedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Value { get; set; }
}
