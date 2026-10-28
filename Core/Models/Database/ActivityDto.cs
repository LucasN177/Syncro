using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

/// <summary>Nur lesbar, wird per Trigger in der Datenbank geschrieben.</summary>
[Table("activity_log")]
public class ActivityDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("project_id")]
    public Guid ProjectId { get; set; }

    [Column("actor_id")]
    public Guid? ActorId { get; set; }

    /// <summary>z. B. issue_created, issue_moved, comment_added.</summary>
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Column("entity_id")]
    public Guid? EntityId { get; set; }

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
