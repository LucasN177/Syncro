using Syncro.Core.Interfaces.Infrastructure;
using Syncro.Core.Interfaces.Response;
using Syncro.Core.Models;
using Syncro.Core.Models.Database;
using static Supabase.Postgrest.Constants;

namespace Syncro.Infrastructure;

public class ProjectMemberRepository(Supabase.Client client) : IProjectMemberRepository
{
    public async Task<IResponse<IReadOnlyList<ProjectMemberDto>>> GetMembers(Guid projectId)
    {
        try
        {
            var result = await client.From<ProjectMemberDto>()
                .Where(member => member.ProjectId == projectId)
                .Order(member => member.CreatedAt, Ordering.Ascending)
                .Get();

            return Response<IReadOnlyList<ProjectMemberDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<ProjectMemberDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IReadOnlyList<ProjectMemberDto>>> GetMyMemberships()
    {
        try
        {
            if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var userId))
                return Response<IReadOnlyList<ProjectMemberDto>>.Failure("Bitte melde dich an.");

            var result = await client.From<ProjectMemberDto>()
                .Where(member => member.UserId == userId)
                .Get();

            return Response<IReadOnlyList<ProjectMemberDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<ProjectMemberDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<IReadOnlyList<ProfileDto>>> GetProfiles(IEnumerable<Guid> userIds)
    {
        try
        {
            var ids = userIds.Distinct().Select(id => id.ToString()).ToList();
            if (ids.Count == 0)
                return Response<IReadOnlyList<ProfileDto>>.Success([]);

            // RLS liefert nur das eigene Profil und Profile von Projektmitgliedern
            var result = await client.From<ProfileDto>()
                .Filter(profile => profile.Id, Operator.In, ids)
                .Get();

            return Response<IReadOnlyList<ProfileDto>>.Success(result.Models);
        }
        catch (Exception exception)
        {
            return Response<IReadOnlyList<ProfileDto>>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProfileDto>> FindProfileByEmail(string email)
    {
        try
        {
            var result = await client.Rpc<List<ProfileDto>>("find_profile_by_email",
                new Dictionary<string, object> { { "p_email", email.Trim() } });

            var profile = result?.FirstOrDefault();
            return profile is not null
                ? Response<ProfileDto>.Success(profile)
                : Response<ProfileDto>.Failure("Es gibt keinen Syncro-Account mit dieser E-Mail-Adresse.");
        }
        catch (Exception exception)
        {
            return Response<ProfileDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProjectMemberDto>> AddMember(Guid projectId, Guid userId, string role)
    {
        try
        {
            var result = await client.From<ProjectMemberDto>().Insert(new ProjectMemberDto
            {
                ProjectId = projectId,
                UserId = userId,
                Role = role
            });

            var member = result.Models.FirstOrDefault();
            return member is not null
                ? Response<ProjectMemberDto>.Success(member)
                : Response<ProjectMemberDto>.Failure("Mitglied konnte nicht hinzugefügt werden.");
        }
        catch (Exception exception)
        {
            return Response<ProjectMemberDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse<ProjectMemberDto>> UpdateRole(Guid memberId, string role)
    {
        try
        {
            var result = await client.From<ProjectMemberDto>()
                .Where(member => member.Id == memberId)
                .Set(member => member.Role, role)
                .Update();

            var member = result.Models.FirstOrDefault();
            return member is not null
                ? Response<ProjectMemberDto>.Success(member)
                : Response<ProjectMemberDto>.Failure("Die Rolle konnte nicht geändert werden.");
        }
        catch (Exception exception)
        {
            return Response<ProjectMemberDto>.Failure(exception.Message);
        }
    }

    public async Task<IResponse> RemoveMember(Guid memberId)
    {
        try
        {
            await client.From<ProjectMemberDto>()
                .Where(member => member.Id == memberId)
                .Delete();

            return Response.Success();
        }
        catch (Exception exception)
        {
            return Response.Failure(exception.Message, exception);
        }
    }
}
