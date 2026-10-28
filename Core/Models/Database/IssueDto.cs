using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("issues")]
public class IssueDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("project_id", ignoreOnUpdate: true)]
    public Guid ProjectId { get; set; }

    /// <summary>Fortlaufend pro Projekt, wird von der Datenbank vergeben.</summary>
    [Column("number", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public int Number { get; set; }

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>Feature, Bug, Task, Improvement oder Documentation.</summary>
    [Column("type")]
    public string Type { get; set; } = "Task";

    /// <summary>Low, Medium, High oder Critical.</summary>
    [Column("priority")]
    public string Priority { get; set; } = "Medium";

    /// <summary>Name des Enums <see cref="Enums.Column"/>.</summary>
    [Column("status")]
    public string Status { get; set; } = nameof(Enums.Column.Backlog);

    [Column("position")]
    public double Position { get; set; }

    [Column("assignee_id")]
    public Guid? AssigneeId { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("progress")]
    public int? Progress { get; set; }

    [Column("tags")]
    public List<string> Tags { get; set; } = [];

    [Column("comment_count", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public int CommentCount { get; set; }

    [Column("created_by", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public Guid? CreatedBy { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}
