using Supabase.Gotrue;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Task = System.Threading.Tasks.Task;

namespace Syncro.Infrastructure;

public class AuthRepository(Supabase.Client client): IAuthRepository
{
    public async Task<IResponse<Session>> Login(string username, string password)
    {
        var result = await client.Auth.SignInWithPassword(username, password);
        return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Login failed");
    }

    public async Task<IResponse<Session>> Register(string username, string password)
    {
        var result = await client.Auth.SignUp(username, password);
        return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Registration failed");
    }

    public async Task<IResponse> Logout()
    {
        await client.Auth.SignOut();
        return Response.Success();
    }

    public Task<IResponse<Session>> GetCurrentSession()
    {
        try
        {
            return Task.FromResult(client.Auth.CurrentSession is not null ? Response<Session>.Success(client.Auth.CurrentSession) : Response<Session>.Failure("No login available"));
        }
        catch (Exception exception)
        {
            return Task.FromException<IResponse<Session>>(exception);
        }
    }

    public Task<IResponse<User>> GetCurrentUser()
    {
        try
        {
            return Task.FromResult(client.Auth.CurrentUser is not null ? Response<User>.Success(client.Auth.CurrentUser) : Response<User>.Failure("No login available"));
        }
        catch (Exception exception)
        {
            return Task.FromException<IResponse<User>>(exception);
        }
    }

    public async Task<IResponse> InsertUserMetadata(UserDto userDto)
    {
        try
        {
            userDto.UserId = client.Auth.CurrentUser.Id;
            var result = await client.From<UserDto>()
                .Where(x => x.UserId == client.Auth.CurrentUser.Id)
                .Set(x => x.Username, userDto.Username)
                .Set(x => x.Vorname, userDto.Vorname)
                .Set(x =>x.Nachname, userDto.Nachname)
                .Update();
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public Task<IResponse> UpdateUserMetadata(UserDto user)
    {
        throw new NotImplementedException();
    }

    public async Task<IResponse<UserDto>> GetUserMetadata(string id)
    {
        var result = await client.From<UserDto>().Where(x => x.UserId == id).Get();
        return result.Model is not null ? Response<UserDto>.Success(result.Model) : Response<UserDto>.Failure("No user found");
    }
}