using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("profiles")]
public class ProfileDto : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("vorname")]
    public string Vorname { get; set; } = string.Empty;

    [Column("nachname")]
    public string Nachname { get; set; } = string.Empty;

    [Column("avatar_color")]
    public string AvatarColor { get; set; } = "#0f766e";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>Vor- und Nachname, sonst Username, sonst E-Mail.</summary>
    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var fullName = $"{Vorname} {Nachname}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName;
            return !string.IsNullOrWhiteSpace(Username) ? Username : Email ?? "Unbekannt";
        }
    }
}
