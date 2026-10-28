using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("events")]
public class EventDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Ersteller, wird von der Datenbank gesetzt.</summary>
    [Column("owner_id", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public Guid? OwnerId { get; set; }

    /// <summary>Ohne Projekt ist der Termin privat (Ersteller und Teilnehmer).</summary>
    [Column("project_id")]
    public Guid? ProjectId { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    /// <summary>Siehe <see cref="Constants.CalendarCategories"/>.</summary>
    [Column("category")]
    public string Category { get; set; } = Constants.CalendarCategories.Meeting;

    /// <summary>In UTC speichern, für die Anzeige mit ToLocalTime() umrechnen.</summary>
    [Column("start_at")]
    public DateTime StartAt { get; set; }

    [Column("end_at")]
    public DateTime EndAt { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}
