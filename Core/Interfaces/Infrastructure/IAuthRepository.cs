using Supabase.Gotrue;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IAuthRepository
{
    public Task<IResponse<Session>> Login(string username, string password);
    
    public Task<IResponse<Session>> Register(string username, string password);
    
    public Task<IResponse> Logout();
    
    public Task<IResponse<Session>> GetCurrentSession();
    
    public Task<IResponse<User>> GetCurrentUser();

    public Task<IResponse> InsertUserMetadata(UserDto user);
    
    public Task<IResponse> UpdateUserMetadata(UserDto user);
    
    public Task<IResponse<UserDto>> GetUserMetadata(string id);
    
}