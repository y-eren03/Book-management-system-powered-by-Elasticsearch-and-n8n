using Nest;

public interface ICartRepository
{
    Task AddToCartAsync(CartItem item);
    Task<List<CartItem>> GetCartByUserAsync(string userId);
    Task RemoveFromCartAsync(string id, string userId);
    Task SetCartItemQuantityAsync(string userId, string productId, int quantity);
    Task<CartItem?> GetCartItemByProductIdAsync(string userId, string productId);
    Task ClearCartAsync(string userId);
}

public class CartRepository : ICartRepository
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "carts";

    public CartRepository(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async Task AddToCartAsync(CartItem item)
    {

        var searchResponse = await _elasticClient.SearchAsync<CartItem>(s => s
            .Index(IndexName)
            .Query(q => q.Bool(b => b
                .Must(
                    m => m.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(item.UserId)),
                    m => m.Term(t => t.Field(f => f.ProductId.Suffix("keyword")).Value(item.ProductId))
                )
            ))
            .Size(1)
        );

        if (searchResponse.Documents.Any())
        {

            var existing = searchResponse.Hits.First();
            existing.Source.Quantity += item.Quantity;

            await _elasticClient.IndexAsync(existing.Source, i => i
                .Index(IndexName)
                .Id(existing.Id) 
            );
        }
        else
        {

            await _elasticClient.IndexAsync(item, i => i.Index(IndexName));
        }
    }


    public async Task<List<CartItem>> GetCartByUserAsync(string userId)
    {
        var response = await _elasticClient.SearchAsync<CartItem>(s => s
            .Index(IndexName)
            .Query(q => q.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(userId)))
            .Size(1000)
        );
        return response.Documents.ToList();
    }

    public async Task RemoveFromCartAsync(string productId, string userId)
{
    
    var searchResponse = await _elasticClient.SearchAsync<CartItem>(s => s
        .Index(IndexName)
        .Query(q => q.Bool(b => b
            .Must(
                m => m.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(userId)),
                m => m.Term(t => t.Field(f => f.ProductId.Suffix("keyword")).Value(productId))
            )
        ))
        .Size(1)
    );

    if (searchResponse.Hits.Any())
    {
        var hit = searchResponse.Hits.First();
        await _elasticClient.DeleteAsync<CartItem>(hit.Id, d => d.Index(IndexName));
    }
}


public async Task ClearCartAsync(string userId)
{
    var response = await _elasticClient.SearchAsync<CartItem>(s => s
        .Index(IndexName)
        .Query(q => q.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(userId)))
        .Size(1000)
    );

    foreach (var hit in response.Hits)
    {
        await _elasticClient.DeleteAsync<CartItem>(hit.Id, d => d.Index(IndexName));
    }
}


public async Task SetCartItemQuantityAsync(string userId, string productId, int quantity)
    {
        var searchResponse = await _elasticClient.SearchAsync<CartItem>(s => s
            .Index(IndexName)
            .Query(q => q.Bool(b => b
                .Must(
                    m => m.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(userId)),
                    m => m.Term(t => t.Field(f => f.ProductId.Suffix("keyword")).Value(productId))
                )
            ))
            .Size(1)
        );


        if (searchResponse.Hits.Any())
        {
            var existing = searchResponse.Hits.First();
            existing.Source.Quantity = quantity;

            await _elasticClient.IndexAsync(existing.Source, i => i
                .Index(IndexName)
                .Id(existing.Id)
            );
        }
    }

public async Task<CartItem?> GetCartItemByProductIdAsync(string userId, string productId)
{
    var response = await _elasticClient.SearchAsync<CartItem>(s => s
        .Index(IndexName)
        .Query(q => q.Bool(b => b
            .Must(
                m => m.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(userId)),
                m => m.Term(t => t.Field(f => f.ProductId.Suffix("keyword")).Value(productId))
            )
        ))
        .Size(1) 
    );

    return response.Hits.FirstOrDefault()?.Source;
}




}



