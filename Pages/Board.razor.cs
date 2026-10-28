using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;
using Syncro.Pages.Dialogs;
using Syncro.Services;

namespace Syncro.Pages;

public partial class Board : ComponentBase, IAsyncDisposable
{
    private const string AllFilter = "all";
    private const string MeFilter = "me";
    private const string UnassignedFilter = "none";

    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IProjectMemberService MemberService { get; set; } = default!;
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IRealtimeService RealtimeService { get; set; } = default!;

    /// <summary>Aus der Route /projects/{id}/board; ohne Id wird das zuletzt genutzte Projekt geöffnet.</summary>
    [Parameter] public Guid? ProjectId { get; set; }

    private readonly List<KanbanColumn> _columns = BoardColumns.Create();
    private readonly List<KanbanCard> _allCards = [];
    private readonly List<IssueDto> _issues = [];
    private IReadOnlyList<ProjectDto> _projects = [];
    private IReadOnlyList<ProjectMember> _members = [];
    private ProjectDto? _project;
    private MudDropContainer<KanbanCard>? _dropContainer;
    private bool _isLoading = true;
    private bool _canEdit;
    private bool _isAdmin;
    private IAsyncDisposable? _issuesSubscription;
    private CancellationTokenSource? _reloadDebounce;

    private string _search = string.Empty;
    private string _assigneeFilter = AllFilter;
    private string _priorityFilter = AllFilter;
    private string _typeFilter = AllFilter;

    private int OpenCardCount => _allCards.Count(c => c.Column != Column.Done);

    private bool IsFiltered => !string.IsNullOrWhiteSpace(_search) || _assigneeFilter != AllFilter
                               || _priorityFilter != AllFilter || _typeFilter != AllFilter;

    protected override void OnInitialized() => AuthService.OnAuthStateChanged += HandleAuthStateChanged;

    protected override async Task OnParametersSetAsync() => await LoadBoard();

    public async ValueTask DisposeAsync()
    {
        AuthService.OnAuthStateChanged -= HandleAuthStateChanged;
        _reloadDebounce?.Cancel();
        if (_issuesSubscription is not null)
            await _issuesSubscription.DisposeAsync();
    }

    // ── Realtime ─────────────────────────────────────────────────────────────

    private async Task SubscribeToChanges()
    {
        if (_issuesSubscription is not null || !AuthService.IsAuthenticated)
            return;

        _issuesSubscription = await RealtimeService.SubscribeToIssues(OnIssuesChanged);
    }

    // Mehrere Änderungen kurz hintereinander (z. B. Drag & Drop) lösen nur ein Neuladen aus
    private void OnIssuesChanged()
    {
        _reloadDebounce?.Cancel();
        var debounce = _reloadDebounce = new CancellationTokenSource();

        _ = InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(400, debounce.Token);
                await LoadIssues();
                StateHasChanged();
            }
            catch (TaskCanceledException)
            {
                // durch eine neuere Änderung ersetzt
            }
        });
    }

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await LoadBoard();
        StateHasChanged();
    });

    private async Task LoadBoard()
    {
        _project = null;
        _allCards.Clear();
        if (!AuthService.IsAuthenticated)
        {
            _isLoading = false;
            return;
        }

        _isLoading = true;
        var projectsResult = await ProjectService.GetProjects();
        if (projectsResult is not { IsSuccess: true, Data: not null })
        {
            Snackbar.Add(projectsResult.ErrorMessage ?? "Projekte konnten nicht geladen werden.", Severity.Error);
            _isLoading = false;
            return;
        }

        _projects = projectsResult.Data;
        _project = _projects.FirstOrDefault(project => project.Id == ProjectId)
                   ?? _projects.FirstOrDefault(project => project.Id == AppState.CurrentProjectId)
                   ?? _projects.FirstOrDefault(project => project.Status == "Aktiv")
                   ?? _projects.FirstOrDefault();

        if (_project is null)
        {
            _isLoading = false;
            return;
        }

        if (ProjectId is { } requestedId && requestedId != _project.Id)
            Snackbar.Add("Das Projekt wurde nicht gefunden oder du bist kein Mitglied.", Severity.Warning);

        AppState.SetCurrentProject(_project.Id);

        var membersResult = await MemberService.GetMembers(_project.Id);
        _members = membersResult.Data ?? [];

        var myRole = _project.UserId == AuthService.CurrentUser?.Id
            ? ProjectRoles.Admin
            : _members.FirstOrDefault(member => member.UserId == AuthService.CurrentUserId)?.Role;
        _canEdit = ProjectRoles.CanEdit(myRole);
        _isAdmin = myRole == ProjectRoles.Admin;

        await LoadIssues();
        _isLoading = false;

        await SubscribeToChanges();
    }

    private async Task LoadIssues()
    {
        if (_project is null)
            return;

        var issuesResult = await IssueService.GetIssues(_project.Id);
        if (issuesResult is not { IsSuccess: true, Data: not null })
        {
            Snackbar.Add(issuesResult.ErrorMessage ?? "Issues konnten nicht geladen werden.", Severity.Error);
            return;
        }

        var profiles = _members
            .Where(member => member.Profile is not null)
            .ToDictionary(member => member.UserId, member => member.Profile!);

        _issues.Clear();
        _issues.AddRange(issuesResult.Data);

        _allCards.Clear();
        _allCards.AddRange(_issues.Select(issue => issue.ToKanbanCard(
            _project.Key,
            issue.AssigneeId is { } assigneeId ? profiles.GetValueOrDefault(assigneeId) : null)));
        SortCards();
        _dropContainer?.Refresh();
    }

    private void SelectProject(Guid projectId) => Navigation.NavigateTo($"projects/{projectId}/board");

    private string ProjectName(Guid projectId) =>
        _projects.FirstOrDefault(project => project.Id == projectId)?.Name ?? string.Empty;

    private int CardCountFor(Column column) =>
        _allCards.Count(c => c.Column == column && MatchesFilter(c));

    private void SortCards() => _allCards.Sort((a, b) => a.Position.CompareTo(b.Position));

    // ── Filter ───────────────────────────────────────────────────────────────

    private bool MatchesFilter(KanbanCard card)
    {
        if (!string.IsNullOrWhiteSpace(_search))
        {
            var search = _search.Trim();
            var matchesSearch = card.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                                || card.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                                || card.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase));
            if (!matchesSearch)
                return false;
        }

        var matchesAssignee = _assigneeFilter switch
        {
            AllFilter => true,
            MeFilter => card.AssigneeId == AuthService.CurrentUserId,
            UnassignedFilter => card.AssigneeId is null,
            _ => card.AssigneeId?.ToString() == _assigneeFilter
        };

        return matchesAssignee
               && (_priorityFilter == AllFilter || card.Priority == _priorityFilter)
               && (_typeFilter == AllFilter || card.Type == _typeFilter);
    }

    private void OnSearchChanged(string? value) => ApplyFilter(() => _search = value ?? string.Empty);
    private void OnAssigneeFilterChanged(string value) => ApplyFilter(() => _assigneeFilter = value);
    private void OnPriorityFilterChanged(string value) => ApplyFilter(() => _priorityFilter = value);
    private void OnTypeFilterChanged(string value) => ApplyFilter(() => _typeFilter = value);

    private void ResetFilters() => ApplyFilter(() =>
    {
        _search = string.Empty;
        _assigneeFilter = _priorityFilter = _typeFilter = AllFilter;
    });

    private void ApplyFilter(Action change)
    {
        change();
        _dropContainer?.Refresh();
    }

    // ── Drag & Drop ──────────────────────────────────────────────────────────

    private async Task CardDropped(MudItemDropInfo<KanbanCard> dropInfo)
    {
        if (dropInfo.Item is not { } card || !_canEdit)
            return;

        var targetColumn = dropInfo.DropzoneIdentifier.ToColumn();
        var zoneCards = _allCards
            .Where(other => other != card && other.Column == targetColumn && MatchesFilter(other))
            .OrderBy(other => other.Position)
            .ToList();

        var index = Math.Clamp(dropInfo.IndexInZone, 0, zoneCards.Count);
        double? before = index > 0 ? zoneCards[index - 1].Position : null;
        double? after = index < zoneCards.Count ? zoneCards[index].Position : null;

        var previousColumn = card.Column;
        card.Column = targetColumn;

        var result = await IssueService.MoveIssue(card.IssueId, targetColumn, before, after);
        if (result.IsSuccess)
        {
            card.Position = result.Data;
        }
        else
        {
            card.Column = previousColumn;
            Snackbar.Add(result.ErrorMessage ?? "Issue konnte nicht verschoben werden.", Severity.Error);
        }

        SortCards();
        _dropContainer?.Refresh();
    }

    // ── Dialog ───────────────────────────────────────────────────────────────

    private Task CreateIssue(Column column)
    {
        if (_project is null)
            return Task.CompletedTask;

        return OpenIssueDialog(new IssueDto
        {
            ProjectId = _project.Id,
            Status = column.ToStatus()
        });
    }

    private Task EditIssue(KanbanCard card)
    {
        var issue = _issues.FirstOrDefault(issue => issue.Id == card.IssueId);
        return issue is null ? Task.CompletedTask : OpenIssueDialog(issue.Copy());
    }

    private async Task OpenIssueDialog(IssueDto issue)
    {
        var parameters = new DialogParameters<IssueDialog>
        {
            { dialog => dialog.Issue, issue },
            { dialog => dialog.ProjectKey, _project?.Key },
            { dialog => dialog.Members, _members },
            { dialog => dialog.CanEdit, _canEdit },
            { dialog => dialog.IsAdmin, _isAdmin }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };

        var dialog = await DialogService.ShowAsync<IssueDialog>(string.Empty, parameters, options);
        await dialog.Result;

        // Auch nach Abbrechen neu laden: Kommentare werden im Dialog sofort gespeichert
        await LoadIssues();
        StateHasChanged();
    }
}
