using System.Text.Json;
using Microsoft.AspNetCore.Session;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddElasticClient(builder.Configuration);


builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();




app.UseStaticFiles();
app.UseSession();


app.MapGet("/", () => Results.Content(GetHomePage(), "text/html"));


app.MapGet("/admin-login", () => Results.Content(GetAdminLoginForm(), "text/html"));


app.MapGet("/create-user", () => Results.Content(GetAddUserForm(), "text/html"));



app.MapPost("/admin-login", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"];
    var password = form["password"];

    if (username == "admin" && password == "eren2003")
    {
        context.Response.Cookies.Append("role", "admin");
        context.Response.Redirect("/admin");
    }
    else
    {
        context.Response.Redirect("/admin-login");
    }
});


app.MapPost("/user-login", async (HttpContext context, IUserRepository userRepo) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    var user = await userRepo.ControlAsync(username, password);

    if (user != null)
    {
        context.Response.Redirect("/books");
        context.Session.SetString("UserId", user.Id);

    }
    else
    { context.Response.Redirect("/"); }
});

app.MapPost("/api/users/add", async (HttpContext context, IUserRepository userRepo) =>
{
    var form = await context.Request.ReadFormAsync();

    var username = form["username"].ToString();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var firstname = form["firstname"].ToString();
    var lastname = form["lastname"].ToString();

    try
    {
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = PasswordHelper.HashPassword(password),
            FirstName = firstname,
            LastName = lastname
        };

        var createdUser = await userRepo.CreateUserAsync(user);

        context.Response.StatusCode = 201;
        await context.Response.WriteAsJsonAsync(new
        {
            Message = "✅ Kullanıcı başarıyla eklendi",
            UserId = createdUser.Id
        });
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"❌ Kullanıcı eklenirken hata: {ex.Message}");
    }
});



app.MapGet("/admin", (HttpContext context) =>
{
    if (context.Request.Cookies["role"] == "admin")
        return Results.Content(GetHtmlForm(), "text/html");
    else
        return Results.Redirect("/");
});

app.MapGet("/books", () => Results.Content(GetUserForm(), "text/html"));


app.MapGet("/add", () => Results.Content(GetAddBookForm(), "text/html"));

app.MapGet("/price-update", () => Results.Content(GetPriceUpdateForm(), "text/html"));


app.MapGet("/api/books", async (IProductRepository repo) =>
{
    var books = await repo.SearchProductsAsync(""); 
    return Results.Ok(books);
});


app.MapGet("/api/books/{id}", async (IProductRepository repo, string id) =>
{
    var book = await repo.GetProductByIdAsync(id);
    return book != null ? Results.Ok(book) : Results.NotFound();
});


app.MapDelete("/api/books/{id}", async (IProductRepository repo, string id) =>
{
    await repo.DeleteProductAsync(id);
    return Results.Ok();
});


app.MapPost("/add-book", async (IProductRepository repo, HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();

    var product = new Product
    {

        Title = form["title"]!,
        Author = form["author"]!,
        Price = decimal.TryParse(form["price"], out decimal price) ? price : 0,
        Category = form["category"]!,
        Stock = decimal.TryParse(form["stock"], out decimal stock) ? stock : 0,

    };

    await repo.IndexProductAsync(product);

    return Results.Redirect("/admin/?success=true");
});


app.MapPost("/api/books/discount", async (IProductRepository repo, HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();

    var bookName = form["bookName"].ToString();
    var category = form["category"].ToString();
    var author = form["author"].ToString(); // Yeni eklenen
    var discountrateStr = form.ContainsKey("percentage") ? form["percentage"].ToString() :
                         form.ContainsKey("discountrate") ? form["discountrate"].ToString() : "0";

    var discountrate = decimal.TryParse(discountrateStr, out var dr) ? dr : 0;

    
    if (string.IsNullOrEmpty(bookName) && string.IsNullOrEmpty(category) && string.IsNullOrEmpty(author))
    {
        return Results.BadRequest("Lütfen bir kitap ismi, yazar veya kategori seçin.");
    }

    if (discountrate <= 0)
    {
        return Results.BadRequest("Geçerli bir indirim oranı girin");
    }

    try
    {
        if (!string.IsNullOrEmpty(bookName))
        {
            await repo.ApplyDiscountAsync(bookName, discountrate);
        }
        else if (!string.IsNullOrEmpty(author))
        {
            await repo.ApplyDiscountAsync(author, discountrate); 
        }
        else
        {
            await repo.ApplyDiscountAsync(category, discountrate);
        }

        return Results.Redirect("/price-update?success=true");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"İndirim uygulanırken hata: {ex.Message}");
    }
});


app.MapPost("/api/books/increase", async (IProductRepository repo, HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();

    var bookName = form["bookName"].ToString();
    var category = form["category"].ToString();
    var author = form["author"].ToString(); // Yeni eklenen
    var increaseRateStr = form.ContainsKey("percentage") ? form["percentage"].ToString() :
                          form.ContainsKey("increaseRate") ? form["increaseRate"].ToString() : "0";

    var increaseRate = decimal.TryParse(increaseRateStr, out var ir) ? ir : 0;

    // En az bir alanın dolu olduğunu kontrol et
    if (string.IsNullOrEmpty(bookName) && string.IsNullOrEmpty(category) && string.IsNullOrEmpty(author))
    {
        return Results.BadRequest("Lütfen bir kitap ismi, yazar veya kategori seçin.");
    }

    if (increaseRate <= 0)
    {
        return Results.BadRequest("Geçerli bir artış oranı girin");
    }

    try
    {
        if (!string.IsNullOrEmpty(bookName))
        {
            await repo.ApplyIncreaseAsync(bookName, increaseRate);
        }
        else if (!string.IsNullOrEmpty(author))
        {
            await repo.ApplyIncreaseAsync(author, increaseRate); // Yeni metot
        }
        else
        {
            await repo.ApplyIncreaseAsync(category, increaseRate);
        }

        return Results.Redirect("/price-update?success=true");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Zam uygulanırken hata: {ex.Message}");
    }
});


app.MapPost("/api/books/remove-discount", async (IProductRepository repo, HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();

    var bookName = form["bookName"].ToString();
    var category = form["category"].ToString();
    var author = form["author"].ToString(); // Yeni eklenen

    // En az bir alanın dolu olduğunu kontrol et
    if (string.IsNullOrEmpty(bookName) && string.IsNullOrEmpty(category) && string.IsNullOrEmpty(author))
    {
        return Results.BadRequest("Lütfen indirimini kaldırmak için bir kitap ismi, yazar veya kategori seçin.");
    }

    try
    {
        if (!string.IsNullOrEmpty(bookName))
        {
            await repo.RemoveDiscountAsync(bookName);
        }
        else if (!string.IsNullOrEmpty(author))
        {
            await repo.RemoveDiscountAsync(author); // Yeni metot
        }
        else
        {
            await repo.RemoveDiscountAsync(category);
        }

        return Results.Redirect("/price-update?success=true");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"İndirim kaldırılırken hata: {ex.Message}");
    }
});

app.MapPost("/api/books/stock", async (IProductRepository repo, HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    
    string query = "";

    if (form.ContainsKey("bookId") && !string.IsNullOrEmpty(form["bookId"].ToString()))
    {
        query = form["bookId"].ToString();
    }
    else if (form.ContainsKey("query") && !string.IsNullOrEmpty(form["query"].ToString()))
    {
        query = form["query"].ToString();
    }
    
    
    
    var changeAmountStr = form.ContainsKey("changeAmount") ? form["changeAmount"].ToString() : "0";
    var changeAmount = decimal.TryParse(changeAmountStr, out var ca) ? ca : 0;
    
    
    if (changeAmount == 0)
    {
        return Results.BadRequest("Geçerli bir stok değişim miktarı girin");
    }
    
    
    try
    {
        await repo.UpdateStockAsync(query, changeAmount);
        return Results.Ok(new { message = "Stok başarıyla güncellendi" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Stok güncellenirken hata: {ex.Message}");
    }
});


app.MapPost("/api/books/price", async (IProductRepository repo, HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();

    var bookName = form["bookName"].ToString();
    var newPriceStr = form["newPrice"].ToString();
    var newPrice = decimal.TryParse(newPriceStr, out var ir) ? ir : 0;

    if (string.IsNullOrEmpty(bookName))
    {
        return Results.BadRequest("Lütfen bir kitap ismi seçin.");
    }

     try
    {
        if (!string.IsNullOrEmpty(bookName))
        {
            await repo.UpdatePriceAsync(bookName , newPrice );
        }
        
        return Results.Redirect("/price-update?success=true");
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Fiyat değişikliği uygulanırken hata: {ex.Message}");
    }
    
});



app.MapGet("/sepet", () => Results.Content(GetCartForm(), "text/html"));

app.MapGet("/wishlist", () => Results.Content(GetWishlistPage(), "text/html"));

app.MapGet("/ai-chat", () => Results.Content(GetAiForm(), "text/html"));

app.MapGet("/user-ai-chat", () => Results.Content(GetUserAiForm(), "text/html"));


app.MapGet("/api/books/search", async (IProductRepository repo, HttpContext context) =>
{
    try
    {
        var query = context.Request.Query["query"].ToString() ?? "";
        var author = context.Request.Query["author"].ToString() ?? "";
        var category = context.Request.Query["category"].ToString() ?? "";
        var minPrice = decimal.TryParse(context.Request.Query["minPrice"], out var min) ? min : 0;
        var maxPrice = decimal.TryParse(context.Request.Query["maxPrice"], out var max) ? max : decimal.MaxValue;
        var sortBy = context.Request.Query["sortBy"].ToString() ?? "";
        var sortOrder = context.Request.Query["sortOrder"].ToString() ?? "asc";

       

        // Use the simple version first to test
        var books = await repo.SearchProductsBeginWithAsync(query, author, category, minPrice, maxPrice, sortBy, sortOrder);
        
        
        
        return Results.Ok(books);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Search endpoint error: {ex.Message}");
        return Results.Ok(new List<Product>());
    }
});


app.MapGet("/api/books/categories", async (IProductRepository repo) =>
{
    try
    {
        var books = await repo.SearchProductsAsync("");
        var categories = books.Select(b => b.Category)
                            .Where(c => !string.IsNullOrEmpty(c))
                            .Distinct()
                            .OrderBy(c => c)
                            .ToList();
        return Results.Ok(categories);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Kategori listesi alınamadı: {ex.Message}");
    }
});


app.MapPost("/api/books/purchase", async (IProductRepository repo, HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        var purchaseData = System.Text.Json.JsonSerializer.Deserialize<PurchaseRequest>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (purchaseData == null || string.IsNullOrEmpty(purchaseData.BookId) || purchaseData.Quantity <= 0)
        {
            return Results.BadRequest("Geçersiz satın alma verisi");
        }

        var book = await repo.GetProductByIdAsync(purchaseData.BookId);
        if (book == null)
        {
            return Results.NotFound($"Kitap bulunamadı: {purchaseData.Title}");
        }

        if (book.Stock < purchaseData.Quantity)
        {
            return Results.BadRequest($"Yetersiz stok! Mevcut: {book.Stock}, İstenen: {purchaseData.Quantity}");
        }

        var stockReduction = -purchaseData.Quantity;
        await repo.UpdateStockAsync(purchaseData.BookId, stockReduction);

        return Results.Ok(new 
        { 
            message = $"{purchaseData.Title} başarıyla satın alındı",
            remainingStock = book.Stock + stockReduction,
            purchasedQuantity = purchaseData.Quantity
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Purchase error: {ex.Message}");
        return Results.BadRequest($"Satın alma işlemi sırasında hata: {ex.Message}");
    }
});

app.MapPost("/api/cart/update", async (HttpContext ctx, ICartRepository cartRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    var form = await ctx.Request.ReadFormAsync();
    var productId = form["productId"].ToString();
    if (!int.TryParse(form["quantity"], out int quantity))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("❌ Geçersiz miktar.");
        return;
    }

    if (quantity < 1)
    {
        
        var item = await cartRepo.GetCartItemByProductIdAsync(userId, productId);
        if (item != null)
            await cartRepo.RemoveFromCartAsync(productId, userId);
    }
    else
    {
        
        await cartRepo.SetCartItemQuantityAsync(userId, productId, quantity);
    }

    await ctx.Response.WriteAsync("✅ Miktar güncellendi.");
});




app.MapPost("/api/cart/add", async (HttpContext ctx, ICartRepository cartRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    var form = await ctx.Request.ReadFormAsync();
    var productId = form["productId"].ToString();
    var quantity = int.Parse(form["quantity"].ToString());

    var item = new CartItem
    {
        UserId = userId,
        ProductId = productId,
        Quantity = quantity
    };

    await cartRepo.AddToCartAsync(item);

    await ctx.Response.WriteAsync("✅ Ürün sepete eklendi.");
});


app.MapGet("/api/cart", async (HttpContext ctx, ICartRepository cartRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    var cart = await cartRepo.GetCartByUserAsync(userId);
    await ctx.Response.WriteAsJsonAsync(cart);
});


app.MapPost("/api/cart/remove", async (HttpContext ctx, ICartRepository cartRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    var form = await ctx.Request.ReadFormAsync();
    var productId = form["productId"].ToString();

    await cartRepo.RemoveFromCartAsync(productId, userId);
    await ctx.Response.WriteAsync("🗑️ Ürün sepetten silindi.");
});


app.MapPost("/api/cart/clear", async (HttpContext ctx, ICartRepository cartRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    await cartRepo.ClearCartAsync(userId);
    await ctx.Response.WriteAsync("🗑️ Sepet temizlendi.");
});


app.MapGet("/api/user/current", async (HttpContext context) =>
{
    var userId = context.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }
    
    await context.Response.WriteAsJsonAsync(new { userId = userId });
});


app.MapPost("/api/wishlist/add", async (HttpContext ctx, IWishlistRepository wishlistRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    var form = await ctx.Request.ReadFormAsync();
    var productId = form["productId"].ToString();
    var productTitle = form["productTitle"].ToString();

    
    var existingItem = await wishlistRepo.GetWishlistItemByProductIdAsync(userId, productId);
    if (existingItem != null)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("❌ Bu ürün zaten istek listenizde.");
        return;
    }

    var item = new WishlistItem
    {
        UserId = userId,
        ProductId = productId,
        ProductTitle = productTitle
    };

    await wishlistRepo.AddToWishlistAsync(item);
    await ctx.Response.WriteAsync("💝 Ürün istek listesine eklendi.");
});


app.MapGet("/api/wishlist", async (HttpContext ctx, IWishlistRepository wishlistRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    var wishlist = await wishlistRepo.GetWishlistByUserAsync(userId);
    await ctx.Response.WriteAsJsonAsync(wishlist);
});


app.MapPost("/api/wishlist/remove", async (HttpContext ctx, IWishlistRepository wishlistRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    var form = await ctx.Request.ReadFormAsync();
    var productId = form["productId"].ToString();

    await wishlistRepo.RemoveFromWishlistAsync(productId, userId);
    await ctx.Response.WriteAsync("🗑️ Ürün istek listesinden silindi.");
});


app.MapPost("/api/wishlist/clear", async (HttpContext ctx, IWishlistRepository wishlistRepo) =>
{
    var userId = ctx.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userId))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("❌ Giriş yapmalısın.");
        return;
    }

    await wishlistRepo.ClearWishlistAsync(userId);
    await ctx.Response.WriteAsync("🗑️ İstek listesi temizlendi.");
});

app.Run();




string GetHtmlForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>📚 Book Store</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { 
font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
min-height: 100vh;
padding: 20px;
}
.container { 
max-width: 1200px; 
margin: 0 auto; 
background: white;
border-radius: 20px;
box-shadow: 0 20px 40px rgba(0,0,0,0.1);
overflow: hidden;
}
.header { 
background: linear-gradient(135deg, #4f46e5, #7c3aed);
color: white;
padding: 40px;
text-align: center;
}
.header h1 { 
font-size: 3rem; 
margin-bottom: 10px;
text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
}
.content { padding: 40px; }
.buttons {
display: flex;
gap: 20px;
justify-content: center;
margin-bottom: 40px;
flex-wrap: wrap;
}
.btn {
padding: 12px 25px;
border: none;
border-radius: 50px;
font-size: 1rem;
cursor: pointer;
text-decoration: none;
display: inline-flex;
align-items: center;
gap: 8px;
transition: all 0.3s ease;
font-weight: 600;
}
.btn-primary {
background: linear-gradient(135deg, #4f46e5, #7c3aed);
color: white;
}
.btn-secondary {
background: linear-gradient(135deg, #06b6d4, #0891b2);
color: white;
}
.btn:hover {
transform: translateY(-2px);
box-shadow: 0 10px 20px rgba(0,0,0,0.2);
}
.books-grid {
display: grid;
grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
gap: 20px;
margin-top: 30px;
}
.book-card {
position: relative;
background: white;
border-radius: 15px;
padding: 20px;
box-shadow: 0 5px 15px rgba(0,0,0,0.1);
border-left: 5px solid #4f46e5;
transition: all 0.3s ease;
display: flex;
flex-direction: column;
justify-content: space-between;
height: 280px;
}
.book-card:hover {
transform: translateY(-5px);
box-shadow: 0 15px 30px rgba(0,0,0,0.2);
}
.book-info {
flex-grow: 1;
}
.discount-info {
background: linear-gradient(135deg, #22c55e, #16a34a); 
color: white; 
padding: 4px 8px;
border-radius: 4px;
font-size: 0.8rem;
margin-top: 5px;
display: inline-block;
}
.card-actions {
display: flex;
justify-content: flex-start;
gap: 10px;
margin-top: auto;
}
.success { 
background: #10b981; 
color: white; 
padding: 15px; 
border-radius: 10px; 
margin-bottom: 20px;
text-align: center;
font-weight: 600;
}
.loading {
text-align: center;
padding: 40px;
font-size: 1.2rem;
color: #666;
}
</style>
</head>
<body>
<div class="container">
<div class="header">
<h1>📚 Book Store</h1>
<p>Elasticsearch ile güçlendirilmiş kitap yönetim sistemi</p>
</div>
<div class="content">
""" + (Environment.GetEnvironmentVariable("QUERY_STRING")?.Contains("success=true") == true ? "<div class='success'>✅ Kitap başarıyla eklendi!</div>" : "") + """

<div class="buttons">
<a href="/add" class="btn btn-primary">➕ Yeni Kitap Ekle</a>
<a href="/price-update" class="btn btn-secondary">💰 Fiyat Değiştir / İndirim / Zam</a>
<button onclick="loadBooks()" class="btn btn-secondary">🔄 Kitapları Yenile</button>
<a href="/api/books" class="btn btn-secondary" target="_blank">📊 API Verisi (JSON)</a>
<a href="/ai-chat" class="btn btn-secondary">🤖 AI Yardım</a>
<a href="/" class="btn btn-secondary">🔑 Giriş Sayfasına Dön</a>
</div>

<div class="search-filters" style="background: #f8fafc; padding: 25px; border-radius: 15px; margin-bottom: 30px;">
    <h3 style="text-align: center; margin-bottom: 20px; color: #374151;">🔍 Arama ve Filtreleme</h3>
    
    <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin-bottom: 20px;">
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Kitap Adı</label>
            <input type="text" id="searchQuery" placeholder="Kitap adı ara..." style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Yazar</label>
            <input type="text" id="searchAuthor" placeholder="Yazar adı..." style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Kategori</label>
            <select id="searchCategory" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="">Tüm Kategoriler</option>
            </select>
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Min Fiyat</label>
            <input type="number" id="minPrice" placeholder="0" min="0" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Max Fiyat</label>
            <input type="number" id="maxPrice" placeholder="1000" min="0" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Sıralama</label>
            <select id="sortBy" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="">Varsayılan</option>
                <option value="title">Kitap Adı</option>
                <option value="author">Yazar</option>
                <option value="price">Fiyat</option>
                <option value="stock">Stok</option>
            </select>
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Sıra</label>
            <select id="sortOrder" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="asc">A-Z (Artan)</option>
                <option value="desc">Z-A (Azalan)</option>
            </select>
        </div>
        
        <div style="display: flex; align-items: end; gap: 10px;">
            <button onclick="searchBooks()" class="btn btn-primary" style="margin: 0;">🔍 Ara</button>
            <button onclick="clearFilters()" class="btn btn-secondary" style="margin: 0; background: #6b7280;">🗑️ Temizle</button>
        </div>
    </div>
</div>

<div id="books-container">
<div class="loading">Kitaplar yükleniyor...</div>
</div>
</div>
</div>

<script>
async function loadBooks() {
    try {
        document.getElementById('books-container').innerHTML = '<div class="loading">Kitaplar yükleniyor...</div>';
        const response = await fetch('/api/books');
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const books = await response.json();
        
        // Ensure books is an array
        if (Array.isArray(books)) {
            displayBooks(books);
        } else {
            throw new Error('Unexpected data format received from server');
        }
        
    } catch (error) {
        console.error('Kitap yükleme hatası:', error);
        document.getElementById('books-container').innerHTML = '<div class="loading">❌ Kitaplar yüklenirken hata oluştu</div>';
    }
}

function displayBooks(books) {
    const container = document.getElementById('books-container');
    
    // Check if books is actually an array
    if (!Array.isArray(books)) {
        console.error('Expected array but got:', books);
        container.innerHTML = '<div class="loading">❌ Veri formatı hatalı</div>';
        return;
    }
    
    if (books.length === 0) {
        container.innerHTML = '<div class="loading">📚 Arama kriterlerinize uygun kitap bulunamadı</div>';
        return;
    }

    const booksHtml = books.map(book => `
        <div class="book-card">
            <div class="book-info">
                <h3 style="color: #4f46e5; margin-bottom: 10px;">${escapeHtml(book.title || '')}</h3>
                <p><strong>Yazar:</strong> ${escapeHtml(book.author || '')}</p>
                <p><strong>Fiyat:</strong> ${book.price || 0}₺</p>
                <p><strong>Kategori:</strong> ${escapeHtml(book.category || '')}</p>
                <p><strong>Stok:</strong> ${book.stock || 0}</p>
                ${book.discountrate ? `<div class="discount-info">%${book.discountrate} İndirim</div>` : ''}
            </div>
            <div class="card-actions">
                <button class="btn btn-secondary" onclick="updateStock('${book.id}')">Stok Güncelle</button>
                <button class="btn btn-primary" onclick="deleteBook('${book.id}')">Sil</button>
            </div>
        </div>
    `).join('');

    container.innerHTML = `<div class="books-grid">${booksHtml}</div>`;
}

function escapeHtml(text) {
const div = document.createElement('div');
div.textContent = text;
return div.innerHTML;
}

async function deleteBook(id) {
if(!confirm("Bu kitabı silmek istediğinize emin misiniz?")) return;
try {
    const response = await fetch('/api/books/' + encodeURIComponent(id), { method: 'DELETE' });
    if (response.ok) {
        loadBooks();
    } else {
        alert('Kitap silinirken hata oluştu');
    }
} catch (error) {
    console.error('Silme hatası:', error);
    alert('Kitap silinirken hata oluştu');
}
}

async function updateStock(id) {
const currentBook = await getCurrentBook(id);
if (!currentBook) {
    alert('Kitap bulunamadı');
    return;
}

const stockChange = prompt(`Mevcut stok: ${currentBook.stock || 0} \n Stok değişim miktarını girin (+ veya -):`);
if(stockChange === null || stockChange === '') return;

const changeAmount = parseFloat(stockChange);
if (isNaN(changeAmount)) {
    alert('Geçerli bir sayı girin');
    return;
}

try {
        const formData = new FormData();
        formData.append('bookId', id);  // Burada API'de 'bookId' bekleniyor olabilir
        formData.append('changeAmount', changeAmount.toString());

        const response = await fetch('/api/books/stock', {
            method: 'POST',
            body: formData
        });
    
    if (response.ok) {
        loadBooks();
    } else {
        alert('Stok güncellenirken hata oluştu');
    }
} catch (error) {
    console.error('Stok güncelleme hatası:', error);
    alert('Stok güncellenirken hata oluştu');
}
}

async function getCurrentBook(id) {
try {
    const response = await fetch('/api/books/' + encodeURIComponent(id));
    if (response.ok) {
        return await response.json();
    }
    return null;
} catch (error) {
    console.error('Kitap bilgisi alınamadı:', error);
    return null;
}
}


async function loadCategories() {
    try {
        const response = await fetch('/api/books/categories');
        const categories = await response.json();
        const categorySelect = document.getElementById('searchCategory');

        categorySelect.innerHTML = '<option value="">Tüm Kategoriler</option>';
        
        categories.forEach(category => {
            const option = document.createElement('option');
            option.value = category;
            option.textContent = category;
            categorySelect.appendChild(option);
        });
    } catch (error) {
        console.error('Kategoriler yüklenirken hata:', error);
    }
}

async function searchBooks() {
    try {
        document.getElementById('books-container').innerHTML = '<div class="loading">Arama yapılıyor...</div>';
        
        const query = document.getElementById('searchQuery')?.value || '';
        const author = document.getElementById('searchAuthor')?.value || '';
        const category = document.getElementById('searchCategory')?.value || '';
        const minPrice = document.getElementById('minPrice')?.value || '0';
        const maxPrice = document.getElementById('maxPrice')?.value || '';
        const sortBy = document.getElementById('sortBy')?.value || '';
        const sortOrder = document.getElementById('sortOrder')?.value || 'asc';
        const params = new URLSearchParams({
            query,
            author,
            category,
            minPrice,
            ...(maxPrice && { maxPrice }),
            sortBy,
            sortOrder
        });
        const response = await fetch(`/api/books/search?${params}`); 
        
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Server error: ${response.status} - ${errorText}`);
        }
        
        const result = await response.json();
        const books = Array.isArray(result) ? result : [];
        
        displayBooks(books);
        
        const resultCount = books.length;
        if (resultCount > 0) {
            const container = document.getElementById('books-container');
            const currentContent = container.innerHTML;
            container.innerHTML = `
                <div style="text-align: center; margin-bottom: 20px; color: #10b981; font-weight: 600;">
                    📚 ${resultCount} kitap bulundu
                </div>
                ${currentContent}
            `;
        }
        
    } catch (error) {
        console.error('Arama hatası:', error);
        document.getElementById('books-container').innerHTML = `
            <div class="loading">❌ Arama sırasında hata oluştu: ${error.message}</div>
        `;
    }
}

function clearFilters() {
    document.getElementById('searchQuery').value = '';
    document.getElementById('searchAuthor').value = '';
    document.getElementById('searchCategory').value = '';
    document.getElementById('minPrice').value = '';
    document.getElementById('maxPrice').value = '';
    document.getElementById('sortBy').value = '';
    document.getElementById('sortOrder').value = 'asc';
    
    // Tüm kitapları tekrar yükle
    loadBooks();
}


function setupRealTimeSearch() {
    const searchInputs = ['searchQuery', 'searchAuthor', 'minPrice', 'maxPrice'];
    const selects = ['searchCategory', 'sortBy', 'sortOrder'];
    
    searchInputs.forEach(id => {
        const input = document.getElementById(id);
        if (input) {
            let timeout;
            input.addEventListener('input', () => {
                clearTimeout(timeout);
                timeout = setTimeout(searchBooks, 500);
            });
        }
    });
    
    selects.forEach(id => {
        const select = document.getElementById(id);
        if (select) {
            select.addEventListener('change', searchBooks);
        }
    });
}

// Sayfa yüklendikten sonra gerçek zamanlı aramayı aktive et
window.addEventListener('load', () => {
    setTimeout(setupRealTimeSearch, 1000);
});

async function initializePage() {
    await loadBooks();
    await loadCategories();
}

window.onload = initializePage;
</script>
</body>
</html>
""";
}


string GetUserForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>📚 Book Store</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: 'Segoe UI', sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; padding: 20px; }
.container { max-width: 1200px; margin: 0 auto; background: white; border-radius: 20px; box-shadow: 0 20px 40px rgba(0,0,0,0.1); overflow: hidden; }
.header { background: linear-gradient(135deg, #4f46e5, #7c3aed); color: white; padding: 40px; text-align: center; }
.header h1 { font-size: 3rem; margin-bottom: 10px; text-shadow: 2px 2px 4px rgba(0,0,0,0.3); }
.content { padding: 40px; }
.buttons { display: flex; gap: 20px; justify-content: center; margin-bottom: 40px; flex-wrap: wrap; }
.btn { padding: 12px 25px; border: none; border-radius: 50px; font-size: 1rem; cursor: pointer; text-decoration: none; display: inline-flex; align-items: center; gap: 8px; transition: all 0.3s ease; font-weight: 600; }
.btn-primary { background: linear-gradient(135deg, #4f46e5, #7c3aed); color: white; }
.btn-secondary { background: linear-gradient(135deg, #06b6d4, #0891b2); color: white; }

.btn-third { 
  background: linear-gradient(135deg, #22c55e, #16a34a); 
  color: white; 
}
.btn-third:hover {
  transform: translateY(-2px);
  box-shadow: 0 10px 20px rgba(0,0,0,0.25);
  background: linear-gradient(135deg, #16a34a, #15803d);
}

.btn:hover { transform: translateY(-2px); box-shadow: 0 10px 20px rgba(0,0,0,0.2); }
.books-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; margin-top: 30px; }
.book-card { position: relative; background: white; border-radius: 15px; padding: 20px; box-shadow: 0 5px 15px rgba(0,0,0,0.1); border-left: 5px solid #4f46e5; transition: all 0.3s ease; display: flex; flex-direction: column; justify-content: space-between; height: 280px; }
.book-card:hover { transform: translateY(-5px); box-shadow: 0 15px 30px rgba(0,0,0,0.2); }
.book-info { flex-grow: 1; }
.card-actions { display: flex; justify-content: flex-start; gap: 10px; margin-top: auto; }
.loading { text-align: center; padding: 40px; font-size: 1.2rem; color: #666; }
.message { padding: 15px; border-radius: 8px; margin: 10px 0; text-align: center; font-weight: 600; }
.success { background: #22c55e; color: white; }
.error { background: #dc2626; color: white; }
.discount-info {
background: linear-gradient(135deg, #22c55e, #16a34a); 
color: white; 
padding: 4px 8px;
border-radius: 4px;
font-size: 0.8rem;
margin-top: 5px;
display: inline-block;
}
</style>
</head>
<body>
<div class="container">
<div class="header">
<h1>📚 Book Store</h1>
<p>Elasticsearch ile güçlendirilmiş kitap yönetim sistemi</p>
</div>
<div class="content">
<div id="message-container"></div>
<div class="buttons">
<a href="/sepet" class="btn btn-primary">🧺 Sepet</a>
<a href="/wishlist" class="btn btn-primary">💝 İstek Listesi</a>
<a href="/user-ai-chat" class="btn btn-secondary">🤖 AI Yardım</a>
<button onclick="loadBooks()" class="btn btn-secondary">🔄 Kitapları Yenile</button>
<a href="/" class="btn btn-secondary">🔑 Giriş Sayfasına Dön</a>
</div>

<div class="search-filters" style="background: #f8fafc; padding: 25px; border-radius: 15px; margin-bottom: 30px;">
    <h3 style="text-align: center; margin-bottom: 20px; color: #374151;">🔍 Arama ve Filtreleme</h3>
    
    <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin-bottom: 20px;">
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Kitap Adı</label>
            <input type="text" id="searchQuery" placeholder="Kitap adı ara..." style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Yazar</label>
            <input type="text" id="searchAuthor" placeholder="Yazar adı..." style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Kategori</label>
            <select id="searchCategory" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="">Tüm Kategoriler</option>
            </select>
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Min Fiyat</label>
            <input type="number" id="minPrice" placeholder="0" min="0" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Max Fiyat</label>
            <input type="number" id="maxPrice" placeholder="1000" min="0" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Sıralama</label>
            <select id="sortBy" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="">Varsayılan</option>
                <option value="title">Kitap Adı</option>
                <option value="author">Yazar</option>
                <option value="price">Fiyat</option>
                <option value="stock">Stok</option>
            </select>
        </div>
        
        <div>
            <label style="display: block; margin-bottom: 5px; font-weight: 600; color: #374151;">Sıra</label>
            <select id="sortOrder" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="asc">A-Z (Artan)</option>
                <option value="desc">Z-A (Azalan)</option>
            </select>
        </div>
        
        <div style="display: flex; align-items: end; gap: 10px;">
            <button onclick="searchBooks()" class="btn btn-primary" style="margin: 0;">🔍 Ara</button>
            <button onclick="clearFilters()" class="btn btn-secondary" style="margin: 0; background: #6b7280;">🗑️ Temizle</button>
        </div>
    </div>
</div>

<div id="books-container">
<div class="loading">Kitaplar yükleniyor...</div>
</div>
</div>
</div>

<script>
function showMessage(message, type) {
    const messageContainer = document.getElementById('message-container');
    const messageClass = type === 'success' ? 'success' : 'error';
    messageContainer.innerHTML = `<div class="message ${messageClass}">${message}</div>`;
    
    setTimeout(() => {
        messageContainer.innerHTML = '';
    }, 5000);
}

async function loadBooks() {
    try {
        document.getElementById('books-container').innerHTML = '<div class="loading">Kitaplar yükleniyor...</div>';
        const response = await fetch('/api/books');
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const books = await response.json();
        
        if (Array.isArray(books)) {
            displayBooks(books);
        } else {
            throw new Error('Unexpected data format received from server');
        }
        
    } catch (error) {
        console.error('Kitap yükleme hatası:', error);
        document.getElementById('books-container').innerHTML = '<div class="loading">❌ Kitaplar yüklenirken hata oluştu</div>';
    }
}

function displayBooks(books) {
    const container = document.getElementById('books-container');
    
    if (!Array.isArray(books)) {
        console.error('Expected array but got:', books);
        container.innerHTML = '<div class="loading">❌ Veri formatı hatalı</div>';
        return;
    }
    
    if (books.length === 0) {
        container.innerHTML = '<div class="loading">📚 Arama kriterlerinize uygun kitap bulunamadı</div>';
        return;
    }

    const booksHtml = books.map(book => `
        <div class="book-card">
            <div class="book-info">
                <h3 style="color: #4f46e5; margin-bottom: 10px;">${escapeHtml(book.title || '')}</h3>
                <p><strong>Yazar:</strong> ${escapeHtml(book.author || '')}</p>
                <p><strong>Fiyat:</strong> ${book.price || 0}₺</p>
                <p><strong>Kategori:</strong> ${escapeHtml(book.category || '')}</p>
                <p><strong>Stok:</strong> ${book.stock || 0}</p>
                ${book.discountrate ? `<div class="discount-info">%${book.discountrate} İndirim</div>` : ''}
            </div>
            <div class="card-actions"> 
            <button class="btn btn-primary" onclick="addToWishlist('${book.id}' ,'${escapeHtml(book.title)}')">İstek Listesine Ekle</button>
            <button class="btn btn-secondary" onclick="addToCart('${book.id}', 1)">Sepete Ekle</button> 
            <button class="btn btn-third" onclick="buy('${escapeHtml(book.title)}')">Satın Al</button> 
            </div>
        </div>
    `).join('');

    container.innerHTML = `<div class="books-grid">${booksHtml}</div>`;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Yeni API ile sepete ekleme fonksiyonu
async function addToCart(productId, quantity = 1) {
    try {
        const formData = new FormData();
        formData.append('productId', productId);
        formData.append('quantity', quantity.toString());

        const response = await fetch('/api/cart/add', {
            method: 'POST',
            body: formData,
            credentials: 'same-origin' // Session için gerekli
        });

        const message = await response.text();

        if (response.ok) {
            showMessage(message, 'success');
        } else {
            showMessage(message, 'error');
        }

    } catch (error) {
        console.error('Sepete ekleme hatası:', error);
        showMessage('❌ Sepete eklenirken hata oluştu!', 'error');
    }
}

async function buy(bookTitle) {
    try {
        const response = await fetch('/api/books');
        const books = await response.json();
        const book = books.find(b => b.title === bookTitle);
        
        if (!book) {
            showMessage('❌ Kitap bulunamadı!', 'error');
            return;
        }
        
        if (book.stock < 1) {
            showMessage('❌ Bu kitap stokta yok!', 'error');
            return;
        }
        
        const confirmPurchase = confirm(`${book.title} kitabını satın almak istediğinize emin misiniz?\n\nFiyat: ${book.price}₺\nMevcut Stok: ${book.stock}`);
        if (!confirmPurchase) {
            return;
        }
        
        const purchaseResponse = await fetch('/api/books/purchase', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                bookId: book.id,
                title: book.title,
                quantity: 1
            }),
            credentials: 'same-origin'
        });
        
        if (purchaseResponse.ok) {
            const result = await purchaseResponse.json();
            showMessage(`✅ ${book.title} başarıyla satın alındı!\n\nKalan stok: ${result.remainingStock}`, 'success');
            
            if (typeof loadBooks === 'function') {
                loadBooks();
            } else if (typeof searchBooks === 'function') {
                searchBooks();
            }
        } else {
            const errorText = await purchaseResponse.text();
            showMessage(`❌ Satın alma başarısız: ${errorText}`, 'error');
        }
        
    } catch (error) {
        console.error('Satın alma hatası:', error);
        showMessage('❌ Satın alma sırasında bir hata oluştu!', 'error');
    }
}

async function loadCategories() {
    try {
        const response = await fetch('/api/books/categories');
        const categories = await response.json();
        const categorySelect = document.getElementById('searchCategory');
        
        categorySelect.innerHTML = '<option value="">Tüm Kategoriler</option>';
        
        categories.forEach(category => {
            const option = document.createElement('option');
            option.value = category;
            option.textContent = category;
            categorySelect.appendChild(option);
        });
    } catch (error) {
        console.error('Kategoriler yüklenirken hata:', error);
    }
}

async function searchBooks() {
    try {
        document.getElementById('books-container').innerHTML = '<div class="loading">Arama yapılıyor...</div>';
        
        const query = document.getElementById('searchQuery')?.value || '';
        const author = document.getElementById('searchAuthor')?.value || '';
        const category = document.getElementById('searchCategory')?.value || '';
        const minPrice = document.getElementById('minPrice')?.value || '0';
        const maxPrice = document.getElementById('maxPrice')?.value || '';
        const sortBy = document.getElementById('sortBy')?.value || '';
        const sortOrder = document.getElementById('sortOrder')?.value || 'asc';
        
        const params = new URLSearchParams({
            query,
            author,
            category,
            minPrice,
            ...(maxPrice && { maxPrice }),
            sortBy,
            sortOrder
        });
        
        const response = await fetch(`/api/books/search?${params}`);
        
        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Server error: ${response.status} - ${errorText}`);
        }
        
        const result = await response.json();
        const books = Array.isArray(result) ? result : [];
        
        displayBooks(books);
        
        const resultCount = books.length;
        if (resultCount > 0) {
            const container = document.getElementById('books-container');
            const currentContent = container.innerHTML;
            container.innerHTML = `
                <div style="text-align: center; margin-bottom: 20px; color: #10b981; font-weight: 600;">
                    📚 ${resultCount} kitap bulundu
                </div>
                ${currentContent}
            `;
        }
        
    } catch (error) {
        console.error('Arama hatası:', error);
        document.getElementById('books-container').innerHTML = `
            <div class="loading">❌ Arama sırasında hata oluştu: ${error.message}</div>
        `;
    }
}

function clearFilters() {
    document.getElementById('searchQuery').value = '';
    document.getElementById('searchAuthor').value = '';
    document.getElementById('searchCategory').value = '';
    document.getElementById('minPrice').value = '';
    document.getElementById('maxPrice').value = '';
    document.getElementById('sortBy').value = '';
    document.getElementById('sortOrder').value = 'asc';
    
    loadBooks();
}

function setupRealTimeSearch() {
    const searchInputs = ['searchQuery', 'searchAuthor', 'minPrice', 'maxPrice'];
    const selects = ['searchCategory', 'sortBy', 'sortOrder'];
    
    searchInputs.forEach(id => {
        const input = document.getElementById(id);
        if (input) {
            let timeout;
            input.addEventListener('input', () => {
                clearTimeout(timeout);
                timeout = setTimeout(searchBooks, 500);
            });
        }
    });
    
    selects.forEach(id => {
        const select = document.getElementById(id);
        if (select) {
            select.addEventListener('change', searchBooks);
        }
    });
}

async function addToWishlist(productId , productTitle) {
    try {
        const formData = new FormData();
        formData.append('productId', productId);
        formData.append('productTitle', productTitle);

        const response = await fetch('/api/wishlist/add', {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        });
        
        const message = await response.text();
        showMessage(response.ok ? message : message, response.ok ? 'success' : 'error');
    } catch (error) {
        showMessage('İstek listesine eklenirken hata oluştu!', 'error');
    }
}

window.addEventListener('load', () => {
    setTimeout(setupRealTimeSearch, 1000);
});

async function initializePage() {
    await loadBooks();
    await loadCategories();
}

window.onload = initializePage;
</script>
</body>
</html>
""";
}


string GetCartForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>🧺 Sepetim</title>
<style>
body { font-family: 'Segoe UI', sans-serif; background: #f9fafb; padding: 20px; }
.container { max-width: 800px; margin: 0 auto; background: white; border-radius: 15px; padding: 20px; box-shadow: 0 5px 20px rgba(0,0,0,0.1); }
h1 { color: #4f46e5; text-align: center; }
.cart-item { 
    display: flex; 
    justify-content: space-between; 
    align-items: center;
    padding: 15px; 
    border-bottom: 1px solid #eee; 
    background: #f8fafc;
    margin-bottom: 10px;
    border-radius: 8px;
}
.cart-item:last-child { border-bottom: none; }
.item-info { flex-grow: 1; }
.item-actions { display: flex; gap: 10px; align-items: center; }
.quantity-controls { display: flex; align-items: center; gap: 10px; }
.quantity-btn { 
    background: #4f46e5; 
    color: white; 
    border: none; 
    width: 30px; 
    height: 30px; 
    border-radius: 50%; 
    cursor: pointer; 
    font-weight: bold;
}
.quantity-btn:hover { background: #4338ca; }
.quantity-btn:disabled { 
    background: #9ca3af; 
    cursor: not-allowed; 
}
.quantity-display { 
    background: #e5e7eb; 
    padding: 5px 15px; 
    border-radius: 5px; 
    font-weight: bold;
    min-width: 40px;
    text-align: center;
}
.total { text-align: right; margin-top: 20px; font-size: 1.2rem; font-weight: bold; }
.btn { padding: 10px 20px; border: none; border-radius: 8px; background: #4f46e5; color: white; cursor: pointer; text-decoration: none; display: inline-block; }
.btn-primary { background: linear-gradient(135deg, #4f46e5, #7c3aed); color: white; margin-top: 20px; }
.btn:hover { background: #4338ca; }
.btn:disabled { 
    background: #9ca3af; 
    cursor: not-allowed; 
    transform: none;
}
.btn-third { 
    padding: 12px 25px;
    background: linear-gradient(135deg, #22c55e, #16a34a); 
    color: white; 
}
.btn-third:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 12px 25px rgba(0,0,0,0.25);
  background: linear-gradient(135deg, #16a34a, #15803d);
}
.btn-danger {
    padding: 8px 15px;
    border: none;
    border-radius: 6px;
    background: #dc2626;
    color: white;
    cursor: pointer;
    font-size: 0.9rem;
}
.btn-danger:hover:not(:disabled) { background: #b91c1c; }
.btn-danger:disabled { 
    background: #9ca3af; 
    cursor: not-allowed; 
}
.actions { display: flex; justify-content: space-between; margin-top: 20px; }
.empty-cart {
    text-align: center;
    font-size: 1.3rem;
    color: #6b7280;
    margin: 40px 0;
    font-weight: 500;
}
.loading {
    text-align: center;
    padding: 20px;
    font-size: 1.1rem;
    color: #6b7280;
}
.success-message {
    background: #22c55e;
    color: white;
    padding: 15px;
    border-radius: 8px;
    margin: 20px 0;
    text-align: center;
    font-weight: 600;
}
.error-message {
    background: #dc2626;
    color: white;
    padding: 15px;
    border-radius: 8px;
    margin: 20px 0;
    text-align: center;
    font-weight: 600;
}
</style>
</head>
<body>
<div class="container">
<h1>🧺 Sepetim</h1>
<div id="message-container"></div>
<div id="cart-container">
    <div class="loading">Sepet yükleniyor...</div>
</div>
<div class="total" id="cart-total"></div>

<div class="actions">
    <button class="btn btn-third" onclick="buyAll()" id="buy-button">✅ Tümünü Satın Al</button>
    <button class="btn btn-danger" onclick="clearCart()" id="clear-button">🗑️ Sepeti Boşalt</button>
</div>

<div style="text-align:center;">
    <a href="/books" class="btn btn-primary">⬅ Alışverişe Devam Et</a>
</div>
</div>

<script>
let cartData = [];
let booksData = {}; 

async function loadBooks() {
    try {
        const response = await fetch('/api/books');
        if (response.ok) {
            const books = await response.json();
            // Kitapları ID'ye göre indexle
            booksData = {};
            books.forEach(book => {
                booksData[book.id] = book;
            });
        }
    } catch (error) {
        console.error('Kitaplar yüklenirken hata:', error);
    }
}

async function loadCart() {
    try {
        const container = document.getElementById("cart-container");
        const totalDiv = document.getElementById("cart-total");
        
        container.innerHTML = '<div class="loading">Sepet yükleniyor...</div>';

        
        await loadBooks();

        const response = await fetch('/api/cart', {
            credentials: 'same-origin'
        });

        if (!response.ok) {
            if (response.status === 401) {
                showMessage("❌ Giriş yapmalısınız!", "error");
                setTimeout(() => {
                    window.location.href = '/';
                }, 2000);
                return;
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        cartData = await response.json();

        if (!Array.isArray(cartData) || cartData.length === 0) {
            container.innerHTML = '<p class="empty-cart">🛒 Sepetiniz boş</p>';
            totalDiv.innerHTML = "";
            return;
        }

        displayCart(cartData);
        
    } catch (error) {
        console.error('Sepet yükleme hatası:', error);
        document.getElementById("cart-container").innerHTML = 
            '<div class="loading">❌ Sepet yüklenirken hata oluştu</div>';
    }
}

function displayCart(cart) {
    const container = document.getElementById("cart-container");
    const totalDiv = document.getElementById("cart-total");

    let html = "";
    let total = 0;

    cart.forEach(item => {
        
        const book = booksData[item.productId] || {};
        const price = book.price || 0;
        const title = book.title || 'Bilinmeyen Kitap';
        const author = book.author || 'Bilinmeyen Yazar';
        const category = book.category || 'Kategori Yok';
        
        const itemTotal = price * (item.quantity || 0);
        total += itemTotal;
        
        html += `
        <div class="cart-item" id="cart-item-${item.id}">
            <div class="item-info">
                <h4 style="color: #4f46e5; margin-bottom: 5px;">${escapeHtml(title)}</h4>
                <p><strong>Yazar:</strong> ${escapeHtml(author)}</p>
                <p><strong>Kategori:</strong> ${escapeHtml(category)}</p>
                <p><strong>Birim Fiyat:</strong> ${price.toFixed(2)}₺</p>
                <p><strong>Adet Toplamı:</strong> ${itemTotal.toFixed(2)}₺</p>
            </div>
            <div class="item-actions">
                <div class="quantity-controls">
                    <button class="quantity-btn" onclick="updateQuantity('${item.productId}', getNewQuantity('${item.productId}', -1))"}>-</button>
                    <div class="quantity-display">${item.quantity || 0}</div>
                    <button class="quantity-btn" onclick="updateQuantity('${item.productId}', getNewQuantity('${item.productId}', 1))">+</button>
                </div>
                <button class="btn-danger" onclick="removeFromCart('${item.id}')">🗑️ Sil</button>
            </div>
        </div>`;
    });

    container.innerHTML = html;
    totalDiv.innerHTML = `<strong>Genel Toplam: ${total.toFixed(2)}₺</strong>`;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function getNewQuantity(productId, delta) {
    const item = cartData.find(i => i.productId === productId);
    return (item?.quantity || 0) + delta;
}


async function updateQuantity(productId, newQuantity) {
    try {
        const buttons = document.querySelectorAll('.quantity-btn');
        buttons.forEach(btn => btn.disabled = true);

        const formData = new FormData();
        formData.append('productId', productId);
        formData.append('quantity', newQuantity.toString());

        const response = await fetch('/api/cart/update', {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        });

        const message = await response.text();

        if (response.ok) {
            await loadCart();
            showMessage("✅ Miktar güncellendi!", "success");
            await loadCart();
        } else {
            showMessage(message, "error");
        }
        
    

    } catch (error) {
        console.error('Miktar güncelleme hatası:', error);
        showMessage('❌ Miktar güncellenirken hata oluştu!', 'error');
    } finally {
        const buttons = document.querySelectorAll('.quantity-btn');
        buttons.forEach(btn => btn.disabled = false);
    }
}


async function removeFromCart(cartItemId) {
    try {
        const button = document.getElementById('clear-button');
        button.disabled = true;

        const formData = new FormData();
        formData.append('id', cartItemId);

        const response = await fetch('/api/cart/remove', {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        });

        const message = await response.text();

        if (response.ok) {
            await loadCart();
            showMessage(message, "success");
            await loadCart();
        } else {
            showMessage(message, "error");
        }
        

    } catch (error) {
        console.error('Ürün silme hatası:', error);
        showMessage('❌ Ürün silinirken hata oluştu!', 'error');
    } finally {
        const button = document.getElementById('clear-button');
        button.disabled = false;
    }
}

async function clearCart() {
    if (!confirm("Sepetinizdeki tüm ürünleri silmek istediğinize emin misiniz?")) {
        return;
    }

    try {
        const clearButton = document.getElementById('clear-button');
        clearButton.disabled = true;
        clearButton.innerHTML = '⏳ Temizleniyor...';

        
        for (const item of cartData) {
            const formData = new FormData();
            formData.append('id', item.id);

            await fetch('/api/cart/clear', {
                method: 'POST',
                body: formData,
                credentials: 'same-origin'
            });
        }

        await loadCart();
        showMessage("🗑️ Sepet temizlendi!", "success");

    } catch (error) {
        console.error('Sepet temizleme hatası:', error);
        showMessage('❌ Sepet temizlenirken hata oluştu!', 'error');
    } finally {
        const clearButton = document.getElementById('clear-button');
        clearButton.disabled = false;
        clearButton.innerHTML = '🗑️ Sepeti Boşalt';
    }
}

function showMessage(message, type) {
    const messageContainer = document.getElementById("message-container");
    const messageClass = type === "success" ? "success-message" : "error-message";
    messageContainer.innerHTML = `<div class="${messageClass}">${message}</div>`;
    
    setTimeout(() => {
        messageContainer.innerHTML = "";
    }, 5000);
}

async function buyAll() {
    if (!Array.isArray(cartData) || cartData.length === 0) {
        showMessage("Sepetiniz zaten boş!", "error");
        return;
    }

    const buyButton = document.getElementById("buy-button");
    const originalText = buyButton.innerHTML;
    buyButton.innerHTML = "⏳ İşleniyor...";
    buyButton.disabled = true;

    try {
        
        for (const item of cartData) {
            const book = booksData[item.productId] || {};
            
            const response = await fetch('/api/books/purchase', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    bookId: item.productId,
                    title: book.title || 'Bilinmeyen Kitap',
                    quantity: item.quantity
                }),
                credentials: 'same-origin'
            });

            if (!response.ok) {
                const errorData = await response.text();
                throw new Error(`${book.title || 'Bilinmeyen Kitap'}: ${errorData}`);
            }
        }

        
        await clearCart();
        await loadCart();
        showMessage("✅ Tüm kitaplar başarıyla satın alındı!", "success");
        

    } catch (error) {
        console.error('Satın alma hatası:', error);
        showMessage(`❌ Satın alma sırasında hata: ${error.message}`, "error");
    } finally {
        buyButton.innerHTML = originalText;
        buyButton.disabled = false;
    }
}

async function checkStock() {
    if (!Array.isArray(cartData) || cartData.length === 0) return;

    try {
        for (const item of cartData) {
            const book = booksData[item.productId];
            if (book && book.stock < item.quantity) {
                showMessage(`⚠️ ${book.title} için yeterli stok yok! Mevcut stok: ${book.stock}, Sepetinizde: ${item.quantity}`, "error");
            }
        }
    } catch (error) {
        console.error('Stok kontrol hatası:', error);
    }
}

async function initializePage() {
    await loadCart();
    await checkStock();
}

window.onload = initializePage;
</script>
</body>
</html>
""";
}

string GetWishlistPage()
{
    return """
    <!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>💝 İstek Listem</title>
<style>
body { 
    font-family: 'Segoe UI', sans-serif; 
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
    min-height: 100vh; 
    padding: 20px; 
}
.container { 
    max-width: 1000px; 
    margin: 0 auto; 
    background: white; 
    border-radius: 20px; 
    box-shadow: 0 20px 40px rgba(0,0,0,0.1); 
    overflow: hidden; 
}
.header { 
    background: linear-gradient(135deg, #e91e63, #ad1457); 
    color: white; 
    padding: 40px; 
    text-align: center; 
}
.header h1 { 
    font-size: 3rem; 
    margin-bottom: 10px; 
    text-shadow: 2px 2px 4px rgba(0,0,0,0.3); 
}
.content { 
    padding: 40px; 
}
.buttons { 
    display: flex; 
    gap: 20px; 
    justify-content: center; 
    margin-bottom: 40px; 
    flex-wrap: wrap; 
}
.btn { 
    padding: 12px 25px; 
    border: none; 
    border-radius: 50px; 
    font-size: 1rem; 
    cursor: pointer; 
    text-decoration: none; 
    display: inline-flex; 
    align-items: center; 
    gap: 8px; 
    transition: all 0.3s ease; 
    font-weight: 600; 
}
.btn-primary { 
    background: linear-gradient(135deg, #4f46e5, #7c3aed); 
    color: white; 
}
.btn-secondary { 
    background: linear-gradient(135deg, #06b6d4, #0891b2); 
    color: white; 
}
.btn-success { 
    background: linear-gradient(135deg, #22c55e, #16a34a); 
    color: white; 
}
.btn-danger {
    background: linear-gradient(135deg, #dc2626, #b91c1c);
    color: white;
}
.btn:hover { 
    transform: translateY(-2px); 
    box-shadow: 0 10px 20px rgba(0,0,0,0.2); 
}
.btn:disabled { 
    background: #9ca3af; 
    cursor: not-allowed; 
    transform: none;
}
.wishlist-grid { 
    display: grid; 
    grid-template-columns: repeat(auto-fill, minmax(350px, 1fr)); 
    gap: 25px; 
    margin-top: 30px; 
}
.wishlist-card { 
    background: white; 
    border-radius: 15px; 
    padding: 25px; 
    box-shadow: 0 5px 15px rgba(0,0,0,0.1); 
    border-left: 5px solid #e91e63; 
    transition: all 0.3s ease; 
    display: flex; 
    flex-direction: column; 
    justify-content: space-between; 
    min-height: 300px; 
}
.wishlist-card:hover { 
    transform: translateY(-5px); 
    box-shadow: 0 15px 30px rgba(0,0,0,0.2); 
}
.book-info { 
    flex-grow: 1; 
    margin-bottom: 20px;
}
.book-title {
    color: #e91e63;
    font-size: 1.3rem;
    font-weight: bold;
    margin-bottom: 15px;
    line-height: 1.3;
}
.book-details {
    color: #374151;
    line-height: 1.6;
}
.book-details p {
    margin: 8px 0;
}
.price-section {
    background: #f8fafc;
    padding: 15px;
    border-radius: 10px;
    margin: 15px 0;
    text-align: center;
}
.original-price {
    text-decoration: line-through;
    color: #9ca3af;
    font-size: 0.9rem;
    margin-right: 10px;
}
.current-price {
    color: #059669;
    font-size: 1.4rem;
    font-weight: bold;
}
.discount-badge {
    background: linear-gradient(135deg, #22c55e, #16a34a); 
    color: white; 
    padding: 4px 8px;
    border-radius: 15px;
    font-size: 0.8rem;
    font-weight: bold;
    margin-left: 10px;
}
.card-actions { 
    display: flex; 
    justify-content: space-between; 
    gap: 10px; 
    margin-top: auto; 
}
.empty-wishlist {
    text-align: center;
    font-size: 1.3rem;
    color: #6b7280;
    margin: 60px 0;
    font-weight: 500;
}
.loading {
    text-align: center;
    padding: 40px;
    font-size: 1.2rem;
    color: #666;
}
.message { 
    padding: 15px; 
    border-radius: 8px; 
    margin: 10px 0; 
    text-align: center; 
    font-weight: 600; 
}
.success { 
    background: #22c55e; 
    color: white; 
}
.error { 
    background: #dc2626; 
    color: white; 
}
.actions-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 30px;
    padding: 20px;
    background: #f8fafc;
    border-radius: 15px;
}
</style>
</head>
<body>
<div class="container">
<div class="header">
<h1>💝 İstek Listem</h1>
<p>Beğendiğiniz kitapları buraya ekleyip daha sonra inceleyebilirsiniz</p>
</div>
<div class="content">
<div id="message-container"></div>

<div class="buttons">
<a href="/sepet" class="btn btn-secondary">🧺 Sepetim</a>
<button onclick="loadWishlist()" class="btn btn-secondary">🔄 Yenile</button>
<a href="/books" class="btn btn-primary">📚 Kitaplara Dön</a>
</div>

<div class="actions-bar">
    <div>
        <strong id="wishlist-count">0 ürün</strong> istek listenizde
    </div>
    <button class="btn btn-danger" onclick="clearWishlist()" id="clear-button">🗑️ Tümünü Temizle</button>
</div>

<div id="wishlist-container">
<div class="loading">İstek listesi yükleniyor...</div>
</div>
</div>
</div>

<script>
let wishlistData = [];
let booksData = {};

function showMessage(message, type) {
    const messageContainer = document.getElementById('message-container');
    const messageClass = type === 'success' ? 'success' : 'error';
    messageContainer.innerHTML = `<div class="message ${messageClass}">${message}</div>`;
    
    setTimeout(() => {
        messageContainer.innerHTML = '';
    }, 5000);
}

async function loadBooks() {
    try {
        const response = await fetch('/api/books');
        if (response.ok) {
            const books = await response.json();
            booksData = {};
            books.forEach(book => {
                booksData[book.id] = book;
            });
        }
    } catch (error) {
        console.error('Kitaplar yüklenirken hata:', error);
    }
}

async function loadWishlist() {
    try {
        const container = document.getElementById("wishlist-container");
        const countElement = document.getElementById("wishlist-count");
        
        container.innerHTML = '<div class="loading">İstek listesi yükleniyor...</div>';

        await loadBooks();

        const response = await fetch('/api/wishlist', {
            credentials: 'same-origin'
        });

        if (!response.ok) {
            if (response.status === 401) {
                showMessage("❌ Giriş yapmalısınız!", "error");
                setTimeout(() => {
                    window.location.href = '/';
                }, 2000);
                return;
            }
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        wishlistData = await response.json();

        if (!Array.isArray(wishlistData) || wishlistData.length === 0) {
            container.innerHTML = '<div class="empty-wishlist">💝 İstek listeniz boş<br><small>Beğendiğiniz kitapları buraya ekleyebilirsiniz</small></div>';
            countElement.textContent = "0 ürün";
            return;
        }

        displayWishlist(wishlistData);
        countElement.textContent = `${wishlistData.length} ürün`;
        
    } catch (error) {
        console.error('İstek listesi yükleme hatası:', error);
        document.getElementById("wishlist-container").innerHTML = 
            '<div class="loading">❌ İstek listesi yüklenirken hata oluştu</div>';
    }
}

function displayWishlist(wishlist) {
    const container = document.getElementById("wishlist-container");

    const wishlistHtml = wishlist.map(item => {
        const book = booksData[item.productId] || {};
        const title = book.title || 'Bilinmeyen Kitap';
        const author = book.author || 'Bilinmeyen Yazar';
        const category = book.category || 'Kategori Yok';
        const price = book.price || 0;
        const realprice = book.realPrice || 0;
        const discountRate = book.discountRate || 0;
        const stock = book.stock || 0;
        
        // İndirim hesaplama
        const discountedPrice = discountRate > 0 ? price * (1 - discountRate / 100) : price;
        const hasDiscount = discountRate > 0;
        
        return `
        <div class="wishlist-card">
            <div class="book-info">
                <div class="book-title">${escapeHtml(title)}</div>
                <div class="book-details">
                    <p><strong>Yazar:</strong> ${escapeHtml(author)}</p>
                    <p><strong>Kategori:</strong> ${escapeHtml(category)}</p>
                    <p><strong>Stok:</strong> ${stock > 0 ? `${stock} adet` : '<span style="color: #dc2626;">Stokta Yok</span>'}</p>
                    
                </div>
                
                <div class="price-section">
                    ${book.discountrate ? `<span class="original-price">${realprice.toFixed(2)}₺</span>` : ''}
                    <span class="current-price">${discountedPrice.toFixed(2)}₺</span>
                    ${book.discountrate ? `<span class="discount-badge">%${book.discountrate} İndirim</span>` : ''}
                    
                </div>
            </div>
            
            <div class="card-actions">
                <button class="btn btn-success" onclick="addToCartFromWishlist('${item.productId}')" ${stock < 1 ? 'disabled' : ''}>
                    🧺 Sepete Ekle
                </button>
                <button class="btn btn-danger" onclick="removeFromWishlist('${item.productId}')">
                    🗑️ Kaldır
                </button>
            </div>
        </div>`;
    }).join('');

    container.innerHTML = `<div class="wishlist-grid">${wishlistHtml}</div>`;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

async function addToCartFromWishlist(productId) {
    try {
        const formData = new FormData();
        formData.append('productId', productId);
        formData.append('quantity', '1');

        const response = await fetch('/api/cart/add', {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        });

        const message = await response.text();

        if (response.ok) {
            showMessage("✅ Ürün sepete eklendi!", "success");
        } else {
            showMessage(message, "error");
        }

    } catch (error) {
        console.error('Sepete ekleme hatası:', error);
        showMessage('❌ Sepete eklenirken hata oluştu!', 'error');
    }
}

async function removeFromWishlist(productId) {
    try {
        const formData = new FormData();
        formData.append('productId', productId);

        const response = await fetch('/api/wishlist/remove', {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        });

        const message = await response.text();

        if (response.ok) {
            await loadWishlist();
            showMessage(message, "success");
            await loadWishlist();
        } else {
            showMessage(message, "error");
        }


    } catch (error) {
        console.error('Ürün silme hatası:', error);
        showMessage('❌ Ürün silinirken hata oluştu!', 'error');
    }
}

async function clearWishlist() {
    if (!confirm("İstek listenizdeki tüm ürünleri silmek istediğinize emin misiniz?")) {
        return;
    }

    try {
        const clearButton = document.getElementById('clear-button');
        clearButton.disabled = true;
        clearButton.innerHTML = '⏳ Temizleniyor...';

        const response = await fetch('/api/wishlist/clear', {
            method: 'POST',
            credentials: 'same-origin'
        });

        const message = await response.text();

        if (response.ok) {
            await loadWishlist();
            showMessage(message, "success");
            await loadWishlist();
        } else {
            showMessage(message, "error");
        }
        

    } catch (error) {
        console.error('Liste temizleme hatası:', error);
        showMessage('❌ Liste temizlenirken hata oluştu!', 'error');
    } finally {
        const clearButton = document.getElementById('clear-button');
        clearButton.disabled = false;
        clearButton.innerHTML = '🗑️ Tümünü Temizle';
    }
}

window.onload = async () => {
    await loadWishlist();
};
</script>
</body>
</html>
""";
}




string GetPriceUpdateForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>💰 Fiyat Güncelle</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body {
font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
min-height: 100vh;
padding: 20px;
}
.container {
max-width: 700px;
margin: 0 auto;
background: white;
border-radius: 20px;
box-shadow: 0 20px 40px rgba(0,0,0,0.1);
overflow: hidden;
}
.header {
background: linear-gradient(135deg, #4f46e5, #7c3aed);
color: white;
padding: 30px;
text-align: center;
}
.header h1 { font-size: 2.5rem; text-shadow: 2px 2px 4px rgba(0,0,0,0.3); }
.form-container { padding: 40px; }
.form-section {
    margin-bottom: 50px;
    padding: 25px;
    border: 2px solid #f3f4f6;
    border-radius: 15px;
    background: #fafafa;
}
.form-section h3 {
    margin-bottom: 20px;
    color: #374151;
    font-size: 1.5rem;
    text-align: center;
}
.form-group { margin-bottom: 20px; }
label { display: block; margin-bottom: 8px; font-weight: 600; color: #374151; }
input, select { width: 100%; padding: 12px 15px; border: 2px solid #e5e7eb; border-radius: 10px; font-size: 1rem; transition: border-color 0.3s; }
input:focus, select:focus { outline: none; border-color: #4f46e5; }
input::placeholder {
            color: rgba(128, 128, 128, 0.6);
            }  
.btn { padding: 15px 30px; border: none; border-radius: 50px; font-size: 1.1rem; cursor: pointer; font-weight: 600; transition: all 0.3s ease; margin-right: 10px; text-decoration: none; display: inline-block; }
.btn-success { background: linear-gradient(135deg, #10b981, #059669); color: white; }
.btn-danger { background: linear-gradient(135deg, #ef4444, #dc2626); color: white; }
.btn-warning { background: linear-gradient(135deg, #f59e0b, #d97706); color: white; }
.btn-secondary { background: #6b7280; color: white; }
.btn:hover { transform: translateY(-2px); box-shadow: 0 10px 20px rgba(0,0,0,0.2); }
.success { background: #10b981; color: white; padding: 15px; border-radius: 10px; margin-bottom: 20px; text-align: center; font-weight: 600; }
.form-buttons { text-align: center; margin-top: 25px; }
</style>
</head>
<body>
<div class="container">
<div class="header">
<h1>💰 Fiyat İşlemleri</h1>
<p>Kitap veya kategori bazlı fiyat değişiklikleri</p>
</div>

<div class="form-container">
""" + (Environment.GetEnvironmentVariable("QUERY_STRING")?.Contains("success=true") == true ? "<div class='success'>✅ İşlem başarıyla uygulandı!</div>" : "") + """

<div class="form-section">
<form method="POST" action="/api/books/increase">
    <h3>📈 Zam Uygula</h3>
    <h4>Kitap , kategori veya yazar seçin</h4>

    <div class="form-group">
        <label for="increaseBookName">Kitap İsmi (İsteğe bağlı)</label>
        <input type="text" id="increaseBookName" name="bookName" placeholder="Örnek: 1984">
    </div>

    <div class="form-group">
            <label for="increaseCategory">Kategori</label>
            <select id="increaseCategory" name="category" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="">Kategoriler</option>
            </select>
        </div>

    <div class="form-group">
        <label for="increaseAuthor">Yazar İsmi (İsteğe bağlı)</label>
        <input type="text" id="increaseAuthor" name="Author" placeholder="Örnek: Orhan Pamuk">
    </div>

    <div class="form-group">
        <label for="increasePercentage">Artış Oranı (%) - Örnek: 10</label>
        <input type="number" id="increasePercentage" name="percentage" placeholder="10" min="0" max="100" required>
    </div>

    <div class="form-buttons">
        <button type="submit" class="btn btn-success">📈 Zam Uygula</button>
    </div>
</form>
</div>

<div class="form-section">
<form method="POST" action="/api/books/discount">
    <h3>📉 İndirim Uygula</h3>
    <h4>Kitap , kategori veya yazar seçin</h4>

    <div class="form-group">
        <label for="discountBookName">Kitap İsmi (İsteğe bağlı)</label>
        <input type="text" id="discountBookName" name="bookName" placeholder="Örnek: 1984">
    </div>

    <div class="form-group">
            <label for="discountCategory">Kategori</label>
            <select id="discountCategory" name="category" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="">Kategoriler</option>
            </select>
        </div>

    <div class="form-group">
        <label for="discountAuthor">Yazar İsmi (İsteğe bağlı)</label>
        <input type="text" id="discountAuthor" name="Author" placeholder="Örnek: Orhan Pamuk">
    </div>

    <div class="form-group">
        <label for="discountPercentage">İndirim Oranı (%) - Örnek: 15</label>
        <input type="number" id="discountPercentage" name="percentage" placeholder="15" min="0" max="100" required>
    </div>

    <div class="form-buttons">
        <button type="submit" class="btn btn-danger">📉 İndirim Uygula</button>
    </div>
</form>
</div>


<div class="form-section">
<form method="POST" action="/api/books/price">
    <h3>Fiyat Değiştir</h3>
    <h4>Kitap seçin</h4>

    <div class="form-group">
        <label for="priceBookName">Kitap İsmi</label>
        <input type="text" id="priceBookName" name="bookName" placeholder="Örnek: 1984">
    </div>

    <div class="form-group">
        <label for="price">Yeni Fiyat</label>
        <input type="number" id="price" name="newPrice" placeholder="15.99" required>
    </div>

    <div class="form-buttons">
        <button type="submit" class="btn btn-danger"> Yeni Fiyatı Onayla</button>
    </div>
</form>
</div>


<div class="form-section">
<form method="POST" action="/api/books/remove-discount">
    <h3>🚫 İndirimi Kaldır</h3>
    <h4>Kitap , kategori veya yazar seçin</h4>

    <div class="form-group">
        <label for="removeBookName">Kitap İsmi (İsteğe bağlı)</label>
        <input type="text" id="removeBookName" name="bookName" placeholder="Örnek: 1984">
    </div>

    <div class="form-group">
            <label for="removeCategory">Kategori</label>
            <select id="removeCategory" name="category" style="width: 100%; padding: 8px 12px; border: 2px solid #e5e7eb; border-radius: 8px;">
                <option value="">Kategoriler</option>
            </select>
        </div>

    <div class="form-group">
        <label for="removeAuthor">Yazar İsmi (İsteğe bağlı)</label>
        <input type="text" id="removeAuthor" name="Author" placeholder="Örnek: Orhan Pamuk">
    </div>

    <div class="form-buttons">
        <button type="submit" class="btn btn-secondary">🚫 İndirimi Kaldır</button>
    </div>
</form>
</div>

<div style="text-align: center; margin-top: 30px;">
    <a href="/admin" class="btn btn-secondary">↩️ Ana Sayfaya Dön</a>
</div>
</div>
</div>

<script>
async function loadCategories() {
    try {
        const response = await fetch('/api/books/categories');
        const categories = await response.json();

        // Kategori select elemanlarını tek seferde al
        const categorySelects = [
            document.getElementById('increaseCategory'),
            document.getElementById('discountCategory'),
            document.getElementById('removeCategory')
        ];

        categorySelects.forEach(select => {
            if (!select) return;

            // Önce içini temizle ve varsayılan option ekle
            select.innerHTML = '<option value="">Tüm Kategoriler</option>';

            // Gelen kategorileri ekle
            categories.forEach(category => {
                const option = document.createElement('option');
                option.value = category;
                option.textContent = category;
                select.appendChild(option);
            });
        });

    } catch (error) {
        console.error('Kategoriler yüklenirken hata:', error);
    }
}

window.onload = loadCategories;
</script>



</body>
</html>
""";
}


string GetAddBookForm()
{
    return """
    <!DOCTYPE html>
    <html lang="tr">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>📚 Yeni Kitap Ekle</title>
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body { 
                font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                min-height: 100vh;
                padding: 20px;
            }
            .container { 
                max-width: 600px; 
                margin: 0 auto; 
                background: white;
                border-radius: 20px;
                box-shadow: 0 20px 40px rgba(0,0,0,0.1);
                overflow: hidden;
            }
            .header { 
                background: linear-gradient(135deg, #4f46e5, #7c3aed);
                color: white;
                padding: 30px;
                text-align: center;
            }
            .form-container { padding: 40px; }
            .form-group {
                margin-bottom: 25px;
            }
            label {
                display: block;
                margin-bottom: 8px;
                font-weight: 600;
                color: #374151;
            }
            input, select, textarea {
                width: 100%;
                padding: 12px 15px;
                border: 2px solid #e5e7eb;
                border-radius: 10px;
                font-size: 1rem;
                transition: border-color 0.3s;
            }
            input:focus, select:focus {
                outline: none;
                border-color: #4f46e5;
            }
            input::placeholder {
            color: rgba(128, 128, 128, 0.6);
            }   
            .checkbox-group {
                display: flex;
                align-items: center;
                gap: 10px;
            }
            .checkbox-group input[type="checkbox"] {
                width: auto;
            }
            .btn {
                padding: 15px 30px;
                border: none;
                border-radius: 50px;
                font-size: 1.1rem;
                cursor: pointer;
                font-weight: 600;
                transition: all 0.3s ease;
                margin-right: 10px;
            }
            .btn-primary {
                background: linear-gradient(135deg, #4f46e5, #7c3aed);
                color: white;
            }
            .btn-secondary {
                background: #6b7280;
                color: white;
            }
            .btn:hover {
                transform: translateY(-2px);
                box-shadow: 0 10px 20px rgba(0,0,0,0.2);
            }
        </style>
    </head>
    <body>
        <div class="container">
            <div class="header">
                <h1>📚 Yeni Kitap Ekle</h1>
            </div>
            <div class="form-container">
                <form method="POST" action="/add-book">
                    <div class="form-group">
                        <label for="title">Kitap Adı</label>
                        <input type="text" id="title" name="title" required>
                    </div>

                    <div class="form-group">
                        <label for="author">Yazar</label>
                        <input type="text" id="author" name="author" required>
                    </div>

                    <div class="form-group">
                        <label for="price">Fiyat</label>
                        <input type="text" id="price" name="price" placeholder="30" required>
                    </div>

                    <div class="form-group">
                    <label for="category">Kategori </label>
                    <input type="text" id="category" name="category" placeholder="Örnek: Roman">
                    </div>

                    <div class="form-group">
                        <label for="stock">Stok</label>
                        <input type="text" id="stock" name="stock" placeholder="50" required>
                    </div>

                    <div style="text-align: center; margin-top: 30px;">
                        <button type="submit" class="btn btn-primary">📚 Kitabı Ekle</button>
                        <a href="/admin" class="btn btn-secondary">↩️ Ana Sayfaya Dön</a>
                    </div>
                </form>
            </div>
        </div>
    </body>
    </html>
    """;
}


string GetAiForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>🤖 AI Chat</title>
<style>
body { 
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    margin: 0;
    padding: 0;
    display: flex;
    justify-content: center;
    align-items: center;
}

.container { 
    width: 90%;
    max-width: 1200px;
    height: 90vh;
    background: white; 
    border-radius: 20px; 
    padding: 30px; 
    box-shadow: 0 20px 40px rgba(0,0,0,0.1);
    display: flex;
    flex-direction: column;
}

.header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
    padding-bottom: 15px;
    border-bottom: 2px solid #f1f5f9;
}
.header h2 {
    color: #4f46e5;
    margin: 0;
    font-size: 2rem;
}
.back-button {
    padding: 10px 20px;
    border: none;
    border-radius: 50px;
    background: linear-gradient(135deg, #6b7280, #4b5563);
    color: white;
    cursor: pointer;
    text-decoration: none;
    display: inline-flex;
    align-items: center;
    gap: 8px;
    font-weight: 600;
    transition: all 0.3s ease;
}
.back-button:hover {
    transform: translateY(-2px);
    box-shadow: 0 10px 20px rgba(0,0,0,0.2);
    background: linear-gradient(135deg, #4b5563, #374151);
}
.chat-box { 
    flex: 1;
    overflow-y: auto; 
    border: 2px solid #e5e7eb; 
    border-radius: 12px; 
    padding: 15px; 
    margin-bottom: 20px; 
    background: #f8fafc;
}
.message { 
    margin: 10px 0; 
    padding: 10px 15px;
    border-radius: 18px;
    max-width: 80%;
    word-wrap: break-word;
}
.user { 
    text-align: left; 
    background: linear-gradient(135deg, #4f46e5, #7c3aed);
    color: white;
    margin-left: auto;
    border-bottom-right-radius: 4px;
}
.ai { 
    text-align: left; 
    background: linear-gradient(135deg, #10b981, #059669);
    color: white;
    margin-right: auto;
    border-bottom-left-radius: 4px;
}
.input-container {
    display: flex;
    gap: 10px;
    align-items: center;
}
.input-container input { 
    flex: 1;
    padding: 12px 15px; 
    border: 2px solid #e5e7eb; 
    border-radius: 25px; 
    font-size: 1rem;
    outline: none;
    transition: border-color 0.3s ease;
    text-align: left;
}
.input-container input:focus {
    border-color: #4f46e5;
}
.send-button { 
    padding: 12px 25px; 
    border: none; 
    border-radius: 25px; 
    background: linear-gradient(135deg, #4f46e5, #7c3aed); 
    color: white; 
    cursor: pointer;
    font-weight: 600;
    transition: all 0.3s ease;
}
.send-button:hover { 
    transform: translateY(-2px);
    box-shadow: 0 8px 15px rgba(79, 70, 229, 0.3);
}
.send-button:disabled {
    background: #9ca3af;
    cursor: not-allowed;
    transform: none;
    box-shadow: none;
}
.typing-indicator {
    display: none;
    text-align: left;
    color: #6b7280;
    font-style: italic;
    padding: 10px 15px;
}

</style>
</head>
<body>
<div class="container">
    <div class="header">
        <h2>🤖 AI Chat Asistanı</h2>
        <a href="/admin" class="back-button">⬅ Geri Dön</a>
    </div>
    
    <div id="chat-box" class="chat-box">
        <div class="message ai">
            Merhaba! Size nasıl yardımcı olabilirim? Kitaplar, stok durumu veya genel sorularınız hakkında konuşabiliriz. 📚
        </div>
    </div>
    
    <div class="typing-indicator" id="typing-indicator">
        AI düşünüyor...
    </div>
    
    <div class="input-container">
        <input type="text" id="user-input" placeholder="Mesajınızı yazın..." onkeypress="handleKeyPress(event)" />
        <button class="send-button" onclick="sendMessage()" id="send-button">
            📤 Gönder
        </button>
    </div>
</div>

<script>
function handleKeyPress(event) {
    if (event.key === 'Enter') sendMessage();
}

async function sendMessage() {
    const input = document.getElementById("user-input");
    const sendButton = document.getElementById("send-button");
    const typingIndicator = document.getElementById("typing-indicator");
    
    const message = input.value.trim();
    if (!message) return;

    sendButton.disabled = true;
    sendButton.innerHTML = "⏳ Gönderiliyor...";
    input.disabled = true;

    addMessage("user", message);
    input.value = "";

    typingIndicator.style.display = "block";

    try {
        const response = await fetch("http://localhost:5678/webhook/chat-bot", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ 
                admin: "admin",   
                message: message  
            })
        });
        
        if (!response.ok) throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        
        const data = await response.json();
        const reply = data.reply || data.message || data.output || "Üzgünüm, bir yanıt alamadım.";
        addMessage("ai", formatMarkdown(reply));
        
    } catch (error) {
        console.error("Chat error:", error);
        addMessage("ai", "❌ Üzgünüm, şu anda AI servisine ulaşamıyorum. Lütfen daha sonra tekrar deneyin.");
    } finally {
        typingIndicator.style.display = "none";
        sendButton.disabled = false;
        sendButton.innerHTML = "📤 Gönder";
        input.disabled = false;
        input.focus();
    }
}

function addMessage(sender, text) {
    const chatBox = document.getElementById("chat-box");
    const div = document.createElement("div");
    div.className = "message " + sender;
    
    if (sender === "ai") {
        div.innerHTML = text;
    } else {
        div.innerText = text;
    }
    
    chatBox.appendChild(div);
    chatBox.scrollTop = chatBox.scrollHeight;
}

// AI cevabındaki escape'leri çözer + basit markdown
function formatMarkdown(text) {
    if (!text) return text;
    return text
        .replace(/\\"/g, '"')      // \" -> "
        .replace(/\\\\/g, '\\')    // \\ -> \
        .replace(/\\n/g, '<br>')   // \n -> <br>
        .replace(/\n/g, '<br>')
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>') // **bold**
        .replace(/^\* (.+)/gm, '• $1');                   // * liste
}

window.onload = () => document.getElementById("user-input").focus();
</script>
</body>
</html>
""";
}


string GetUserAiForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>🤖 AI Chat</title>
<style>
body { 
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    margin: 0;
    padding: 0;
    display: flex;
    justify-content: center;
    align-items: center;
}

.container { 
    width: 90%;
    max-width: 1200px;
    height: 90vh;
    background: white; 
    border-radius: 20px; 
    padding: 30px; 
    box-shadow: 0 20px 40px rgba(0,0,0,0.1);
    display: flex;
    flex-direction: column;
}

.header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
    padding-bottom: 15px;
    border-bottom: 2px solid #f1f5f9;
}
.header h2 {
    color: #4f46e5;
    margin: 0;
    font-size: 2rem;
}
.back-button {
    padding: 10px 20px;
    border: none;
    border-radius: 50px;
    background: linear-gradient(135deg, #6b7280, #4b5563);
    color: white;
    cursor: pointer;
    text-decoration: none;
    display: inline-flex;
    align-items: center;
    gap: 8px;
    font-weight: 600;
    transition: all 0.3s ease;
}
.back-button:hover {
    transform: translateY(-2px);
    box-shadow: 0 10px 20px rgba(0,0,0,0.2);
    background: linear-gradient(135deg, #4b5563, #374151);
}
.chat-box { 
    flex: 1;
    overflow-y: auto; 
    border: 2px solid #e5e7eb; 
    border-radius: 12px; 
    padding: 15px; 
    margin-bottom: 20px; 
    background: #f8fafc;
}
.message { 
    margin: 10px 0; 
    padding: 10px 15px;
    border-radius: 18px;
    max-width: 80%;
    word-wrap: break-word;
}
.user { 
    text-align: left; 
    background: linear-gradient(135deg, #4f46e5, #7c3aed);
    color: white;
    margin-left: auto;
    border-bottom-right-radius: 4px;
}
.ai { 
    text-align: left; 
    background: linear-gradient(135deg, #10b981, #059669);
    color: white;
    margin-right: auto;
    border-bottom-left-radius: 4px;
}
.input-container {
    display: flex;
    gap: 10px;
    align-items: center;
}
.input-container input { 
    flex: 1;
    padding: 12px 15px; 
    border: 2px solid #e5e7eb; 
    border-radius: 25px; 
    font-size: 1rem;
    outline: none;
    transition: border-color 0.3s ease;
    text-align: left;
}
.input-container input:focus {
    border-color: #4f46e5;
}
.send-button { 
    padding: 12px 25px; 
    border: none; 
    border-radius: 25px; 
    background: linear-gradient(135deg, #4f46e5, #7c3aed); 
    color: white; 
    cursor: pointer;
    font-weight: 600;
    transition: all 0.3s ease;
}
.send-button:hover { 
    transform: translateY(-2px);
    box-shadow: 0 8px 15px rgba(79, 70, 229, 0.3);
}
.send-button:disabled {
    background: #9ca3af;
    cursor: not-allowed;
    transform: none;
    box-shadow: none;
}
.typing-indicator {
    display: none;
    text-align: left;
    color: #6b7280;
    font-style: italic;
    padding: 10px 15px;
}
</style>
</head>
<body>
<div class="container">
    <div class="header">
        <h2>🤖 AI Chat Asistanı</h2>
        <a href="/books" class="back-button">⬅ Geri Dön</a>
    </div>
    
    <div id="chat-box" class="chat-box">
        <div class="message ai">
            Merhaba! Size nasıl yardımcı olabilirim? Kitaplar, indirimler veya genel sorularınız hakkında konuşabiliriz. 📚
        </div>
    </div>
    
    <div class="typing-indicator" id="typing-indicator">
        AI düşünüyor...
    </div>
    
    <div class="input-container">
        <input type="text" id="user-input" placeholder="Mesajınızı yazın..." onkeypress="handleKeyPress(event)" />
        <button class="send-button" onclick="sendMessage()" id="send-button">
            📤 Gönder
        </button>
    </div>
</div>

<script>
let currentUserId = null;


async function loadUserId() {
    try {
        const response = await fetch('/api/user/current', {
            credentials: 'same-origin'
        });
        
        if (response.ok) {
            const data = await response.json();
            currentUserId = data.userId;
            console.log('User ID loaded:', currentUserId);
        } else {
            console.error('User ID alınamadı, giriş sayfasına yönlendirilecek');
            setTimeout(() => {
                window.location.href = '/';
            }, 2000);
        }
    } catch (error) {
        console.error('User ID yükleme hatası:', error);
        addMessage("ai", "❌ Kullanıcı bilgisi alınamadı. Lütfen tekrar giriş yapın.");
    }
}

function handleKeyPress(event) {
    if (event.key === 'Enter') sendMessage();
}

async function sendMessage() {
    const input = document.getElementById("user-input");
    const sendButton = document.getElementById("send-button");
    const typingIndicator = document.getElementById("typing-indicator");
    
    const message = input.value.trim();
    if (!message) return;

    if (!currentUserId) {
        addMessage("ai", "❌ Kullanıcı bilgisi bulunamadı. Lütfen tekrar giriş yapın.");
        setTimeout(() => {
            window.location.href = '/';
        }, 2000);
        return;
    }

    sendButton.disabled = true;
    sendButton.innerHTML = "⏳ Gönderiliyor...";
    input.disabled = true;

    addMessage("user", message);
    input.value = "";

    typingIndicator.style.display = "block";

    try {
        const response = await fetch("http://localhost:5678/webhook/user-chat-bot", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ 
                currentuserId: currentUserId,   
                message: message  
            })
        });
        
        if (!response.ok) throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        
        const data = await response.json();
        addMessage("ai", formatMarkdown(data.reply || "Üzgünüm, bir yanıt alamadım."));
        
    } catch (error) {
        console.error("Chat error:", error);
        addMessage("ai", "❌ Üzgünüm, şu anda AI servisine ulaşamıyorum. Lütfen daha sonra tekrar deneyin.");
    } finally {
        typingIndicator.style.display = "none";
        sendButton.disabled = false;
        sendButton.innerHTML = "📤 Gönder";
        input.disabled = false;
        input.focus();
    }
}

function addMessage(sender, text) {
    const chatBox = document.getElementById("chat-box");
    const div = document.createElement("div");
    div.className = "message " + sender;
    div.innerHTML = text; 
    chatBox.appendChild(div);
    chatBox.scrollTop = chatBox.scrollHeight;
}

// Escape çözme + newline
function formatMarkdown(text) {
    if (!text) return text;
    return text
        .replace(/\\"/g, '"')
        .replace(/\\\\/g, '\\')
        .replace(/\\n/g, '<br>')
        .replace(/\n/g, '<br>');
}

window.onload = async () => {
    await loadUserId();
    document.getElementById("user-input").focus();
};
</script>
</body>
</html>
""";
}

string GetHomePage()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<title>Kullanıcı Girişi</title>
<style>
body { font-family: Arial, sans-serif; display:flex; height:100vh; justify-content:center; align-items:center; background:linear-gradient(135deg, #667eea, #764ba2); }
.container { background:white; padding:30px; border-radius:20px; box-shadow:0 10px 25px rgba(0,0,0,0.3); width:350px; text-align:center; }
h2 { margin-bottom:20px; color:#4f46e5; }
input { display:block; margin:10px auto; padding:10px; width:90%; border:1px solid #ccc; border-radius:8px; }
button { margin-top:10px; padding:10px 16px; border:none; border-radius:8px; cursor:pointer; font-weight:600; width:95%; }
.btn-user { background:#10b981; color:white; }
.btn-admin { background:#4f46e5; color:white; }
.btn-create { background:#6b7280; color:white; }
.btn-user:hover { background:#047857; }
.btn-admin:hover { background:#3730a3; }
.btn-create:hover { background:#374151; }
</style>
</head>
<body>
<div class="container">
<h2>👤 Kullanıcı Girişi</h2>
<form method="post" action="/user-login">
<input type="text" name="username" placeholder="Kullanıcı Adı" required />
<input type="password" name="password" placeholder="Şifre" required />
<button type="submit" class="btn-user">Giriş Yap</button>
</form>
<hr/>
<form method="get" action="/admin-login">
<button type="submit" class="btn-admin">🔑 Yönetici olarak devam et</button>
</form>
<form method="get" action="/create-user">
<button type="submit" class="btn-create">➕ Kullanıcı Oluştur</button>
</form>
</div>
</body>
</html>
""";
}

string GetAdminLoginForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<title>Yönetici Giriş</title>
<style>
body { font-family: Arial, sans-serif; display:flex; height:100vh; justify-content:center; align-items:center; background:linear-gradient(135deg, #667eea, #764ba2); }
.login-box { background:white; padding:30px; border-radius:20px; box-shadow:0 10px 25px rgba(0,0,0,0.3); width:320px; text-align:center; }
h2 { margin-bottom:20px; color:#4f46e5; }
input { display:block; margin:10px auto; padding:10px; width:90%; border:1px solid #ccc; border-radius:8px; }
button { margin-top:10px; padding:10px 16px; border:none; border-radius:8px; cursor:pointer; font-weight:600; width:95%; }
.btn-admin { background:#4f46e5; color:white; }
.btn-back { background:#6b7280; color:white; }
.btn-admin:hover { background:#3730a3; }
.btn-back:hover { background:#374151; }
</style>
</head>
<body>
<div class="login-box">
<h2>🔑 Yönetici Girişi</h2>
<form method="post" action="/admin-login">
<input type="text" name="username" placeholder="Kullanıcı Adı" required />
<input type="password" name="password" placeholder="Şifre" required />
<button type="submit" class="btn-admin">Giriş Yap</button>
</form>
<hr/>
<a href="/" style="text-decoration:none;"><button class="btn-back">🏠 Anasayfa</button></a>
</div>
</body>
</html>
""";
}


string GetAddUserForm()
{
    return """
<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>👤 Kullanıcı Ekle</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { 
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    padding: 20px;
}
.container { 
    max-width: 600px; 
    margin: 0 auto; 
    background: white;
    border-radius: 20px;
    box-shadow: 0 20px 40px rgba(0,0,0,0.1);
    overflow: hidden;
}
.header { 
    background: linear-gradient(135deg, #4f46e5, #7c3aed);
    color: white;
    padding: 30px;
    text-align: center;
}
.header h1 { 
    font-size: 2rem; 
    margin-bottom: 10px;
}
.content { padding: 40px; }
.form-group {
    margin-bottom: 25px;
}
.form-group label {
    display: block;
    margin-bottom: 8px;
    font-weight: 600;
    color: #374151;
}
.form-group input, .form-group select {
    width: 100%;
    padding: 12px 15px;
    border: 2px solid #e5e7eb;
    border-radius: 10px;
    font-size: 1rem;
    transition: border-color 0.3s ease;
}
.form-group input:focus, .form-group select:focus {
    outline: none;
    border-color: #4f46e5;
    box-shadow: 0 0 0 3px rgba(79, 70, 229, 0.1);
}
.form-row {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 20px;
}
.btn {
    padding: 12px 25px;
    border: none;
    border-radius: 50px;
    font-size: 1rem;
    cursor: pointer;
    text-decoration: none;
    display: inline-flex;
    align-items: center;
    gap: 8px;
    transition: all 0.3s ease;
    font-weight: 600;
}
.btn-primary {
    background: linear-gradient(135deg, #4f46e5, #7c3aed);
    color: white;
}
.btn-secondary {
    background: linear-gradient(135deg, #6b7280, #4b5563);
    color: white;
}
.btn:hover {
    transform: translateY(-2px);
    box-shadow: 0 10px 20px rgba(0,0,0,0.2);
}
.btn:disabled {
    background: #9ca3af;
    cursor: not-allowed;
    transform: none;
    box-shadow: none;
}
.buttons {
    display: flex;
    gap: 15px;
    justify-content: center;
    margin-top: 30px;
}
.success {
    background: #10b981;
    color: white;
    padding: 15px;
    border-radius: 10px;
    margin-bottom: 20px;
    text-align: center;
    font-weight: 600;
}
.error {
    background: #ef4444;
    color: white;
    padding: 15px;
    border-radius: 10px;
    margin-bottom: 20px;
    text-align: center;
    font-weight: 600;
}
.required {
    color: #ef4444;
}
</style>
</head>
<body>
<div class="container">
    <div class="header">
        <h1>👤 Yeni Kullanıcı Ekle</h1>
        <p>Sisteme yeni kullanıcı ekleyin</p>
    </div>
    
    <div class="content">
        <div id="message-container"></div>
        
        <form id="userForm" onsubmit="addUser(event)">
            <div class="form-row">
                <div class="form-group">
                    <label for="firstName">Ad <span class="required">*</span></label>
                    <input type="text" id="firstName" name="firstName" required>
                </div>
                <div class="form-group">
                    <label for="lastName">Soyad <span class="required">*</span></label>
                    <input type="text" id="lastName" name="lastName" required>
                </div>
            </div>
            
            <div class="form-group">
                <label for="username">Kullanıcı Adı <span class="required">*</span></label>
                <input type="text" id="username" name="username" required minlength="3" maxlength="50">
                <small style="color: #6b7280;">En az 3 karakter, sadece harf, rakam ve alt çizgi</small>
            </div>
            
            <div class="form-group">
                <label for="email">Email <span class="required">*</span></label>
                <input type="email" id="email" name="email" required>
            </div>
            
            <div class="form-group">
                <label for="password">Şifre <span class="required">*</span></label>
                <input type="password" id="password" name="password" required minlength="6">
                <small style="color: #6b7280;">En az 6 karakter</small>
            </div>
            
            <div class="form-group">
                <label for="confirmPassword">Şifre Tekrar <span class="required">*</span></label>
                <input type="password" id="confirmPassword" name="confirmPassword" required minlength="6">
            </div>
            
            
            <div class="buttons">
                <button type="submit" class="btn btn-primary" id="submitBtn">
                    Kullanıcı Ekle
                </button>
                <a href="/" class="btn btn-secondary">
                    Geri Dön
                </a>
            </div>
        </form>
    </div>
</div>

<script>
function showMessage(message, type) {
    const container = document.getElementById('message-container');
    const className = type === 'success' ? 'success' : 'error';
    container.innerHTML = `<div class="${className}">${message}</div>`;
    
    setTimeout(() => {
        container.innerHTML = '';
    }, 5000);
}

async function addUser(event) {
    event.preventDefault();
    
    const submitBtn = document.getElementById('submitBtn');
    const originalText = submitBtn.innerHTML;
    
    const password = document.getElementById('password').value;
    const confirmPassword = document.getElementById('confirmPassword').value;
    
    if (password !== confirmPassword) {
        showMessage('Şifreler eşleşmiyor!', 'error');
        return;
    }
    
    submitBtn.disabled = true;
    submitBtn.innerHTML = 'Ekleniyor...';
    
    try {
        const formData = new FormData(event.target);
        
        const response = await fetch('/api/users/add', {
            method: 'POST',
            body: formData
        });
        
        if (response.ok) {
            showMessage('Kullanıcı başarıyla eklendi!', 'success');
            event.target.reset();
        } else {
            const errorText = await response.text();
            showMessage(`Hata: ${errorText}`, 'error');
        }
        
    } catch (error) {
        console.error('Add user error:', error);
        showMessage('Kullanıcı eklenirken hata oluştu!', 'error');
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = originalText;
    }
}
</script>
</body>
</html>
""";
}
