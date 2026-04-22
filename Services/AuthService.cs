using Supabase.Gotrue;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Task = System.Threading.Tasks.Task;

namespace Syncro.Services;

public class AuthService(IAuthRepository authRepository) : IAuthService
{
    public Session? CurrentSession { get; set; }
    public User? CurrentUser { get; set; } 
    
    public async Task<IResponse<User?>> GetCurrentUser()
    {
        var result =  await authRepository.GetCurrentUser();
        if (result.IsSuccess)
        {
            CurrentUser = result.Data;
            return Response<User?>.Success(result.Data);
        }
        return Response<User?>.Failure(result.ErrorMessage ?? "No current user");
    }

    public async Task<IResponse<Session?>> GetCurrentSession()
    {
        var result =  await authRepository.GetCurrentSession();
        if (result.IsSuccess)
        {
            CurrentSession = result.Data;
            return Response<Session?>.Success(result.Data);
        } 
        return Response<Session?>.Failure(result.ErrorMessage ?? "No current session");
    }

    public async Task<IResponse<Session>> Login(string username, string password)
    {
        var result =  await authRepository.Login(username, password);
        return result is { IsSuccess: true, Data: not null } ? Response<Session>.Success(result.Data) : Response<Session>.Failure(result.ErrorMessage ?? "Login failed");
    }

    public async Task<IResponse<Session>> Register(string username, string password)
    {
        var result =  await authRepository.Register(username, password);
        return result is { IsSuccess: true, Data: not null } ? Response<Session>.Success(result.Data) : Response<Session>.Failure(result.ErrorMessage ?? "Register failed.");
    }

    public Task<IResponse> Logout()
    {
        CurrentSession = null;
        CurrentUser = null;
        authRepository.Logout();
        return Task.FromResult(Response.Success());
    }

    public async Task<IResponse> InsertUserMetadata(UserDto user)
    {
        return await authRepository.InsertUserMetadata(user);
    }

    public Task<IResponse> UpdateUserMetadata(UserDto user)
    {
        throw new NotImplementedException();
    }

    public async Task<IResponse<UserDto>> GetUserMetadata(string id)
    {
        var result = await authRepository.GetUserMetadata(id);
        return result is { IsSuccess: true, Data: not null } ? Response<UserDto>.Success(result.Data) : Response<UserDto>.Failure(result.ErrorMessage ?? "No metadata found");
    }
}