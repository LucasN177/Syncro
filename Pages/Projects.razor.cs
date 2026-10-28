using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models.Database;

namespace Syncro.Pages;

public partial class Projects : ComponentBase, IDisposable
{
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<ProjectDto> _projects = [];
    private ProjectDto? _editingProject;
    private bool _isLoading = true;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        await LoadProjects();
    }

    private async Task LoadProjects()
    {
        _projects.Clear();
        if (!AuthService.IsAuthenticated)
        {
            _isLoading = false;
            return;
        }

        _isLoading = true;
        var result = await ProjectService.GetProjects();
        if (result.IsSuccess && result.Data is not null)
            _projects.AddRange(result.Data);
        else
            Snackbar.Add(result.ErrorMessage ?? "Projekte konnten nicht geladen werden.", Severity.Error);

        _isLoading = false;
    }

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await LoadProjects();
        StateHasChanged();
    });

    public void Dispose() => AuthService.OnAuthStateChanged -= HandleAuthStateChanged;

    private void CreateProject() => _editingProject = new ProjectDto { Color = "#0f766e" };

    private void EditProject(ProjectDto project) => _editingProject = Copy(project);

    private void CloseEditor() => _editingProject = null;

    private async Task SaveProject()
    {
        if (_editingProject is null || string.IsNullOrWhiteSpace(_editingProject.Name))
        {
            Snackbar.Add("Bitte gib einen Projektnamen ein.", Severity.Warning);
            return;
        }

        _isSaving = true;
        var result = await ProjectService.SaveProject(_editingProject);
        _isSaving = false;

        if (!result.IsSuccess || result.Data is null)
        {
            Snackbar.Add(result.ErrorMessage ?? "Projekt konnte nicht gespeichert werden.", Severity.Error);
            return;
        }

        var index = _projects.FindIndex(project => project.Id == result.Data.Id);
        if (index >= 0)
            _projects[index] = result.Data;
        else
            _projects.Insert(0, result.Data);

        _editingProject = null;
        Snackbar.Add("Projekt wurde gespeichert.", Severity.Success);
    }

    private static ProjectDto Copy(ProjectDto project) => new()
    {
        Id = project.Id,
        UserId = project.UserId,
        Name = project.Name,
        Description = project.Description,
        Status = project.Status,
        Color = project.Color,
        CreatedAt = project.CreatedAt
    };

    private static string ProjectInitials(string name) => string.Concat(
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));

    private static Color StatusColor(string status) => status switch
    {
        "Aktiv" => Color.Success,
        "Pausiert" => Color.Warning,
        "Abgeschlossen" => Color.Info,
        _ => Color.Default
    };
}
