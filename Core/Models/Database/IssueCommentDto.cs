using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("issue_comments")]
public class IssueCommentDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("issue_id", ignoreOnUpdate: true)]
    public Guid IssueId { get; set; }

    /// <summary>Autor, wird von der Datenbank gesetzt.</summary>
    [Column("user_id", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public Guid? UserId { get; set; }

    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}
