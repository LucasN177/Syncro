using Microsoft.AspNetCore.Components;
using Syncro.Core.Interfaces.Services;

namespace Syncro.Pages;

/// <summary>
/// Ziel des Links aus der Mail "Passwort zurücksetzen". Supabase hängt die Session
/// (oder einen Fehler) als URL-Fragment an, z. B. #access_token=…&amp;type=recovery.
/// </summary>
public partial class ResetPassword : ComponentBase
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isDone;
    private string? _linkError;
    private string? _errorMessage;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var uri = new Uri(Navigation.Uri);
        var fragment = ParseFragment(uri.Fragment);

        if (fragment.TryGetValue("error_description", out var error))
        {
            _linkError = $"Der Link funktioniert nicht: {error}";
        }
        else if (fragment.ContainsKey("access_token"))
        {
            var result = await AuthService.RecoverSessionFromUrl(uri);
            if (result.IsSuccess)
                // Tokens nicht im Verlauf stehen lassen
                Navigation.NavigateTo("reset-password", replace: true);
            else
                _linkError = result.ErrorMessage;
        }

        _isLoading = false;
    }

    private async Task SavePassword()
    {
        _errorMessage = null;
        if (_newPassword.Length < 8)
        {
            _errorMessage = "Das Passwort muss mindestens 8 Zeichen lang sein.";
            return;
        }

        if (_newPassword != _confirmPassword)
        {
            _errorMessage = "Die beiden Passwörter stimmen nicht überein.";
            return;
        }

        _isSaving = true;
        var result = await AuthService.UpdatePassword(_newPassword);
        _isSaving = false;

        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Passwort konnte nicht geändert werden.";
            return;
        }

        _isDone = true;
    }

    private static Dictionary<string, string> ParseFragment(string fragment) =>
        fragment.TrimStart('#')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .GroupBy(pair => pair[0])
            .ToDictionary(group => group.Key, group => Uri.UnescapeDataString(group.First()[1].Replace('+', ' ')));
}
