using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Mapping;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Services;

public class EventService(IEventRepository eventRepository, IProjectMemberRepository memberRepository) : IEventService
{
    private static readonly string[] Categories =
    [
        CalendarCategories.Meeting, CalendarCategories.Review, CalendarCategories.Release,
        CalendarCategories.Personal, CalendarCategories.Deadline
    ];

    public async Task<IResponse<IReadOnlyList<CalendarEvent>>> GetEvents(DateTime from, DateTime to)
    {
        var eventsResult = await eventRepository.GetEvents(from.ToUniversalTime(), to.ToUniversalTime());
        if (eventsResult is not { IsSuccess: true, Data: not null })
            return Response<IReadOnlyList<CalendarEvent>>.Failure(eventsResult.ErrorMessage ?? "Termine konnten nicht geladen werden.");

        var attendeesResult = await eventRepository.GetAttendees(eventsResult.Data.Select(e => e.Id));
        var attendees = attendeesResult.Data ?? [];

        var profilesResult = await memberRepository.GetProfiles(attendees.Select(attendee => attendee.UserId));
        var profiles = profilesResult.Data?.ToDictionary(profile => profile.Id) ?? new Dictionary<Guid, ProfileDto>();

        IReadOnlyList<CalendarEvent> events = eventsResult.Data
            .Select(dto => dto.ToCalendarEvent(
                attendees.Where(attendee => attendee.EventId == dto.Id).Select(attendee => attendee.UserId),
                profiles))
            .ToList();

        return Response<IReadOnlyList<CalendarEvent>>.Success(events);
    }

    public async Task<IResponse<CalendarEvent>> SaveEvent(CalendarEvent calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Title))
            return Response<CalendarEvent>.Failure("Bitte gib einen Titel ein.");

        if (calendarEvent.End < calendarEvent.Start)
            return Response<CalendarEvent>.Failure("Das Ende darf nicht vor dem Beginn liegen.");

        if (!Categories.Contains(calendarEvent.Category))
            return Response<CalendarEvent>.Failure("Unbekannte Kategorie.");

        var dto = calendarEvent.ToEventDto();
        var saveResult = calendarEvent.Id == Guid.Empty
            ? await eventRepository.CreateEvent(dto)
            : await eventRepository.UpdateEvent(dto);

        if (saveResult is not { IsSuccess: true, Data: not null })
            return Response<CalendarEvent>.Failure(saveResult.ErrorMessage ?? "Termin konnte nicht gespeichert werden.");

        var attendeesResult = await eventRepository.SetAttendees(saveResult.Data.Id, calendarEvent.AttendeeIds);
        if (!attendeesResult.IsSuccess)
            return Response<CalendarEvent>.Failure(attendeesResult.ErrorMessage ?? "Teilnehmer konnten nicht gespeichert werden.");

        var saved = saveResult.Data.ToCalendarEvent(calendarEvent.AttendeeIds, new Dictionary<Guid, ProfileDto>());
        saved.Attendees = calendarEvent.Attendees;
        return Response<CalendarEvent>.Success(saved);
    }

    public Task<IResponse> DeleteEvent(Guid id) => eventRepository.DeleteEvent(id);
}
