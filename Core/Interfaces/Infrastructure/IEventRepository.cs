using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IEventRepository
{
    /// <summary>Alle sichtbaren Termine, die den Zeitraum (UTC) überschneiden.</summary>
    Task<IResponse<IReadOnlyList<EventDto>>> GetEvents(DateTime fromUtc, DateTime toUtc);

    Task<IResponse<IReadOnlyList<EventAttendeeDto>>> GetAttendees(IEnumerable<Guid> eventIds);
    Task<IResponse<EventDto>> CreateEvent(EventDto calendarEvent);
    Task<IResponse<EventDto>> UpdateEvent(EventDto calendarEvent);
    Task<IResponse> DeleteEvent(Guid id);

    /// <summary>Gleicht die Teilnehmer ab: fehlende werden hinzugefügt, entfernte gelöscht.</summary>
    Task<IResponse> SetAttendees(Guid eventId, IEnumerable<Guid> userIds);
}
