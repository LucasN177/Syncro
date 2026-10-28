using Syncro.Core.Constants;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Models;

/// <summary>Projektmitgliedschaft zusammen mit dem Profil des Nutzers.</summary>
public class ProjectMember
{
    public Guid MemberId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = ProjectRoles.Member;
    public bool IsOwner { get; set; }
    public ProfileDto? Profile { get; set; }

    public string DisplayName => Profile?.DisplayName ?? "Unbekannt";
    public string? Email => Profile?.Email;
    public string AvatarColor => Profile?.AvatarColor ?? "#0f766e";
    public bool CanEdit => ProjectRoles.CanEdit(Role);
}
