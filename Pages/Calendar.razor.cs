using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;
using Syncro.Layout.Helper;
using Syncro.Layout.UI_Models;
using Syncro.Pages.Dialogs;

namespace Syncro.Pages;

public partial class Calendar : ComponentBase, IDisposable
{
    [Inject] private IEventService EventService { get; set; } = default!;
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IProjectMemberService MemberService { get; set; } = default!;
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private DateTime _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _selectedDay;
    private string _activeView = "Month";
    private bool _isLoading;
    private bool _showIssueDeadlines = true;

    private readonly List<CalendarCategoryFilter> _categories = CalendarCategories.Create();
    private readonly List<CalendarEvent> _events = [];
    private readonly List<CalendarEvent> _issueDeadlines = [];

    private IReadOnlyList<ProjectDto> _projects = [];
    private IReadOnlyDictionary<Guid, string> _roles = new Dictionary<Guid, string>();
    private Dictionary<Guid, IReadOnlyList<ProjectMember>> _membersByProject = [];

    private IEnumerable<CalendarEvent> VisibleEvents =>
        _events.Concat(_showIssueDeadlines ? _issueDeadlines : [])
            .Where(e => _categories.FirstOrDefault(c => c.Name == e.Category)?.Active ?? true);

    private string HeaderText
    {
        get
        {
            if (_activeView != "Week")
                return _currentMonth.ToString("MMMM yyyy");

            var days = CalendarHelper.GetCurrentWeekDays(_selectedDay);
            var week = ISOWeek.GetWeekOfYear(days[0]);
            return $"KW {week} · {days[0]:dd.MM.} – {days[6]:dd.MM.yyyy}";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        await LoadAll();
    }

    public void Dispose() => AuthService.OnAuthStateChanged -= HandleAuthStateChanged;

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await LoadAll();
        StateHasChanged();
    });

    private async Task LoadAll()
    {
        if (!AuthService.IsAuthenticated)
        {
            _events.Clear();
            _issueDeadlines.Clear();
            return;
        }

        var projectsResult = await ProjectService.GetProjects();
        _projects = projectsResult.Data ?? [];

        var rolesResult = await MemberService.GetMyRoles();
        _roles = rolesResult.Data ?? new Dictionary<Guid, string>();

        var memberResults = await Task.WhenAll(_projects.Select(project => MemberService.GetMembers(project.Id)));
        _membersByProject = _projects
            .Zip(memberResults, (project, result) => (project.Id, Members: result.Data ?? []))
            .ToDictionary(entry => entry.Id, entry => entry.Members);

        await LoadEvents();
    }

    /// <summary>Lädt Termine und fällige Issues für den sichtbaren Zeitraum.</summary>
    private async Task LoadEvents()
    {
        var (from, to) = VisibleRange();
        _isLoading = true;

        var eventsResult = await EventService.GetEvents(from, to);
        _events.Clear();
        if (eventsResult is { IsSuccess: true, Data: not null })
            _events.AddRange(eventsResult.Data);
        else
            Snackbar.Add(eventsResult.ErrorMessage ?? "Termine konnten nicht geladen werden.", Severity.Error);

        var issuesResult = await IssueService.GetIssues(_projects.Select(project => project.Id));
        _issueDeadlines.Clear();
        _issueDeadlines.AddRange((issuesResult.Data ?? [])
            .Where(issue => issue.DueDate is { } due && due.Date >= from.Date && due.Date < to.Date)
            .Where(issue => issue.Status.ToColumn() != Column.Done)
            .Select(ToDeadline));

        _isLoading = false;
    }

    private (DateTime From, DateTime To) VisibleRange()
    {
        if (_activeView == "Week")
        {
            var days = CalendarHelper.GetCurrentWeekDays(_selectedDay);
            return (days[0], days[6].AddDays(1));
        }

        var weeks = CalendarHelper.GetCalendarWeeks(_currentMonth);
        return (weeks[0][0], weeks[^1][6].AddDays(1));
    }

    private CalendarEvent ToDeadline(IssueDto issue)
    {
        var project = _projects.FirstOrDefault(p => p.Id == issue.ProjectId);
        var due = issue.DueDate!.Value.Date;
        return new CalendarEvent
        {
            Id = issue.Id,
            IssueId = issue.Id,
            ProjectId = issue.ProjectId,
            Title = $"{issue.ToIssueKey(project?.Key)} {issue.Title}",
            Description = issue.Description ?? string.Empty,
            Category = CalendarCategories.Deadline,
            Start = due,
            End = due,
            IsAllDay = true
        };
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private async Task Previous() => await Move(-1);
    private async Task Next() => await Move(1);

    private async Task Move(int direction)
    {
        if (_activeView == "Week")
        {
            _selectedDay = (_selectedDay ?? DateTime.Today).AddDays(7 * direction);
            _currentMonth = new DateTime(_selectedDay.Value.Year, _selectedDay.Value.Month, 1);
        }
        else
        {
            _currentMonth = _currentMonth.AddMonths(direction);
        }

        await LoadEvents();
    }

    private async Task GoToToday()
    {
        _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _selectedDay = DateTime.Today;
        await LoadEvents();
    }

    private async Task SetView(string view)
    {
        _activeView = view;
        await LoadEvents();
    }

    private void SelectDay(DateTime day) => _selectedDay = day;
    private void ToggleCategory(CalendarCategoryFilter cat) => cat.Active = !cat.Active;
    private void ToggleIssueDeadlines(bool value) => _showIssueDeadlines = value;

    private static string ViewLabel(string view) => view switch
    {
        "Month" => "Monat",
        "Week" => "Woche",
        _ => "Liste"
    };

    // ── Darstellung ──────────────────────────────────────────────────────────

    private string HexFor(CalendarEvent ev) =>
        _categories.FirstOrDefault(c => c.Name == ev.Category)?.Hex ?? "#6366F1";

    private string? ProjectNameFor(CalendarEvent ev) =>
        ev.ProjectId is { } projectId ? _projects.FirstOrDefault(p => p.Id == projectId)?.Name : null;

    private bool CanEdit(CalendarEvent ev) =>
        ev.IssueId is null &&
        (ev.OwnerId == AuthService.CurrentUserId ||
         (ev.ProjectId is { } projectId && (IsOwner(projectId) || ProjectRoles.CanEdit(_roles.GetValueOrDefault(projectId)))));

    private bool IsOwner(Guid projectId) =>
        _projects.FirstOrDefault(p => p.Id == projectId)?.UserId == AuthService.CurrentUser?.Id;

    private IReadOnlyList<ProjectDto> EditableProjects =>
        _projects.Where(p => p.Status != "Archiviert" && (IsOwner(p.Id) || ProjectRoles.CanEdit(_roles.GetValueOrDefault(p.Id)))).ToList();

    // ── Dialoge ──────────────────────────────────────────────────────────────

    private async Task OpenEventDetail(CalendarEvent ev)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };
        var parameters = new DialogParameters<CalendarDetailsDialog>
        {
            { x => x.Categories, _categories },
            { x => x.SelectedEvent, ev },
            { x => x.ProjectName, ProjectNameFor(ev) },
            { x => x.CanEdit, CanEdit(ev) }
        };

        var dialog = await DialogService.ShowAsync<CalendarDetailsDialog>(string.Empty, parameters, options);
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: CalendarDetailsAction action })
            return;

        if (action == CalendarDetailsAction.Deleted)
        {
            _events.RemoveAll(e => e.Id == ev.Id);
            Snackbar.Add("Termin wurde gelöscht.", Severity.Success);
            StateHasChanged();
        }
        else
        {
            await OpenEventDialog(Copy(ev));
        }
    }

    private Task OpenAddDialog()
    {
        var day = (_selectedDay ?? DateTime.Today).Date;
        return OpenEventDialog(new CalendarEvent
        {
            Start = day.AddHours(9),
            End = day.AddHours(10),
            Category = CalendarCategories.Meeting
        });
    }

    private async Task OpenEventDialog(CalendarEvent calendarEvent)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };
        var parameters = new DialogParameters<CalendarEventDialog>
        {
            { x => x.Event, calendarEvent },
            { x => x.Projects, EditableProjects },
            { x => x.MembersByProject, _membersByProject }
        };

        var dialog = await DialogService.ShowAsync<CalendarEventDialog>(string.Empty, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await LoadEvents();
            Snackbar.Add("Termin wurde gespeichert.", Severity.Success);
            StateHasChanged();
        }
    }

    private static CalendarEvent Copy(CalendarEvent ev) => new()
    {
        Id = ev.Id,
        OwnerId = ev.OwnerId,
        ProjectId = ev.ProjectId,
        Title = ev.Title,
        Description = ev.Description,
        Location = ev.Location,
        Category = ev.Category,
        Start = ev.Start,
        End = ev.End,
        AttendeeIds = ev.AttendeeIds.ToList(),
        Attendees = ev.Attendees.ToList()
    };
}
