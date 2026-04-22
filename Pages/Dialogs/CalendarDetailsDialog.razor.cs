using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Models;
using Syncro.Layout.UI_Models;

namespace Syncro.Pages.Dialogs;

public partial class CalendarDetailsDialog : ComponentBase
{
    
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public List<CalendarCategoryFilter>? Categories { get; set; }
    
    [Parameter]
    public CalendarEvent? SelectedEvent { get; set; }
    
    private void Submit()
    {
        MudDialog.Close(DialogResult.Ok(SelectedEvent));
    }

    private void Cancel() => MudDialog.Cancel();
}