namespace Syncro.Core.Models;

public class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public string Category { get; set; } = "Meeting";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public List<string> Attendees { get; set; } = new();
}