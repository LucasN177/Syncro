using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models.Database;

namespace Syncro.Pages.Dialogs;

public record ProjectDialogResult(ProjectDto? Project, bool Deleted);

public partial class ProjectDialog : ComponentBase
{
    public static readonly string[] ProjectStatuses = ["Aktiv", "Pausiert", "Abgeschlossen", "Archiviert"];

    private static readonly string[] Colors =
    [
        "#0f766e", "#2563eb", "#7c3aed", "#db2777", "#dc2626", "#ea580c", "#ca8a04", "#16a34a", "#475569"
    ];

    [CascadingParameter] public required IMudDialogInstance MudDialog { get; set; }
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    /// <summary>Bearbeitungskopie; Id leer für ein neues Projekt.</summary>
    [Parameter] public ProjectDto Project { get; set; } = new();

    /// <summary>Nur der Eigentümer darf ein Projekt löschen.</summary>
    [Parameter] public bool CanDelete { get; set; }

    private bool IsNew => Project.Id == Guid.Empty;
    private string? _errorMessage;
    private bool _isBusy;

    private void OnKeyChanged(string? value)
    {
        Project.Key = string.IsNullOrWhiteSpace(value)
            ? null
            : Regex.Replace(value.ToUpperInvariant(), "[^A-Z0-9]", string.Empty);
    }

    private async Task SaveAsync()
    {
        _errorMessage = null;
        if (string.IsNullOrWhiteSpace(Project.Name))
        {
            _errorMessage = "Bitte gib einen Projektnamen ein.";
            return;
        }

        if (!string.IsNullOrEmpty(Project.Key) && !Regex.IsMatch(Project.Key, "^[A-Z][A-Z0-9]{1,9}$"))
        {
            _errorMessage = "Das Kürzel muss mit einem Buchstaben beginnen und 2–10 Zeichen lang sein.";
            return;
        }

        if (!IsNew && string.IsNullOrEmpty(Project.Key))
        {
            _errorMessage = "Bitte gib ein Kürzel ein.";
            return;
        }

        Project.Name = Project.Name.Trim();
        _isBusy = true;
        var result = await ProjectService.SaveProject(Project);
        _isBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _errorMessage = result.ErrorMessage ?? "Projekt konnte nicht gespeichert werden.";
            return;
        }

        MudDialog.Close(DialogResult.Ok(new ProjectDialogResult(result.Data, false)));
    }

    private async Task DeleteAsync()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Projekt löschen?",
            $"„{Project.Name}“ wird mit allen Issues, Kommentaren und Projektterminen endgültig gelöscht.",
            yesText: "Endgültig löschen", cancelText: "Abbrechen");

        if (confirmed != true)
            return;

        _isBusy = true;
        var result = await ProjectService.DeleteProject(Project.Id);
        _isBusy = false;

        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Projekt konnte nicht gelöscht werden.";
            return;
        }

        MudDialog.Close(DialogResult.Ok(new ProjectDialogResult(null, true)));
    }

    private void Cancel() => MudDialog.Cancel();
}
