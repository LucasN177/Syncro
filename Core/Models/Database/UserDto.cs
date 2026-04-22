using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Syncro.Core.Models.Database;

[Table("users")]
public class UserDto : BaseModel
{
    [Column("id")]
    [PrimaryKey]
    public string UserId { get; set; } = string.Empty;
    
    [Column("username")] 
    public string Username { get; set; } = string.Empty;
    
    [Column("vorname")] 
    public string Vorname { get; set; } = string.Empty;
    
    [Column("nachname")] 
    public string Nachname { get; set; } = string.Empty;
}