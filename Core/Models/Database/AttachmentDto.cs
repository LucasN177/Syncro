using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("issue_attachments")]
public class AttachmentDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("issue_id")]
    public Guid IssueId { get; set; }

    /// <summary>Wird von der Datenbank aus dem Issue übernommen.</summary>
    [Column("project_id", ignoreOnUpdate: true)]
    public Guid ProjectId { get; set; }

    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Pfad im Bucket "syncro-attachments": {project_id}/{issue_id}/{datei}.</summary>
    [Column("storage_path")]
    public string StoragePath { get; set; } = string.Empty;

    [Column("content_type")]
    public string? ContentType { get; set; }

    [Column("size_bytes")]
    public long SizeBytes { get; set; }

    [Column("uploaded_by", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public Guid? UploadedBy { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }
}
