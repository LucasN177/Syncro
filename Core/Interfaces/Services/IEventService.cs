using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;

namespace Syncro.Core.Interfaces.Services;

public interface IEventService
{
    /// <summary>Sichtbare Termine im Zeitraum (lokale Zeit), inkl. Teilnehmernamen.</summary>
    Task<IResponse<IReadOnlyList<CalendarEvent>>> GetEvents(DateTime from, DateTime to);

    /// <summary>Legt den Termin an (Id leer) oder speichert ihn, inkl. Teilnehmer.</summary>
    Task<IResponse<CalendarEvent>> SaveEvent(CalendarEvent calendarEvent);

    Task<IResponse> DeleteEvent(Guid id);
}
