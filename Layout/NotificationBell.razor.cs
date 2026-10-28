using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models.Database;
using Syncro.Core.Models.Enums;
using Syncro.Services;

namespace Syncro.Layout;

public partial class NotificationBell : ComponentBase, IAsyncDisposable
{
    /// <summary>Issues gelten bis zu so vielen Tagen vor der Fälligkeit als "bald fällig".</summary>
    private const int DueSoonDays = 2;

    [Inject] private INotificationService NotificationService { get; set; } = default!;
    [Inject] private IRealtimeService RealtimeService { get; set; } = default!;
    [Inject] private IProjectService ProjectService { get; set; } = default!;
    [Inject] private IIssueService IssueService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IReadOnlyList<NotificationDto> _notifications = [];
    private IReadOnlyList<IssueDto> _dueSoon = [];
    private Dictionary<Guid, string?> _projectKeys = [];
    private readonly HashSet<Guid> _knownIds = [];
    private IAsyncDisposable? _subscription;
    private Guid? _subscribedUserId;
    private bool _initialLoadDone;

    private int UnreadCount => _notifications.Count(notification => !notification.IsRead);
    private int BadgeCount => UnreadCount + _dueSoon.Count;

    protected override async Task OnInitializedAsync()
    {
        AuthService.OnAuthStateChanged += HandleAuthStateChanged;
        await Reload();
    }

    public async ValueTask DisposeAsync()
    {
        AuthService.OnAuthStateChanged -= HandleAuthStateChanged;
        if (_subscription is not null)
            await _subscription.DisposeAsync();
    }

    private void HandleAuthStateChanged() => InvokeAsync(async () =>
    {
        await Reload();
        StateHasChanged();
    });

    private async Task Reload()
    {
        if (!AuthService.IsAuthenticated || AuthService.CurrentUserId is not { } userId)
        {
            _notifications = [];
            _dueSoon = [];
            _knownIds.Clear();
            _initialLoadDone = false;
            await Unsubscribe();
            return;
        }

        await LoadNotifications();
        await LoadDueSoon(userId);

        if (_subscribedUserId != userId)
        {
            await Unsubscribe();
            _subscription = await RealtimeService.SubscribeToNotifications(userId, OnNotificationsChanged);
            _subscribedUserId = _subscription is null ? null : userId;
        }
    }

    private async Task Unsubscribe()
    {
        if (_subscription is not null)
            await _subscription.DisposeAsync();
        _subscription = null;
        _subscribedUserId = null;
    }

    private async Task LoadNotifications()
    {
        var result = await NotificationService.GetNotifications();
        if (result is not { IsSuccess: true, Data: not null })
            return;

        _notifications = result.Data;

        // Browser-Hinweis nur für Benachrichtigungen, die seit dem ersten Laden neu sind
        var fresh = _notifications.Where(notification => !notification.IsRead && !_knownIds.Contains(notification.Id)).ToList();
        foreach (var notification in _notifications)
            _knownIds.Add(notification.Id);

        if (_initialLoadDone && AppState.BrowserNotificationsEnabled)
        {
            foreach (var notification in fresh)
                await ShowBrowserNotification(notification.Message);
        }

        _initialLoadDone = true;
    }

    private async Task LoadDueSoon(Guid userId)
    {
        var projects = await ProjectService.GetProjects();
        var activeProjects = (projects.Data ?? []).Where(project => project.Status != "Archiviert").ToList();
        _projectKeys = activeProjects.ToDictionary(project => project.Id, project => project.Key);

        var issues = await IssueService.GetIssues(activeProjects.Select(project => project.Id));
        var limit = DateTime.Today.AddDays(DueSoonDays);
        _dueSoon = (issues.Data ?? [])
            .Where(issue => issue.AssigneeId == userId
                            && issue.Status.ToColumn() != Column.Done
                            && issue.DueDate is { } due && due.Date <= limit)
            .OrderBy(issue => issue.DueDate)
            .Take(5)
            .ToList();
    }

    private void OnNotificationsChanged() => InvokeAsync(async () =>
    {
        await LoadNotifications();
        StateHasChanged();
    });

    private async Task ShowBrowserNotification(string message)
    {
        try
        {
            await JS.InvokeVoidAsync("syncroNotifications.show", "Syncro", message);
        }
        catch (JSException)
        {
            // Browser unterstützt keine Benachrichtigungen
        }
    }

    private async Task OpenNotification(NotificationDto notification)
    {
        if (!notification.IsRead)
        {
            await NotificationService.MarkAsRead(notification.Id);
            notification.ReadAt = DateTime.UtcNow;
        }

        if (notification.ProjectId is { } projectId)
            OpenProject(projectId);
    }

    private void OpenProject(Guid projectId)
    {
        AppState.SetCurrentProject(projectId);
        Navigation.NavigateTo($"projects/{projectId}/board");
    }

    private async Task MarkAllAsRead()
    {
        var result = await NotificationService.MarkAllAsRead();
        if (result.IsSuccess)
            await LoadNotifications();
    }

    private static string DueText(DateTime dueDate)
    {
        var days = (dueDate.Date - DateTime.Today).Days;
        return days switch
        {
            < 0 => $"seit {-days} Tag{(days == -1 ? string.Empty : "en")} überfällig",
            0 => "heute fällig",
            1 => "morgen fällig",
            _ => $"fällig am {dueDate:dd.MM.}"
        };
    }

    private static string NotificationIcon(string type) => type switch
    {
        "assigned" => Icons.Material.Outlined.AssignmentInd,
        "mentioned" => Icons.Material.Outlined.AlternateEmail,
        "commented" => Icons.Material.Outlined.ChatBubbleOutline,
        "member_added" => Icons.Material.Outlined.GroupAdd,
        _ => Icons.Material.Outlined.Notifications
    };
}
