using Nest;
using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ElasticClientExtensions
{
    public static IServiceCollection AddElasticClient(this IServiceCollection services, IConfiguration configuration)
    {
        
        var cloudId = configuration["Elasticsearch:CloudId"];
        var username = configuration["Elasticsearch:Username"];
        var password = configuration["Elasticsearch:Password"];

        var settings = new ConnectionSettings(
            cloudId,
            new BasicAuthenticationCredentials(username, password))
            .DefaultIndex("bookstore")
            .DisableDirectStreaming()
            .DefaultMappingFor<Product>(m => m
                .IndexName("bookstore")
                .PropertyName(p => p.Id, "id")
            )
            .DefaultMappingFor<User>(m => m
                .IndexName("users")
                .PropertyName(p => p.Id, "id")
            )
            .DefaultMappingFor<CartItem>(m => m
                .IndexName("carts")
                .PropertyName(p => p.Id, "id")
            )
            .DefaultMappingFor<WishlistItem>(m => m
                .IndexName("wishlists")
                .PropertyName(p => p.Id, "id")
            );

        var client = new ElasticClient(settings);

        
        services.AddSingleton<IElasticClient>(client);

        return services;
    }

    private static void CreateDefaultAdminUser(IElasticClient client)
    {
        try
        {
            
            var searchResponse = client.Search<User>(s => s
                .Index("users")
                .Query(q => q.Term(t => t.Field(f => f.Username.Suffix("keyword")).Value("admin")))
                .Size(1)
            );

            if (!searchResponse.Documents.Any())
            {
                var adminUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "admin",
                    Email = "admin@bookstore.com",
                    PasswordHash = PasswordHelper.HashPassword("eren2003"), // Varsayılan şifre
                    FirstName = "Admin",
                    LastName = "User"
                };

                var indexResponse = client.IndexDocument(adminUser);
                
                if (indexResponse.IsValid)
                {
                    Console.WriteLine("Varsayılan admin kullanıcısı oluşturuldu! (Username: admin, Password: eren2003)");
                }
                else
                {
                    Console.WriteLine($"Admin kullanıcısı oluşturulurken hata: {indexResponse.OriginalException?.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Default admin user oluşturma hatası: {ex.Message}");
        }
    }
}