using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

/// <summary>Wird per Trigger geschrieben; Clients ändern nur read_at.</summary>
[Table("notifications")]
public class NotificationDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("project_id")]
    public Guid? ProjectId { get; set; }

    [Column("issue_id")]
    public Guid? IssueId { get; set; }

    [Column("actor_id")]
    public Guid? ActorId { get; set; }

    /// <summary>assigned, commented, mentioned oder member_added.</summary>
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonIgnore]
    public bool IsRead => ReadAt is not null;
}
