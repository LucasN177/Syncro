using Supabase.Gotrue;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Task = System.Threading.Tasks.Task;

namespace Syncro.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;

    public AuthService(IAuthRepository authRepository)
    {
        _authRepository = authRepository;
        // Token-Refresh oder Abmeldung durch Supabase im Hintergrund übernehmen
        _authRepository.AddAuthStateListener(SyncFromClient);
    }

    public Session? CurrentSession { get; private set; }
    public User? CurrentUser { get; private set; }
    public ProfileDto? CurrentProfile { get; private set; }

    public Guid? CurrentUserId => Guid.TryParse(CurrentUser?.Id, out var id) ? id : null;

    public bool IsAuthenticated => CurrentUser is not null;

    public string DisplayName => CurrentProfile?.DisplayName
                                 ?? CurrentUser?.Email?.Split('@')[0]
                                 ?? string.Empty;

    public event Action? OnAuthStateChanged;

    public async Task InitializeAsync()
    {
        CurrentSession = _authRepository.CurrentSession;
        CurrentUser = _authRepository.CurrentUser;
        await LoadProfile();
        NotifyAuthStateChanged();
    }

    public async Task<IResponse<User?>> GetCurrentUser()
    {
        var result =  await _authRepository.GetCurrentUser();
        if (result.IsSuccess)
        {
            CurrentUser = result.Data;
            return Response<User?>.Success(result.Data);
        }
        CurrentUser = null;
        return Response<User?>.Failure(result.ErrorMessage ?? "No current user");
    }

    public async Task<IResponse<Session?>> GetCurrentSession()
    {
        var result =  await _authRepository.GetCurrentSession();
        if (result.IsSuccess)
        {
            CurrentSession = result.Data;
            return Response<Session?>.Success(result.Data);
        }
        CurrentSession = null;
        CurrentUser = null;
        CurrentProfile = null;
        return Response<Session?>.Failure(result.ErrorMessage ?? "No current session");
    }

    public async Task<IResponse<Session>> Login(string username, string password)
    {
        var result =  await _authRepository.Login(username, password);
        if (result is not { IsSuccess: true, Data: not null })
            return Response<Session>.Failure(result.ErrorMessage ?? "Login failed");

        CurrentSession = result.Data;
        await GetCurrentUser();
        await LoadProfile();
        NotifyAuthStateChanged();
        return Response<Session>.Success(result.Data);
    }

    public async Task<IResponse<Session>> Register(string username, string password)
    {
        var result =  await _authRepository.Register(username, password);
        if (result is not { IsSuccess: true, Data: not null })
            return Response<Session>.Failure(result.ErrorMessage ?? "Register failed.");

        CurrentSession = result.Data;
        await GetCurrentUser();
        await LoadProfile();
        NotifyAuthStateChanged();
        return Response<Session>.Success(result.Data);
    }

    public async Task<IResponse> Logout()
    {
        var result = await _authRepository.Logout();
        if (!result.IsSuccess)
            return result;

        CurrentSession = null;
        CurrentUser = null;
        CurrentProfile = null;
        NotifyAuthStateChanged();
        return Response.Success();
    }

    public Task<IResponse> UpdatePassword(string password) => _authRepository.UpdatePassword(password);

    public async Task<IResponse<ProfileDto>> UpdateProfile(ProfileDto profile)
    {
        var result = await _authRepository.UpdateProfile(profile);
        if (result is { IsSuccess: true, Data: not null })
        {
            CurrentProfile = result.Data;
            NotifyAuthStateChanged();
        }
        return result;
    }

    private async Task LoadProfile()
    {
        if (CurrentUserId is not { } userId)
        {
            CurrentProfile = null;
            return;
        }

        var result = await _authRepository.GetProfile(userId);
        CurrentProfile = result.IsSuccess ? result.Data : null;
    }

    private async void SyncFromClient()
    {
        try
        {
            var previousUserId = CurrentUserId;
            CurrentSession = _authRepository.CurrentSession;
            CurrentUser = _authRepository.CurrentUser;

            if (CurrentUserId != previousUserId)
                await LoadProfile();

            NotifyAuthStateChanged();
        }
        catch (Exception)
        {
            // Hintergrund-Events dürfen die App nicht beenden
        }
    }

    private void NotifyAuthStateChanged() => OnAuthStateChanged?.Invoke();
}
