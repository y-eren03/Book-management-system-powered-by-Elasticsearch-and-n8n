public class Product
{
    public string? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public decimal Price { get; set; } // Anlık geçerli fiyat
    public string Category { get; set; } = string.Empty;
    public decimal Stock { get; set; }
    public decimal? discountrate { get; set; }
    public decimal? İncreaseRate { get; set; }
    
    
    public decimal RealPrice { get; set;}

    

}