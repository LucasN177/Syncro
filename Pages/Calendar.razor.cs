using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Constants;
using Syncro.Core.Models;
using Syncro.Layout.UI_Models;
using Syncro.Pages.Dialogs;

namespace Syncro.Pages;

public partial class Calendar : ComponentBase
{
    private DateTime _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _selectedDay;
    private string _activeView = "Month";

    private readonly List<CalendarCategoryFilter> _categories = CalendarCategories.Create();

    private readonly List<CalendarEvent> _events = [];

    private void PrevMonth() => _currentMonth = _currentMonth.AddMonths(-1);
    private void NextMonth() => _currentMonth = _currentMonth.AddMonths(1);
    private void GoToToday()  { _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); _selectedDay = DateTime.Today; }
    private void SelectDay(DateTime day) => _selectedDay = day;
    private void ToggleCategory(CalendarCategoryFilter cat) => cat.Active = !cat.Active;

    private static string ViewLabel(string view) => view switch
    {
        "Month" => "Monat",
        "Week" => "Woche",
        _ => "Liste"
    };

    // ── Dialogs ──────────────────────────────────────────────────────────────

    private async Task OpenEventDetail(CalendarEvent ev)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true};
        var parameters = new DialogParameters<CalendarDetailsDialog>()
        {
            {x => x.Categories, _categories},
            {x => x.SelectedEvent, ev},
        };
        
        var dialog = await DialogService.ShowAsync<CalendarDetailsDialog>("Termin",parameters, options);
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
        
        var dialog = await DialogService.ShowAsync<CalendarAddDialog>("Neuer Termin",parameters, options);
        var result =  await dialog.Result;
        if (result is { Canceled: false })
        {
            var newEvent = (CalendarEvent)result.Data!;
            _events.Add(newEvent);
            await InvokeAsync(StateHasChanged);
        }
    }
}