using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models.Database;
using Syncro.Services;

namespace Syncro.Pages;

public partial class Settings : ComponentBase, IDisposable
{
    private static readonly string[] AvatarColors =
    [
        "#0f766e", "#2563eb", "#7c3aed", "#db2777", "#dc2626", "#ea580c", "#ca8a04", "#16a34a", "#475569"
    ];

    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private async Task ToggleBrowserNotifications(bool enabled)
    {
        if (!enabled)
        {
            AppState.SetBrowserNotifications(false);
            return;
        }

        var permission = await JS.InvokeAsync<string>("syncroNotifications.request");
        switch (permission)
        {
            case "granted":
                AppState.SetBrowserNotifications(true);
                await JS.InvokeVoidAsync("syncroNotifications.show", "Syncro", "Browser-Benachrichtigungen sind aktiviert.");
                break;
            case "unsupported":
                Snackbar.Add("Dein Browser unterstützt keine Benachrichtigungen.", Severity.Warning);
                break;
            default:
                Snackbar.Add("Benachrichtigungen wurden im Browser blockiert. Du kannst sie in den Website-Einstellungen erlauben.", Severity.Warning);
                break;
        }
    }

    // Bearbeitungskopie, damit Änderungen erst nach dem Speichern überall sichtbar werden
    private ProfileDto _profile = new();
    private bool _isSavingProfile;

    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _passwordError;
    private bool _isSavingPassword;

    protected override void OnInitialized()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        AppState.OnChange += HandleAppStateChanged;
        LoadProfile();
    }

    public void Dispose()
    {
        AuthService.OnAuthStateChanged -= HandleAuthStateChanged;
        AppState.OnChange -= HandleAppStateChanged;
    }

    private void HandleAuthStateChanged() => InvokeAsync(() =>
    {
        LoadProfile();
        StateHasChanged();
    });

    private void HandleAppStateChanged() => InvokeAsync(StateHasChanged);

    private void LoadProfile()
    {
        var current = AuthService.CurrentProfile;
        _profile = current is null
            ? new ProfileDto { Email = AuthService.CurrentUser?.Email }
            : new ProfileDto
            {
                Id = current.Id,
                Email = current.Email,
                Username = current.Username,
                Vorname = current.Vorname,
                Nachname = current.Nachname,
                AvatarColor = current.AvatarColor
            };
    }

    private async Task SaveProfile()
    {
        if (_profile.Id == Guid.Empty)
        {
            Snackbar.Add("Dein Profil wurde nicht gefunden. Wurde 001_profiles.sql in Supabase ausgeführt?", Severity.Error);
            return;
        }

        _profile.Username = _profile.Username.Trim();
        _profile.Vorname = _profile.Vorname.Trim();
        _profile.Nachname = _profile.Nachname.Trim();

        _isSavingProfile = true;
        var result = await AuthService.UpdateProfile(_profile);
        _isSavingProfile = false;

        Snackbar.Add(result.IsSuccess ? "Profil wurde gespeichert." : result.ErrorMessage ?? "Profil konnte nicht gespeichert werden.",
            result.IsSuccess ? Severity.Success : Severity.Error);
    }

    private async Task ChangePassword()
    {
        _passwordError = null;
        if (_newPassword.Length < 8)
        {
            _passwordError = "Das Passwort muss mindestens 8 Zeichen lang sein.";
            return;
        }

        if (_newPassword != _confirmPassword)
        {
            _passwordError = "Die beiden Passwörter stimmen nicht überein.";
            return;
        }

        _isSavingPassword = true;
        var result = await AuthService.UpdatePassword(_newPassword);
        _isSavingPassword = false;

        if (!result.IsSuccess)
        {
            _passwordError = result.ErrorMessage ?? "Passwort konnte nicht geändert werden.";
            return;
        }

        _newPassword = _confirmPassword = string.Empty;
        Snackbar.Add("Passwort wurde geändert.", Severity.Success);
    }

    private async Task Logout()
    {
        var result = await AuthService.Logout();
        if (!result.IsSuccess)
        {
            Snackbar.Add(result.ErrorMessage ?? "Abmelden nicht möglich.", Severity.Error);
            return;
        }

        Navigation.NavigateTo("");
    }
}
