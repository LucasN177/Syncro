using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;

namespace Syncro.Core.Interfaces.Services;

public interface IProjectMemberService
{
    /// <summary>Mitglieder inkl. Profil, Eigentümer zuerst.</summary>
    Task<IResponse<IReadOnlyList<ProjectMember>>> GetMembers(Guid projectId);

    /// <summary>Rolle des angemeldeten Nutzers je Projekt-Id.</summary>
    Task<IResponse<IReadOnlyDictionary<Guid, string>>> GetMyRoles();

    /// <summary>Lädt einen registrierten Nutzer per E-Mail in das Projekt ein.</summary>
    Task<IResponse<ProjectMember>> InviteByEmail(Guid projectId, string email, string role);

    Task<IResponse> UpdateRole(Guid memberId, string role);
    Task<IResponse> RemoveMember(Guid memberId);
}
