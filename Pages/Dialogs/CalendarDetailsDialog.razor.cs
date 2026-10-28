using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models;
using Syncro.Layout.UI_Models;

namespace Syncro.Pages.Dialogs;

public enum CalendarDetailsAction
{
    Edit,
    Deleted
}

public partial class CalendarDetailsDialog : ComponentBase
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject] private IEventService EventService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter]
    public List<CalendarCategoryFilter>? Categories { get; set; }

    [Parameter]
    public CalendarEvent? SelectedEvent { get; set; }

    [Parameter]
    public string? ProjectName { get; set; }

    /// <summary>Ersteller oder bearbeitende Projektmitglieder.</summary>
    [Parameter]
    public bool CanEdit { get; set; }

    private string? _errorMessage;
    private bool _isBusy;

    private string TimeText
    {
        get
        {
            if (SelectedEvent is null)
                return string.Empty;
            if (SelectedEvent.IsAllDay)
                return $"{SelectedEvent.Start:dddd, d. MMMM} · fällig";
            if (SelectedEvent.Start.Date == SelectedEvent.End.Date)
                return $"{SelectedEvent.Start:dddd, d. MMMM · HH:mm} – {SelectedEvent.End:HH:mm}";
            return $"{SelectedEvent.Start:dd.MM.yyyy HH:mm} – {SelectedEvent.End:dd.MM.yyyy HH:mm}";
        }
    }

    private void Edit() => MudDialog.Close(DialogResult.Ok(CalendarDetailsAction.Edit));

    private async Task DeleteAsync()
    {
        if (SelectedEvent is null)
            return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "Termin löschen?", $"„{SelectedEvent.Title}“ wird für alle Teilnehmer gelöscht.",
            yesText: "Löschen", cancelText: "Abbrechen");
        if (confirmed != true)
            return;

        _isBusy = true;
        var result = await EventService.DeleteEvent(SelectedEvent.Id);
        _isBusy = false;

        if (!result.IsSuccess)
        {
            _errorMessage = result.ErrorMessage ?? "Termin konnte nicht gelöscht werden.";
            return;
        }

        MudDialog.Close(DialogResult.Ok(CalendarDetailsAction.Deleted));
    }

    private void OpenBoard()
    {
        MudDialog.Cancel();
        Navigation.NavigateTo($"projects/{SelectedEvent?.ProjectId}/board");
    }

    private void Cancel() => MudDialog.Cancel();
}
