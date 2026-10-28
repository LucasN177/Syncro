using Supabase.Gotrue;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IAuthRepository
{
    public Session? CurrentSession { get; }

    public User? CurrentUser { get; }

    /// <summary>Wird bei Login, Logout, Token-Refresh und Benutzer-Updates von Supabase ausgelöst.</summary>
    public void AddAuthStateListener(Action listener);

    public Task<IResponse<Session>> Login(string username, string password);

    public Task<IResponse<Session>> Register(string username, string password);

    public Task<IResponse> Logout();

    public Task<IResponse<Session>> GetCurrentSession();

    public Task<IResponse<User>> GetCurrentUser();

    public Task<IResponse> UpdatePassword(string password);

    /// <summary>Schickt eine Mail mit Link zum Zurücksetzen; der Link führt auf redirectTo.</summary>
    public Task<IResponse> SendPasswordReset(string email, string redirectTo);

    /// <summary>Übernimmt die Session aus dem Link der Reset-Mail (Tokens im URL-Fragment).</summary>
    public Task<IResponse<Session>> RecoverSessionFromUrl(Uri url);

    public Task<IResponse<ProfileDto>> GetProfile(Guid id);

    public Task<IResponse<ProfileDto>> UpdateProfile(ProfileDto profile);
}
