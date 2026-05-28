namespace ToolsLibrary.Models;

public enum CustomerTier { A, B, C }

public class Customer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public CustomerTier Tier { get; set; }
    public string AccountManager { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
