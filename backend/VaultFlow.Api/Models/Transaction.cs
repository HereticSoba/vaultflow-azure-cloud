namespace VaultFlow.Api.Models;

public class Transaction
{
    public int Id { get; set; }
    public string? Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string Merchant { get; set; } = string.Empty;
}