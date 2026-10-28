using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class EventRepository(Supabase.Client client) : IEventRepository
{
    public async Task<IResponse<IReadOnlyList<EventDto>>> GetEvents(DateTime fromUtc, DateTime toUtc)
    {
        try
        {
            // RLS liefert eigene, Projekt- und eingeladene Termine
            var result = await client.From<EventDto>()
                .Filter("start_at", Operator.LessThan, toUtc.ToUniversalTime().ToString("o"))
                .Filter("end_at", Operator.GreaterThanOrEqual, fromUtc.ToUniversalTime().ToString("o"))
                .Order(calendarEvent => calendarEvent.StartAt, Ordering.Ascending)
                .Get();

            return Response<IReadOnlyList<EventDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<EventDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IReadOnlyList<EventAttendeeDto>>> GetAttendees(IEnumerable<Guid> eventIds)
    {
        try
        {
            var ids = eventIds.Distinct().Select(id => id.ToString()).ToList();
            if (ids.Count == 0)
                return Response<IReadOnlyList<EventAttendeeDto>>.Success([]);

            var result = await client.From<EventAttendeeDto>()
                .Filter(attendee => attendee.EventId, Operator.In, ids)
                .Get();

            return Response<IReadOnlyList<EventAttendeeDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<EventAttendeeDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<EventDto>> CreateEvent(EventDto calendarEvent)
    {
        try
        {
            var result = await client.From<EventDto>().Insert(calendarEvent);
            var savedEvent = result.Models.FirstOrDefault();
            return savedEvent is not null
                ? Response<EventDto>.Success(savedEvent)
                : Response<EventDto>.Failure("Termin konnte nicht angelegt werden.");
        }
        catch (Exception exception)
        {
            return Response<EventDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<EventDto>> UpdateEvent(EventDto calendarEvent)
    {
        try
        {
            var result = await client.From<EventDto>()
                .Where(existingEvent => existingEvent.Id == calendarEvent.Id)
                .Update(calendarEvent);

            var savedEvent = result.Models.FirstOrDefault();
            return savedEvent is not null
                ? Response<EventDto>.Success(savedEvent)
                : Response<EventDto>.Failure("Termin konnte nicht gespeichert werden.");
        }
        catch (Exception exception)
        {
            return Response<EventDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> DeleteEvent(Guid id)
    {
        try
        {
            await client.From<EventDto>()
                .Where(calendarEvent => calendarEvent.Id == id)
                .Delete();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }

    public async Task<IResponse> SetAttendees(Guid eventId, IEnumerable<Guid> userIds)
    {
        try
        {
            var wanted = userIds.ToHashSet();
            var existing = (await client.From<EventAttendeeDto>()
                .Where(attendee => attendee.EventId == eventId)
                .Get()).Models;

            foreach (var attendee in existing.Where(attendee => !wanted.Contains(attendee.UserId)))
            {
                await client.From<EventAttendeeDto>()
                    .Where(existingAttendee => existingAttendee.Id == attendee.Id)
                    .Delete();
            }

            var missing = wanted
                .Except(existing.Select(attendee => attendee.UserId))
                .Select(userId => new EventAttendeeDto { EventId = eventId, UserId = userId })
                .ToList();

            if (missing.Count > 0)
                await client.From<EventAttendeeDto>().Insert(missing);

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }
}
