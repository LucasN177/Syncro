using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Mapping;

public static class EventMapper
{
    public static CalendarEvent ToCalendarEvent(this EventDto dto, IEnumerable<Guid> attendeeIds,
        IReadOnlyDictionary<Guid, ProfileDto> profiles)
    {
        var ids = attendeeIds.ToList();
        return new CalendarEvent
        {
            Id = dto.Id,
            OwnerId = dto.OwnerId,
            ProjectId = dto.ProjectId,
            Title = dto.Title,
            Description = dto.Description ?? string.Empty,
            Location = dto.Location ?? string.Empty,
            Category = dto.Category,
            Start = dto.StartAt.ToLocalTime(),
            End = dto.EndAt.ToLocalTime(),
            AttendeeIds = ids,
            Attendees = ids
                .Select(id => profiles.TryGetValue(id, out var profile) ? profile.DisplayName : null)
                .OfType<string>()
                .ToList()
        };
    }

    public static EventDto ToEventDto(this CalendarEvent calendarEvent) => new()
    {
        Id = calendarEvent.Id,
        ProjectId = calendarEvent.ProjectId,
        Title = calendarEvent.Title.Trim(),
        Description = string.IsNullOrWhiteSpace(calendarEvent.Description) ? null : calendarEvent.Description.Trim(),
        Location = string.IsNullOrWhiteSpace(calendarEvent.Location) ? null : calendarEvent.Location.Trim(),
        Category = calendarEvent.Category,
        StartAt = DateTime.SpecifyKind(calendarEvent.Start, DateTimeKind.Local).ToUniversalTime(),
        EndAt = DateTime.SpecifyKind(calendarEvent.End, DateTimeKind.Local).ToUniversalTime()
    };
}
