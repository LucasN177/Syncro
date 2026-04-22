using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Models;
using Syncro.Layout.UI_Models;
using Syncro.Pages.Dialogs;

namespace Syncro.Pages;

public partial class Calendar : ComponentBase
{
    private DateTime _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _selectedDay;
    private string _activeView = "Month";
    private bool _detailOpen;
    private bool _addOpen;
    private CalendarEvent? _selectedEvent;
    private CalendarEvent _newEvent = new();
    private DateTime? _newStartDate = DateTime.Today;
    private DateTime? _newEndDate = DateTime.Today;
    private TimeSpan? _newStartTime = new TimeSpan(9, 0, 0);
    private TimeSpan? _newEndTime = new TimeSpan(10, 0, 0);

    private List<CalendarCategoryFilter> _categories = new()
    {
        new() { Name = "Meeting",  Color = Color.Primary,   Hex = "#6366F1" },
        new() { Name = "Review",   Color = Color.Warning,   Hex = "#F59E0B" },
        new() { Name = "Release",  Color = Color.Success,   Hex = "#10B981" },
        new() { Name = "Personal", Color = Color.Secondary, Hex = "#8B5CF6" },
        new() { Name = "Deadline", Color = Color.Error,     Hex = "#EF4444" },
    };

    private List<CalendarEvent> _events = new()
    {
        new() { Title = "Sprint Planning",       Category = "Meeting",  Start = DateTime.Today.AddDays(0).Date.AddHours(9),  End = DateTime.Today.AddDays(0).Date.AddHours(10),  Location = "Room A", Attendees = new() { "Lucas M.", "Anna K.", "Tim R." }, Description = "Sprint 5 kick-off planning session." },
        new() { Title = "Design Review",         Category = "Review",   Start = DateTime.Today.AddDays(1).Date.AddHours(14), End = DateTime.Today.AddDays(1).Date.AddHours(15),  Location = "Zoom",   Attendees = new() { "Sara B.", "Anna K." },             Description = "Review new onboarding screens." },
        new() { Title = "v2.0 Release",          Category = "Release",  Start = DateTime.Today.AddDays(3).Date.AddHours(10), End = DateTime.Today.AddDays(3).Date.AddHours(11),  Location = "",       Attendees = new() { "Lucas M.", "Tim R." },              Description = "Production release of v2.0." },
        new() { Title = "1:1 with Anna",         Category = "Meeting",  Start = DateTime.Today.AddDays(2).Date.AddHours(11), End = DateTime.Today.AddDays(2).Date.AddHours(12),  Location = "Office", Attendees = new() { "Anna K." },                        Description = "" },
        new() { Title = "API Deadline",          Category = "Deadline", Start = DateTime.Today.AddDays(5).Date.AddHours(18), End = DateTime.Today.AddDays(5).Date.AddHours(18),  Location = "",       Attendees = new(),                                      Description = "Public API documentation must be finalized." },
        new() { Title = "Team Retrospective",    Category = "Meeting",  Start = DateTime.Today.AddDays(7).Date.AddHours(15), End = DateTime.Today.AddDays(7).Date.AddHours(16),  Location = "Room B", Attendees = new() { "Lucas M.", "Anna K.", "Tim R.", "Sara B." }, Description = "" },
        new() { Title = "Doctor Appointment",    Category = "Personal", Start = DateTime.Today.AddDays(4).Date.AddHours(8),  End = DateTime.Today.AddDays(4).Date.AddHours(9),   Location = "City Center", Attendees = new(),                               Description = "" },
        new() { Title = "Code Review Session",   Category = "Review",   Start = DateTime.Today.AddDays(6).Date.AddHours(10), End = DateTime.Today.AddDays(6).Date.AddHours(11),  Location = "Zoom",   Attendees = new() { "Tim R.", "Lucas M." },              Description = "Review EF Core migration PR." },
        new() { Title = "Stakeholder Demo",      Category = "Meeting",  Start = DateTime.Today.AddDays(9).Date.AddHours(13), End = DateTime.Today.AddDays(9).Date.AddHours(14),  Location = "Boardroom", Attendees = new() { "Lucas M.", "Anna K." },          Description = "Demo of sprint results to stakeholders." },
        new() { Title = "Hotfix Deadline",       Category = "Deadline", Start = DateTime.Today.AddDays(2).Date.AddHours(17), End = DateTime.Today.AddDays(2).Date.AddHours(17),  Location = "",       Attendees = new(),                                      Description = "Auth hotfix must be deployed." },
        new() { Title = "Gym",                   Category = "Personal", Start = DateTime.Today.AddDays(1).Date.AddHours(7),  End = DateTime.Today.AddDays(1).Date.AddHours(8),   Location = "FitX",   Attendees = new(),                                      Description = "" },
        new() { Title = "Architecture Review",   Category = "Review",   Start = DateTime.Today.AddDays(10).Date.AddHours(9),End = DateTime.Today.AddDays(10).Date.AddHours(11),  Location = "Room A", Attendees = new() { "Lucas M.", "Tim R.", "Sara B." },   Description = "Review new microservice boundaries." },
    };
    
    private void PrevMonth() => _currentMonth = _currentMonth.AddMonths(-1);
    private void NextMonth() => _currentMonth = _currentMonth.AddMonths(1);
    private void GoToToday()  { _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); _selectedDay = DateTime.Today; }
    private void SelectDay(DateTime day) => _selectedDay = day;
    private void ToggleCategory(CalendarCategoryFilter cat) => cat.Active = !cat.Active;

    // ── Dialogs ──────────────────────────────────────────────────────────────

    private async Task OpenEventDetail(CalendarEvent ev)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        var parameters = new DialogParameters<CalendarDetailsDialog>()
        {
            {x => x.Categories, _categories},
            {x => x.SelectedEvent, ev},
        };
        
        var dialog = await DialogService.ShowAsync<CalendarDetailsDialog>("Details",parameters, options);
        var result =  await dialog.Result;
        if (result is { Canceled: false })
        {
            var delete = (CalendarEvent)result.Data!;
            _events.Remove(delete);
        }
    }

    private async Task OpenAddDialog()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        var parameters = new DialogParameters<CalendarAddDialog>()
        {
            {x => x.Categories, _categories},
            {x => x.SelectedDay, _selectedDay},
        };
        
        var dialog = await DialogService.ShowAsync<CalendarAddDialog>("Add event",parameters, options);
        var result =  await dialog.Result;
        if (result is { Canceled: false })
        {
            var newEvent = (CalendarEvent)result.Data!;
            _events.Add(newEvent);
            await InvokeAsync(StateHasChanged);
        }
        
    }

    private void SaveNewEvent()
    {
        if (string.IsNullOrWhiteSpace(_newEvent.Title)) return;
        _newEvent.Start = (_newStartDate ?? DateTime.Today).Date + (_newStartTime ?? TimeSpan.Zero);
        _newEvent.End   = (_newEndDate   ?? DateTime.Today).Date + (_newEndTime   ?? TimeSpan.Zero);
        _events.Add(_newEvent);
        _addOpen = false;
    }
}