using Nest;

public interface IWishlistRepository
{
    Task AddToWishlistAsync(WishlistItem item);
    Task<List<WishlistItem>> GetWishlistByUserAsync(string userId);
    Task RemoveFromWishlistAsync(string productId, string userId);
    Task ClearWishlistAsync(string userId);
    Task<WishlistItem?> GetWishlistItemByProductIdAsync(string userId, string productId);
}

public class WishlistRepository : IWishlistRepository
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "wishlists";

    public WishlistRepository(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async Task AddToWishlistAsync(WishlistItem item)
    {
       
        var searchResponse = await _elasticClient.SearchAsync<WishlistItem>(s => s
            .Index(IndexName)
            .Query(q => q.Bool(b => b
                .Must(
                    m => m.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(item.UserId)),
                    m => m.Term(t => t.Field(f => f.ProductId.Suffix("keyword")).Value(item.ProductId)),
                    m => m.Term(t => t.Field(f => f.ProductTitle.Suffix("keyword")).Value(item.ProductTitle))
                )
            ))
            .Size(1)
        );

        
        if (!searchResponse.Documents.Any())
        {
            await _elasticClient.IndexAsync(item, i => i.Index(IndexName));
        }
    }

    public async Task<List<WishlistItem>> GetWishlistByUserAsync(string userId)
    {
        var response = await _elasticClient.SearchAsync<WishlistItem>(s => s
            .Index(IndexName)
            .Query(q => q.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(userId)))
            .Size(1000)
        );

        return response.Documents.ToList();
    }

    public async Task RemoveFromWishlistAsync(string productId, string userId)
    {
        var searchResponse = await _elasticClient.SearchAsync<WishlistItem>(s => s
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
            await _elasticClient.DeleteAsync<WishlistItem>(hit.Id, d => d.Index(IndexName));
        }
    }

    public async Task ClearWishlistAsync(string userId)
    {
        var response = await _elasticClient.SearchAsync<WishlistItem>(s => s
            .Index(IndexName)
            .Query(q => q.Term(t => t.Field(f => f.UserId.Suffix("keyword")).Value(userId)))
            .Size(1000)
        );

        foreach (var hit in response.Hits)
        {
            await _elasticClient.DeleteAsync<WishlistItem>(hit.Id, d => d.Index(IndexName));
        }
    }

    public async Task<WishlistItem?> GetWishlistItemByProductIdAsync(string userId, string productId)
    {
        var response = await _elasticClient.SearchAsync<WishlistItem>(s => s
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
