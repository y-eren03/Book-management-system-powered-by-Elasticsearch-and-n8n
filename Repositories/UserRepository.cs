using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Nest;
using Elasticsearch.Net;



public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(string id);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<User?> GetUserByEmailAsync(string email);
    Task<List<User>> GetAllUsersAsync();
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task DeleteUserAsync(string id);
    Task<User?> ControlAsync(string username, string password);
}


public class UserRepository : IUserRepository
{
    private readonly IElasticClient _elasticClient;
    private const string IndexName = "users";

    public UserRepository(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        try
        {
            var response = await _elasticClient.GetAsync<User>(id, g => g.Index(IndexName));
            return response.IsValid && response.Found ? response.Source : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetUserByIdAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        try
        {
            var response = await _elasticClient.SearchAsync<User>(s => s
                .Index(IndexName)
                .Query(q => q.Term(t => t.Field(f => f.Username.Suffix("keyword")).Value(username.ToLower())))
                .Size(1)
            );

            return response.IsValid && response.Documents.Any() ? response.Documents.First() : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetUserByUsernameAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        try
        {
            var response = await _elasticClient.SearchAsync<User>(s => s
                .Index(IndexName)
                .Query(q => q.Term(t => t.Field(f => f.Email.Suffix("keyword")).Value(email.ToLower())))
                .Size(1)
            );

            return response.IsValid && response.Documents.Any() ? response.Documents.First() : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetUserByEmailAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        try
        {
            var response = await _elasticClient.SearchAsync<User>(s => s
                .Index(IndexName)
                .Query(q => q.MatchAll())
                .Size(1000)
                .Sort(sort => sort.Descending(f => f.Username))
            );

            return response.IsValid ? response.Documents.ToList() : new List<User>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAllUsersAsync error: {ex.Message}");
            return new List<User>();
        }
    }

    public async Task<User> CreateUserAsync(User user)
    {
        try
        {
            // Username ve email'in benzersiz olduğunu kontrol et
            var existingUser = await GetUserByUsernameAsync(user.Username);
            if (existingUser != null)
            {
                throw new Exception("Bu kullanıcı adı zaten kullanılıyor");
            }

            var existingEmail = await GetUserByEmailAsync(user.Email);
            if (existingEmail != null)
            {
                throw new Exception("Bu email adresi zaten kullanılıyor");
            }

            user.Id = Guid.NewGuid().ToString();
            user.Username = user.Username.ToLower();
            user.Email = user.Email.ToLower();
            user.LastName = user.LastName.ToLower();
            user.FirstName = user.FirstName.ToLower();
            

            var response = await _elasticClient.IndexDocumentAsync(user);
            if (!response.IsValid)
            {
                throw new Exception($"Kullanıcı oluşturulurken hata: {response.OriginalException?.Message}");
            }

            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CreateUserAsync error: {ex.Message}");
            throw;
        }
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        try
        {
            var response = await _elasticClient.UpdateAsync<User>(user.Id, u => u
                .Index(IndexName)
                .Doc(user)
                .RetryOnConflict(3)
            );

            if (!response.IsValid)
            {
                throw new Exception($"Kullanıcı güncellenirken hata: {response.OriginalException?.Message}");
            }

            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateUserAsync error: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteUserAsync(string id)
    {
        try
        {
            var response = await _elasticClient.DeleteAsync<User>(id, d => d.Index(IndexName));
            if (!response.IsValid)
            {
                throw new Exception($"Kullanıcı silinirken hata: {response.OriginalException?.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeleteUserAsync error: {ex.Message}");
            throw;
        }
    }

    

    public async Task<User?> ControlAsync(string username, string password)
    {
        try
        {
            var user = await GetUserByUsernameAsync(username);
            if (user == null)
                return null;

            if (!PasswordHelper.VerifyPassword(password, user.PasswordHash))
                return null;

           
            await UpdateUserAsync(user);

            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ControlAsync error: {ex.Message}");
            return null;
        }
    }
}


