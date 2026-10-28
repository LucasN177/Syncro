using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Interfaces.Services;

namespace Syncro.Pages.Dialogs;

public partial class AuthDialog : ComponentBase
{
    private enum AuthMode
    {
        Login,
        Register,
        Reset
    }

    [CascadingParameter] public required IMudDialogInstance MudDialog { get; set; }
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter] public bool StartInRegistration { get; set; }

    private AuthMode _mode;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _errorMessage;
    private bool _isSubmitting;
    private bool _showPassword;
    private bool _resetMailSent;

    private string Title => _mode switch
    {
        AuthMode.Register => "Account erstellen",
        AuthMode.Reset => "Passwort vergessen",
        _ => "Willkommen zurück"
    };

    private string Subtitle => _mode switch
    {
        AuthMode.Register => "Starte mit deinem Syncro-Workspace.",
        AuthMode.Reset => "Wir schicken dir einen Link zum Zurücksetzen.",
        _ => "Melde dich bei deinem Workspace an."
    };

    private string SubmitText => _mode switch
    {
        AuthMode.Register => "Account erstellen",
        AuthMode.Reset => "Link senden",
        _ => "Anmelden"
    };

    protected override void OnParametersSet() => _mode = StartInRegistration ? AuthMode.Register : AuthMode.Login;

    private void SetMode(AuthMode mode)
    {
        _mode = mode;
        _errorMessage = null;
        _resetMailSent = false;
    }

    private async Task SubmitAsync()
    {
        _errorMessage = null;
        if (_mode == AuthMode.Reset)
        {
            await SendResetMailAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(_email) || string.IsNullOrWhiteSpace(_password))
        {
            _errorMessage = "Bitte gib deine E-Mail-Adresse und dein Passwort ein.";
            return;
        }

        var isRegistering = _mode == AuthMode.Register;
        if (isRegistering && _password != _confirmPassword)
        {
            _errorMessage = "Die beiden Passwörter stimmen nicht überein.";
            return;
        }

        _isSubmitting = true;
        var result = isRegistering
            ? await AuthService.Register(_email, _password)
            : await AuthService.Login(_email, _password);
        _isSubmitting = false;

        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Die Anmeldung war nicht erfolgreich.";
            return;
        }

        if (isRegistering && AuthService.CurrentUser is null)
            Snackbar.Add("Account erstellt. Bitte bestätige deine E-Mail-Adresse und melde dich anschließend an.", Severity.Success);
        else
            Snackbar.Add(isRegistering ? "Dein Account wurde erstellt." : "Erfolgreich angemeldet.", Severity.Success);

        MudDialog.Close(DialogResult.Ok(true));
    }

    private async Task SendResetMailAsync()
    {
        _resetMailSent = false;
        _isSubmitting = true;
        var result = await AuthService.SendPasswordReset(_email, Navigation.BaseUri);
        _isSubmitting = false;

        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Die Mail konnte nicht gesendet werden.";
            return;
        }

        _resetMailSent = true;
    }
}
