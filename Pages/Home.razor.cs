using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;
using Syncro.Layout.UI_Models;
using Syncro.Pages.Dialogs;
using Syncro.Services;

namespace Syncro.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IProjectMemberService MemberService { get; set; } = default!;
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IEventService EventService { get; set; } = default!;
    [Inject] private IActivityService ActivityService { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private record KpiCard(string Label, int Value, string Hint, string Icon, string Hex, double Progress);

    private record WorkloadEntry(string Name, string Hex, int Open);

    private record DonutSegment(string Label, int Count, string Hex);

    private readonly List<CalendarCategoryFilter> _categories = CalendarCategories.Create();
    private IReadOnlyList<ProjectDto> _projects = [];
    private IReadOnlyList<IssueDto> _issues = [];
    private IReadOnlyList<ActivityDto> _activities = [];
    private IReadOnlyList<CalendarEvent> _upcomingEvents = [];
    private Dictionary<Guid, ProfileDto> _profiles = [];
    private bool _isLoading;

    private bool IsAuthenticated => AuthService.IsAuthenticated;

    private string DisplayName => IsAuthenticated ? AuthService.DisplayName : "bei Syncro";

    private string HeroText
    {
        get
        {
            if (!IsAuthenticated)
                return "Melde dich an, um deine Projekte sicher mit deinem persönlichen Workspace zu synchronisieren.";
            if (!_projects.Any())
                return "Dein Workspace ist bereit. Lege ein Projekt an, um Aufgaben zu planen und dein Team einzuladen.";

            var overdue = MyIssues.Count(issue => issue.DueDate is { } due && due.Date < DateTime.Today);
            return overdue > 0
                ? $"Du hast {MyIssues.Count()} offene Aufgaben, davon {overdue} überfällig."
                : $"Du hast {MyIssues.Count()} offene Aufgaben in {ActiveProjects.Count()} aktiven Projekten.";
        }
    }

    private IEnumerable<ProjectDto> ActiveProjects => _projects.Where(project => project.Status != "Archiviert");

    private IEnumerable<IssueDto> OpenIssues => _issues.Where(issue => issue.Status.ToColumn() != Column.Done);

    private IEnumerable<IssueDto> MyIssues => OpenIssues
        .Where(issue => issue.AssigneeId == AuthService.CurrentUserId)
        .OrderBy(issue => issue.DueDate ?? DateTime.MaxValue)
        .ThenByDescending(issue => IssueOptions.Priorities.ToList().IndexOf(issue.Priority));

    private int TotalIssues => _issues.Count;

    private IEnumerable<KpiCard> Kpis
    {
        get
        {
            var total = Math.Max(1, TotalIssues);
            var open = OpenIssues.ToList();
            var done = TotalIssues - open.Count;
            var inProgress = open.Count(issue => issue.Status.ToColumn() is Column.InProgress or Column.Review);
            var inReview = open.Count(issue => issue.Status.ToColumn() == Column.Review);
            var overdue = open.Count(issue => issue.DueDate is { } due && due.Date < DateTime.Today);
            var bugs = open.Where(issue => issue.Type == "Bug").ToList();
            var urgentBugs = bugs.Count(issue => issue.Priority is "High" or "Critical");

            return
            [
                new("Offene Issues", open.Count, overdue > 0 ? $"davon {overdue} überfällig" : "nichts überfällig",
                    Icons.Material.Outlined.Assignment, "#6366F1", open.Count * 100.0 / total),
                new("Erledigt", done, $"{done * 100 / total} % aller Issues",
                    Icons.Material.Outlined.CheckCircle, "#10B981", done * 100.0 / total),
                new("In Arbeit", inProgress, $"davon {inReview} in Review",
                    Icons.Material.Outlined.Autorenew, "#F59E0B", inProgress * 100.0 / total),
                new("Offene Bugs", bugs.Count, $"{urgentBugs} mit hoher Priorität",
                    Icons.Material.Outlined.BugReport, "#EF4444", bugs.Count * 100.0 / Math.Max(1, open.Count))
            ];
        }
    }

    private IEnumerable<WorkloadEntry> Workload => OpenIssues
        .Where(issue => issue.AssigneeId is not null)
        .GroupBy(issue => issue.AssigneeId!.Value)
        .Select(group =>
        {
            var profile = _profiles.GetValueOrDefault(group.Key);
            return new WorkloadEntry(profile?.DisplayName ?? "Ehemaliges Mitglied", profile?.AvatarColor ?? "#64748b", group.Count());
        })
        .OrderByDescending(entry => entry.Open)
        .Take(6)
        .ToList();

    private IEnumerable<DonutSegment> Segments =>
    [
        new(Column.Done.ToDisplayName(), CountIn(Column.Done), "#10B981"),
        new(Column.Review.ToDisplayName(), CountIn(Column.Review), "#2563EB"),
        new(Column.InProgress.ToDisplayName(), CountIn(Column.InProgress), "#F59E0B"),
        new(Column.Todo.ToDisplayName(), CountIn(Column.Todo), "#6366F1"),
        new(Column.Backlog.ToDisplayName(), CountIn(Column.Backlog), "#94A3B8")
    ];

    private int CountIn(Column column) => _issues.Count(issue => issue.Status.ToColumn() == column);

    protected override async Task OnInitializedAsync()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        await LoadDashboard();
    }

    public void Dispose() => AuthService.OnAuthStateChanged -= HandleAuthStateChanged;

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await LoadDashboard();
        StateHasChanged();
    });

    private async Task LoadDashboard()
    {
        if (!IsAuthenticated)
        {
            _projects = [];
            _issues = [];
            _activities = [];
            _upcomingEvents = [];
            return;
        }

        _isLoading = true;

        var projectsResult = await ProjectService.GetProjects();
        _projects = projectsResult.Data ?? [];
        var activeIds = ActiveProjects.Select(project => project.Id).ToList();

        var issuesTask = IssueService.GetIssues(activeIds);
        var activityTask = ActivityService.GetRecentActivity(8);
        var eventsTask = EventService.GetEvents(DateTime.Now, DateTime.Today.AddDays(15));
        var membersTask = Task.WhenAll(activeIds.Select(MemberService.GetMembers));
        await Task.WhenAll(issuesTask, activityTask, eventsTask, membersTask);

        _issues = issuesTask.Result.Data ?? [];
        _activities = activityTask.Result.Data ?? [];
        _upcomingEvents = (eventsTask.Result.Data ?? []).OrderBy(e => e.Start).Take(5).ToList();
        _profiles = membersTask.Result
            .SelectMany(result => result.Data ?? [])
            .Where(member => member.Profile is not null)
            .DistinctBy(member => member.UserId)
            .ToDictionary(member => member.UserId, member => member.Profile!);

        if (!projectsResult.IsSuccess)
            Snackbar.Add(projectsResult.ErrorMessage ?? "Dashboard konnte nicht geladen werden.", Severity.Error);

        _isLoading = false;
    }

    private ProjectDto? ProjectFor(Guid projectId) => _projects.FirstOrDefault(project => project.Id == projectId);

    private ProfileDto? ProfileFor(Guid? userId) =>
        userId is { } id ? _profiles.GetValueOrDefault(id) ?? (id == AuthService.CurrentUserId ? AuthService.CurrentProfile : null) : null;

    private void OpenBoard(Guid projectId)
    {
        AppState.SetCurrentProject(projectId);
        Navigation.NavigateTo($"projects/{projectId}/board");
    }

    private static Color ActivityColor(string action) => action switch
    {
        "issue_created" or "project_created" => Color.Primary,
        "issue_moved" => Color.Warning,
        "issue_assigned" or "member_added" => Color.Info,
        "comment_added" => Color.Secondary,
        "issue_deleted" or "member_removed" => Color.Error,
        _ => Color.Default
    };

    private async Task OpenAuthDialogAsync(bool register)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };
        var parameters = new DialogParameters<AuthDialog> { { dialog => dialog.StartInRegistration, register } };
        await DialogService.ShowAsync<AuthDialog>(register ? "Registrieren" : "Anmelden", parameters, options);
    }

    // ── CSV-Export ───────────────────────────────────────────────────────────

    private async Task ExportCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Projekt;Nummer;Titel;Typ;Priorität;Status;Zugewiesen;Fällig;Fortschritt;Tags;Erstellt");

        foreach (var issue in _issues.OrderBy(issue => ProjectFor(issue.ProjectId)?.Name).ThenBy(issue => issue.Number))
        {
            var project = ProjectFor(issue.ProjectId);
            csv.AppendLine(string.Join(';',
                Escape(project?.Name),
                Escape(issue.ToIssueKey(project?.Key)),
                Escape(issue.Title),
                Escape(IssueOptions.TypeDisplayName(issue.Type)),
                Escape(IssueOptions.PriorityDisplayName(issue.Priority)),
                Escape(issue.Status.ToColumn().ToDisplayName()),
                Escape(ProfileFor(issue.AssigneeId)?.DisplayName),
                Escape(issue.DueDate?.ToString("dd.MM.yyyy")),
                Escape(issue.Progress?.ToString()),
                Escape(string.Join(", ", issue.Tags)),
                Escape(issue.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy"))));
        }

        // BOM, damit Excel Umlaute korrekt erkennt
        await JS.InvokeVoidAsync("syncroDownloadFile",
            $"syncro-issues-{DateTime.Today:yyyy-MM-dd}.csv", "﻿" + csv, "text/csv;charset=utf-8");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    // ── SVG-Donut ────────────────────────────────────────────────────────────

    private string CalcDash(int count, int total)
    {
        var circ = CalcCircumference(55);
        var dash = total == 0 ? 0 : circ * count / total;
        return dash.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    // Negativer Offset: das Segment beginnt genau dort, wo das vorherige endet
    private string CalcOffset(int startCount, int total, double r)
    {
        var circ = 2 * Math.PI * r;
        var offset = total == 0 ? 0 : -circ * startCount / total;
        return offset.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    private double CalcCircumference(double r) => 2 * Math.PI * r;
}
