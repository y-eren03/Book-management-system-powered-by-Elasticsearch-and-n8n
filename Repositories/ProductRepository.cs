using Nest;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


public interface IProductRepository
{
    Task IndexProductAsync(Product product);
    Task<Product?> GetProductByIdAsync(string id);
    Task<IEnumerable<Product>> SearchProductsAsync(string query);

    Task<List<Product>> SearchProductsBeginWithAsync(string query, string author, string category, decimal minPrice, decimal maxPrice, string sortBy, string sortOrder);
    Task<Product?> SearchProductByIdAsync(string productId);
    Task DeleteProductAsync(string id);
    Task ApplyDiscountAsync(string query, decimal discountrate);
    Task ApplyIncreaseAsync(string query, decimal increaseRate);
    Task UpdateStockAsync(string query, decimal changeAmount);
    Task UpdatePriceAsync(string query, decimal newPrice);
    Task RemoveDiscountAsync(string query);
    
}


public class ProductRepository : IProductRepository
{
    private readonly IElasticClient _client;
    private const string IndexName = "bookstore";

    


    

    public ProductRepository(IElasticClient client)
    {
        _client = client;
    }

    public async Task IndexProductAsync(Product product)
    {
        var response = await _client.IndexDocumentAsync(product);
        if (!response.IsValid)
            throw new Exception($"Indexing failed: {response.DebugInformation}");
    }

    public async Task<List<Product>> SearchProductsBeginWithAsync(string query, string author, string category, decimal minPrice, decimal maxPrice, string sortBy, string sortOrder)
{
    try
    {
        // Use your existing working search method as base
        var allBooks = await SearchProductsAsync("");
        
        if (!allBooks.Any())
            return new List<Product>();

        // Apply filters in memory (not ideal for large datasets but works)
        var filtered = allBooks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(b => 
                (b.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (b.Author?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (b.Category?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            filtered = filtered.Where(b => 
                b.Author?.Contains(author, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(b => 
                string.Equals(b.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (minPrice > 0)
        {
            filtered = filtered.Where(b => b.Price >= minPrice);
        }

        if (maxPrice < decimal.MaxValue)
        {
            filtered = filtered.Where(b => b.Price <= maxPrice);
        }

        // Apply sorting
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var isDescending = sortOrder?.ToLower() == "desc";
            
            filtered = sortBy.ToLower() switch
            {
                "title" => isDescending ? filtered.OrderByDescending(b => b.Title) : filtered.OrderBy(b => b.Title),
                "author" => isDescending ? filtered.OrderByDescending(b => b.Author) : filtered.OrderBy(b => b.Author),
                "price" => isDescending ? filtered.OrderByDescending(b => b.Price) : filtered.OrderBy(b => b.Price),
                "stock" => isDescending ? filtered.OrderByDescending(b => b.Stock) : filtered.OrderBy(b => b.Stock),
                _ => filtered.OrderByDescending(b => b.Title)
            };
        }

        return filtered.ToList();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Simple search error: {ex.Message}");
        return new List<Product>();
    }
}

    public async Task<Product?> GetProductByIdAsync(string id)
    {
        var response = await _client.GetAsync<Product>(id);
        return response.Found ? response.Source : null;
    }

    public async Task<Product?> SearchProductByIdAsync(string productId)
{
    var searchResponse = await _client.SearchAsync<Product>(s => s
        .Query(q => q.Ids(i => i.Values(productId)))
        .Size(1)
    );

    if (!searchResponse.IsValid)
        throw new Exception($"ID search failed: {searchResponse.DebugInformation}");

    var product = searchResponse.Documents.FirstOrDefault();
    if (product != null)
    {
        product.Id = productId;
    }
    
    return product;
}

    public async Task<IEnumerable<Product>> SearchProductsAsync(string query)
    {
        var searchResponse = await _client.SearchAsync<Product>(s => s
            .Query(q =>
            {
                if (string.IsNullOrEmpty(query))
                    return q.MatchAll();




                return q.MultiMatch(m => m
                    .Fields(f => f
                        .Field(p => p.Title, 2.0)
                        .Field(p => p.Author, 1.5)
                        .Field(p => p.Category, 1.0)
                    )
                    .Query(query)
                    .Fuzziness(Fuzziness.Auto)
                );
            })
            .Size(100)
        );

        if (!searchResponse.IsValid)
            throw new Exception($"Search failed: {searchResponse.DebugInformation}");

        return searchResponse.Documents.Select(d =>
        {
            var hit = searchResponse.Hits.First(h => h.Source == d);
            d.Id = hit.Id;
            return d;
        }).ToList();
    }

    public async Task DeleteProductAsync(string id)
    {
        var response = await _client.DeleteAsync<Product>(id);
        if (!response.IsValid)
            throw new Exception($"Delete failed: {response.DebugInformation}");
    }

    public async Task ApplyDiscountAsync(string query, decimal discountrate)
    {
        var products = await SearchProductsAsync(query);
        using var httpClient = new HttpClient();

        foreach (var p in products)
        {
            if (p.discountrate != 0) continue;

            var newPrice = p.Price - (p.Price * discountrate / 100);

            var updateDoc = new
            { Price = newPrice, discountrate = discountrate };
            var response = await _client.UpdateAsync<object>(p.Id, u => u
                .Doc(updateDoc)
            );
            if (!response.IsValid)
                throw new Exception($"Discount failed: {response.DebugInformation}");


            var webhookUrl = "http://localhost:5678/webhook/discount";
            var payload = new { productId = p.Id };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            await httpClient.PostAsync(webhookUrl, content);
        }
    }

    public async Task ApplyIncreaseAsync(string query, decimal increaseRate)
    {
        var products = await SearchProductsAsync(query);

        foreach (var p in products)
        {
            var newPrice = p.Price + (p.Price * increaseRate / 100);
            var updateDoc = new 
            { RealPrice =newPrice , Price = newPrice, İncreaseRate = increaseRate };

            var response = await _client.UpdateAsync<object>(p.Id, u => u
                .Doc(updateDoc)
            );

            if (!response.IsValid)
                throw new Exception($"Price increase failed: {response.DebugInformation}");
        }
    }

    public async Task UpdatePriceAsync(string query, decimal newPrice)
    {
        var products = await SearchProductsAsync(query);

        foreach (var p in products)
        {
            
            var updateDoc = new 
            { RealPrice =newPrice , Price = newPrice};

            var response = await _client.UpdateAsync<object>(p.Id, u => u
                .Doc(updateDoc)
            );

            if (!response.IsValid)
                throw new Exception($"Price change failed: {response.DebugInformation}");
        }
    }


    public async Task UpdateStockAsync(string query, decimal changeAmount)
    {
        var product = await SearchProductByIdAsync(query);

            var newStock = product.Stock + changeAmount;

            // ✅ Generic parametreyi 'object' yaparak anonim tip kabul etmesini sağla
            var response = await _client.UpdateAsync<object>(product.Id, u => u
                .Doc(new { Stock = newStock }) // Sadece Stock güncellenir
            );

            if (!response.IsValid)
                throw new Exception($"Stock update failed: {response.DebugInformation}");
        
        
    }


    public async Task RemoveDiscountAsync(string query)
    {
        var products = await SearchProductsAsync(query);

        foreach (var p in products)
        {
            if (!p.discountrate.HasValue) continue;


            var updateDoc = new
            { Price = p.RealPrice, discountrate = 0 };

            var response = await _client.UpdateAsync<object>(p.Id, u => u
                .Doc(updateDoc)
            );

            if (!response.IsValid)
                throw new Exception($"Remove discount failed: {response.DebugInformation}");
        }
    }
}
