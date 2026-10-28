using Microsoft.AspNetCore.Components;
using MudBlazor;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Pages.Dialogs;

public partial class CalendarEventDialog : ComponentBase
{
    [CascadingParameter] public required IMudDialogInstance MudDialog { get; set; }
    [Inject] private IEventService EventService { get; set; } = default!;
    [Inject] private IAuthService AuthService { get; set; } = default!;

    /// <summary>Bearbeitungskopie; Id leer für einen neuen Termin.</summary>
    [Parameter] public CalendarEvent Event { get; set; } = new();

    /// <summary>Projekte, in denen der Nutzer Termine anlegen darf.</summary>
    [Parameter] public IReadOnlyList<ProjectDto> Projects { get; set; } = [];

    [Parameter] public IReadOnlyDictionary<Guid, IReadOnlyList<ProjectMember>> MembersByProject { get; set; } =
        new Dictionary<Guid, IReadOnlyList<ProjectMember>>();

    private bool IsNew => Event.Id == Guid.Empty;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private TimeSpan? _startTime;
    private TimeSpan? _endTime;
    private IReadOnlyCollection<Guid> _attendees = [];
    private string? _errorMessage;
    private bool _isBusy;

    /// <summary>Mitglieder des gewählten Projekts, bei privaten Terminen alle Personen aus eigenen Projekten.</summary>
    private IEnumerable<ProjectMember> AttendeeOptions =>
        (Event.ProjectId is { } projectId
            ? MembersByProject.GetValueOrDefault(projectId) ?? []
            : MembersByProject.Values.SelectMany(members => members))
        .Where(member => member.UserId != AuthService.CurrentUserId)
        .DistinctBy(member => member.UserId)
        .OrderBy(member => member.DisplayName);

    protected override void OnInitialized()
    {
        _startDate = Event.Start.Date;
        _startTime = Event.Start.TimeOfDay;
        _endDate = Event.End.Date;
        _endTime = Event.End.TimeOfDay;
        _attendees = Event.AttendeeIds.ToList();
    }

    private string ProjectName(Guid? projectId) =>
        projectId is null ? "Nur mich und Teilnehmer" : Projects.FirstOrDefault(p => p.Id == projectId)?.Name ?? "Projekt";

    private string PersonName(Guid userId) =>
        MembersByProject.Values.SelectMany(members => members).FirstOrDefault(member => member.UserId == userId)?.DisplayName
        ?? "Unbekannt";

    private void OnProjectChanged(Guid? projectId)
    {
        Event.ProjectId = projectId;
        // Teilnehmer, die nicht zum neuen Projekt gehören, entfernen
        var allowed = AttendeeOptions.Select(member => member.UserId).ToHashSet();
        _attendees = _attendees.Where(allowed.Contains).ToList();
    }

    private async Task SaveAsync()
    {
        _errorMessage = null;
        if (_startDate is null || _endDate is null || _startTime is null || _endTime is null)
        {
            _errorMessage = "Bitte gib Beginn und Ende an.";
            return;
        }

        Event.Start = _startDate.Value.Date + _startTime.Value;
        Event.End = _endDate.Value.Date + _endTime.Value;
        Event.AttendeeIds = _attendees.ToList();
        Event.Attendees = Event.AttendeeIds.Select(PersonName).ToList();

        _isBusy = true;
        var result = await EventService.SaveEvent(Event);
        _isBusy = false;

        if (!result.IsSuccess || result.Data is null)
        {
            _errorMessage = result.ErrorMessage ?? "Termin konnte nicht gespeichert werden.";
            return;
        }

        MudDialog.Close(DialogResult.Ok(result.Data));
    }

    private void Cancel() => MudDialog.Cancel();
}
