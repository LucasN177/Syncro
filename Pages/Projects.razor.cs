using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;
using Syncro.Pages.Dialogs;
using Syncro.Services;

namespace Syncro.Pages;

public partial class Projects : ComponentBase, IDisposable
{
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IProjectMemberService MemberService { get; set; } = default!;
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly List<ProjectDto> _projects = [];
    private IReadOnlyDictionary<Guid, string> _roles = new Dictionary<Guid, string>();
    private Dictionary<Guid, ProjectStats> _stats = [];
    private bool _isLoading = true;
    private bool _showArchived;

    private record ProjectStats(int Open, int Done)
    {
        public int Percent => Open + Done == 0 ? 0 : Done * 100 / (Open + Done);
    }

    private IEnumerable<ProjectDto> VisibleProjects =>
        _projects.Where(project => _showArchived || project.Status != "Archiviert");

    private int ActiveProjectCount => _projects.Count(project => project.Status == "Aktiv");

    protected override async Task OnInitializedAsync()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        await LoadProjects();
    }

    private async Task LoadProjects()
    {
        _projects.Clear();
        _stats = [];
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

        var rolesResult = await MemberService.GetMyRoles();
        _roles = rolesResult.Data ?? new Dictionary<Guid, string>();

        var issuesResult = await IssueService.GetIssues(_projects.Select(project => project.Id));
        _stats = (issuesResult.Data ?? [])
            .GroupBy(issue => issue.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => new ProjectStats(
                    group.Count(issue => issue.Status.ToColumn() != Column.Done),
                    group.Count(issue => issue.Status.ToColumn() == Column.Done)));

        _isLoading = false;
    }

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await LoadProjects();
        StateHasChanged();
    });

    public void Dispose() => AuthService.OnAuthStateChanged -= HandleAuthStateChanged;

    private ProjectStats StatsFor(Guid projectId) => _stats.GetValueOrDefault(projectId) ?? new ProjectStats(0, 0);

    private bool IsOwner(ProjectDto project) => project.UserId == AuthService.CurrentUser?.Id;

    private string? RoleFor(ProjectDto project) =>
        IsOwner(project) ? ProjectRoles.Admin : _roles.GetValueOrDefault(project.Id);

    private void OpenBoard(ProjectDto project)
    {
        AppState.SetCurrentProject(project.Id);
        Navigation.NavigateTo($"projects/{project.Id}/board");
    }

    private Task CreateProject() => OpenProjectDialog(new ProjectDto { Color = "#0f766e" });

    private Task EditProject(ProjectDto project) => OpenProjectDialog(Copy(project));

    private async Task OpenProjectDialog(ProjectDto project)
    {
        var parameters = new DialogParameters<ProjectDialog>
        {
            { dialog => dialog.Project, project },
            { dialog => dialog.CanDelete, project.Id != Guid.Empty && IsOwner(project) }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };

        var dialog = await DialogService.ShowAsync<ProjectDialog>(string.Empty, parameters, options);
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: ProjectDialogResult dialogResult })
            return;

        if (dialogResult.Deleted)
        {
            _projects.RemoveAll(existing => existing.Id == project.Id);
            if (AppState.CurrentProjectId == project.Id)
                AppState.SetCurrentProject(null);
            Snackbar.Add("Projekt wurde gelöscht.", Severity.Success);
        }
        else if (dialogResult.Project is { } saved)
        {
            var index = _projects.FindIndex(existing => existing.Id == saved.Id);
            if (index >= 0)
                _projects[index] = saved;
            else
                _projects.Insert(0, saved);
            Snackbar.Add("Projekt wurde gespeichert.", Severity.Success);
        }

        StateHasChanged();
    }

    private async Task ToggleArchived(ProjectDto project)
    {
        var copy = Copy(project);
        copy.Status = project.Status == "Archiviert" ? "Aktiv" : "Archiviert";

        var result = await ProjectService.SaveProject(copy);
        if (!result.IsSuccess || result.Data is null)
        {
            Snackbar.Add(result.ErrorMessage ?? "Status konnte nicht geändert werden.", Severity.Error);
            return;
        }

        var index = _projects.FindIndex(existing => existing.Id == project.Id);
        _projects[index] = result.Data;
        Snackbar.Add(copy.Status == "Archiviert" ? "Projekt wurde archiviert." : "Projekt ist wieder aktiv.", Severity.Success);
    }

    private static ProjectDto Copy(ProjectDto project) => new()
    {
        Id = project.Id,
        UserId = project.UserId,
        Name = project.Name,
        Description = project.Description,
        Status = project.Status,
        Color = project.Color,
        Key = project.Key,
        IssueCounter = project.IssueCounter,
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt
    };

    private static string ProjectInitials(ProjectDto project) =>
        string.IsNullOrWhiteSpace(project.Key) ? "?" : project.Key[..Math.Min(2, project.Key.Length)];

    private static Color StatusColor(string status) => status switch
    {
        "Aktiv" => Color.Success,
        "Pausiert" => Color.Warning,
        "Abgeschlossen" => Color.Info,
        _ => Color.Default
    };
}
