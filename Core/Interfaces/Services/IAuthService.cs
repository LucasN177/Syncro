using Supabase.Gotrue;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Services;

public interface IAuthService
{
    public Session? CurrentSession { get; }

    public User? CurrentUser { get; }

    public ProfileDto? CurrentProfile { get; }

    public Guid? CurrentUserId { get; }

    public bool IsAuthenticated { get; }

    /// <summary>Anzeigename aus dem Profil, sonst der Teil der E-Mail vor dem @.</summary>
    public string DisplayName { get; }

    /// <summary>Wird ausgelöst, wenn sich Anmeldung oder Profil ändern.</summary>
    public event Action? OnAuthStateChanged;

    /// <summary>Übernimmt die beim Start wiederhergestellte Session und lädt das Profil.</summary>
    public Task InitializeAsync();

    public Task<IResponse<User?>> GetCurrentUser();

    public Task<IResponse<Session?>> GetCurrentSession();

    public Task<IResponse<Session>> Login(string username, string password);

    public Task<IResponse<Session>> Register(string username, string password);

    public Task<IResponse> Logout();

    public Task<IResponse> UpdatePassword(string password);

    /// <summary>Schickt eine Mail mit Link auf die Seite reset-password.</summary>
    public Task<IResponse> SendPasswordReset(string email, string appBaseUri);

    /// <summary>Meldet den Nutzer über den Link aus der Reset-Mail an.</summary>
    public Task<IResponse> RecoverSessionFromUrl(Uri url);

    public Task<IResponse<ProfileDto>> UpdateProfile(ProfileDto profile);
}
