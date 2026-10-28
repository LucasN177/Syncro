using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;
using Syncro.Layout.Helper;
using Syncro.Layout.UI_Models;

namespace Syncro.Pages;

public partial class Reports : ComponentBase, IDisposable
{
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IProjectMemberService MemberService { get; set; } = default!;
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private record PersonRow(string Name, int Open, int Done, int Overdue);

    private record WeekBar(string Label, int Count);

    private IReadOnlyList<ProjectDto> _projects = [];
    private IReadOnlyList<IssueDto> _issues = [];
    private Dictionary<Guid, ProfileDto> _profiles = [];
    private Guid _selectedProjectId = Guid.Empty;
    private bool _isLoading = true;

    private IEnumerable<IssueDto> OpenIssues => _issues.Where(issue => issue.Status.ToColumn() != Column.Done);

    private IEnumerable<IssueDto> OverdueIssues => OpenIssues
        .Where(issue => issue.DueDate is { } due && due.Date < DateTime.Today)
        .OrderBy(issue => issue.DueDate);

    private IEnumerable<(string Label, string Value, string Hint, string Hex)> Kpis
    {
        get
        {
            var done = _issues.Count - OpenIssues.Count();
            var percent = _issues.Count == 0 ? 0 : done * 100 / _issues.Count;
            return
            [
                ("Issues gesamt", _issues.Count.ToString(), $"in {ProjectCount} Projekt(en)", "var(--mud-palette-text-primary)"),
                ("Erledigt", $"{percent} %", $"{done} von {_issues.Count}", "#10B981"),
                ("Offen", OpenIssues.Count().ToString(), $"{OpenIssues.Count(issue => issue.AssigneeId is null)} nicht zugewiesen", "#6366F1"),
                ("Überfällig", OverdueIssues.Count().ToString(), "offen mit Fälligkeit in der Vergangenheit", "#EF4444")
            ];
        }
    }

    private int ProjectCount => _issues.Select(issue => issue.ProjectId).Distinct().Count();

    private IReadOnlyList<BarItem> ByStatus =>
    [
        new(Column.Backlog.ToDisplayName(), CountIn(Column.Backlog), "#94A3B8"),
        new(Column.Todo.ToDisplayName(), CountIn(Column.Todo), "#6366F1"),
        new(Column.InProgress.ToDisplayName(), CountIn(Column.InProgress), "#F59E0B"),
        new(Column.Review.ToDisplayName(), CountIn(Column.Review), "#2563EB"),
        new(Column.Done.ToDisplayName(), CountIn(Column.Done), "#10B981")
    ];

    private IReadOnlyList<BarItem> ByType => IssueOptions.Types
        .Select(type => new BarItem(IssueOptions.TypeDisplayName(type), OpenIssues.Count(issue => issue.Type == type),
            type == "Bug" ? "#EF4444" : "#0f766e"))
        .ToList();

    private IReadOnlyList<BarItem> ByPriority => IssueOptions.Priorities
        .Reverse()
        .Select(priority => new BarItem(IssueOptions.PriorityDisplayName(priority), OpenIssues.Count(issue => issue.Priority == priority),
            priority switch { "Critical" => "#DC2626", "High" => "#F59E0B", "Medium" => "#0284C7", _ => "#16A34A" }))
        .ToList();

    private IReadOnlyList<WeekBar> CreatedPerWeek
    {
        get
        {
            var thisMonday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
            return Enumerable.Range(0, 8)
                .Select(offset => thisMonday.AddDays(-7 * (7 - offset)))
                .Select(monday => new WeekBar(
                    $"KW {ISOWeek.GetWeekOfYear(monday)}",
                    _issues.Count(issue =>
                    {
                        var created = issue.CreatedAt.ToLocalTime().Date;
                        return created >= monday && created < monday.AddDays(7);
                    })))
                .ToList();
        }
    }

    private IEnumerable<PersonRow> PerPerson => _issues
        .GroupBy(issue => issue.AssigneeId)
        .Select(group => new PersonRow(
            AssigneeName(group.Key) ?? "Nicht zugewiesen",
            group.Count(issue => issue.Status.ToColumn() != Column.Done),
            group.Count(issue => issue.Status.ToColumn() == Column.Done),
            group.Count(issue => issue.Status.ToColumn() != Column.Done && issue.DueDate is { } due && due.Date < DateTime.Today)))
        .OrderByDescending(row => row.Open);

    private int CountIn(Column column) => _issues.Count(issue => issue.Status.ToColumn() == column);

    protected override async Task OnInitializedAsync()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        await LoadReport();
    }

    public void Dispose() => AuthService.OnAuthStateChanged -= HandleAuthStateChanged;

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await LoadReport();
        StateHasChanged();
    });

    private async Task SelectProject(Guid projectId)
    {
        _selectedProjectId = projectId;
        await LoadReport();
    }

    private async Task LoadReport()
    {
        if (!AuthService.IsAuthenticated)
        {
            _isLoading = false;
            return;
        }

        _isLoading = true;
        var projectsResult = await ProjectService.GetProjects();
        _projects = projectsResult.Data ?? [];

        var projectIds = _selectedProjectId == Guid.Empty
            ? _projects.Where(project => project.Status != "Archiviert").Select(project => project.Id).ToList()
            : [_selectedProjectId];

        var issuesTask = IssueService.GetIssues(projectIds);
        var membersTask = Task.WhenAll(projectIds.Select(MemberService.GetMembers));
        await Task.WhenAll(issuesTask, membersTask);

        _issues = issuesTask.Result.Data ?? [];
        _profiles = membersTask.Result
            .SelectMany(result => result.Data ?? [])
            .Where(member => member.Profile is not null)
            .DistinctBy(member => member.UserId)
            .ToDictionary(member => member.UserId, member => member.Profile!);

        _isLoading = false;
    }

    private ProjectDto? ProjectFor(Guid projectId) => _projects.FirstOrDefault(project => project.Id == projectId);

    private string? AssigneeName(Guid? userId) =>
        userId is { } id ? _profiles.GetValueOrDefault(id)?.DisplayName ?? "Ehemaliges Mitglied" : null;

    private async Task ExportCsv()
    {
        var csv = CsvExporter.ExportIssues(_issues, _projects, _profiles);
        var suffix = _selectedProjectId == Guid.Empty ? "alle" : ProjectFor(_selectedProjectId)?.Key?.ToLowerInvariant();
        await JS.InvokeVoidAsync("syncroDownloadFile", $"syncro-report-{suffix}-{DateTime.Today:yyyy-MM-dd}.csv", csv, "text/csv;charset=utf-8");
    }
}
