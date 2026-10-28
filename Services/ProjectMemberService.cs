using Syncro.Core.Constants;
using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Interfaces.Services;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;

namespace Syncro.Services;

public class ProjectMemberService(
    IProjectMemberRepository memberRepository,
    IProjectRepository projectRepository) : IProjectMemberService
{
    public async Task<IResponse<IReadOnlyList<ProjectMember>>> GetMembers(Guid projectId)
    {
        var membersResult = await memberRepository.GetMembers(projectId);
        if (membersResult is not { IsSuccess: true, Data: not null })
            return Response<IReadOnlyList<ProjectMember>>.Failure(membersResult.ErrorMessage ?? "Mitglieder konnten nicht geladen werden.");

        var profilesResult = await memberRepository.GetProfiles(membersResult.Data.Select(member => member.UserId));
        var profiles = profilesResult.Data?.ToDictionary(profile => profile.Id) ?? new Dictionary<Guid, ProfileDto>();

        var project = await projectRepository.GetProject(projectId);
        Guid.TryParse(project.Data?.UserId, out var ownerId);

        IReadOnlyList<ProjectMember> members = membersResult.Data
            .Select(member => ToProjectMember(member, profiles.GetValueOrDefault(member.UserId), ownerId))
            .OrderByDescending(member => member.IsOwner)
            .ThenBy(member => member.DisplayName)
            .ToList();

        return Response<IReadOnlyList<ProjectMember>>.Success(members);
    }

    public async Task<IResponse<IReadOnlyDictionary<Guid, string>>> GetMyRoles()
    {
        var result = await memberRepository.GetMyMemberships();
        if (result is not { IsSuccess: true, Data: not null })
            return Response<IReadOnlyDictionary<Guid, string>>.Failure(result.ErrorMessage ?? "Rollen konnten nicht geladen werden.");

        IReadOnlyDictionary<Guid, string> roles = result.Data.ToDictionary(member => member.ProjectId, member => member.Role);
        return Response<IReadOnlyDictionary<Guid, string>>.Success(roles);
    }

    public async Task<IResponse<ProjectMember>> InviteByEmail(Guid projectId, string email, string role)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Response<ProjectMember>.Failure("Bitte gib eine E-Mail-Adresse ein.");

        if (!ProjectRoles.All.Contains(role))
            return Response<ProjectMember>.Failure("Unbekannte Rolle.");

        var profileResult = await memberRepository.FindProfileByEmail(email);
        if (profileResult is not { IsSuccess: true, Data: not null })
            return Response<ProjectMember>.Failure(profileResult.ErrorMessage ?? "Nutzer wurde nicht gefunden.");

        var existing = await memberRepository.GetMembers(projectId);
        if (existing.Data?.Any(member => member.UserId == profileResult.Data.Id) == true)
            return Response<ProjectMember>.Failure("Dieser Nutzer ist bereits Mitglied des Projekts.");

        var memberResult = await memberRepository.AddMember(projectId, profileResult.Data.Id, role);
        if (memberResult is not { IsSuccess: true, Data: not null })
            return Response<ProjectMember>.Failure(memberResult.ErrorMessage ?? "Nur Projekt-Admins können Mitglieder einladen.");

        return Response<ProjectMember>.Success(ToProjectMember(memberResult.Data, profileResult.Data, Guid.Empty));
    }

    public async Task<IResponse> UpdateRole(Guid memberId, string role)
    {
        if (!ProjectRoles.All.Contains(role))
            return Response.Failure("Unbekannte Rolle.", null);

        var result = await memberRepository.UpdateRole(memberId, role);
        return result.IsSuccess ? Response.Success() : Response.Failure(result.ErrorMessage, result.Exception);
    }

    public Task<IResponse> RemoveMember(Guid memberId) => memberRepository.RemoveMember(memberId);

    private static ProjectMember ToProjectMember(ProjectMemberDto member, ProfileDto? profile, Guid ownerId) => new()
    {
        MemberId = member.Id,
        ProjectId = member.ProjectId,
        UserId = member.UserId,
        Role = member.Role,
        IsOwner = member.UserId == ownerId,
        Profile = profile
    };
}
