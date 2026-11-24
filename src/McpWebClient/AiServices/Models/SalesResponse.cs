namespace McpWebClient.AiServices.Models;

public record SalesCustomerView(
    string Id,
    string Name,
    string Industry,
    string Tier,
    string AccountManager
);

public record SalesOrderView(
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

public record SalesChatMessage(string Role, string Content);

public record McpToolCall(string ToolName, string? Arguments, bool? Success);

public record SalesApproval(string Id, string ToolName, string Arguments);

public record SalesNoteView(string Id, string CustomerId, string Text, DateTime CreatedAtUtc);

public record SalesNotesView(
    List<SalesCustomerView> Customers,
    string? SelectedCustomerId,
    List<SalesNoteView> Notes);

public record SalesResponse(
    string? FinalAnswer,
    List<SalesChatMessage> ChatHistory,
    List<SalesCustomerView> Customers,
    List<SalesOrderView> Orders,
    List<McpToolCall> ToolCalls,
    List<SalesApproval> PendingApprovals
);
