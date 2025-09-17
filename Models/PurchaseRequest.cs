public class PurchaseRequest
{
    public string BookId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}