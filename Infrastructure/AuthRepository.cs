using Supabase.Gotrue;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Task = System.Threading.Tasks.Task;

namespace Syncro.Infrastructure;

public class AuthRepository(Supabase.Client client): IAuthRepository
{
    public Session? CurrentSession => client.Auth.CurrentSession;

    public User? CurrentUser => client.Auth.CurrentUser;

    public void AddAuthStateListener(Action listener)
    {
        client.Auth.AddStateChangedListener((_, _) => listener());
    }

    public async Task<IResponse<Session>> Login(string username, string password)
    {
        try
        {
            var result = await client.Auth.SignInWithPassword(username, password);
            return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Login failed");
        }
        catch (Exception exception)
        {
            return Response<Session>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<Session>> Register(string username, string password)
    {
        try
        {
            var result = await client.Auth.SignUp(username, password);
            return result is not null ? Response<Session>.Success(result) : Response<Session>.Failure("Registration failed");
        }
        catch (Exception exception)
        {
            return Response<Session>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> Logout()
    {
        try
        {
            await client.Auth.SignOut();
            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
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

    public async Task<IResponse> UpdatePassword(string password)
    {
        try
        {
            var user = await client.Auth.Update(new UserAttributes { Password = password });
            return user is not null ? Response.Success() : Response.Failure("Passwort konnte nicht geändert werden.", null);
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }

    public async Task<IResponse> SendPasswordReset(string email, string redirectTo)
    {
        try
        {
            var sent = await client.Auth.ResetPasswordForEmail(new ResetPasswordForEmailOptions(email.Trim())
            {
                RedirectTo = redirectTo
            });
            return sent is not null ? Response.Success() : Response.Failure("Die Mail konnte nicht gesendet werden.", null);
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }

    public async Task<IResponse<Session>> RecoverSessionFromUrl(Uri url)
    {
        try
        {
            var session = await client.Auth.GetSessionFromUrl(url);
            return session is not null
                ? Response<Session>.Success(session)
                : Response<Session>.Failure("Der Link ist ungültig.");
        }
        catch (Exception exception)
        {
            return Response<Session>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProfileDto>> GetProfile(Guid id)
    {
        try
        {
            var result = await client.From<ProfileDto>().Where(profile => profile.Id == id).Single();
            return result is not null ? Response<ProfileDto>.Success(result) : Response<ProfileDto>.Failure("Profil wurde nicht gefunden.");
        }
        catch (Exception exception)
        {
            return Response<ProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProfileDto>> UpdateProfile(ProfileDto profile)
    {
        try
        {
            var result = await client.From<ProfileDto>()
                .Where(existing => existing.Id == profile.Id)
                .Set(existing => existing.Username, profile.Username)
                .Set(existing => existing.Vorname, profile.Vorname)
                .Set(existing => existing.Nachname, profile.Nachname)
                .Set(existing => existing.AvatarColor, profile.AvatarColor)
                .Update();

            var savedProfile = result.Models.FirstOrDefault();
            return savedProfile is not null
                ? Response<ProfileDto>.Success(savedProfile)
                : Response<ProfileDto>.Failure("Profil konnte nicht gespeichert werden.");
        }
        catch (Exception exception)
        {
            return Response<ProfileDto>.Failure(exception.Message);
        }
    }
}
