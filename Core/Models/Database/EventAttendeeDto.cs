using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("event_attendees")]
public class EventAttendeeDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("event_id")]
    public Guid EventId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }
}
