using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Models;
using Syncro.FakeData;
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

    private List<CalendarCategoryFilter> _categories = SampleData.Categories;

    private List<CalendarEvent> _events = SampleData.Events;
    
    private void PrevMonth() => _currentMonth = _currentMonth.AddMonths(-1);
    private void NextMonth() => _currentMonth = _currentMonth.AddMonths(1);
    private void GoToToday()  { _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); _selectedDay = DateTime.Today; }
    private void SelectDay(DateTime day) => _selectedDay = day;
    private void ToggleCategory(CalendarCategoryFilter cat) => cat.Active = !cat.Active;

    // ── Dialogs ──────────────────────────────────────────────────────────────

    private async Task OpenEventDetail(CalendarEvent ev)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true};
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
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true};
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