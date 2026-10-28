namespace Syncro.Core.Models;

public class CalendarEvent
{
    /// <summary>Guid.Empty für einen noch nicht gespeicherten Termin.</summary>
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public string Category { get; set; } = "Meeting";

    /// <summary>Lokale Zeit.</summary>
    public DateTime Start { get; set; }

    /// <summary>Lokale Zeit.</summary>
    public DateTime End { get; set; }

    /// <summary>Gesetzt, wenn der Eintrag die Fälligkeit eines Issues ist (nicht bearbeitbar).</summary>
    public Guid? IssueId { get; set; }

    public bool IsAllDay { get; set; }

    public List<Guid> AttendeeIds { get; set; } = new();

    /// <summary>Anzeigenamen der Teilnehmer (soweit das Profil sichtbar ist).</summary>
    public List<string> Attendees { get; set; } = new();
}
