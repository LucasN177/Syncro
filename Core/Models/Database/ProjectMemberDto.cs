using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("project_members")]
public class ProjectMemberDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("project_id", ignoreOnUpdate: true)]
    public Guid ProjectId { get; set; }

    [Column("user_id", ignoreOnUpdate: true)]
    public Guid UserId { get; set; }

    /// <summary>admin, member oder viewer (siehe <see cref="Constants.ProjectRoles"/>).</summary>
    [Column("role")]
    public string Role { get; set; } = Constants.ProjectRoles.Member;

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }
}
