using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("projects")]
public class ProjectDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Eigentümer des Projekts. Wird bei Updates von der Datenbank geschützt.</summary>
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    public string Status { get; set; } = "Aktiv";

    [Column("color")]
    public string Color { get; set; } = "#0f766e";

    /// <summary>Kürzel für Issue-Nummern, z. B. SYN. Leer lassen, um es aus dem Namen abzuleiten.</summary>
    [Column("key", NullValueHandling.Ignore)]
    public string? Key { get; set; }

    [Column("issue_counter", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public int IssueCounter { get; set; }

    [Column("created_at", ignoreOnUpdate: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}
