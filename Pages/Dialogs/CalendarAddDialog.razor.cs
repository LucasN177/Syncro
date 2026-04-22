using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Models;
using Syncro.Layout.UI_Models;

namespace Syncro.Pages.Dialogs;

public partial class CalendarAddDialog : ComponentBase
{
    public CalendarEvent NewEvent = new CalendarEvent();
    
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public List<CalendarCategoryFilter>? Categories { get; set; }
    
    [Parameter]
    public DateTime? SelectedDay { get; set; }

    private DateTime? _newStartDate = DateTime.Today;
    private DateTime? _newEndDate   = DateTime.Today;
    private TimeSpan? _newStartTime = new TimeSpan(9, 0, 0);
    private TimeSpan? _newEndTime   = new TimeSpan(10, 0, 0);
    
    private void Submit()
    {
        NewEvent.Start = Combine(_newStartDate!.Value, _newStartTime!.Value);
        NewEvent.End   = Combine(_newEndDate!.Value, _newEndTime!.Value);
        MudDialog.Close(DialogResult.Ok(NewEvent));
    }

    private void Cancel() => MudDialog.Cancel();
    
    private static DateTime Combine(DateTime date, TimeSpan time)
        => date.Date.Add(time);
}