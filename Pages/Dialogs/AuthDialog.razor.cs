using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Interfaces.Services;

namespace Syncro.Pages.Dialogs;

public partial class AuthDialog : ComponentBase
{
    [CascadingParameter] public required IMudDialogInstance MudDialog { get; set; }
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter] public bool StartInRegistration { get; set; }

    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _errorMessage;
    private bool _isRegistering;
    private bool _isSubmitting;
    private bool _showPassword;

    protected override void OnParametersSet() => _isRegistering = StartInRegistration;

    private void ToggleMode()
    {
        _isRegistering = !_isRegistering;
        _errorMessage = null;
    }

    private async Task SubmitAsync()
    {
        _errorMessage = null;
        if (string.IsNullOrWhiteSpace(_email) || string.IsNullOrWhiteSpace(_password))
        {
            _errorMessage = "Bitte gib deine E-Mail-Adresse und dein Passwort ein.";
            return;
        }

        if (_isRegistering && _password != _confirmPassword)
        {
            _errorMessage = "Die beiden Passwörter stimmen nicht überein.";
            return;
        }

        _isSubmitting = true;
        var result = _isRegistering
            ? await AuthService.Register(_email, _password)
            : await AuthService.Login(_email, _password);
        _isSubmitting = false;

        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Die Anmeldung war nicht erfolgreich.";
            return;
        }

        if (_isRegistering && AuthService.CurrentUser is null)
            Snackbar.Add("Account erstellt. Bitte bestätige deine E-Mail-Adresse und melde dich anschließend an.", Severity.Success);
        else
            Snackbar.Add(_isRegistering ? "Dein Account wurde erstellt." : "Erfolgreich angemeldet.", Severity.Success);

        MudDialog.Close(DialogResult.Ok(true));
    }
}
