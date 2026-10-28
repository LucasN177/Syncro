using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models.Database;

namespace Syncro.Core.Interfaces.Infrastructure;

public interface IProjectMemberRepository
{
    Task<IResponse<IReadOnlyList<ProjectMemberDto>>> GetMembers(Guid projectId);

    /// <summary>Mitgliedschaften des angemeldeten Nutzers über alle Projekte.</summary>
    Task<IResponse<IReadOnlyList<ProjectMemberDto>>> GetMyMemberships();

    Task<IResponse<IReadOnlyList<ProfileDto>>> GetProfiles(IEnumerable<Guid> userIds);
    Task<IResponse<ProfileDto>> FindProfileByEmail(string email);
    Task<IResponse<ProjectMemberDto>> AddMember(Guid projectId, Guid userId, string role);
    Task<IResponse<ProjectMemberDto>> UpdateRole(Guid memberId, string role);
    Task<IResponse> RemoveMember(Guid memberId);
}
